// <copyright file="ActionTraitCacheConcurrencyTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.NativeUI.Helpers;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>Protects Framework snapshot reads while coordinator workers publish canonical cache rows.</summary>
public sealed class ActionTraitCacheConcurrencyTests
{
    /// <summary>Concurrent publication and snapshot enumeration retain internally consistent managed lookups.</summary>
    /// <param name="action">Whether to exercise the Action cache rather than Trait.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateAndGetTextLookupSnapshot_AreSafeConcurrently(bool action)
    {
        const int Count = 256;
        ActionTooltipCacheManager.Clear();
        TraitCacheManager.Clear();
        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writerReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readerReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scope = new TranslationReuseScope("en", "pt", 0, true);
        try
        {
            var writer = Task.Run(async () =>
            {
                writerReady.SetResult(true);
                await start.Task;
                for (var index = 0; index < Count; index++)
                {
                    Update(action, (uint)index + 1);
                    await Task.Yield();
                }
            });
            var reader = Task.Run(async () =>
            {
                readerReady.SetResult(true);
                await start.Task;
                for (var index = 0; index < Count; index++)
                {
                    var snapshot = action
                        ? ActionTooltipCacheManager.GetTextLookupSnapshot(scope, "7.3")
                        : TraitCacheManager.GetTextLookupSnapshot(scope, "7.3");
                    foreach (var pair in snapshot.ForwardLookup)
                    {
                        Assert.Contains(pair.Key, snapshot.OriginalTexts);
                        Assert.True(snapshot.TryFindOriginalText(pair.Value, out var original));
                        Assert.Equal(pair.Key, original);
                    }

                    await Task.Yield();
                }
            });
            await Task.WhenAll(writerReady.Task, readerReady.Task).WaitAsync(TimeSpan.FromSeconds(5));
            start.SetResult(true);
            await Task.WhenAll(writer, reader).WaitAsync(TimeSpan.FromSeconds(10));
            var final = action
                ? ActionTooltipCacheManager.GetTextLookupSnapshot(scope, "7.3")
                : TraitCacheManager.GetTextLookupSnapshot(scope, "7.3");
            Assert.Equal(Count, final.ForwardLookup.Count);
            for (var id = 1; id <= Count; id++)
            {
                Assert.Equal("Traduzido " + id, final.ForwardLookup["Original " + id]);
            }
        }
        finally
        {
            start.TrySetResult(true);
            ActionTooltipCacheManager.Clear();
            TraitCacheManager.Clear();
        }
    }

    /// <summary>Publishes a complete distinct identity using the production domain builders.</summary>
    /// <param name="action">Whether to publish an Action row.</param>
    /// <param name="id">The canonical id.</param>
    private static void Update(bool action, uint id)
    {
        if (action)
        {
            var payload = new ActionTooltipCanonicalPayload
            {
                ActionId = id,
                Name = "Original " + id,
                TranslatedName = "Traduzido " + id,
            };
            ActionTooltipCacheManager.Update(ActionTooltipPersistenceHelper.CreateCanonicalRow(
                "en", "pt", 0, "7.3", payload, payload));
        }
        else
        {
            var payload = new TraitCanonicalPayload
            {
                TraitId = id,
                Name = "Original " + id,
                TranslatedName = "Traduzido " + id,
            };
            TraitCacheManager.Update(TraitPersistenceHelper.CreateCanonicalRow(
                "en", "pt", 0, "7.3", payload, payload));
        }
    }
}

