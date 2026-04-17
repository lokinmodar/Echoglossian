// <copyright file="TranslationDisplayModeHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers shared display-mode behavior used by quest and DB-first UI
///     surfaces.
/// </summary>
public class TranslationDisplayModeHelperTests
{
    /// <summary>
    ///     Ensures overlay-only languages always collapse to tooltip-only mode,
    ///     even when a native-writing mode is configured.
    /// </summary>
    [Fact]
    public void OverlayOnlyLanguage_ForcesTooltipTranslationMode()
    {
        var effectiveMode = TranslationDisplayModeHelper.GetEffectiveDisplayMode(
            JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
            overlayOnlyLanguage: true);

        Assert.Equal(
            JournalTranslationDisplayMode.TooltipTranslation,
            effectiveMode);
        Assert.True(
            TranslationDisplayModeHelper.UsesHoverTooltips(
                JournalTranslationDisplayMode.NativeUiTranslation,
                overlayOnlyLanguage: true));
        Assert.True(
            TranslationDisplayModeHelper.UsesOverlayPresentation(
                JournalTranslationDisplayMode.NativeUiTranslation,
                overlayOnlyLanguage: true));
        Assert.False(
            TranslationDisplayModeHelper.WritesNativeTranslation(
                JournalTranslationDisplayMode.NativeUiTranslation,
                overlayOnlyLanguage: true));
        Assert.False(
            TranslationDisplayModeHelper.ShowsOriginalTooltips(
                JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
                overlayOnlyLanguage: true));
        Assert.False(
            TranslationDisplayModeHelper.ShowsOriginalOverlayText(
                JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
                overlayOnlyLanguage: true));
    }

    /// <summary>
    ///     Ensures swap mode still behaves normally when native font support is
    ///     available.
    /// </summary>
    [Fact]
    public void NativeFontLanguages_KeepConfiguredSwapMode()
    {
        Assert.True(
            TranslationDisplayModeHelper.WritesNativeTranslation(
                JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips));
        Assert.True(
            TranslationDisplayModeHelper.UsesHoverTooltips(
                JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips));
        Assert.True(
            TranslationDisplayModeHelper.UsesOverlayPresentation(
                JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips));
        Assert.True(
            TranslationDisplayModeHelper.ShowsOriginalTooltips(
                JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips));
        Assert.True(
            TranslationDisplayModeHelper.ShowsOriginalOverlayText(
                JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips));
    }
}
