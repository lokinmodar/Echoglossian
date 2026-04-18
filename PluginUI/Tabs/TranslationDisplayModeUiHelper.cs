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

    /// <summary>
    ///     Draws a shared display-mode combo.
    /// </summary>
    /// <param name="comboId">The unique ImGui id suffix for this combo.</param>
    /// <param name="displayMode">The display mode to edit.</param>
    /// <param name="overlayOnlyLanguage">
    ///     Whether the selected language forbids native UI mutation.
    /// </param>
    /// <param name="label">Optional label override for the combo.</param>
    /// <param name="description">Optional help text shown below the combo.</param>
    /// <param name="modeLabels">Optional display labels for the three modes.</param>
    /// <returns><c>true</c> when the selection changed.</returns>
    public static bool DrawDisplayModeCombo(
        string comboId,
        ref JournalTranslationDisplayMode displayMode,
        bool overlayOnlyLanguage = false,
        string? label = null,
        string? description = null,
        string[]? modeLabels = null)
    {
        var changed = false;
        label ??= Resources.QuestDisplayModeLabel;
        description ??= Resources.QuestDisplayModeDescription;
        modeLabels ??= DisplayModes;

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
                modeLabels,
                modeLabels.Length);
            ImGui.EndDisabled();
        }
        else if (ImGui.Combo(
                     label,
                     ref modeValue,
                     modeLabels,
                     modeLabels.Length))
        {
            displayMode = (JournalTranslationDisplayMode)modeValue;
            changed = true;
        }

        ImGui.TextWrapped(description);
        if (overlayOnlyLanguage)
        {
            ImGui.TextWrapped(Resources.OverlayOnlyLanguageModeDescription);
        }

        ImGui.PopID();
        return changed;
    }
}
