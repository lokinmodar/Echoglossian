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
        Action<string>? OnResolved);

    private readonly ConcurrentDictionary<string, string> translationCache = new();
    private readonly ConcurrentDictionary<string, byte> translationInFlight = new();
    private readonly ConcurrentDictionary<string, DateTime> failedTranslations = new();
    private readonly ConcurrentQueue<QueuedTranslationRequest> pendingRequests = new();
    private readonly SemaphoreSlim pendingRequestsSignal = new(0);
    private readonly CancellationTokenSource shutdownTokenSource = new();
    private readonly object pacingLock = new();
    private readonly TimeSpan failureRetryCooldown = TimeSpan.FromSeconds(30);
    private readonly TimeSpan minimumRequestSpacing = TimeSpan.FromMilliseconds(125);
    private DateTime nextAvailableRequestUtc = DateTime.MinValue;
    private int pumpStarted;

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
            new QueuedTranslationRequest(key, resolver, onResolved));
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
        try
        {
            var translatedText = await request.Resolver().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(translatedText))
            {
                this.translationCache[request.Key] = translatedText;
                this.failedTranslations.TryRemove(request.Key, out _);
                request.OnResolved?.Invoke(translatedText);
                return;
            }

            this.failedTranslations[request.Key] = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            global::Echoglossian.Echoglossian.PluginLog.Error(
                $"[QueuedTranslationBroker] Error resolving '{request.Key}': {ex}");
            this.failedTranslations[request.Key] = DateTime.UtcNow;
        }
        finally
        {
            this.translationInFlight.TryRemove(request.Key, out _);
        }
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
