

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
			changed |= ImGui.InputText(Resources.DeeplTranslatorApiKey, ref config.DeeplTranslatorApiKey, 100);

			if (string.IsNullOrWhiteSpace(config.DeeplTranslatorApiKey))
			{
				FieldValidationHelper.ValidatedInputText(Resources.DeeplTranslatorApiKey);
			}
		}

		return changed;
	}
}
