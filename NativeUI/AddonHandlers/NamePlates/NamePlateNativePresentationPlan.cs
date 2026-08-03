// <copyright file="NamePlateNativePresentationPlan.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

namespace Echoglossian.NativeUI.AddonHandlers.NamePlates;

/// <summary>
/// Describes the native NamePlate field writes required for one translated
/// presentation result.
/// </summary>
/// <param name="WritesTranslatedName">
/// Whether the runtime should write the translated text into the native name
/// field.
/// </param>
/// <param name="NameText">
/// The translated name to write when <paramref name="WritesTranslatedName" />
/// is <see langword="true" />.
/// </param>
/// <param name="ShowsTitle">
/// Whether the runtime should use the native title line as the auxiliary
/// presentation surface.
/// </param>
/// <param name="TitleText">
/// The title text to write when <paramref name="ShowsTitle" /> is
/// <see langword="true" />.
/// </param>
internal readonly record struct NamePlateNativePresentationPlan(
    bool WritesTranslatedName,
    string? NameText,
    bool ShowsTitle,
    string? TitleText)
{
    /// <summary>
    /// Creates the native NamePlate field plan for the supplied display mode.
    /// </summary>
    /// <param name="originalText">The original source name.</param>
    /// <param name="translatedText">The cached translated name.</param>
    /// <param name="displayMode">The configured display mode.</param>
    /// <param name="overlayOnlyLanguage">
    /// Whether the selected language forbids native replacement.
    /// </param>
    /// <returns>The field mutation plan for the current mode.</returns>
    internal static NamePlateNativePresentationPlan Create(
        string originalText,
        string translatedText,
        JournalTranslationDisplayMode displayMode,
        bool overlayOnlyLanguage)
    {
        var writesNative = TranslationDisplayModeHelper.WritesNativeTranslation(
            displayMode,
            overlayOnlyLanguage);
        var showsOriginalOverlay = TranslationDisplayModeHelper.ShowsOriginalOverlayText(
            displayMode,
            overlayOnlyLanguage);
        var titleText = showsOriginalOverlay ? originalText : translatedText;
        var showsTitle = !writesNative || showsOriginalOverlay;

        return new NamePlateNativePresentationPlan(
            WritesTranslatedName: writesNative,
            NameText: writesNative ? translatedText : null,
            ShowsTitle: showsTitle && !string.IsNullOrWhiteSpace(titleText),
            TitleText: showsTitle ? titleText : null);
    }
}
