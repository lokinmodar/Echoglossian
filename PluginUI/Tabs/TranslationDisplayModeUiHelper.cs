// <copyright file="TranslationDisplayModeUiHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
///     Renders the shared translation display-mode dropdown used by DB-first UI
///     surfaces.
/// </summary>
internal static class TranslationDisplayModeUiHelper
{
    private static readonly string[] DisplayModes =
    [
        Resources.QuestDisplayModeNativeUiTranslation,
        Resources.QuestDisplayModeTooltipTranslationOnly,
        Resources.QuestDisplayModeNativeUiTranslationWithOriginalTooltips,
    ];

    private const string OverlayOnlyLanguageModeDescription =
        "The selected language does not support the native game font, so this surface is limited to Echoglossian overlays and custom tooltips.";

    /// <summary>
    ///     Draws a shared display-mode combo.
    /// </summary>
    /// <param name="comboId">The unique ImGui id suffix for this combo.</param>
    /// <param name="displayMode">The display mode to edit.</param>
    /// <returns><c>true</c> when the selection changed.</returns>
    public static bool DrawDisplayModeCombo(
        string comboId,
        ref JournalTranslationDisplayMode displayMode,
        bool overlayOnlyLanguage = false,
        string? label = null,
        string? description = null)
    {
        var changed = false;
        label ??= Resources.QuestDisplayModeLabel;
        description ??= Resources.QuestDisplayModeDescription;

        if (overlayOnlyLanguage &&
            displayMode != JournalTranslationDisplayMode.TooltipTranslation)
        {
            displayMode = JournalTranslationDisplayMode.TooltipTranslation;
            changed = true;
        }

        var modeValue = (int)displayMode;
        ImGui.PushID(comboId);
        if (overlayOnlyLanguage)
        {
            ImGui.BeginDisabled();
            ImGui.Combo(
                label,
                ref modeValue,
                DisplayModes,
                DisplayModes.Length);
            ImGui.EndDisabled();
        }
        else if (ImGui.Combo(
                     label,
                     ref modeValue,
                     DisplayModes,
                     DisplayModes.Length))
        {
            displayMode = (JournalTranslationDisplayMode)modeValue;
            changed = true;
        }

        ImGui.TextWrapped(description);
        if (overlayOnlyLanguage)
        {
            ImGui.TextWrapped(OverlayOnlyLanguageModeDescription);
        }

        ImGui.PopID();
        return changed;
    }
}
