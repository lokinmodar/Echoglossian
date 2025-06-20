namespace Echoglossian.PluginUI.EngineConfigUI;
public static class ChatGPTEngineUI
{
	public static bool Draw(Config config, PromptTemplateManager promptManager)
	{
		bool changed = false;

		ImGui.TextWrapped(Resources.SettingsForChatGptTransText);
		ImGui.Spacing();

		if (ImGui.Button(Resources.ChatGPTAPIKeyLink))
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = "https://platform.openai.com/settings/profile?tab=api-keys",
				UseShellExecute = true,
			});
		}

		bool isApiKeyInvalid;
		changed |= FieldValidationHelper.ValidatedInputText(Resources.ChatGptApiKey, ref config.ChatGptApiKey, 400, out isApiKeyInvalid);

		bool isBaseUrlInvalid;
		changed |= FieldValidationHelper.ValidatedInputText(Resources.ModelEndpoint, ref config.ChatGPTBaseUrl, 400, out isBaseUrlInvalid);

		bool isModelInvalid;
		changed |= FieldValidationHelper.ValidatedInputText(Resources.LLMModel, ref config.OpenAILlmModel, 400, out isModelInvalid);

		float temp = config.ChatGptTemperature;
		if (ImGui.SliderFloat(Resources.Temperature, ref temp, 0.1f, 1.0f, "%.1f"))
		{
			config.ChatGptTemperature = temp;
			changed = true;
		}

		PromptEditorUI.Draw(promptManager, PromptType.ChatGPT, DefaultPrompt, TransEngines.ChatGPT.ToString());

		if (changed)
		{
			FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
			SaveConfig(config);
		}

		return changed;
	}
}
