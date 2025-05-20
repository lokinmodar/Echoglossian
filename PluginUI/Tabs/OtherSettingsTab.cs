using ImGuiNET;

namespace Echoglossian.Tabs;

/// <summary>
/// Renders additional settings for select strings, confirmations, and UI elements.
/// </summary>
public static class OtherSettingsTab
{
    public static bool Draw(Config config)
    {
        bool changed = false;

        changed |= ImGui.Checkbox(Resources.TranslateYesNoScreenLabel, ref config.TranslateYesNoScreen);
        changed |= ImGui.Checkbox(Resources.TranslateCutSceneSelectStringLabel, ref config.TranslateCutSceneSelectString);
        changed |= ImGui.Checkbox(Resources.TranslateSelectStringLabel, ref config.TranslateSelectString);
        changed |= ImGui.Checkbox(Resources.TranslateSelectOkLabel, ref config.TranslateSelectOk);

        return changed;
    }
}