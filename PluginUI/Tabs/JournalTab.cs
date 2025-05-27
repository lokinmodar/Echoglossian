using ImGuiNET;

namespace Echoglossian.Tabs;

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

    return changed;
  }

}