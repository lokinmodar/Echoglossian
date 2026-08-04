// <copyright file="NamePlateNativePresentationPlanTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.NamePlates;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers the pure NamePlate native presentation semantics used by overlay and
/// swap modes.
/// </summary>
public sealed class NamePlateNativePresentationPlanTests
{
    /// <summary>
    /// Verifies that native mode writes the translated name and clears the
    /// auxiliary title line.
    /// </summary>
    [Fact]
    public void Create_native_mode_writes_translated_name_and_clears_title()
    {
        var plan = NamePlateNativePresentationPlan.Create(
            originalText: "Lush Vegetation",
            translatedText: "Vegetação Luxuriante",
            displayMode: JournalTranslationDisplayMode.NativeUiTranslation,
            overlayOnlyLanguage: false);

        Assert.True(plan.WritesTranslatedName);
        Assert.Equal("Vegetação Luxuriante", plan.NameText);
        Assert.False(plan.ShowsTitle);
        Assert.Null(plan.TitleText);
    }

    /// <summary>
    /// Verifies that overlay-only mode preserves the native name and uses the
    /// translated text on the title line.
    /// </summary>
    [Fact]
    public void Create_overlay_only_mode_keeps_original_name_and_uses_translated_title()
    {
        var plan = NamePlateNativePresentationPlan.Create(
            originalText: "Lush Vegetation",
            translatedText: "Vegetação Luxuriante",
            displayMode: JournalTranslationDisplayMode.TooltipTranslation,
            overlayOnlyLanguage: false);

        Assert.False(plan.WritesTranslatedName);
        Assert.Null(plan.NameText);
        Assert.True(plan.ShowsTitle);
        Assert.Equal("Vegetação Luxuriante", plan.TitleText);
    }

    /// <summary>
    /// Verifies that swap mode writes the translated name and shows the
    /// original source text on the title line.
    /// </summary>
    [Fact]
    public void Create_swap_mode_writes_translated_name_and_uses_original_title()
    {
        var plan = NamePlateNativePresentationPlan.Create(
            originalText: "Lush Vegetation",
            translatedText: "Vegetação Luxuriante",
            displayMode:
            JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
            overlayOnlyLanguage: false);

        Assert.True(plan.WritesTranslatedName);
        Assert.Equal("Vegetação Luxuriante", plan.NameText);
        Assert.True(plan.ShowsTitle);
        Assert.Equal("Lush Vegetation", plan.TitleText);
    }

    /// <summary>
    /// Verifies that overlay-only languages force the non-native presentation
    /// path even when the configured mode would normally write native text.
    /// </summary>
    [Fact]
    public void Create_overlay_only_language_forces_translated_title_without_native_name_write()
    {
        var plan = NamePlateNativePresentationPlan.Create(
            originalText: "Lush Vegetation",
            translatedText: "Vegetação Luxuriante",
            displayMode: JournalTranslationDisplayMode.NativeUiTranslation,
            overlayOnlyLanguage: true);

        Assert.False(plan.WritesTranslatedName);
        Assert.Null(plan.NameText);
        Assert.True(plan.ShowsTitle);
        Assert.Equal("Vegetação Luxuriante", plan.TitleText);
    }
}
