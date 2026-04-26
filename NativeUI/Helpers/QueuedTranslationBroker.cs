// <copyright file="QueuedTranslationBroker.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;
using System.Threading;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Keeps a shared in-memory translation cache and drains translation
///     requests through a single paced background pump so dense UI refreshes do
///     not spam the translator or create request bursts.
/// </summary>
public sealed class QueuedTranslationBroker : IDisposable
{
    private sealed record QueuedTranslationRequest(
        string Key,
        Func<Task<string>> Resolver,
        Action<string>? OnResolved,
        int RateLimitAttempt);

    private readonly ConcurrentDictionary<string, string> translationCache = new();
    private readonly ConcurrentDictionary<string, byte> translationInFlight = new();
    private readonly ConcurrentDictionary<string, DateTime> failedTranslations = new();
    private readonly ConcurrentQueue<QueuedTranslationRequest> pendingRequests = new();
    private readonly SemaphoreSlim pendingRequestsSignal = new(0);
    private readonly CancellationTokenSource shutdownTokenSource = new();
    private readonly object pacingLock = new();
    private readonly Action<string>? errorLog;
    private readonly TimeSpan failureRetryCooldown;
    private readonly TimeSpan minimumRequestSpacing;
    private readonly int maxRateLimitRetries;
    private readonly TimeSpan rateLimitCooldown;
    private readonly TimeSpan requestTimeout;
    private readonly Action<string>? warningLog;
    private DateTime nextAvailableRequestUtc = DateTime.MinValue;
    private int pumpStarted;

    /// <summary>
    ///     Initializes a new instance of the <see cref="QueuedTranslationBroker" />
    ///     class using a conservative policy for the active translation engine.
    /// </summary>
    /// <param name="engine">The configured translation engine.</param>
    /// <param name="warningLog">Optional warning logger.</param>
    /// <param name="errorLog">Optional error logger.</param>
    public QueuedTranslationBroker(
        Echoglossian.TransEngines engine,
        Action<string>? warningLog = null,
        Action<string>? errorLog = null)
        : this(
            ResolveMinimumRequestSpacing(engine),
            TimeSpan.FromSeconds(30),
            ResolveRequestTimeout(engine),
            ResolveRateLimitCooldown(engine),
            maxRateLimitRetries: 2,
            warningLog,
            errorLog)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="QueuedTranslationBroker" />
    ///     class with explicit timing parameters for tests and narrow tuning.
    /// </summary>
    /// <param name="minimumRequestSpacing">The minimum gap between request starts.</param>
    /// <param name="failureRetryCooldown">The cooldown applied after a failed request for one key.</param>
    /// <param name="requestTimeout">The timeout applied to one resolver execution.</param>
    /// <param name="rateLimitCooldown">The global cooldown applied after rate-limit signals.</param>
    /// <param name="maxRateLimitRetries">The number of broker-level retries after rate-limit signals.</param>
    /// <param name="warningLog">Optional warning logger.</param>
    /// <param name="errorLog">Optional error logger.</param>
    internal QueuedTranslationBroker(
        TimeSpan minimumRequestSpacing,
        TimeSpan failureRetryCooldown,
        TimeSpan requestTimeout,
        TimeSpan rateLimitCooldown,
        int maxRateLimitRetries,
        Action<string>? warningLog = null,
        Action<string>? errorLog = null)
    {
        this.minimumRequestSpacing = minimumRequestSpacing;
        this.failureRetryCooldown = failureRetryCooldown;
        this.requestTimeout = requestTimeout;
        this.rateLimitCooldown = rateLimitCooldown;
        this.maxRateLimitRetries = Math.Max(0, maxRateLimitRetries);
        this.warningLog = warningLog;
        this.errorLog = errorLog;
    }

    /// <summary>
    ///     Returns a cached translation if we already resolved it.
    /// </summary>
    public bool TryGetCached(string key, out string translatedText)
    {
        return this.translationCache.TryGetValue(key, out translatedText!);
    }

