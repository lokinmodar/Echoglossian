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
    private static readonly string[] QuestDisplayModes =
    [
        Resources.QuestDisplayModeNativeUiTranslation,
        Resources.QuestDisplayModeTooltipTranslationOnly,
        Resources.QuestDisplayModeNativeUiTranslationWithOriginalTooltips,
    ];

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
                Resources.TranslateJournalAcceptToggle,
                ref config.TranslateJournalAccept,
                ref config.JournalAcceptTranslationDisplayMode);
            changed |= DrawQuestFamilySection(
                Resources.TranslateJournalResultToggle,
                ref config.TranslateJournalResult,
                ref config.JournalResultTranslationDisplayMode);
            changed |= DrawQuestFamilySection(
                Resources.TranslateToDoListToggle,
                ref config.TranslateToDoList,
                ref config.ToDoListTranslationDisplayMode);
            changed |= DrawQuestFamilySection(
                Resources.TranslateScenarioTreeToggle,
                ref config.TranslateScenarioTree,
                ref config.ScenarioTreeTranslationDisplayMode);
            changed |= DrawQuestFamilySection(
                Resources.TranslateRecommendListToggle,
                ref config.TranslateRecommendList,
                ref config.RecommendListTranslationDisplayMode);
            changed |= DrawQuestFamilySection(
                Resources.TranslateAreaMapToggle,
                ref config.TranslateAreaMap,
                ref config.AreaMapTranslationDisplayMode);
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
            ref config.JournalTranslationDisplayMode);

        changed |= ImGui.Checkbox(
            Resources.JournalGlobalHoverTooltipsLabel,
            ref config.TranslateTooltips);

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
        string sectionLabel,
        ref bool enabled,
        ref JournalTranslationDisplayMode displayMode)
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextUnformatted(sectionLabel);
        ImGui.Separator();

        changed |= ImGui.Checkbox(sectionLabel, ref enabled);
        changed |= DrawQuestDisplayModeCombo(ref displayMode);

        return changed;
    }

    /// <summary>
    ///     Draws the generic quest display mode combo and description text.
    /// </summary>
    /// <param name="displayMode">The configured display mode to edit.</param>
    /// <returns><c>true</c> when the combo selection changed.</returns>
    private static bool DrawQuestDisplayModeCombo(
        ref JournalTranslationDisplayMode displayMode)
    {
        var changed = false;
        var modeValue = (int)displayMode;
        if (ImGui.Combo(
                Resources.QuestDisplayModeLabel,
                ref modeValue,
                QuestDisplayModes,
                QuestDisplayModes.Length))
        {
            displayMode = (JournalTranslationDisplayMode)modeValue;
            changed = true;
        }

        ImGui.TextWrapped(Resources.QuestDisplayModeDescription);
        return changed;
    }
}
