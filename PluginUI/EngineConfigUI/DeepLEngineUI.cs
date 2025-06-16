namespace Echoglossian.PluginUI.EngineConfigUI;

/// <summary>
/// Renders the configuration UI for DeepL Translator.
/// </summary>
public static class DeepLEngineUI
{
  public static bool Draw(Config config)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForDeepLTransText);
    ImGui.Spacing();

    changed |= ImGui.Checkbox(Resources.DeepLTransAPIKey, ref config.DeeplTranslatorUsingApiKey);
    if (config.DeeplTranslatorUsingApiKey)
    {
      if (ImGui.Button(Resources.DeepLTranslatorAPIKeyLink))
      {
        Process.Start(new ProcessStartInfo
        {
          FileName = "https://www.deepl.com/pro-api",
          UseShellExecute = true,
        });
      }

      ImGui.Spacing();

      bool isApiKeyInvalid;
      changed |= FieldValidationHelper.ValidatedInputText(Resources.DeeplTranslatorApiKey, ref config.DeeplTranslatorApiKey, 100, out isApiKeyInvalid);
    }

    return changed;
  }
}
