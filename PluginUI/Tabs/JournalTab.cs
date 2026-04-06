// <copyright file="JournalTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
///     Renders the settings tab for journal, quest, and tooltip translation.
/// </summary>
public static class JournalTab
{
    public static bool Draw(Config config, bool langToRemoveDiacritics)
    {
        var changed = false;
        var displayMode = (int)config.JournalTranslationDisplayMode;

        if (config.Translate)
        {
            changed |= ImGui.Checkbox(
                Resources.TranslateJournalToggle,
                ref config.TranslateJournal);

            var journalDisplayModes = new[]
            {
                "Native UI translation",
                "Tooltip translation only",
                "Native UI translation + original tooltips",
            };

            if (ImGui.Combo(
                    "Journal quest display mode",
                    ref displayMode,
                    journalDisplayModes,
                    journalDisplayModes.Length))
            {
                config.JournalTranslationDisplayMode = (JournalTranslationDisplayMode)displayMode;
                changed = true;
            }

            ImGui.TextWrapped(
                "This mode controls Journal, ToDoList, RecommendList, ScenarioTree, AreaMap, JournalAccept, and JournalResult. Hover tooltips from other plugins still use the global tooltip toggle.");

            changed |= ImGui.Checkbox(
                "Enable global hover tooltips for other translated UI",
                ref config.TranslateTooltips);
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
}
