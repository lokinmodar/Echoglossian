using ImGuiNET;

namespace Echoglossian.Tabs;

/// <summary>
/// Renders the tab used for debugging and troubleshooting plugin behavior.
/// </summary>
public static class TroubleshootingTab
{
    public static bool Draw(Config config)
    {
        bool changed = false;

        changed |= ImGui.Checkbox(Resources.RemoveDiacriticsQuestLabel, ref config.RemoveDiacriticsWhenUsingReplacementQuest);
        changed |= ImGui.Checkbox(Resources.RemoveDiacriticsTalkLabel, ref config.RemoveDiacriticsWhenUsingReplacementTalkBTalk);
        changed |= ImGui.Checkbox(Resources.TranslateAlreadyTranslatedTextsLabel, ref config.TranslateAlreadyTranslatedTexts);
        changed |= ImGui.Checkbox(Resources.ForceShowTitleLabel, ref config.ForceShowTitle);

        return changed;
    }
}