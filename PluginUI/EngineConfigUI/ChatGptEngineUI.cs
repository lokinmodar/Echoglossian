// <copyright file="ChatGptEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.Translators.OpenAI;

namespace Echoglossian.PluginUI.EngineConfigUI;

public static class ChatGPTEngineUI
{
    public static bool Draw(Config config, PromptTemplateManager promptManager)
    {
        var changed = false;

        ImGui.TextWrapped(Resources.SettingsForChatGptTransText);
        ImGui.Spacing();

        if (ImGui.Button(Resources.ChatGPTAPIKeyLink))
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        "https://platform.openai.com/settings/profile?tab=api-keys",
                    UseShellExecute = true,
                });
        }

        bool isApiKeyInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.ChatGptApiKey,
            ref config.ChatGptApiKey,
            400,
            out isApiKeyInvalid);

        bool isBaseUrlInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.ModelEndpoint,
            ref config.ChatGPTBaseUrl,
            400,
            out isBaseUrlInvalid);

        // Live model fetch toggle
        var prev = config.UseLiveOpenAIModelList;
        if (ImGui.Checkbox(
                Resources.FetchLiveModels,
                ref config.UseLiveOpenAIModelList))
        {
            changed = true;
            if (config.UseLiveOpenAIModelList && !prev)
            {
                _ = Task.Run(() =>
                    OpenAIModelManager.RefreshAsync(config.ChatGptApiKey ?? string.Empty));
            }
            else if (!config.UseLiveOpenAIModelList)
            {
                OpenAIModelManager.ResetToDefault();
            }
        }

        var tooltips = new Dictionary<string, string>
        {
            ["gpt-3.5-turbo"] = Resources.ResourceManager.GetString("ChatGptModelTooltipGpt35Turbo", Resources.Culture) ??
                                "⚡ Fast and affordable (4k tokens)",
            ["gpt-3.5-turbo-16k"] = Resources.ResourceManager.GetString("ChatGptModelTooltipGpt35Turbo16k", Resources.Culture) ??
                                    "⚡ 16k token context",
            ["gpt-4"] = Resources.ResourceManager.GetString("ChatGptModelTooltipGpt4", Resources.Culture) ??
                        "🧠 More capable but slower and costly",
            ["gpt-4-turbo"] = Resources.ResourceManager.GetString("ChatGptModelTooltipGpt4Turbo", Resources.Culture) ??
                              "🟢 Faster and cheaper GPT-4 variant",
            ["gpt-4o"] = Resources.ResourceManager.GetString("ChatGptModelTooltipGpt4o", Resources.Culture) ??
                         "👁 Multimodal and real-time model",
            ["gpt-4o-mini"] = Resources.ResourceManager.GetString("ChatGptModelTooltipGpt4oMini", Resources.Culture) ??
                              "⚡ GPT-4o Mini — fast and compact",
        };

        var models = config.UseLiveOpenAIModelList
            ? OpenAIModelManager.CurrentModelList
            : OpenAITextModelDefaults.PredefinedModels;

        changed |= ModelDropdownUI.Draw(
            Resources.LLMModel,
            ref config.OpenAILlmModel,
            models,
            "OpenAI",
            tooltips);

        var temp = config.ChatGptTemperature;
        if (ImGui.SliderFloat(
                Resources.Temperature,
                ref temp,
                0.1f,
                1.0f,
                "%.1f"))
        {
            config.ChatGptTemperature = temp;
            changed = true;
        }

        PromptEditorUI.Draw(
            promptManager,
            Echoglossian.PromptType.ChatGPT,
            PromptTemplateManager.DefaultPrompt,
            Echoglossian.TransEngines.ChatGPT.ToString());

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }
}
