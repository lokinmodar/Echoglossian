// <copyright file="JournalTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
///     Renders the settings tab for journal and quest-family translation.
/// </summary>
public static class JournalTab
{
    /// <summary>
    ///     Draws the Journal settings tab.
    /// </summary>
    /// <param name="config">The current plugin configuration.</param>
    /// <param name="langToRemoveDiacritics">Whether the selected language supports diacritic removal.</param>
    /// <returns><c>true</c> when a setting changed.</returns>
    public static bool Draw(Config config, bool langToRemoveDiacritics)
    {
        var changed = false;

        if (config.Translate)
        {
            changed |= DrawJournalSection(config);
            changed |= DrawQuestFamilySection(
                config,
                Resources.TranslateJournalDetailToggle,
                ref config.TranslateJournalDetail,
                ref config.JournalDetailTranslationDisplayMode);
            changed |= DrawQuestFamilySection(
                config,
                Resources.TranslateToDoListToggle,
                ref config.TranslateToDoList,
                ref config.ToDoListTranslationDisplayMode);
            changed |= DrawQuestFamilySection(
                config,
                Resources.TranslateScenarioTreeToggle,
                ref config.TranslateScenarioTree,
                ref config.ScenarioTreeTranslationDisplayMode);
            changed |= DrawQuestNotificationSection(config);
        }

        if (langToRemoveDiacritics)
        {
            changed |= ImGui.Checkbox(
                Resources.RemoveDiacriticsToggle,
                ref config.RemoveDiacriticsWhenUsingReplacementQuest);
        }

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }

    /// <summary>
    ///     Draws the quest-background notification toggle.
    /// </summary>
    /// <param name="config">The current plugin configuration.</param>
    /// <returns><c>true</c> when a setting changed.</returns>
    private static bool DrawQuestNotificationSection(Config config)
    {
        ImGui.Spacing();
        ImGui.TextUnformatted(Resources.ShowQuestProgressNotificationsToggle);
        ImGui.Separator();

        var changed = ImGui.Checkbox(
            Resources.ShowQuestProgressNotificationsToggle,
            ref config.ShowQuestProgressNotifications);
        ImGui.TextWrapped(Resources.ShowQuestProgressNotificationsDescription);
        return changed;
    }

    /// <summary>
    ///     Draws the Journal-specific quest translation controls.
    /// </summary>
    /// <param name="config">The current plugin configuration.</param>
    /// <returns><c>true</c> when a setting changed.</returns>
    private static bool DrawJournalSection(Config config)
    {
        var changed = false;
        ImGui.TextUnformatted(Resources.TranslateJournalToggle);
        ImGui.Separator();

        changed |= ImGui.Checkbox(
            Resources.TranslateJournalToggle,
            ref config.TranslateJournal);

        changed |= DrawQuestDisplayModeCombo(
            nameof(config.JournalTranslationDisplayMode),
            ref config.JournalTranslationDisplayMode,
            config.OverlayOnlyLanguage,
            Resources.JournalQuestDisplayModeLabel,
            Resources.JournalQuestDisplayModeDescription);

        return changed;
    }

    /// <summary>
    ///     Draws a quest family settings section with an enable toggle and
    ///     display mode combo.
    /// </summary>
    /// <param name="sectionLabel">The label used for the section.</param>
    /// <param name="enabled">The toggle that enables translation for the family.</param>
    /// <param name="displayMode">The configured display mode for the family.</param>
    /// <returns><c>true</c> when a setting changed.</returns>
    private static bool DrawQuestFamilySection(
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
        changed |= DrawQuestDisplayModeCombo(
            sectionLabel,
            ref displayMode,
            config.OverlayOnlyLanguage);

        return changed;
    }

    /// <summary>
    ///     Draws the generic quest display mode combo and description text.
    /// </summary>
    /// <param name="comboId">A unique ImGui id suffix for this combo.</param>
    /// <param name="displayMode">The configured display mode to edit.</param>
    /// <returns><c>true</c> when the combo selection changed.</returns>
    private static bool DrawQuestDisplayModeCombo(
        string comboId,
        ref JournalTranslationDisplayMode displayMode,
        bool overlayOnlyLanguage,
        string? label = null,
        string? description = null)
    {
        return TranslationDisplayModeUiHelper.DrawDisplayModeCombo(
            comboId,
            ref displayMode,
            overlayOnlyLanguage,
            label,
            description);
    }
}
