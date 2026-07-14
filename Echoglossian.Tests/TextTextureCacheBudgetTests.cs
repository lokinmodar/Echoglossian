// <copyright file="TextTextureCacheBudgetTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

using Echoglossian.ImageGeneration;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers byte-budget behavior for the text texture cache.
/// </summary>
public class TextTextureCacheBudgetTests
{
    /// <summary>
    /// Ensures the cache evicts least-recently-used entries when the soft byte
    /// budget would otherwise be exceeded.
    /// </summary>
    [Fact]
    public void GetOrCreate_WhenSoftBudgetExceeded_EvictsLeastRecentlyUsedEntry()
    {
        using var cache = new TextTextureCache(
            maxCapacity: 8,
            inactivityTimeoutSeconds: 300,
            softByteBudget: 700,
            hardByteBudget: 2000);

        var textureA = new FakeTextureWrap(width: 10, height: 10);
        var textureB = new FakeTextureWrap(width: 10, height: 10);

        cache.GetOrCreate("a", () => textureA);
        cache.GetOrCreate("b", () => textureB);

        var stats = cache.GetDebugStats();

        Assert.Equal(1, stats.Count);
        Assert.True(textureA.DisposeCount > 0);
        Assert.Equal(400, stats.EstimatedMemoryBytes);
    }

    /// <summary>
    /// Ensures an entry that exceeds the hard limit is rejected instead of being
    /// cached.
    /// </summary>
    [Fact]
    public void GetOrCreate_WhenEntryExceedsHardBudget_ThrowsAndDisposesTexture()
    {
        using var cache = new TextTextureCache(
            maxCapacity: 8,
            inactivityTimeoutSeconds: 300,
            softByteBudget: 1000,
            hardByteBudget: 1000);

        var hugeTexture = new FakeTextureWrap(width: 40, height: 40);

        Assert.Throws<InvalidOperationException>(() =>
            cache.GetOrCreate("huge", () => hugeTexture));
        Assert.True(hugeTexture.DisposeCount > 0);
        Assert.Equal((0, 0L), cache.GetDebugStats());
    }

    /// <summary>
    /// Ensures the Phase A hard cache budget rejects a 49 MiB texture while
    /// preserving support for a texture exactly at the 48 MiB limit.
    /// </summary>
    [Fact]
    public void GetOrCreate_PhaseAHardBudget_Rejects49MiBAndAllows48MiB()
    {
        using var cache = new TextTextureCache();
        var oversizedTexture = new FakeTextureWrap(width: 2048, height: 6272);
        var limitTexture = new FakeTextureWrap(width: 2048, height: 6144);

        Assert.Throws<InvalidOperationException>(() =>
            cache.GetOrCreate("oversized", () => oversizedTexture));
        var cachedTexture = cache.GetOrCreate("limit", () => limitTexture);

        Assert.Same(limitTexture, cachedTexture);
        Assert.Equal(1, cache.GetDebugStats().Count);
        Assert.Equal(1, oversizedTexture.DisposeCount);
        Assert.Equal(0, limitTexture.DisposeCount);
    }

    private sealed class FakeTextureWrap : IDalamudTextureWrap
    {
        public FakeTextureWrap(int width, int height)
        {
            this.Width = width;
            this.Height = height;
        }

        public int DisposeCount { get; private set; }

        public ImTextureID Handle => default;

        public int Width { get; }

        public int Height { get; }

        public void Dispose()
        {
            this.DisposeCount++;
        }
    }
}
