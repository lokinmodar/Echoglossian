// <copyright file="TranslationOverlayRendererTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TextPresentation;
using Echoglossian.UIOverlays.TranslationOverlay;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers renderer decisions that do not require a live ImGui runtime.
/// </summary>
public sealed class TranslationOverlayRendererTests
{
    /// <summary>
    /// Ensures the inline RTL title path only activates after the title block is available.
    /// </summary>
    /// <param name="hasTitleBlock">Whether the RTL title texture is ready.</param>
    /// <param name="expected">The expected helper result.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ShouldUseInlineRtlTitle_RequiresReadyTitleBlock(
        bool hasTitleBlock,
        bool expected)
    {
        var actual = TranslationOverlayRenderer.ShouldUseInlineRtlTitle(
            TextPresentationBackendKind.RtlTexture,
            forceShowTitle: true,
            resolvedTitle: "Battle Talk",
            hasTitleBlock);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Ensures non-RTL or untitled paths do not hide the normal title bar.
    /// </summary>
    [Fact]
    public void ShouldUseInlineRtlTitle_NonRtlOrMissingTitle_ReturnsFalse()
    {
        Assert.False(
            TranslationOverlayRenderer.ShouldUseInlineRtlTitle(
                TextPresentationBackendKind.PlainImGui,
                forceShowTitle: true,
                resolvedTitle: "Talk",
                hasTitleBlock: true));
        Assert.False(
            TranslationOverlayRenderer.ShouldUseInlineRtlTitle(
                TextPresentationBackendKind.RtlTexture,
                forceShowTitle: true,
                resolvedTitle: string.Empty,
                hasTitleBlock: true));
        Assert.False(
            TranslationOverlayRenderer.ShouldUseInlineRtlTitle(
                TextPresentationBackendKind.RtlTexture,
                forceShowTitle: false,
                resolvedTitle: "Talk",
                hasTitleBlock: true));
    }

    /// <summary>
    /// Ensures the RTL fallback title bar shows the resolved title while keeping
    /// a stable ImGui identity suffix.
    /// </summary>
    [Fact]
    public void BuildWindowLabel_RtlFallbackUsesResolvedTitleWithStableSuffix()
    {
        var actual = TranslationOverlayRenderer.BuildWindowLabel(
            TextPresentationBackendKind.RtlTexture,
            defaultTitle: "Talk",
            resolvedTitle: "Krile",
            overlayId: 42,
            useInlineRtlTitle: false);

        Assert.Equal("Krile##overlay-42", actual);
    }

    /// <summary>
    /// Ensures the inline RTL title path keeps the default hidden window label.
    /// </summary>
    [Fact]
    public void BuildWindowLabel_RtlInlineTitleKeepsDefaultStableLabel()
    {
        var actual = TranslationOverlayRenderer.BuildWindowLabel(
            TextPresentationBackendKind.RtlTexture,
            defaultTitle: "Talk",
            resolvedTitle: "Krile",
            overlayId: 42,
            useInlineRtlTitle: true);

        Assert.Equal("Talk##overlay-42", actual);
    }

    /// <summary>
    /// Ensures the plain ImGui title path also preserves a stable hidden
    /// identity suffix when the visible title changes.
    /// </summary>
    [Fact]
    public void BuildWindowLabel_PlainImGuiUsesResolvedTitleWithStableSuffix()
    {
        var actual = TranslationOverlayRenderer.BuildWindowLabel(
            TextPresentationBackendKind.PlainImGui,
            defaultTitle: "Talk",
            resolvedTitle: "Krile",
            overlayId: 42,
            useInlineRtlTitle: false);

        Assert.Equal("Krile##overlay-42", actual);
    }

    /// <summary>
    /// Ensures raw SeString line-break payload markers become real overlay line
    /// breaks instead of invalid glyph boxes.
    /// </summary>
    [Fact]
    public void NormalizeOverlayText_RawSeStringLineBreaksBecomeNewLines()
    {
        const string rawText =
            "N\u00e3o consigo \u0002\u0010\u0001\u0003ganhar nada... " +
            "\u0002\u0010\u0001\u0003Estou \u0002\u0010\u0001\u0003come\u00e7ando.";

        var actual = TranslationOverlayTextNormalizationHelper.NormalizeForDisplay(rawText);

        Assert.Equal(
            "N\u00e3o consigo \nganhar nada... \nEstou \ncome\u00e7ando.",
            actual);
    }

    /// <summary>
    /// Ensures carriage-return line endings and residual control bytes are
    /// normalized before the overlay renderer measures or draws the text.
    /// </summary>
    [Fact]
    public void NormalizeOverlayText_NormalizesCarriageReturnsAndDropsResidualControls()
    {
        const string rawText = "But... But... You said this\rwas your first time playing!\u0002";

        var actual = TranslationOverlayTextNormalizationHelper.NormalizeForDisplay(rawText);

        Assert.Equal("But... But... You said this\nwas your first time playing!", actual);
    }
}
