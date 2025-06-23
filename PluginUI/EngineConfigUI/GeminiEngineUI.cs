namespace Echoglossian.PluginUI.EngineConfigUI;
public static class GeminiEngineUI
{
	public static bool Draw(Config config, PromptTemplateManager promptManager)
	{
		bool changed = false;

		ImGui.TextWrapped(Resources.SettingsForGeminiText);

		bool isGeminiApiKeyInvalid;
		changed |= FieldValidationHelper.ValidatedInputText("Gemini API Key", ref config.GeminiTranslatorApiKey, 300, out isGeminiApiKeyInvalid);

		PromptEditorUI.Draw(promptManager, PromptType.Gemini, PromptTemplateManager.DefaultPrompt, TransEngines.Gemini.ToString());

		if (changed)
		{
			FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
			SaveConfig(config);
		}

		return changed;
	}
}
