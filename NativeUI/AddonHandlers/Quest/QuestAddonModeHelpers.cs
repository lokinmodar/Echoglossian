// <copyright file="QuestAddonModeHelpers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Shared display-mode helpers for quest-family addon handlers.
/// </summary>
internal static class QuestAddonModeHelpers
{
  /// <summary>
  ///     Gets whether the quest family display mode should render hover
  ///     tooltips.
  /// </summary>
  /// <param name="displayMode">The configured display mode.</param>
  /// <returns><c>true</c> when hover tooltips should be rendered.</returns>
  internal static bool UsesHoverTooltips(
      JournalTranslationDisplayMode displayMode)
  {
    return displayMode != JournalTranslationDisplayMode.NativeUiTranslation;
  }

  /// <summary>
  ///     Gets whether the quest family display mode should write translated
  ///     text into the native addon.
  /// </summary>
  /// <param name="displayMode">The configured display mode.</param>
  /// <returns><c>true</c> when native addon text should be replaced.</returns>
  internal static bool WritesNativeTranslation(
      JournalTranslationDisplayMode displayMode)
  {
    return displayMode != JournalTranslationDisplayMode.TooltipTranslation;
  }

  /// <summary>
  ///     Gets whether the quest family hover tooltips should show the original
  ///     text.
  /// </summary>
  /// <param name="displayMode">The configured display mode.</param>
  /// <returns><c>true</c> when hover tooltips should keep the original text.</returns>
  internal static bool ShowsOriginalTooltips(
      JournalTranslationDisplayMode displayMode)
  {
    return displayMode ==
           JournalTranslationDisplayMode
               .NativeUiTranslationWithOriginalTooltips;
  }

  /// <summary>
  ///     Gets whether a quest-family hover tooltip may be rendered for the
  ///     current display mode, given the readiness of the translated payload
  ///     backing that tooltip.
  /// </summary>
  /// <param name="displayMode">The configured display mode.</param>
  /// <param name="translatedPayloadReady">
  ///     Whether the translated payload required by the current mode is fully
  ///     ready.
  /// </param>
  /// <returns>
  ///     <c>true</c> when the tooltip may be rendered for the current mode.
  /// </returns>
  internal static bool CanRenderHoverTooltip(
      JournalTranslationDisplayMode displayMode,
      bool translatedPayloadReady)
  {
    return UsesHoverTooltips(displayMode) && translatedPayloadReady;
  }

  /// <summary>
  ///     Gets whether translated quest text should be normalized before being
  ///     written into the native UI.
  /// </summary>
  /// <param name="displayMode">The configured display mode.</param>
  /// <param name="removeDiacriticsWhenUsingReplacementQuest">
  ///     Whether the quest-family diacritics toggle is enabled.
  /// </param>
  /// <returns><c>true</c> when normalization should be applied.</returns>
  internal static bool ShouldRemoveDiacritics(
      JournalTranslationDisplayMode displayMode,
      bool removeDiacriticsWhenUsingReplacementQuest)
  {
    return WritesNativeTranslation(displayMode) &&
           removeDiacriticsWhenUsingReplacementQuest;
  }
}
