public static class MicrosoftEngineUI
{
  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForMicrosoftText);

    bool isApiKeyInvalid;
    changed |= FieldValidationHelper.ValidatedInputText(Resources.MicrosoftTranslatorAPIKey, ref config.MicrosoftTranslatorApiKey, 200, out isApiKeyInvalid);

    bool isRegionInvalid;
    changed |= FieldValidationHelper.ValidatedInputText(Resources.Region, ref config.MicrosoftTranslatorRegion, 100, out isRegionInvalid);

    PromptEditorUI.Draw(promptManager, PromptType.Microsoft, DefaultPrompt, TransEngines.Microsoft.ToString());

    if (ImGui.Button(Resources.Save))
    {
      FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
      SaveConfig(config);
    }

    return changed;
  }
}
