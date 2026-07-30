// <copyright file="NativeTextNodeLayoutHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers reusable geometry calculations in the native text-node reflow
///     helper.
/// </summary>
public class NativeTextNodeLayoutHelperTests
{
    /// <summary>
    ///     Ensures wrapped text reflow prefers the draw height when the live
    ///     node height still reflects the stale one-line layout.
    /// </summary>
    [Fact]
    public void ResolveMeasuredTextExtent_PrefersDrawHeight_WhenWrappedNodeHeightIsStale()
    {
        var resolved = NativeTextNodeLayoutHelper.ResolveMeasuredTextExtent(
            liveWidth: 392,
            liveHeight: 42,
            drawWidth: 368,
            drawHeight: 74,
            textFlags: TextFlags.WordWrap | TextFlags.MultiLine | TextFlags.AutoAdjustNodeSize);

        Assert.Equal((ushort)392, resolved.Width);
        Assert.Equal((ushort)74, resolved.Height);
    }

    /// <summary>
    ///     Ensures zero-sized nodes still fall back to the text draw size.
    /// </summary>
    [Fact]
    public void ResolveMeasuredTextExtent_FallsBackToDrawSize_WhenLiveSizeIsMissing()
    {
        var resolved = NativeTextNodeLayoutHelper.ResolveMeasuredTextExtent(
            liveWidth: 0,
            liveHeight: 0,
            drawWidth: 120,
            drawHeight: 32,
            textFlags: default);

        Assert.Equal((ushort)120, resolved.Width);
        Assert.Equal((ushort)32, resolved.Height);
    }

    /// <summary>
    ///     Ensures the secondary background width is left untouched when the
    ///     anchored node already fits inside the current bounds.
    /// </summary>
    [Fact]
    public void ResolveMinimumSecondaryWidthForAnchoredNode_ReturnsCurrentWidth_WhenAnchoredNodeIsAlreadyCovered()
    {
        var resolvedWidth = NativeTextNodeLayoutHelper.ResolveMinimumSecondaryWidthForAnchoredNode(
            secondaryContainerX: 100,
            currentSecondaryWidth: 220,
            anchoredNodeX: 260,
            anchoredNodeWidth: 24,
            preferredRightPadding: 8);

        Assert.Equal((ushort)220, resolvedWidth);
    }

    /// <summary>
    ///     Ensures the secondary background widens enough to cover an anchored
    ///     timer or spinner node when it would otherwise extend past the
    ///     current right edge.
    /// </summary>
    [Fact]
    public void ResolveMinimumSecondaryWidthForAnchoredNode_GrowsWidth_WhenAnchoredNodeExtendsPastCurrentBounds()
    {
        var resolvedWidth = NativeTextNodeLayoutHelper.ResolveMinimumSecondaryWidthForAnchoredNode(
            secondaryContainerX: 100,
            currentSecondaryWidth: 220,
            anchoredNodeX: 310,
            anchoredNodeWidth: 24,
            preferredRightPadding: 8);

        Assert.Equal((ushort)242, resolvedWidth);
    }

    /// <summary>
    ///     Ensures negative historical padding still results in coverage up to
    ///     the anchored node edge instead of shrinking the background further.
    /// </summary>
    [Fact]
    public void ResolveMinimumSecondaryWidthForAnchoredNode_ClampsNegativePaddingToZero()
    {
        var resolvedWidth = NativeTextNodeLayoutHelper.ResolveMinimumSecondaryWidthForAnchoredNode(
            secondaryContainerX: 100,
            currentSecondaryWidth: 220,
            anchoredNodeX: 330,
            anchoredNodeWidth: 20,
            preferredRightPadding: -12);

        Assert.Equal((ushort)250, resolvedWidth);
    }

    /// <summary>
    ///     Ensures container growth preserves the original padding when it is
    ///     already wider than the minimum requested padding.
    /// </summary>
    [Fact]
    public void ResolveExpandedContainerExtent_PreservesHistoricalPadding_WhenItAlreadyExceedsMinimum()
    {
        var resolvedExtent = NativeTextNodeLayoutHelper.ResolveExpandedContainerExtent(
            currentContainerExtent: 384,
            currentTextExtent: 368,
            measuredTextExtent: 392,
            minimumPadding: 8);

        Assert.Equal((ushort)408, resolvedExtent);
    }

    /// <summary>
    ///     Ensures dense tooltip backgrounds can enforce a minimum vertical or
    ///     horizontal cushion even when the original layout reported none.
    /// </summary>
    [Fact]
    public void ResolveExpandedContainerExtent_UsesMinimumPadding_WhenHistoricalPaddingIsTooSmall()
    {
        var resolvedExtent = NativeTextNodeLayoutHelper.ResolveExpandedContainerExtent(
            currentContainerExtent: 42,
            currentTextExtent: 42,
            measuredTextExtent: 74,
            minimumPadding: 8);

        Assert.Equal((ushort)82, resolvedExtent);
    }
}
