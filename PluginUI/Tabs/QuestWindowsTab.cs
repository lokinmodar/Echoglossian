// <copyright file="QuestWindowsTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
///     Renders quest-related surfaces that do not belong in the main Journal tab.
/// </summary>
public static class QuestWindowsTab
{
    /// <summary>
    ///     Draws the quest-windows settings tab.
    /// </summary>
    /// <param name="config">The current plugin configuration.</param>
    /// <returns><c>true</c> when a setting changed.</returns>
    public static bool Draw(Config config)
    {
        var changed = false;

        if (!config.Translate)
        {
            return false;
        }

        changed |= DrawQuestSurfaceSection(
            config,
            Resources.TranslateJournalAcceptToggle,
            ref config.TranslateJournalAccept,
            ref config.JournalAcceptTranslationDisplayMode);
        changed |= DrawQuestSurfaceSection(
            config,
            Resources.TranslateJournalResultToggle,
            ref config.TranslateJournalResult,
            ref config.JournalResultTranslationDisplayMode);
        changed |= DrawQuestSurfaceSection(
            config,
            Resources.TranslateRecommendListToggle,
            ref config.TranslateRecommendList,
            ref config.RecommendListTranslationDisplayMode);
        changed |= DrawQuestSurfaceSection(
            config,
            Resources.TranslateAreaMapToggle,
            ref config.TranslateAreaMap,
            ref config.AreaMapTranslationDisplayMode);

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }

    /// <summary>
    ///     Draws one quest surface settings section with an enable toggle and
    ///     display-mode combo.
    /// </summary>
    /// <param name="config">The current plugin configuration.</param>
    /// <param name="sectionLabel">The section label.</param>
    /// <param name="enabled">The toggle that enables translation.</param>
    /// <param name="displayMode">The configured display mode.</param>
    /// <returns><c>true</c> when a setting changed.</returns>
    private static bool DrawQuestSurfaceSection(
        Config config,
        string sectionLabel,
        ref bool enabled,
        ref JournalTranslationDisplayMode displayMode)
    {
        var changed = false;

        ImGui.Spacing();
        ImGui.TextUnformatted(sectionLabel);
        ImGui.Separator();

        changed |= ImGui.Checkbox(sectionLabel, ref enabled);
        changed |= TranslationDisplayModeUiHelper.DrawDisplayModeCombo(
            sectionLabel,
            ref displayMode,
            config.OverlayOnlyLanguage);

        return changed;
    }
}
