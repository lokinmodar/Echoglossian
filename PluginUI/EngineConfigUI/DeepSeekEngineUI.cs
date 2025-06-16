namespace Echoglossian.PluginUI.EngineConfigUI;
public static class DeepSeekEngineUI
{
  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForDeepSeekText);

    bool isApiKeyInvalid;
    changed |= FieldValidationHelper.ValidatedInputText(Resources.APIKey, ref config.DeepSeekTranslatorApiKey, 300, out isApiKeyInvalid);

    bool isEndpointInvalid;
    changed |= FieldValidationHelper.ValidatedInputText(Resources.Endpoint, ref config.DeepSeekBaseUrl, 300, out isEndpointInvalid);

    PromptEditorUI.Draw(promptManager, PromptType.DeepSeek, DefaultPrompt, TransEngines.DeepSeek.ToString());

    if (ImGui.Button(Resources.Save))
    {
      FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
      SaveConfig(config);
    }

    return changed;
  }
}
