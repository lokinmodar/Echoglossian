// <copyright file="TranslationDisplayModeHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Shared helpers for per-addon translation display modes.
/// </summary>
internal static class TranslationDisplayModeHelper
{
    /// <summary>
    ///     Resolves the effective display mode after applying language
    ///     limitations that forbid native UI mutation.
    /// </summary>
    /// <param name="displayMode">The configured display mode.</param>
    /// <param name="overlayOnlyLanguage">
    ///     Whether the selected language only supports overlays and custom
    ///     tooltips.
    /// </param>
    /// <returns>The effective runtime display mode.</returns>
    public static JournalTranslationDisplayMode GetEffectiveDisplayMode(
        JournalTranslationDisplayMode displayMode,
        bool overlayOnlyLanguage)
    {
        return overlayOnlyLanguage
            ? JournalTranslationDisplayMode.TooltipTranslation
            : displayMode;
    }

    /// <summary>
    ///     Gets whether a display mode should register hover tooltips.
    /// </summary>
    /// <param name="displayMode">The configured display mode.</param>
    /// <param name="overlayOnlyLanguage">
    ///     Whether the selected language only supports overlays and custom
    ///     tooltips.
    /// </param>
    /// <returns><c>true</c> when hover tooltips should be used.</returns>
    public static bool UsesHoverTooltips(
        JournalTranslationDisplayMode displayMode,
        bool overlayOnlyLanguage = false)
    {
        displayMode = GetEffectiveDisplayMode(displayMode, overlayOnlyLanguage);
        return displayMode != JournalTranslationDisplayMode.NativeUiTranslation;
    }

    /// <summary>
    ///     Gets whether a display mode should render through an ImGui overlay.
    /// </summary>
    /// <param name="displayMode">The configured display mode.</param>
    /// <param name="overlayOnlyLanguage">
    ///     Whether the selected language only supports overlays and custom
    ///     tooltips.
    /// </param>
    /// <returns><c>true</c> when the overlay path should be used.</returns>
    public static bool UsesOverlayPresentation(
        JournalTranslationDisplayMode displayMode,
        bool overlayOnlyLanguage = false)
    {
        return UsesHoverTooltips(displayMode, overlayOnlyLanguage);
    }

    /// <summary>
    ///     Gets whether a display mode should write translated text into the
    ///     native addon.
    /// </summary>
    /// <param name="displayMode">The configured display mode.</param>
    /// <param name="overlayOnlyLanguage">
    ///     Whether the selected language only supports overlays and custom
    ///     tooltips.
    /// </param>
    /// <returns><c>true</c> when native text should be rewritten.</returns>
    public static bool WritesNativeTranslation(
        JournalTranslationDisplayMode displayMode,
        bool overlayOnlyLanguage = false)
    {
        displayMode = GetEffectiveDisplayMode(displayMode, overlayOnlyLanguage);
        return displayMode != JournalTranslationDisplayMode.TooltipTranslation;
    }

    /// <summary>
    ///     Gets whether hover tooltips should show the original text rather than
    ///     the translated text.
    /// </summary>
    /// <param name="displayMode">The configured display mode.</param>
    /// <param name="overlayOnlyLanguage">
    ///     Whether the selected language only supports overlays and custom
    ///     tooltips.
    /// </param>
    /// <returns><c>true</c> when hover tooltips should show the original text.</returns>
    public static bool ShowsOriginalTooltips(
        JournalTranslationDisplayMode displayMode,
        bool overlayOnlyLanguage = false)
    {
        displayMode = GetEffectiveDisplayMode(displayMode, overlayOnlyLanguage);
        return displayMode ==
               JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips;
    }

    /// <summary>
    ///     Gets whether an overlay should show the original text while the
    ///     native UI receives translated text.
    /// </summary>
    /// <param name="displayMode">The configured display mode.</param>
    /// <param name="overlayOnlyLanguage">
    ///     Whether the selected language only supports overlays and custom
    ///     tooltips.
    /// </param>
    /// <returns>
    ///     <c>true</c> when the overlay should display the original text.
    /// </returns>
    public static bool ShowsOriginalOverlayText(
        JournalTranslationDisplayMode displayMode,
        bool overlayOnlyLanguage = false)
    {
        return ShowsOriginalTooltips(displayMode, overlayOnlyLanguage);
    }
}
