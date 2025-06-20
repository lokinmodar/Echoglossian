namespace Echoglossian.PluginUI.EngineConfigUI;
public static class OpenRouterEngineUI
{
	public static bool Draw(Config config, PromptTemplateManager promptManager)
	{
		bool changed = false;

		ImGui.TextWrapped(Resources.SettingsForOpenRouterText);
		ImGui.Spacing();

		bool isApiKeyInvalid;
		changed |= FieldValidationHelper.ValidatedInputText("API Key", ref config.OpenRouterApiKey, 300, out isApiKeyInvalid);

		bool isBaseUrlInvalid;
		changed |= FieldValidationHelper.ValidatedInputText("Model Endpoint", ref config.OpenRouterBaseUrl, 400, out isBaseUrlInvalid);

		bool isModelInvalid;
		changed |= FieldValidationHelper.ValidatedInputText("LLM Model", ref config.OpenRouterModel, 200, out isModelInvalid);

		float temp = config.OpenRouterTemperature;
		if (ImGui.SliderFloat("Temperature", ref temp, 0.1f, 1.0f, "%.1f"))
		{
			config.OpenRouterTemperature = temp;
			changed = true;
		}

		ImGui.Separator();

		PromptEditorUI.Draw(promptManager, PromptType.OpenRouter, DefaultPrompt, TransEngines.OpenRouter.ToString());

		if (changed)
		{
			FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
			SaveConfig(config);
		}

		return changed;
	}
}
