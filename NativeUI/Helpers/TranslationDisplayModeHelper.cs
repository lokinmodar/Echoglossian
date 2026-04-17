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
    ///     Gets whether a display mode should register hover tooltips.
    /// </summary>
    /// <param name="displayMode">The configured display mode.</param>
    /// <returns><c>true</c> when hover tooltips should be used.</returns>
    public static bool UsesHoverTooltips(JournalTranslationDisplayMode displayMode)
    {
        return displayMode != JournalTranslationDisplayMode.NativeUiTranslation;
    }

    /// <summary>
    ///     Gets whether a display mode should write translated text into the
    ///     native addon.
    /// </summary>
    /// <param name="displayMode">The configured display mode.</param>
    /// <returns><c>true</c> when native text should be rewritten.</returns>
    public static bool WritesNativeTranslation(JournalTranslationDisplayMode displayMode)
    {
        return displayMode != JournalTranslationDisplayMode.TooltipTranslation;
    }

    /// <summary>
    ///     Gets whether hover tooltips should show the original text rather than
    ///     the translated text.
    /// </summary>
    /// <param name="displayMode">The configured display mode.</param>
    /// <returns><c>true</c> when hover tooltips should show the original text.</returns>
    public static bool ShowsOriginalTooltips(JournalTranslationDisplayMode displayMode)
    {
        return displayMode ==
               JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips;
    }
}