    /// <summary>
    ///     Queues a translation request if one is not already in flight for the key.
    /// </summary>
    public bool Queue(string key, Func<Task<string>> resolver, Action<string>? onResolved = null)
    {
        if (this.failedTranslations.TryGetValue(key, out var lastFailureUtc) &&
            DateTime.UtcNow - lastFailureUtc < this.failureRetryCooldown)
        {
            return false;
        }

        if (!this.translationInFlight.TryAdd(key, 0))
        {
            return false;
        }

        this.pendingRequests.Enqueue(
            new QueuedTranslationRequest(
                key,
                resolver,
                onResolved,
                RateLimitAttempt: 0));
        this.pendingRequestsSignal.Release();
        this.StartPump();

        return true;
    }

    /// <summary>
    ///     Starts the shared background pump once for the lifetime of the broker.
    /// </summary>
    private void StartPump()
    {
        if (Interlocked.CompareExchange(ref this.pumpStarted, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(this.ProcessQueueAsync);
    }

    /// <summary>
    ///     Processes queued translations sequentially with a small pacing gap.
    /// </summary>
    private async Task ProcessQueueAsync()
    {
        try
        {
            while (!this.shutdownTokenSource.IsCancellationRequested)
            {
                await this.pendingRequestsSignal.WaitAsync(
                    this.shutdownTokenSource.Token).ConfigureAwait(false);

                while (this.pendingRequests.TryDequeue(out var request))
                {
                    await this.DelayForNextRequestSlotAsync(
                        this.shutdownTokenSource.Token).ConfigureAwait(false);
                    await this.ProcessRequestAsync(request)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Interlocked.Exchange(ref this.pumpStarted, 0);
        }
    }

    /// <summary>
    ///     Ensures translation requests start at a controlled pace.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for shutdown.</param>
    private async Task DelayForNextRequestSlotAsync(
        CancellationToken cancellationToken)
    {
        var delay = TimeSpan.Zero;
        lock (this.pacingLock)
        {
            var nowUtc = DateTime.UtcNow;
            if (nowUtc < this.nextAvailableRequestUtc)
            {
                delay = this.nextAvailableRequestUtc - nowUtc;
            }

            this.nextAvailableRequestUtc =
                (nowUtc > this.nextAvailableRequestUtc
                    ? nowUtc
                    : this.nextAvailableRequestUtc) + this.minimumRequestSpacing;
        }

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Resolves a single queued translation request.
    /// </summary>
    /// <param name="request">The queued translation request.</param>
    private async Task ProcessRequestAsync(QueuedTranslationRequest request)
    {
        var requeued = false;
        try
        {
            var translatedText = await request.Resolver().WaitAsync(
                this.requestTimeout,
                this.shutdownTokenSource.Token).ConfigureAwait(false);
            if (LooksLikeRateLimitPayload(translatedText))
            {
                requeued = this.TryRequeueAfterRateLimit(
                    request,
                    translatedText);
                if (requeued)
                {
                    return;
                }

                this.failedTranslations[request.Key] = DateTime.UtcNow;
                return;
            }

            if (LooksLikeFailurePayload(translatedText))
            {
                this.failedTranslations[request.Key] = DateTime.UtcNow;
                return;
            }

            if (!string.IsNullOrWhiteSpace(translatedText))
            {
                this.translationCache[request.Key] = translatedText;
                this.failedTranslations.TryRemove(request.Key, out _);
                request.OnResolved?.Invoke(translatedText);
                return;
            }

            this.failedTranslations[request.Key] = DateTime.UtcNow;
        }
        catch (TimeoutException)
        {
            this.warningLog?.Invoke(
                $"[QueuedTranslationBroker] Translation timed out after {this.requestTimeout.TotalSeconds:F0}s for '{request.Key}'.");
            this.failedTranslations[request.Key] = DateTime.UtcNow;
        }
        catch (Exception ex) when (LooksLikeRateLimitException(ex))
        {
            requeued = this.TryRequeueAfterRateLimit(request, ex.Message);
            if (!requeued)
            {
                this.failedTranslations[request.Key] = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            this.errorLog?.Invoke(
                $"[QueuedTranslationBroker] Error resolving '{request.Key}': {ex}");
            this.failedTranslations[request.Key] = DateTime.UtcNow;
        }
        finally
        {
            if (!requeued)
            {
                this.translationInFlight.TryRemove(request.Key, out _);
            }
        }
    }

    /// <summary>
    ///     Applies a global cooldown and optionally requeues the request after a
    ///     detected rate-limit signal.
    /// </summary>
    /// <param name="request">The active request.</param>
    /// <param name="details">The failure details used for diagnostics.</param>
    /// <returns>
    ///     <see langword="true" /> when the request was requeued for another
    ///     attempt; otherwise, <see langword="false" />.
    /// </returns>
    private bool TryRequeueAfterRateLimit(
        QueuedTranslationRequest request,
        string details)
    {
        this.ExtendGlobalCooldown(this.rateLimitCooldown);

        if (request.RateLimitAttempt >= this.maxRateLimitRetries)
        {
            this.warningLog?.Invoke(
                $"[QueuedTranslationBroker] Rate limit persisted for '{request.Key}' after {request.RateLimitAttempt + 1} attempts. " +
                $"Cooling queue for {this.rateLimitCooldown.TotalSeconds:F0}s. Details: {FormatDiagnosticPreview(details)}");
            return false;
        }

        this.warningLog?.Invoke(
            $"[QueuedTranslationBroker] Rate limit detected for '{request.Key}'. " +
            $"Cooling queue for {this.rateLimitCooldown.TotalSeconds:F0}s before retry {request.RateLimitAttempt + 2}. " +
            $"Details: {FormatDiagnosticPreview(details)}");

        this.pendingRequests.Enqueue(
            request with
            {
                RateLimitAttempt = request.RateLimitAttempt + 1,
            });
        this.pendingRequestsSignal.Release();
        return true;
    }

    /// <summary>
    ///     Extends the next allowed request slot by a global cooldown window.
    /// </summary>
    /// <param name="cooldown">The cooldown to apply.</param>
    private void ExtendGlobalCooldown(TimeSpan cooldown)
    {
        lock (this.pacingLock)
        {
            var cooldownUntilUtc = DateTime.UtcNow + cooldown;
            if (cooldownUntilUtc > this.nextAvailableRequestUtc)
            {
                this.nextAvailableRequestUtc = cooldownUntilUtc;
            }
        }
    }

    /// <summary>
    ///     Determines whether a resolver payload represents one of the
    ///     translator error strings that should not be cached as a translation.
    /// </summary>
    /// <param name="translatedText">The resolver payload.</param>
    /// <returns>
    ///     <see langword="true" /> when the payload looks like a translator
    ///     failure string; otherwise, <see langword="false" />.
    /// </returns>
    private static bool LooksLikeFailurePayload(string? translatedText)
    {
        if (string.IsNullOrWhiteSpace(translatedText))
        {
            return false;
        }

        var trimmed = translatedText.Trim();
        if (!trimmed.StartsWith("[", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return trimmed.Contains(
                   Resources.TranslationError,
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains(
                   "translation error",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains(
                   "http error",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains(
                   "json error",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains(
                   "request failed with status code",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains(
                   "returned an empty",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains(
                   "unavailable",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains(
                   "please check your api key",
                   StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Determines whether a payload is specifically signaling
    ///     server-side throttling or quota pressure.
    /// </summary>
    /// <param name="translatedText">The resolver payload.</param>
    /// <returns>
    ///     <see langword="true" /> when the payload looks like a rate-limit
    ///     response; otherwise, <see langword="false" />.
    /// </returns>
    private static bool LooksLikeRateLimitPayload(string? translatedText)
    {
        return LooksLikeFailurePayload(translatedText) &&
               ContainsRateLimitToken(translatedText);
    }

    /// <summary>
    ///     Determines whether an exception likely represents server-side
    ///     throttling or quota pressure.
    /// </summary>
    /// <param name="exception">The thrown exception.</param>
    /// <returns>
    ///     <see langword="true" /> when the exception looks like a rate-limit
    ///     signal; otherwise, <see langword="false" />.
    /// </returns>
    private static bool LooksLikeRateLimitException(Exception exception)
    {
        return ContainsRateLimitToken(exception.ToString());
    }

    /// <summary>
    ///     Determines whether the supplied diagnostic text contains one of the
    ///     common 429 or rate-limit markers.
    /// </summary>
    /// <param name="diagnosticText">The diagnostic text to inspect.</param>
    /// <returns>
    ///     <see langword="true" /> when the text contains a rate-limit marker;
    ///     otherwise, <see langword="false" />.
    /// </returns>
    private static bool ContainsRateLimitToken(string? diagnosticText)
    {
        if (string.IsNullOrWhiteSpace(diagnosticText))
        {
            return false;
        }

        return diagnosticText.Contains(
                   "429",
                   StringComparison.OrdinalIgnoreCase) ||
               diagnosticText.Contains(
                   "too many requests",
                   StringComparison.OrdinalIgnoreCase) ||
               diagnosticText.Contains(
                   "toomanyrequests",
                   StringComparison.OrdinalIgnoreCase) ||
               diagnosticText.Contains(
                   "rate limit",
                   StringComparison.OrdinalIgnoreCase) ||
               diagnosticText.Contains(
                   "retry-after",
                   StringComparison.OrdinalIgnoreCase) ||
               diagnosticText.Contains(
                   "quota",
                   StringComparison.OrdinalIgnoreCase) ||
               diagnosticText.Contains(
                   "resource exhausted",
                   StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Returns a short single-line diagnostic preview.
    /// </summary>
    /// <param name="details">The details to format.</param>
    /// <returns>The preview text.</returns>
    private static string FormatDiagnosticPreview(string details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return "<empty>";
        }

        var preview = details.ReplaceLineEndings(" ").Trim();
        const int maxLength = 180;
        if (preview.Length <= maxLength)
        {
            return preview;
        }

        return preview[..maxLength] + "...";
    }

    /// <summary>
    ///     Returns the conservative inter-request spacing for the configured
    ///     engine family.
    /// </summary>
    /// <param name="engine">The active translation engine.</param>
    /// <returns>The minimum spacing between queued request starts.</returns>
    private static TimeSpan ResolveMinimumRequestSpacing(
        Echoglossian.TransEngines engine)
    {
        return engine switch
        {
            Echoglossian.TransEngines.ChatGPT or
                Echoglossian.TransEngines.DeepSeek or
                Echoglossian.TransEngines.Gemini or
                Echoglossian.TransEngines.OpenRouter =>
                TimeSpan.FromSeconds(1.5),
            Echoglossian.TransEngines.Ollama or
                Echoglossian.TransEngines.LmStudio =>
                TimeSpan.FromMilliseconds(250),
            Echoglossian.TransEngines.Google or
                Echoglossian.TransEngines.GTranslate or
                Echoglossian.TransEngines.YandexPublic or
                Echoglossian.TransEngines.LibreTranslate =>
                TimeSpan.FromSeconds(1),
            _ => TimeSpan.FromMilliseconds(750),
        };
    }

    /// <summary>
    ///     Returns the timeout to apply to one resolver execution.
    /// </summary>
    /// <param name="engine">The active translation engine.</param>
    /// <returns>The timeout for one queued request.</returns>
    private static TimeSpan ResolveRequestTimeout(
        Echoglossian.TransEngines engine)
    {
        return engine switch
        {
            Echoglossian.TransEngines.ChatGPT or
                Echoglossian.TransEngines.DeepSeek or
                Echoglossian.TransEngines.Gemini or
                Echoglossian.TransEngines.OpenRouter =>
                TimeSpan.FromSeconds(90),
            Echoglossian.TransEngines.Ollama or
                Echoglossian.TransEngines.LmStudio =>
                TimeSpan.FromMinutes(2),
            _ => TimeSpan.FromSeconds(45),
        };
    }

    /// <summary>
    ///     Returns the global queue cooldown applied after rate-limit signals.
    /// </summary>
    /// <param name="engine">The active translation engine.</param>
    /// <returns>The cooldown window for the shared queue.</returns>
    private static TimeSpan ResolveRateLimitCooldown(
        Echoglossian.TransEngines engine)
    {
        return engine switch
        {
            Echoglossian.TransEngines.Ollama or
                Echoglossian.TransEngines.LmStudio =>
                TimeSpan.FromSeconds(5),
            _ => TimeSpan.FromSeconds(20),
        };
    }

    /// <summary>
    ///     Releases broker resources and stops the background pump.
    /// </summary>
    public void Dispose()
    {
        this.shutdownTokenSource.Cancel();
        this.pendingRequestsSignal.Release();
        this.pendingRequestsSignal.Dispose();
        this.shutdownTokenSource.Dispose();
    }
}
