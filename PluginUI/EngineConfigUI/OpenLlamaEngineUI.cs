namespace Echoglossian.PluginUI.EngineConfigUI;
public static class OpenLlamaEngineUI
{
  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForOpenLlamaText);

    bool isEndpointInvalid;
    changed |= FieldValidationHelper.ValidatedInputText("Model Endpoint", ref config.OpenLlamaUrl, 400, out isEndpointInvalid);

    PromptEditorUI.Draw(promptManager, PromptType.OpenLlama, DefaultPrompt, TransEngines.OpenLlama.ToString());

    if (ImGui.Button(Resources.Save))
    {
      FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
      SaveConfig(config);
    }

    return changed;
  }
}
