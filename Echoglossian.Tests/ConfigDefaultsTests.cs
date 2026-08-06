// <copyright file="ConfigDefaultsTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers default values for hidden configuration toggles.
/// </summary>
public class ConfigDefaultsTests
{
    /// <summary>
    ///     Ensures the debug login addon-probe path stays opt-in by default.
    /// </summary>
    [Fact]
    public void EnableDebugLoginAddonProbe_DefaultsToFalse()
    {
        var config = new Config();

        Assert.False(config.EnableDebugLoginAddonProbe);
    }

    /// <summary>
    ///     Ensures the full ToastGui runtime path for supported toasts stays
    ///     opt-in by default.
    /// </summary>
    [Fact]
    public void UseToastGuiRuntimeForSupportedToasts_DefaultsToFalse()
    {
        var config = new Config();

        Assert.False(config.UseToastGuiRuntimeForSupportedToasts);
    }

    /// <summary>
    ///     Ensures the DB-first ActionDetail / ItemDetail runtime is active by
    ///     default while using the safe hover path until native payload writes
    ///     are explicitly selected.
    /// </summary>
    [Fact]
    public void TranslateTooltips_DefaultsToTrue()
    {
        var config = new Config();

        Assert.True(config.TranslateTooltips);
        Assert.Equal(
            JournalTranslationDisplayMode.TooltipTranslation,
            config.TooltipTranslationDisplayMode);
    }

    /// <summary>
    ///     Ensures hover tooltips use a slightly smaller text scale than the
    ///     global overlay default so dense RTL content remains readable.
    /// </summary>
    [Fact]
    public void HoverTooltipFontScale_DefaultsToEightyFivePercent()
    {
        var config = new Config();
        var field = typeof(Config).GetField(nameof(Config.HoverTooltipFontScale));

        Assert.NotNull(field);
        Assert.Equal(0.85f, Assert.IsType<float>(field!.GetValue(config)));
    }

    /// <summary>
    ///     Ensures the Tooltip addon anchored overlay defaults to a smaller
    ///     text scale than generic hover tooltips so the overlay better
    ///     matches the native tooltip text size.
    /// </summary>
    [Fact]
    public void TooltipAddonOverlayFontScaleAdjustment_DefaultsToSixtyFivePercent()
    {
        var config = new Config();
        var field = typeof(Config).GetField(
            nameof(Config.TooltipAddonOverlayFontScaleAdjustment));

        Assert.NotNull(field);
        Assert.Equal(0.65f, Assert.IsType<float>(field!.GetValue(config)));
    }

    /// <summary>
    ///     Ensures ActionDetail and ItemDetail overlays default to the same
    ///     smaller scale as the Tooltip addon overlay bucket so detail text
    ///     stays visually aligned with the native surfaces they anchor to.
    /// </summary>
    [Fact]
    public void ActionItemDetailOverlayFontScaleAdjustment_DefaultsToSixtyFivePercent()
    {
        var config = new Config();
        var field = typeof(Config).GetField(
            nameof(Config.ActionItemDetailOverlayFontScaleAdjustment));

        Assert.NotNull(field);
        Assert.Equal(0.65f, Assert.IsType<float>(field!.GetValue(config)));
    }

    /// <summary>
    ///     Ensures hover tooltips can use a wider layout cap than the legacy
    ///     hardcoded width so long RTL paragraphs do not collapse into a tall
    ///     column.
    /// </summary>
    [Fact]
    public void HoverTooltipMaxWidth_DefaultsToSevenHundredTwentyPixels()
    {
        var config = new Config();
        var field = typeof(Config).GetField(nameof(Config.HoverTooltipMaxWidth));

        Assert.NotNull(field);
        Assert.Equal(720f, Assert.IsType<float>(field!.GetValue(config)));
    }

    /// <summary>
    ///     Ensures texture-backed complex-script text uses a slightly tighter
    ///     default line height so multiline output does not become overly tall.
    /// </summary>
    [Fact]
    public void TexturePresentationLineHeightScale_DefaultsToNinetyPercent()
    {
        var config = new Config();
        var field = typeof(Config).GetField(
            nameof(Config.TexturePresentationLineHeightScale));

        Assert.NotNull(field);
        Assert.Equal(0.9f, Assert.IsType<float>(field!.GetValue(config)));
    }
}
