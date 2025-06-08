using System.Diagnostics;
using Echoglossian.Helpers;
using Echoglossian.Properties;
using ImGuiNET;

namespace Echoglossian.PluginUI.EngineConfigUI;

/// <summary>
/// Renders the configuration UI for ChatGPT Translator.
/// </summary>
public static class ChatGptEngineUI
{
	public static bool Draw(Config config)
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

		changed |= ImGui.InputText(Resources.ChatGptApiKey, ref config.ChatGptApiKey, 400);
		if (string.IsNullOrWhiteSpace(config.ChatGptApiKey))
			FieldValidationHelper.ShowFieldRequiredWarningIfEmpty(Resources.ChatGptApiKey);

		changed |= ImGui.InputText("Model endpoint", ref config.ChatGPTBaseUrl, 400);
		if (string.IsNullOrWhiteSpace(config.ChatGPTBaseUrl))
			FieldValidationHelper.ShowFieldRequiredWarningIfEmpty("Model endpoint");

		changed |= ImGui.InputText("LLM Model", ref config.OpenAILlmModel, 400);
		if (string.IsNullOrWhiteSpace(config.OpenAILlmModel))
			FieldValidationHelper.ShowFieldRequiredWarningIfEmpty("LLM Model");

		float temperature = config.ChatGptTemperature;
		if (ImGui.SliderFloat("Temperature", ref temperature, 0.1f, 1.0f, "%.1f"))
		{
			config.ChatGptTemperature = temperature;
			changed = true;
		}

		PromptTemplateManager.DrawPromptEditor(config, PromptType.ChatGPT, PromptTemplateManager.DefaultPrompt, "ChatGPT");

		return changed;
	}
}
