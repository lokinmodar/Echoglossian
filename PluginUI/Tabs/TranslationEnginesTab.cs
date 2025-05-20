using ImGuiNET;

namespace Echoglossian.Tabs;

/// <summary>
/// Renders the Translation Engine settings tab.
/// </summary>
public static class TranslationEnginesTab
{
    public static bool Draw(Config config)
    {
        bool changed = false;

        changed |= ImGui.Combo(Resources.EngineSelectLabel, ref config.ChosenTransEngine, Resources.EngineList, Resources.EngineList.Length);
        changed |= ImGui.Checkbox(Resources.TranslateAlreadyTranslatedTextsLabel, ref config.TranslateAlreadyTranslatedTexts);

        // Further engine settings (DeepL, ChatGPT, Gemini, etc.) to be added here

        return changed;
    }
}