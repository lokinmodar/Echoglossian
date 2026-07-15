// <copyright file="OpenRouterEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.Translators.OpenAI;
using Echoglossian.Translators.OpenRouter;

namespace Echoglossian.PluginUI.EngineConfigUI;

public static class OpenRouterEngineUI
{
    private const string LiveModelRefreshScope = "OpenRouter";

    public static bool Draw(Config config, PromptTemplateManager promptManager)
    {
        var changed = false;

        ImGui.TextWrapped(Resources.SettingsForOpenRouterText);
        ImGui.Spacing();

        var apiKey = config.OpenRouterApiKey ?? string.Empty;
        bool isApiKeyInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.APIKey,
            ref apiKey,
            300,
            out isApiKeyInvalid);
        config.OpenRouterApiKey = apiKey;

        var baseUrl = config.OpenRouterBaseUrl ?? string.Empty;
        bool isBaseUrlInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.ModelEndpoint,
            ref baseUrl,
            400,
            out isBaseUrlInvalid);
        config.OpenRouterBaseUrl = baseUrl;

        var newToggle = config.UseLiveOpenRouterModelList;
        if (ImGui.Checkbox(Resources.FetchLiveModels, ref newToggle))
        {
            config.UseLiveOpenRouterModelList = newToggle;
            changed = true;

            if (newToggle)
            {
                LiveModelRefreshCoordinator.ForceRefresh(
                    LiveModelRefreshScope,
                    BuildLiveModelRefreshSignature(config),
                    () => OpenRouterModelManager.RefreshAsync(
                        config.OpenRouterApiKey ?? string.Empty,
                        config.OpenRouterBaseUrl ?? string.Empty));
            }
            else
            {
                OpenRouterModelManager.ResetToDefault();
                LiveModelRefreshCoordinator.Clear(LiveModelRefreshScope);
            }
        }

        if (config.UseLiveOpenRouterModelList)
        {
            ImGui.SameLine();
            if (ImGui.Button(Resources.Reload))
            {
                LiveModelRefreshCoordinator.ForceRefresh(
                    LiveModelRefreshScope,
                    BuildLiveModelRefreshSignature(config),
                    () => OpenRouterModelManager.RefreshAsync(
                        config.OpenRouterApiKey ?? string.Empty,
                        config.OpenRouterBaseUrl ?? string.Empty));
            }
        }

        LiveModelRefreshCoordinator.RequestIfNeeded(
            LiveModelRefreshScope,
            config.UseLiveOpenRouterModelList,
            BuildLiveModelRefreshSignature(config),
            () => OpenRouterModelManager.RefreshAsync(
                config.OpenRouterApiKey ?? string.Empty,
                config.OpenRouterBaseUrl ?? string.Empty));

        var models = config.UseLiveOpenRouterModelList
            ? OpenRouterModelManager.CurrentModelList
            : OpenRouterTextModelDefaults.PredefinedModels;

        var model = config.OpenRouterModel ?? string.Empty;
        changed |= ModelDropdownUI.Draw(
            Resources.LLMModel,
            ref model,
            models,
            "OpenRouter");
        config.OpenRouterModel = model;

        var temp = config.OpenRouterTemperature;
        if (ImGui.SliderFloat(Resources.Temperature, ref temp, 0.1f, 1.0f, "%.1f"))
        {
            config.OpenRouterTemperature = temp;
            changed = true;
        }

        PromptEditorUI.Draw(
            promptManager,
            Echoglossian.PromptType.OpenRouter,
            PromptTemplateManager.DefaultPrompt,
            Echoglossian.TransEngines.OpenRouter.ToString());

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
                config.OpenRouterApiKey,
                Sensitive: true),
            new LiveModelRefreshSignatureComponent(
                "baseUrl",
                config.OpenRouterBaseUrl));
    }
}
