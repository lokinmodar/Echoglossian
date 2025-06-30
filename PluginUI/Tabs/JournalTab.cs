// <copyright file="JournalTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
/// Renders the settings tab for journal, quest, and tooltip translation.
/// </summary>
public static class JournalTab
{
  public static bool Draw(Config config, bool langToRemoveDiacritics)
  {
    bool changed = false;

    if (config.Translate)
    {
      changed |= ImGui.Checkbox(Resources.TranslateJournalToggle, ref config.TranslateJournal);
    }

    if (langToRemoveDiacritics)
    {
      changed |= ImGui.Checkbox(Resources.RemoveDiacriticsToggle, ref config.RemoveDiacriticsWhenUsingReplacementQuest);
    }

    if (changed)
    {
      FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
      SaveConfig(config);
    }

    return changed;
  }
}