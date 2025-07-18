// <copyright file="DeepSeekEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.Translators.DeepSeek;

namespace Echoglossian.PluginUI.EngineConfigUI;

public static class DeepSeekEngineUI
{
    public static bool Draw(Config config, PromptTemplateManager promptManager)
    {
        var changed = false;

        ImGui.TextWrapped(Resources.SettingsForDeepSeekText);

        bool isApiKeyInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.APIKey,
            ref config.DeepSeekTranslatorApiKey,
            300,
            out isApiKeyInvalid);

        bool isEndpointInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.Endpoint,
            ref config.DeepSeekBaseUrl,
            300,
            out isEndpointInvalid);

        // Live model fetch toggle
        var prev = config.UseLiveDeepSeekModelList;
        if (ImGui.Checkbox(
                "Fetch DeepSeek model list live",
                ref config.UseLiveDeepSeekModelList))
        {
            changed = true;
            if (config.UseLiveDeepSeekModelList && !prev)
            {
                _ = Task.Run(() => DeepSeekModelManager.RefreshAsync(
                    config.DeepSeekTranslatorApiKey,
                    config.DeepSeekBaseUrl));
            }
            else if (!config.UseLiveDeepSeekModelList)
            {
                DeepSeekModelManager.ResetToDefault();
            }
        }

        var tooltips = new Dictionary<string, string>
        {
            ["deepseek-chat"] = "💬 Optimized for general chat and speed",
            ["deepseek-reasoner"] = "🧠 Reasoning and problem-solving tasks",
        };

        var models = config.UseLiveDeepSeekModelList
            ? DeepSeekModelManager.CurrentModelList
            : DeepSeekTextModelDefaults.PredefinedModels;

        changed |= ModelDropdownUI.Draw(
            Resources.LLMModel,
            ref config.DeepSeekModel,
            models,
            "DeepSeek",
            tooltips);

        PromptEditorUI.Draw(
            promptManager,
            Echoglossian.PromptType.DeepSeek,
            PromptTemplateManager.DefaultPrompt,
            Echoglossian.TransEngines.DeepSeek.ToString());

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }
}