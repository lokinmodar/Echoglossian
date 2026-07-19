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
    ///     Ensures the validated DB-first ActionDetail / ItemDetail runtime is
    ///     active by default instead of staying dormant for release builds.
    /// </summary>
    [Fact]
    public void TranslateTooltips_DefaultsToTrue()
    {
        var config = new Config();

        Assert.True(config.TranslateTooltips);
        Assert.Equal(
            JournalTranslationDisplayMode.NativeUiTranslation,
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
