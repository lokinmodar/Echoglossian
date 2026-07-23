// <copyright file="TranslationOverlayRichOriginalPresentationTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TextPresentation;
using Echoglossian.UIOverlays.TranslationOverlay;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers rich-original state and renderer selection for translation overlays.
/// </summary>
public sealed class TranslationOverlayRichOriginalPresentationTests
{
    /// <summary>
    /// Ensures an overlay keeps a rich payload only while it is explicitly
    /// showing original swap content.
    /// </summary>
    [Fact]
    public void UpdateContentPresentation_NonSwapContent_ClearsRichPayload()
    {
        var overlay = new TranslationOverlay();
        var presentation = new RichOriginalTextPresentation(
            "Original",
            new byte[] { 0x02, 0x03 });

        overlay.UpdateContentPresentation(
            displaysOriginalSwapText: true,
            presentation);

        Assert.True(overlay.DisplaysOriginalSwapText);
        Assert.Same(presentation, overlay.RichOriginalTextPresentation);

        overlay.UpdateContentPresentation(
            displaysOriginalSwapText: false,
            presentation);

        Assert.False(overlay.DisplaysOriginalSwapText);
        Assert.Null(overlay.RichOriginalTextPresentation);
    }

    /// <summary>
    /// Ensures clearing an overlay's retained content also clears its owned
    /// original payload state.
    /// </summary>
    [Fact]
    public void ClearContentPresentation_ClearsSwapAndPayloadState()
    {
        var overlay = new TranslationOverlay();
        overlay.UpdateContentPresentation(
            displaysOriginalSwapText: true,
            new RichOriginalTextPresentation(
                "Original",
                new byte[] { 0x02, 0x03 }));

        overlay.ClearContentPresentation();

        Assert.False(overlay.DisplaysOriginalSwapText);
        Assert.Null(overlay.RichOriginalTextPresentation);
    }

    /// <summary>
    /// Ensures rich overlay rendering is restricted to the normal left-aligned
    /// ImGui swap path and preserves the existing alignment fallbacks.
    /// </summary>
    [Fact]
    public void ShouldRenderRichOriginalText_RequiresPlainUnalignedSwapPresentation()
    {
        var presentation = new RichOriginalTextPresentation(
            "Original",
            new byte[] { 0x02, 0x03 });

        Assert.True(TranslationOverlayRenderer.ShouldRenderRichOriginalText(
            TextPresentationBackendKind.PlainImGui,
            displaysOriginalSwapText: true,
            hasSpecialAlignment: false,
            presentation));
        Assert.False(TranslationOverlayRenderer.ShouldRenderRichOriginalText(
            TextPresentationBackendKind.PlainImGui,
            displaysOriginalSwapText: true,
            hasSpecialAlignment: true,
            presentation));
        Assert.False(TranslationOverlayRenderer.ShouldRenderRichOriginalText(
            TextPresentationBackendKind.RtlTexture,
            displaysOriginalSwapText: true,
            hasSpecialAlignment: false,
            presentation));
    }
}
