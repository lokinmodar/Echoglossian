// <copyright file="ScreenshotCropTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Screenshots;
using Echoglossian.UIOverlays.TextPresentation;
using Echoglossian.UIOverlays.TranslationOverlay;

using System.Drawing;
using System.Numerics;

using Xunit;

namespace Echoglossian.Previewer.Tests.Screenshots;

/// <summary>
/// Covers selected-surface screenshot crop clamping.
/// </summary>
public sealed class ScreenshotCropTests
{
    /// <summary>
    /// Ensures a zero-sized render result produces an empty crop.
    /// </summary>
    [Fact]
    public void CalculateSurfaceCrop_ZeroSize_ReturnsEmptyRectangle()
    {
        var result = new TranslationOverlayRenderResult(
            true,
            new Vector2(100f, 50f),
            Vector2.Zero,
            TextPresentationBackendKind.PlainImGui);

        var crop = VeldridScreenshotCapture.CalculateSurfaceCrop(
            result,
            logicalViewportWidth: 1920,
            logicalViewportHeight: 1080,
            logicalMargin: 8f,
            framebufferScale: 1f);

        Assert.Equal(Rectangle.Empty, crop);
    }

    /// <summary>
    /// Ensures logical crop margins are clamped to the rendered target bounds.
    /// </summary>
    [Fact]
    public void CalculateSurfaceCrop_PartiallyOffScreen_ClampsToViewport()
    {
        var result = new TranslationOverlayRenderResult(
            true,
            new Vector2(-4f, 1068f),
            new Vector2(80f, 40f),
            TextPresentationBackendKind.PlainImGui);

        var crop = VeldridScreenshotCapture.CalculateSurfaceCrop(
            result,
            logicalViewportWidth: 1920,
            logicalViewportHeight: 1080,
            logicalMargin: 8f,
            framebufferScale: 1f);

        Assert.Equal(new Rectangle(0, 1060, 84, 20), crop);
    }

    /// <summary>
    /// Ensures logical coordinates and margins scale to HiDPI framebuffer pixels.
    /// </summary>
    [Fact]
    public void CalculateSurfaceCrop_HiDpiScale_ReturnsPhysicalPixelRectangle()
    {
        var result = new TranslationOverlayRenderResult(
            true,
            new Vector2(10f, 20f),
            new Vector2(100f, 50f),
            TextPresentationBackendKind.RtlTexture);

        var crop = VeldridScreenshotCapture.CalculateSurfaceCrop(
            result,
            logicalViewportWidth: 200,
            logicalViewportHeight: 100,
            logicalMargin: 8f,
            framebufferScale: 1.5f);

        Assert.Equal(new Rectangle(3, 18, 174, 99), crop);
    }
}
