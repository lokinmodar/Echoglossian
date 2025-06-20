using ImGuiNET;

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
/// Renders additional settings for select strings, confirmations, and UI elements.
/// </summary>
public static class OtherUIElementsSettingsTab
{
  public static bool Draw(Config config)
  {
    bool changed = false;

    changed |= ImGui.Checkbox(Resources.TranslateYesNoScreenLabel, ref config.TranslateYesNoScreen);
    changed |= ImGui.Checkbox(Resources.TranslateCutSceneSelectStringLabel, ref config.TranslateCutSceneSelectString);
    changed |= ImGui.Checkbox(Resources.TranslateSelectStringLabel, ref config.TranslateSelectString);
    changed |= ImGui.Checkbox(Resources.TranslateSelectOkLabel, ref config.TranslateSelectOk);

    if (changed)
    {
      FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
      SaveConfig(config);
    }

    return changed;
  }
}