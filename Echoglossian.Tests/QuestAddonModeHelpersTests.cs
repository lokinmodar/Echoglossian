// <copyright file="QuestAddonModeHelpersTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Quest;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers quest-family display-mode behavior that must stay aligned with
///     the shared overlay-only language policy.
/// </summary>
public sealed class QuestAddonModeHelpersTests
{
    /// <summary>
    ///     Ensures overlay-only languages collapse quest-family runtime
    ///     semantics away from native writes.
    /// </summary>
    [Fact]
    public void OverlayOnlyLanguage_forces_quest_family_into_tooltip_semantics()
    {
        Assert.True(QuestAddonModeHelpers.UsesHoverTooltips(
            JournalTranslationDisplayMode.NativeUiTranslation,
            overlayOnlyLanguage: true));
        Assert.False(QuestAddonModeHelpers.WritesNativeTranslation(
            JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
            overlayOnlyLanguage: true));
        Assert.False(QuestAddonModeHelpers.ShowsOriginalTooltips(
            JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
            overlayOnlyLanguage: true));
        Assert.True(QuestAddonModeHelpers.CanRenderHoverTooltip(
            JournalTranslationDisplayMode.NativeUiTranslation,
            translatedPayloadReady: true,
            overlayOnlyLanguage: true));
        Assert.False(QuestAddonModeHelpers.ShouldRemoveDiacritics(
            JournalTranslationDisplayMode.NativeUiTranslation,
            removeDiacriticsWhenUsingReplacementQuest: true,
            overlayOnlyLanguage: true));
    }

    /// <summary>
    ///     Ensures native-font languages keep the configured swap semantics.
    /// </summary>
    [Fact]
    public void NativeFontLanguage_preserves_existing_swap_semantics()
    {
        Assert.True(QuestAddonModeHelpers.UsesHoverTooltips(
            JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips));
        Assert.True(QuestAddonModeHelpers.WritesNativeTranslation(
            JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips));
        Assert.True(QuestAddonModeHelpers.ShowsOriginalTooltips(
            JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips));
    }
}
