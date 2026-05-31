// <copyright file="ConcurrentTranslationRequestCacheTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.Helpers;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the shared concurrent translation request cache used by the
///     translator implementations.
/// </summary>
public class ConcurrentTranslationRequestCacheTests
{
    /// <summary>
    ///     Verifies that concurrent requests for the same key share a single
    ///     in-flight task.
    /// </summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task GetOrAddAsync_ConcurrentSameKey_CoalescesFactoryExecution()
    {
        var cache = new ConcurrentTranslationRequestCache();
        var invocationCount = 0;
        var releaseGate = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<string?> Factory()
        {
            Interlocked.Increment(ref invocationCount);
            await releaseGate.Task;
            return "translated";
        }

        var first = cache.GetOrAddAsync("line", Factory);
        var second = cache.GetOrAddAsync("line", Factory);
        releaseGate.SetResult(true);

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, invocationCount);
        Assert.All(results, static result => Assert.Equal("translated", result));
    }

    /// <summary>
    ///     Verifies that remembered translations are returned through the stable
    ///     cache lookup path.
    /// </summary>
    [Fact]
    public void Remember_TryGetValue_RoundTripsStoredTranslation()
    {
        var cache = new ConcurrentTranslationRequestCache();

        cache.Remember("line", "translated");

        var found = cache.TryGetValue("line", out var translatedText);

        Assert.True(found);
        Assert.Equal("translated", translatedText);
    }
}
