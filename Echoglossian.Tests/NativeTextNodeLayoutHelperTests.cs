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
    ///     Ensures wrapped text measurement preserves the larger live height by
    ///     default when it may represent intentional game-owned padding rather
    ///     than a stale plugin-applied extent.
    /// </summary>
    [Fact]
    public void ResolveMeasuredTextExtent_PreservesWrappedLiveHeightByDefault_WhenItExceedsDrawHeight()
    {
        var resolved = NativeTextNodeLayoutHelper.ResolveMeasuredTextExtent(
            liveWidth: 392,
            liveHeight: 118,
            drawWidth: 368,
            drawHeight: 42,
            textFlags: TextFlags.WordWrap | TextFlags.MultiLine | TextFlags.AutoAdjustNodeSize);

        Assert.Equal((ushort)392, resolved.Width);
        Assert.Equal((ushort)118, resolved.Height);
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
    ///     Ensures wrapped native measurement does not keep a stale translated
    ///     high-water height when the live node was already left oversized by a
    ///     previous apply.
    /// </summary>
    [Fact]
    public void ResolveMeasuredTextExtent_IgnoresStaleWrappedLiveHeight_WhenDrawHeightIsShorter()
    {
        var resolved = NativeTextNodeLayoutHelper.ResolveMeasuredTextExtent(
            liveWidth: 392,
            liveHeight: 118,
            drawWidth: 368,
            drawHeight: 42,
            textFlags: TextFlags.WordWrap | TextFlags.MultiLine | TextFlags.AutoAdjustNodeSize,
            preferCompactWrappedHeight: true);

        Assert.Equal((ushort)392, resolved.Width);
        Assert.Equal((ushort)42, resolved.Height);
    }

    /// <summary>
    ///     Ensures pre-apply native width measurement honors explicit tooltip
    ///     line breaks instead of treating the whole payload as one unwrapped
    ///     line.
    /// </summary>
    [Fact]
    public void SplitMeasurementLines_SplitsExplicitCarriageReturnBreaks()
    {
        var lines = NativeTextNodeLayoutHelper.SplitMeasurementLines(
            "Linha um\rLinha dois\rLinha tres");

        Assert.Equal(
            [
                "Linha um",
                "Linha dois",
                "Linha tres",
            ],
            lines);
    }

    /// <summary>
    ///     Ensures the helper normalizes mixed Windows and Unix line endings so
    ///     native pre-apply width checks can measure the widest explicit line
    ///     instead of a concatenated paragraph.
    /// </summary>
    [Fact]
    public void SplitMeasurementLines_NormalizesMixedLineEndings()
    {
        var lines = NativeTextNodeLayoutHelper.SplitMeasurementLines(
            "Linha um\r\nLinha dois\nLinha tres");

        Assert.Equal(
            [
                "Linha um",
                "Linha dois",
                "Linha tres",
            ],
            lines);
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
    ///     Ensures post-apply native auto-sizing can keep a wider live text
    ///     node when the caller explicitly opted into preserving measured
    ///     overflow.
    /// </summary>
    [Fact]
    public void ResolvePostApplyTextNodeWidth_PreservesWiderNativeWidth_WhenMeasuredOverflowIsEnabled()
    {
        var resolved = NativeTextNodeLayoutHelper.ResolvePostApplyTextNodeWidth(
            resolvedWrapWidth: 223,
            postApplyNodeWidth: 256,
            preferMeasuredOverflow: true);

        Assert.Equal((ushort)256, resolved);
    }

    /// <summary>
    ///     Ensures callers that do not opt into measured overflow still clamp
    ///     the live text node back to the preserved wrap width after native
    ///     reflow runs.
    /// </summary>
    [Fact]
    public void ResolvePostApplyTextNodeWidth_ReclampsToWrapWidth_WhenMeasuredOverflowIsDisabled()
    {
        var resolved = NativeTextNodeLayoutHelper.ResolvePostApplyTextNodeWidth(
            resolvedWrapWidth: 223,
            postApplyNodeWidth: 256,
            preferMeasuredOverflow: false);

        Assert.Equal((ushort)223, resolved);
    }

    /// <summary>
    ///     Ensures post-apply sizing still falls back to the preserved wrap
    ///     width when the native node did not report any width after reflow.
    /// </summary>
    [Fact]
    public void ResolvePostApplyTextNodeWidth_FallsBackToWrapWidth_WhenNativeWidthIsMissing()
    {
        var resolved = NativeTextNodeLayoutHelper.ResolvePostApplyTextNodeWidth(
            resolvedWrapWidth: 223,
            postApplyNodeWidth: 0,
            preferMeasuredOverflow: true);

        Assert.Equal((ushort)223, resolved);
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

    /// <summary>
    ///     Ensures detached-container synchronization keeps the historical
    ///     largest container extent by default so tooltip and toast surfaces do
    ///     not shrink their root containers unexpectedly.
    /// </summary>
    [Fact]
    public void ResolveSynchronizedContainerExtent_PreservesLargestDetachedBaseline_ByDefault()
    {
        var resolvedExtent = NativeTextNodeLayoutHelper.ResolveSynchronizedContainerExtent(
            primaryContainerExtent: 192,
            secondaryContainerExtent: 42,
            currentTextExtent: 37,
            measuredTextExtent: 38,
            minimumSecondaryPadding: 0);

        Assert.Equal((ushort)192, resolvedExtent);
    }

    /// <summary>
    ///     Ensures detached MiniTalk-style component roots can explicitly prefer
    ///     the compact bubble baseline when the detached primary container is
    ///     the stale oversize surface from a recycled slot.
    /// </summary>
    [Fact]
    public void ResolveSynchronizedContainerExtent_PrefersCompactDetachedBaseline_WhenRequestedAndPrimaryIsStale()
    {
        var resolvedExtent = NativeTextNodeLayoutHelper.ResolveSynchronizedContainerExtent(
            primaryContainerExtent: 192,
            secondaryContainerExtent: 42,
            currentTextExtent: 37,
            measuredTextExtent: 38,
            minimumSecondaryPadding: 0,
            preferCompactDetachedBaseline: true);

        Assert.Equal((ushort)43, resolvedExtent);
    }

    /// <summary>
    ///     Ensures detached MiniTalk-style component roots can also recover
    ///     when the visible nine-grid background is the stale oversize surface
    ///     from a recycled slot and the detached primary height is the compact
    ///     baseline that should win.
    /// </summary>
    [Fact]
    public void ResolveSynchronizedContainerExtent_PrefersCompactDetachedBaseline_WhenRequestedAndSecondaryIsStale()
    {
        var resolvedExtent = NativeTextNodeLayoutHelper.ResolveSynchronizedContainerExtent(
            primaryContainerExtent: 42,
            secondaryContainerExtent: 192,
            currentTextExtent: 37,
            measuredTextExtent: 38,
            minimumSecondaryPadding: 0,
            preferCompactDetachedBaseline: true);

        Assert.Equal((ushort)43, resolvedExtent);
    }
}
