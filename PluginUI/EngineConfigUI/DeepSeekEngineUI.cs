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

        var apiKey = config.DeepSeekTranslatorApiKey ?? string.Empty;
        bool isApiKeyInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.APIKey,
            ref apiKey,
            300,
            out isApiKeyInvalid);
        config.DeepSeekTranslatorApiKey = apiKey;

        var endpoint = config.DeepSeekBaseUrl;
        bool isEndpointInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.Endpoint,
            ref endpoint,
            300,
            out isEndpointInvalid);
        config.DeepSeekBaseUrl = endpoint;

        // Live model fetch toggle
        var prev = config.UseLiveDeepSeekModelList;
        if (ImGui.Checkbox(
                Resources.FetchLiveModels,
                ref config.UseLiveDeepSeekModelList))
        {
            changed = true;
            if (config.UseLiveDeepSeekModelList && !prev)
            {
                _ = Task.Run(() => DeepSeekModelManager.RefreshAsync(
                    config.DeepSeekTranslatorApiKey ?? string.Empty,
                    config.DeepSeekBaseUrl ?? string.Empty));
            }
            else if (!config.UseLiveDeepSeekModelList)
            {
                DeepSeekModelManager.ResetToDefault();
            }
        }

        var tooltips = new Dictionary<string, string>
        {
            ["deepseek-chat"] = Resources.ResourceManager.GetString("DeepSeekModelTooltipChat", Resources.Culture) ??
                                "💬 Optimized for general chat and speed",
            ["deepseek-reasoner"] = Resources.ResourceManager.GetString("DeepSeekModelTooltipReasoner", Resources.Culture) ??
                                    "🧠 Reasoning and problem-solving tasks",
        };

        var models = config.UseLiveDeepSeekModelList
            ? DeepSeekModelManager.CurrentModelList
            : DeepSeekTextModelDefaults.PredefinedModels;

        var model = config.DeepSeekModel ?? string.Empty;
        changed |= ModelDropdownUI.Draw(
            Resources.LLMModel,
            ref model,
            models,
            "DeepSeek",
            tooltips);
        config.DeepSeekModel = model;

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
