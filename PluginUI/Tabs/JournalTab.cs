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
                Resources.JournalQuestDisplayModeNativeUiTranslation,
                Resources.JournalQuestDisplayModeTooltipTranslationOnly,
                Resources.JournalQuestDisplayModeNativeUiTranslationWithOriginalTooltips,
            };

            if (ImGui.Combo(
                    Resources.JournalQuestDisplayModeLabel,
                    ref displayMode,
                    journalDisplayModes,
                    journalDisplayModes.Length))
            {
                config.JournalTranslationDisplayMode = (JournalTranslationDisplayMode)displayMode;
                changed = true;
            }

            ImGui.TextWrapped(
                Resources.JournalQuestDisplayModeDescription);

            changed |= ImGui.Checkbox(
                Resources.JournalGlobalHoverTooltipsLabel,
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
