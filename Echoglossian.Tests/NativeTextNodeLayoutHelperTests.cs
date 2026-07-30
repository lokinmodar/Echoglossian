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
    ///     Ensures native reflow can widen the text node to the candidate text
    ///     draw width when the caller explicitly allows width growth.
    /// </summary>
    [Fact]
    public void ResolveReplacementWrapWidth_PrefersCandidateDrawWidth_WhenGrowthIsAllowed()
    {
        var resolved = NativeTextNodeLayoutHelper.ResolveReplacementWrapWidth(
            preferredWrapWidth: 171,
            candidateDrawWidth: 344,
            allowWidthGrowth: true);

        Assert.Equal((ushort)344, resolved);
    }

    /// <summary>
    ///     Ensures native reflow preserves the historical wrap width when the
    ///     caller does not allow horizontal growth.
    /// </summary>
    [Fact]
    public void ResolveReplacementWrapWidth_PreservesHistoricalWidth_WhenGrowthIsDisabled()
    {
        var resolved = NativeTextNodeLayoutHelper.ResolveReplacementWrapWidth(
            preferredWrapWidth: 171,
            candidateDrawWidth: 344,
            allowWidthGrowth: false);

        Assert.Equal((ushort)171, resolved);
    }

    /// <summary>
    ///     Ensures native reflow can still fall back to the candidate text draw
    ///     width when no historical wrap width is available.
    /// </summary>
    [Fact]
    public void ResolveReplacementWrapWidth_FallsBackToCandidateWidth_WhenHistoricalWidthIsMissing()
    {
        var resolved = NativeTextNodeLayoutHelper.ResolveReplacementWrapWidth(
            preferredWrapWidth: 0,
            candidateDrawWidth: 239,
            allowWidthGrowth: true);

        Assert.Equal((ushort)239, resolved);
    }

    /// <summary>
    ///     Ensures tooltip-style native reflow keeps the larger measured width
    ///     when the live text draw size still exceeds the preserved wrap width.
    /// </summary>
    [Fact]
    public void ResolveReplacementContainerWidth_PrefersMeasuredOverflow_WhenEnabled()
    {
        var resolved = NativeTextNodeLayoutHelper.ResolveReplacementContainerWidth(
            resolvedWrapWidth: 209,
            measuredWidth: 294,
            preferMeasuredOverflow: true);

        Assert.Equal((ushort)294, resolved);
    }

    /// <summary>
    ///     Ensures callers that do not opt into measured overflow continue to
    ///     preserve the historical wrap width.
    /// </summary>
    [Fact]
    public void ResolveReplacementContainerWidth_PreservesWrapWidth_WhenMeasuredOverflowIsDisabled()
    {
        var resolved = NativeTextNodeLayoutHelper.ResolveReplacementContainerWidth(
            resolvedWrapWidth: 209,
            measuredWidth: 294,
            preferMeasuredOverflow: false);

        Assert.Equal((ushort)209, resolved);
    }

    /// <summary>
    ///     Ensures native reflow still falls back to the preserved wrap width
    ///     when no post-apply measurement is available.
    /// </summary>
    [Fact]
    public void ResolveReplacementContainerWidth_FallsBackToWrapWidth_WhenMeasuredWidthIsMissing()
    {
        var resolved = NativeTextNodeLayoutHelper.ResolveReplacementContainerWidth(
            resolvedWrapWidth: 209,
            measuredWidth: 0,
            preferMeasuredOverflow: true);

        Assert.Equal((ushort)209, resolved);
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

    /// <summary>
    ///     Ensures tooltip-style native reflow can coordinate the primary
    ///     container with the secondary background when the background needs
    ///     extra horizontal padding beyond the primary container's historical
    ///     width.
    /// </summary>
    [Fact]
    public void ResolveSynchronizedContainerExtent_UsesLargestResolvedWidthAcrossTooltipContainers()
    {
        var resolvedExtent = NativeTextNodeLayoutHelper.ResolveSynchronizedContainerExtent(
            primaryContainerExtent: 171,
            secondaryContainerExtent: 171,
            currentTextExtent: 143,
            measuredTextExtent: 179,
            minimumSecondaryPadding: 24);

        Assert.Equal((ushort)203, resolvedExtent);
    }

    /// <summary>
    ///     Ensures tooltip-style native reflow can coordinate the primary
    ///     container with the secondary background when translated multiline
    ///     text needs extra vertical padding in the nine-grid.
    /// </summary>
    [Fact]
    public void ResolveSynchronizedContainerExtent_UsesLargestResolvedHeightAcrossTooltipContainers()
    {
        var resolvedExtent = NativeTextNodeLayoutHelper.ResolveSynchronizedContainerExtent(
            primaryContainerExtent: 42,
            secondaryContainerExtent: 42,
            currentTextExtent: 37,
            measuredTextExtent: 55,
            minimumSecondaryPadding: 8);

        Assert.Equal((ushort)63, resolvedExtent);
    }
}
