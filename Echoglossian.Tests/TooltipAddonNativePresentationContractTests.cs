// <copyright file="TooltipAddonNativePresentationContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards native Tooltip addon presentation behavior that depends on text
///     reflow and config layout.
/// </summary>
public sealed class TooltipAddonNativePresentationContractTests
{
    /// <summary>
    ///     Ensures the dedicated Tooltip handler owns a custom text-node apply
    ///     path that reflows the text node and its background instead of
    ///     relying on raw <c>SetText</c> only.
    /// </summary>
    [Fact]
    public void TooltipHandler_UsesNativeTextReflowHelpers()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Common",
            "TooltipHandler.cs"));

        Assert.Contains(
            "TryApplyCustomTextNodePayload",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ApplyTextReplacementWithInferredReflow",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RestoreLayoutSnapshot",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the native Tooltip path attempts to project translated text
    ///     onto a captured readable SeString payload before falling back to
    ///     plain-text writes.
    /// </summary>
    [Fact]
    public void TooltipHandler_NativeMode_ProjectsReadablePayloadsBeforeFallback()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Common",
            "TooltipHandler.cs"));

        Assert.Contains(
            "ReadableSeStringPayloadHelper.TryCaptureMatchingPayload(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ReadableSeStringPayloadHelper.ProjectReadablePayloadBytes(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "replacementPayload:",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the shared native reflow helper can apply rich SeString
    ///     payload bytes instead of always degrading translated nodes to plain
    ///     text.
    /// </summary>
    [Fact]
    public void NativeTextNodeLayoutHelper_CanApplyReadablePayloadBytes()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "NativeTextNodeLayoutHelper.cs"));

        Assert.Contains(
            "byte[]? replacementPayload",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "textNode->SetText(replacementPayload);",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the dedicated Tooltip handler normalizes wrapped SeString
    ///     payloads before persistence and compares live native text by meaning
    ///     instead of raw wrap-marker bytes.
    /// </summary>
    [Fact]
    public void TooltipHandler_NormalizesCaptureAndComparison()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Common",
            "TooltipHandler.cs"));

        Assert.Contains(
            "NormalizeCapturedTextNodes",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TooltipTextNormalizationHelper.NormalizeForCapture",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "NativeTextComparisonNormalizationHelper.NormalizeForComparison",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures steady-state native Tooltip reapply does not restore the
    ///     cached layout snapshot before it determines whether the translated
    ///     text is already visible, otherwise the background collapses while the
    ///     translated text remains on screen.
    /// </summary>
    [Fact]
    public void TooltipHandler_SteadyStateApply_DoesNotRestoreLayoutBeforeComparison()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Common",
            "TooltipHandler.cs"));

        var methodStart = source.IndexOf(
            "private protected override bool TryApplyCustomTextNodePayload(",
            StringComparison.Ordinal);
        Assert.True(methodStart >= 0);

        var methodEnd = source.IndexOf(
            "return true;",
            methodStart,
            StringComparison.Ordinal);
        Assert.True(methodEnd > methodStart);

        var methodBody = source.Substring(methodStart, methodEnd - methodStart);

        Assert.DoesNotContain(
            "this.RestoreAppliedLayoutSnapshots(addon);",
            methodBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "NativeTextComparisonNormalizationHelper.NormalizeForComparison",
            methodBody,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the dedicated Tooltip addon keeps only a narrow horizontal
    ///     padding floor so short native balloons preserve their historical
    ///     compact sizing instead of inflating to a blanket wide background.
    /// </summary>
    [Fact]
    public void TooltipHandler_UsesNarrowHorizontalPaddingFloor()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Common",
            "TooltipHandler.cs"));

        Assert.Contains(
            "private const int MinimumTooltipBackgroundHorizontalPadding = 8;",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the dedicated Tooltip runtime does not keep polling on
    ///     every pre-draw once a translated state is already applied. Tooltip
    ///     reflow is too expensive for continuous frame-by-frame refresh.
    /// </summary>
    [Fact]
    public void TooltipHandler_DisablesContinuousPreDrawRefresh()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Common",
            "TooltipHandler.cs"));

        Assert.Contains(
            "protected override bool ShouldRefreshAppliedStateOnPreDraw()",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "return false;",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the dedicated Tooltip addon toggle is rendered after the
    ///     shared hover-tooltip appearance controls so addon-specific toggles
    ///     do not mix with formatting sliders from other flows.
    /// </summary>
    [Fact]
    public void TooltipTab_RendersDedicatedTooltipControlsAfterAppearanceSection()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "PluginUI",
            "Tabs",
            "TooltipTab.cs"));

        var appearanceIndex = source.IndexOf(
            "Resources.HoverTooltipAppearanceSectionLabel",
            StringComparison.Ordinal);
        var tooltipAddonIndex = source.IndexOf(
            "Resources.TranslateTooltipAddonLabel",
            StringComparison.Ordinal);

        Assert.True(appearanceIndex >= 0);
        Assert.True(tooltipAddonIndex >= 0);
        Assert.True(
            appearanceIndex < tooltipAddonIndex,
            "Tooltip addon controls should render after hover appearance settings.");
    }

    /// <summary>
    ///     Ensures the Tooltip addon anchored overlay is cleared and native
    ///     visibility can be restored when native presentation retakes
    ///     ownership.
    /// </summary>
    [Fact]
    public void TooltipHandler_ClearsAnchoredOverlayWhenNativeModeOwnsPresentation()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Common",
            "TooltipHandler.cs"));

        Assert.Contains(
            "ClearAnchoredOverlay",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TooltipAddonHideNativeTooltipWhenOverlayActive",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryBuildTooltipAddonOverlayFrame",
            source,
            StringComparison.Ordinal);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }
}
