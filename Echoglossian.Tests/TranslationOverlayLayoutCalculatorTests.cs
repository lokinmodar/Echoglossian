// <copyright file="TranslationOverlayLayoutCalculatorTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TranslationOverlay;

using System.Numerics;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers ImGui-independent translation overlay geometry calculations.
/// </summary>
public class TranslationOverlayLayoutCalculatorTests
{
    /// <summary>
    /// Ensures fixed-width talk overlays preserve their configured addon-relative
    /// width and anchor above the addon.
    /// </summary>
    [Fact]
    public void Calculate_FixedTalk_UsesAddonRelativeWidthAndAboveAddonPosition()
    {
        var request = CreateRequest(
            TranslationOverlaySurfaceId.Talk,
            addonPosition: new Vector2(300f, 400f),
            addonSize: new Vector2(500f, 100f),
            previousWindowSize: new Vector2(500f, 80f),
            measuredTextSize: new Vector2(900f, 48f));

        var result = TranslationOverlayLayoutCalculator.Calculate(request);

        Assert.Equal(new Vector2(300f, 300f), result.RequestedPosition);
        Assert.Equal(500f, result.RequestedSize.X);
        Assert.Equal(476f, result.ContentWrapWidth);
    }

    /// <summary>
    /// Ensures BattleTalk can expand for measured text without exceeding its
    /// configured viewport fractions.
    /// </summary>
    [Fact]
    public void Calculate_ExpandingBattleTalk_ClampsWidthToViewportFraction()
    {
        var request = CreateRequest(
            TranslationOverlaySurfaceId.BattleTalk,
            viewportSize: new Vector2(1000f, 700f),
            addonSize: new Vector2(500f, 80f),
            measuredTextSize: new Vector2(2000f, 48f));

        var result = TranslationOverlayLayoutCalculator.Calculate(request);

        Assert.Equal(800f, result.RequestedSize.X);
        Assert.Equal(776f, result.ContentWrapWidth);
    }

    /// <summary>
    /// Ensures MiniTalk auto-sizes from measured content and remains centered on
    /// its source addon.
    /// </summary>
    [Fact]
    public void Calculate_CenteredMiniTalk_AutoSizesAndCentersOnAddon()
    {
        var request = CreateRequest(
            TranslationOverlaySurfaceId.MiniTalk,
            addonPosition: new Vector2(200f, 100f),
            addonSize: new Vector2(300f, 120f),
            previousWindowSize: new Vector2(240f, 60f),
            measuredTextSize: new Vector2(400f, 48f));

        var result = TranslationOverlayLayoutCalculator.Calculate(request);

        Assert.Equal(new Vector2(230f, 130f), result.RequestedPosition);
        Assert.Equal(424f, result.RequestedSize.X);
        Assert.Equal(400f, result.ContentWrapWidth);
    }

    /// <summary>
    /// Ensures no-background toast geometry still uses measured text sizing.
    /// </summary>
    [Fact]
    public void Calculate_NoBackgroundToast_UsesMeasuredTextWidth()
    {
        var request = CreateRequest(
            TranslationOverlaySurfaceId.ErrorToast,
            addonSize: new Vector2(300f, 60f),
            measuredTextSize: new Vector2(500f, 30f),
            configure: config => config with
            {
                NoBackground = true,
                BackgroundOpacity = 0f,
            });

        var result = TranslationOverlayLayoutCalculator.Calculate(request);

        Assert.Equal(524f, result.RequestedSize.X);
        Assert.Equal(500f, result.ContentWrapWidth);
    }

    /// <summary>
    /// Ensures position corrections and requested bounds stay inside the viewport.
    /// </summary>
    [Fact]
    public void Calculate_OffscreenCorrectedOverlay_ClampsToViewport()
    {
        var request = CreateRequest(
            TranslationOverlaySurfaceId.Talk,
            viewportPosition: new Vector2(100f, 50f),
            viewportSize: new Vector2(800f, 600f),
            addonPosition: new Vector2(850f, 80f),
            addonSize: new Vector2(300f, 80f),
            previousWindowSize: new Vector2(300f, 60f),
            configure: config => config with { PosCorrection = new Vector2(100f, -100f) });

        var result = TranslationOverlayLayoutCalculator.Calculate(request);

        Assert.Equal(new Vector2(600f, 50f), result.RequestedPosition);
    }

    /// <summary>
    /// Creates a deterministic pure-layout request for one overlay surface.
    /// </summary>
    private static TranslationOverlayLayoutRequest CreateRequest(
        TranslationOverlaySurfaceId surfaceId,
        Vector2? viewportPosition = null,
        Vector2? viewportSize = null,
        Vector2? addonPosition = null,
        Vector2? addonSize = null,
        Vector2? previousWindowSize = null,
        Vector2? measuredTextSize = null,
        Func<TranslationWindowConfig, TranslationWindowConfig>? configure = null)
    {
        var config = TranslationWindowConfig.ForSurface(new Config(), surfaceId);
        if (configure != null)
        {
            config = configure(config);
        }

        return new TranslationOverlayLayoutRequest(
            viewportPosition ?? Vector2.Zero,
            viewportSize ?? new Vector2(1920f, 1080f),
            addonPosition ?? new Vector2(100f, 200f),
            addonSize ?? new Vector2(300f, 80f),
            previousWindowSize ?? new Vector2(300f, 60f),
            measuredTextSize ?? new Vector2(200f, 30f),
            Vector2.Zero,
            24f,
            config);
    }
}
