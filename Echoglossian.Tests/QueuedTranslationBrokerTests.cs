// <copyright file="QueuedTranslationBrokerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;
using Echoglossian.Properties;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the shared queued translation broker used by bulk prefetch and
///     DB-first capture paths.
/// </summary>
public class QueuedTranslationBrokerTests
{
    /// <summary>Terminal notification occurs once only after all broker rate-limit attempts fail.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Queue_TerminalFailureOccursAfterRetryExhaustion(bool throws)
    {
        using var broker = new QueuedTranslationBroker(TimeSpan.Zero, TimeSpan.Zero,
            TimeSpan.FromSeconds(1), TimeSpan.Zero, maxRateLimitRetries: 2);
        var attempts = 0;
        var failures = 0;
        var terminal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(broker.Queue("terminal-retry", () =>
        {
            Interlocked.Increment(ref attempts);
            if (throws)
            {
                throw new HttpRequestException("HTTP 429 Too Many Requests");
            }

            return Task.FromResult("[translation error HTTP 429]");
        }, _ => throw new InvalidOperationException("Failure cached as success."), "test", cancelled =>
        {
            Interlocked.Increment(ref failures);
            terminal.TrySetResult(cancelled);
        }));
        Assert.False(await terminal.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(3, Volatile.Read(ref attempts));
        Assert.Equal(1, Volatile.Read(ref failures));
        Assert.False(broker.TryGetCached("terminal-retry", out _));
    }

    /// <summary>Terminal provider payloads notify once without invoking success.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("[translation error unavailable]")]
    public async Task Queue_TerminalPayload_NotifiesFailure(string payload)
    {
        using var broker = new QueuedTranslationBroker(TimeSpan.Zero, TimeSpan.Zero,
            TimeSpan.FromSeconds(1), TimeSpan.Zero, 0);
        var terminal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(broker.Queue("terminal-payload", () => Task.FromResult(payload),
            _ => throw new InvalidOperationException("Invalid payload published."), "test", cancelled => terminal.TrySetResult(cancelled)));
        Assert.False(await terminal.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(broker.TryGetCached("terminal-payload", out _));
    }

    /// <summary>Shutdown reports cancellation for the active request and every queued subscriber.</summary>
    [Fact]
    public async Task Dispose_CancelsActiveAndQueuedTerminalSubscribers()
    {
        using var broker = new QueuedTranslationBroker(TimeSpan.Zero, TimeSpan.Zero,
            TimeSpan.FromSeconds(30), TimeSpan.Zero, 0);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolver = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(broker.Queue("active-cancel", () => { entered.SetResult(); return resolver.Task; },
            null, "test", cancelled => active.TrySetResult(cancelled)));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(broker.Queue("queued-cancel", () => Task.FromResult("should not execute"),
            null, "test", cancelled => queued.TrySetResult(cancelled)));
        broker.Dispose();
        try
        {
            Assert.True(await active.Task.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.True(await queued.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        }
        finally
        {
            resolver.TrySetResult("late success");
        }
    }

    /// <summary>
    ///     Ensures error payloads do not get cached as successful
    ///     translations.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Queue_DoesNotCacheFailurePayload()
    {
        var resolverCompleted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var broker = new QueuedTranslationBroker(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(25),
            maxRateLimitRetries: 0);

        var queued = broker.Queue(
            "failure-key",
            () =>
            {
                resolverCompleted.TrySetResult(true);
                return Task.FromResult(
                    $"[{Resources.TranslationError} simulated failure]");
            });

        Assert.True(queued);
        await resolverCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Task.Delay(50);

        Assert.False(broker.TryGetCached("failure-key", out _));
        Assert.False(
            broker.Queue(
                "failure-key",
                () => Task.FromResult("retry-too-soon")));
    }

    /// <summary>
    ///     Ensures a detected 429-style payload cools the shared queue and
    ///     retries the same request instead of caching the failure text.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Queue_RateLimitPayload_RequeuesAndEventuallyCaches()
    {
        var resolved = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;

        using var broker = new QueuedTranslationBroker(
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(25),
            maxRateLimitRetries: 1);

        var queued = broker.Queue(
            "rate-limit-key",
            () =>
            {
                invocationCount++;
                return Task.FromResult(
                    invocationCount == 1
                        ? $"[{Resources.TranslationError} 429 TooManyRequests]"
                        : "translated");
            },
            translated => resolved.TrySetResult(translated));

        Assert.True(queued);

        var translated = await resolved.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("translated", translated);
        Assert.Equal(2, invocationCount);
        Assert.True(broker.TryGetCached("rate-limit-key", out var cached));
        Assert.Equal("translated", cached);
    }

    /// <summary>
    ///     Ensures one hung translation does not block the single shared pump
    ///     forever.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Queue_Timeout_AllowsLaterRequestsToProceed()
    {
        var resolved = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var broker = new QueuedTranslationBroker(
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(25),
            maxRateLimitRetries: 0);

        Assert.True(
            broker.Queue(
                "slow-key",
                async () =>
                {
                    await Task.Delay(250);
                    return "too-late";
                }));

        Assert.True(
            broker.Queue(
                "fast-key",
                () => Task.FromResult("fast"),
                translated => resolved.TrySetResult(translated)));

        var translated = await resolved.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("fast", translated);
        Assert.False(broker.TryGetCached("slow-key", out _));
        Assert.True(broker.TryGetCached("fast-key", out var cached));
        Assert.Equal("fast", cached);
    }

    /// <summary>
    ///     Ensures broker failure diagnostics include the initiating surface
    ///     identity so translation failures can be traced back to the owning
    ///     runtime.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Queue_ExceptionLog_IncludesSurfaceIdentity()
    {
        var errors = new List<string>();

        using var broker = new QueuedTranslationBroker(
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(25),
            maxRateLimitRetries: 0,
            errorLog: errors.Add);

        Assert.True(
            broker.Queue(
                "error-key",
                () => throw new InvalidOperationException("boom"),
                surfaceIdentity: "QuestToast/Centre"));

        await Task.Delay(100);

        Assert.Contains(
            errors,
            message => message.Contains(
                "QuestToast/Centre",
                StringComparison.Ordinal));
    }
}
