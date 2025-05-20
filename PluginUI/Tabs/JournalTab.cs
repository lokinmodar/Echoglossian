using ImGuiNET;

namespace Echoglossian.Tabs;

/// <summary>
/// Renders the settings tab for journal, quest, and tooltip translation.
/// </summary>
public static class JournalTab
{
    public static bool Draw(Config config)
    {
        bool changed = false;

        changed |= ImGui.Checkbox(Resources.TranslateJournalLabel, ref config.TranslateJournal);
        changed |= ImGui.Checkbox(Resources.TranslateTooltipsLabel, ref config.TranslateTooltips);
        changed |= ImGui.Checkbox(Resources.TranslateToDoListLabel, ref config.TranslateToDoList);
        changed |= ImGui.Checkbox(Resources.TranslateScenarioTreeLabel, ref config.TranslateScenarioTree);

        return changed;
    }
}