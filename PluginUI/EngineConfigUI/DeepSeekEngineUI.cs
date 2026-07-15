// <copyright file="DeepSeekEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.Translators.DeepSeek;

namespace Echoglossian.PluginUI.EngineConfigUI;

public static class DeepSeekEngineUI
{
    private const string LiveModelRefreshScope = "DeepSeek";

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

        var prev = config.UseLiveDeepSeekModelList;
        if (ImGui.Checkbox(
                Resources.FetchLiveModels,
                ref config.UseLiveDeepSeekModelList))
        {
            changed = true;
            if (config.UseLiveDeepSeekModelList && !prev)
            {
                LiveModelRefreshCoordinator.ForceRefresh(
                    LiveModelRefreshScope,
                    BuildLiveModelRefreshSignature(config),
                    () => DeepSeekModelManager.RefreshAsync(
                        config.DeepSeekTranslatorApiKey ?? string.Empty,
                        config.DeepSeekBaseUrl ?? string.Empty));
            }
            else if (!config.UseLiveDeepSeekModelList)
            {
                DeepSeekModelManager.ResetToDefault();
                LiveModelRefreshCoordinator.Clear(LiveModelRefreshScope);
            }
        }

        if (config.UseLiveDeepSeekModelList)
        {
            ImGui.SameLine();
            if (ImGui.Button(Resources.Reload))
            {
                LiveModelRefreshCoordinator.ForceRefresh(
                    LiveModelRefreshScope,
                    BuildLiveModelRefreshSignature(config),
                    () => DeepSeekModelManager.RefreshAsync(
                        config.DeepSeekTranslatorApiKey ?? string.Empty,
                        config.DeepSeekBaseUrl ?? string.Empty));
            }
        }

        LiveModelRefreshCoordinator.RequestIfNeeded(
            LiveModelRefreshScope,
            config.UseLiveDeepSeekModelList,
            BuildLiveModelRefreshSignature(config),
            () => DeepSeekModelManager.RefreshAsync(
                config.DeepSeekTranslatorApiKey ?? string.Empty,
                config.DeepSeekBaseUrl ?? string.Empty));

        var tooltips = new Dictionary<string, string>
        {
            ["deepseek-chat"] = Resources.DeepSeekModelTooltipChat,
            ["deepseek-reasoner"] = Resources.DeepSeekModelTooltipReasoner,
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

    private static string BuildLiveModelRefreshSignature(Config config)
    {
        return LiveModelRefreshSignatureHelper.Build(
            new LiveModelRefreshSignatureComponent(
                "apiKeyHash",
                config.DeepSeekTranslatorApiKey,
                Sensitive: true),
            new LiveModelRefreshSignatureComponent(
                "baseUrl",
                config.DeepSeekBaseUrl));
    }
}
