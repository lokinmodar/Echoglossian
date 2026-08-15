// <copyright file="OpenRouterEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators.Capabilities;
using Echoglossian.Translators.OpenAI;
using Echoglossian.Translators.OpenRouter;

namespace Echoglossian.PluginUI.EngineConfigUI;

public static class OpenRouterEngineUI
{
    private const string LiveModelRefreshScope = "OpenRouter";

    public static bool Draw(
        Config config,
        PromptTemplateManager promptManager,
        bool runtimeActionsAvailable = true)
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
            if (PreviewRuntimeActionUiHelper.DrawButton(
                    Resources.Reload,
                    runtimeActionsAvailable))
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

        var scope = LlmCapabilityPolicyService.CreateScope(
            Echoglossian.TransEngines.OpenRouter,
            "OpenRouter",
            string.IsNullOrWhiteSpace(config.OpenRouterBaseUrl)
                ? "https://openrouter.ai/api/v1"
                : config.OpenRouterBaseUrl,
            config.OpenRouterModel ?? "mistral");
        var sliderState = LlmCapabilityUiHelper.GetTemperatureSliderState(
            scope,
            0.1f,
            1.0f);
        var temp = config.OpenRouterTemperature;
        ImGui.BeginDisabled(!sliderState.IsEnabled);
        if (ImGui.SliderFloat(
                Resources.Temperature,
                ref temp,
                sliderState.MinValue,
                sliderState.MaxValue,
                "%.1f"))
        {
            config.OpenRouterTemperature = temp;
            changed = true;
        }

        ImGui.EndDisabled();
        if (!sliderState.IsEnabled &&
            ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(sliderState.TooltipText);
        }

        changed |= PromptEditorUI.Draw(
            promptManager,
            Echoglossian.PromptType.OpenRouter,
            PromptTemplateManager.DefaultPrompt,
            Echoglossian.TransEngines.OpenRouter.ToString());

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
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
