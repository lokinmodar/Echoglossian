// <copyright file="TooltipAddonAnchoredOverlayPresentationPolicy.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Defines the Tooltip addon's anchored-overlay presentation policy.
/// </summary>
internal static class TooltipAddonAnchoredOverlayPresentationPolicy
{
    /// <summary>
    ///     Gets whether the effective display mode uses the anchored overlay.
    /// </summary>
    /// <param name="displayMode">The configured display mode.</param>
    /// <param name="overlayOnlyLanguage">Whether native mutation is unavailable.</param>
    /// <returns><c>true</c> when the anchored overlay should be used.</returns>
    internal static bool UsesAnchoredOverlay(
        JournalTranslationDisplayMode displayMode,
        bool overlayOnlyLanguage)
    {
        var effective = TranslationDisplayModeHelper.GetEffectiveDisplayMode(
            displayMode,
            overlayOnlyLanguage);
        return effective != JournalTranslationDisplayMode.NativeUiTranslation;
    }

    /// <summary>
    ///     Gets whether the anchored overlay should show original text.
    /// </summary>
    /// <param name="displayMode">The configured display mode.</param>
    /// <param name="overlayOnlyLanguage">Whether native mutation is unavailable.</param>
    /// <returns><c>true</c> when original text should be shown.</returns>
    internal static bool ShowsOriginalOverlayText(
        JournalTranslationDisplayMode displayMode,
        bool overlayOnlyLanguage)
    {
        var effective = TranslationDisplayModeHelper.GetEffectiveDisplayMode(
            displayMode,
            overlayOnlyLanguage);
        return effective == JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips;
    }

    /// <summary>
    ///     Selects the anchored overlay body for the effective display mode.
    /// </summary>
    /// <param name="displayMode">The configured display mode.</param>
    /// <param name="overlayOnlyLanguage">Whether native mutation is unavailable.</param>
    /// <param name="originalText">The original text.</param>
    /// <param name="translatedText">The translated text.</param>
    /// <returns>The text to render in the anchored overlay.</returns>
    internal static string SelectOverlayBody(
        JournalTranslationDisplayMode displayMode,
        bool overlayOnlyLanguage,
        string originalText,
        string translatedText)
    {
        return ShowsOriginalOverlayText(displayMode, overlayOnlyLanguage)
            ? originalText
            : translatedText;
    }
}
