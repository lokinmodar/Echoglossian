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

        if (config.Translate)
        {
            changed |= ImGui.Checkbox(
                Resources.TranslateJournalToggle,
                ref config.TranslateJournal);

            changed |= ImGui.Checkbox(
                "Show journal and quest translations as hover tooltips",
                ref config.TranslateTooltips);

            changed |= ImGui.Checkbox(
                "Swap original and translated text in hover tooltips",
                ref config.SwapTextsUsingImGui);
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
