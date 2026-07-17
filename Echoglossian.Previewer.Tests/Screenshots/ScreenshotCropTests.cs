// <copyright file="ScreenshotCropTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Screenshots;
using Echoglossian.Previewer.Scenarios;
using Echoglossian.UIOverlays.TextPresentation;
using Echoglossian.UIOverlays.TranslationOverlay;

using System.Drawing;
using System.Numerics;

using VeldridPixelFormat = Veldrid.PixelFormat;

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

    /// <summary>
    /// Ensures interactive surface crops clamp against the active host display
    /// bounds instead of the scenario's logical viewport dimensions.
    /// </summary>
    [Fact]
    public void CalculateInteractiveSurfaceCrop_UsesDisplayBoundsInsteadOfScenarioViewport()
    {
        var request = new ScreenshotRequest(
            ScreenshotMode.Surface,
            new PreviewScenario(
                "talk",
                "Talk",
                TranslationOverlaySurfaceId.Talk,
                new PreviewAddonBounds(0f, 0f, 10f, 10f),
                "body",
                "title",
                true,
                false),
            new PreviewViewportPreset("scenario", 640, 360),
            OutputDirectory: "artifacts");
        var result = new TranslationOverlayRenderResult(
            true,
            new Vector2(700f, 100f),
            new Vector2(100f, 100f),
            TextPresentationBackendKind.PlainImGui);

        var crop = Program.CalculateInteractiveSurfaceCrop(
            request,
            result,
            new Vector2(1400f, 900f),
            new Vector2(1400f, 900f));

        Assert.Equal(new Rectangle(692, 92, 116, 116), crop);
    }

    /// <summary>
    /// Ensures interactive surface crops scale into physical framebuffer pixels
    /// when the host display uses a HiDPI backing target.
    /// </summary>
    [Fact]
    public void CalculateInteractiveSurfaceCrop_UsesFramebufferScaleForHiDpiHosts()
    {
        var request = new ScreenshotRequest(
            ScreenshotMode.Surface,
            new PreviewScenario(
                "talk",
                "Talk",
                TranslationOverlaySurfaceId.Talk,
                new PreviewAddonBounds(0f, 0f, 10f, 10f),
                "body",
                "title",
                true,
                false),
            new PreviewViewportPreset("scenario", 640, 360),
            OutputDirectory: "artifacts");
        var result = new TranslationOverlayRenderResult(
            true,
            new Vector2(10f, 20f),
            new Vector2(100f, 50f),
            TextPresentationBackendKind.PlainImGui);

        var crop = Program.CalculateInteractiveSurfaceCrop(
            request,
            result,
            new Vector2(1400f, 900f),
            new Vector2(2800f, 1800f));

        Assert.Equal(new Rectangle(4, 24, 232, 132), crop);
    }

    /// <summary>
    /// Ensures logical plugin-window bounds scale into physical framebuffer
    /// pixels before interactive capture.
    /// </summary>
    [Fact]
    public void CalculateInteractiveWindowCrop_UsesFramebufferScaleForHiDpiHosts()
    {
        var crop = Program.CalculateInteractiveWindowCrop(
            new Rectangle(100, 50, 400, 200),
            new Vector2(1400f, 900f),
            new Vector2(2800f, 1800f));

        Assert.Equal(new Rectangle(200, 100, 800, 400), crop);
    }

    /// <summary>
    /// Ensures screenshot readback accepts only the supported 32-bit formats.
    /// </summary>
    [Theory]
    [InlineData(VeldridPixelFormat.R8_G8_B8_A8_UNorm, true)]
    [InlineData(VeldridPixelFormat.R8_G8_B8_A8_UNorm_SRgb, true)]
    [InlineData(VeldridPixelFormat.B8_G8_R8_A8_UNorm, true)]
    [InlineData(VeldridPixelFormat.B8_G8_R8_A8_UNorm_SRgb, true)]
    [InlineData(VeldridPixelFormat.R16_G16_B16_A16_Float, false)]
    [InlineData(VeldridPixelFormat.R10_G10_B10_A2_UNorm, false)]
    public void SupportsReadbackFormat_RecognizesSupportedFormats(
        VeldridPixelFormat format,
        bool expected)
    {
        Assert.Equal(expected, VeldridScreenshotCapture.SupportsReadbackFormat(format));
    }
}
