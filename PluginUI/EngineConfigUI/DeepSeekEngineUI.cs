using System.Numerics;
using Echoglossian.Helpers;
using Echoglossian.Properties;
using Echoglossian.Translators;
using ImGuiNET;

namespace Echoglossian.PluginUI.Helpers.Engines;

public static class DeepSeekEngineUI
{
  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForDeepSeekText);

    changed |= ImGui.InputText("API Key", ref config.DeeplTranslatorApiKey, 300);
    if (string.IsNullOrWhiteSpace(config.DeepSeekTranslatorApiKey))
    {
      FieldValidationHelper.ShowFieldRequiredWarningIfEmpty("API Key", config.DeeplTranslatorApiKey);
    }

    changed |= ImGui.InputText("Endpoint", ref config.DeepSeekBaseUrl, 300);
    if (string.IsNullOrWhiteSpace(config.DeepSeekBaseUrl))
    {
      FieldValidationHelper.ShowFieldRequiredWarningIfEmpty("Endpoint", config.DeepSeekBaseUrl);
    }

    PromptEditorUI.Draw(promptManager, PromptType.DeepSeek, DefaultPrompt, TransEngines.DeepSeek.ToString());

    return changed;
  }
}
