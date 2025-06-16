using ImGuiNET;

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
/// Provides methods to render the "Whats, Whens and Hows" general configuration tab.
/// </summary>
public static class GeneralTab
{
  /// <summary>
  /// Renders the general configuration tab and handles user interactions.
  /// </summary>
  /// <param name="config">The configuration object to be modified based on user input.</param>
  /// <returns>True if any configuration value was changed; otherwise, false.</returns>
  public static bool Draw(Config config)
  {
    bool changed = false;

    changed |= ImGui.Checkbox(Resources.ShowInCutscenesLabel, ref config.ShowInCutscenes);
    changed |= ImGui.Checkbox(Resources.CopyTranslationToClipboardLabel, ref config.CopyTranslationToClipboard);
    changed |= ImGui.SliderInt(Resources.FontSizeLabel, ref config.FontSize, 12, 48);

    if (ImGui.Button(Resources.Save))
    {
      FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
      SaveConfig(config);
    }

    return changed;
  }
}
