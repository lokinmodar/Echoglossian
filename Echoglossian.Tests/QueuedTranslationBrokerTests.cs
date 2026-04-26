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
}
