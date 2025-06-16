namespace Echoglossian.PluginUI.EngineConfigUI;
public static class YandexCloudEngineUI
{
  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForYandexCloudText);

    bool isFolderIdInvalid;
    changed |= FieldValidationHelper.ValidatedInputText(Resources.YandexCloudFolderId, ref config.YandexFolderId, 200, out isFolderIdInvalid);

    bool isApiKeyInvalid;
    changed |= FieldValidationHelper.ValidatedInputText(Resources.YandexCloudApiKey, ref config.YandexPaidApiKey, 300, out isApiKeyInvalid);

    PromptEditorUI.Draw(promptManager, PromptType.YandexCloud, DefaultPrompt, TransEngines.YandexCloud.ToString());

    if (ImGui.Button(Resources.Save))
    {
      FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
      SaveConfig(config);
    }

    return changed;
  }
}
