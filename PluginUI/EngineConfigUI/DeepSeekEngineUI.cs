// <copyright file="DeepSeekEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators.Capabilities;
using Echoglossian.Translators.DeepSeek;

namespace Echoglossian.PluginUI.EngineConfigUI;

public static class DeepSeekEngineUI
{
    private const string LiveModelRefreshScope = "DeepSeek";

    public static bool Draw(
        Config config,
        PromptTemplateManager promptManager,
        bool runtimeActionsAvailable = true)
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
                    cancellationToken => DeepSeekModelManager.RefreshAsync(
                        config.DeepSeekTranslatorApiKey ?? string.Empty,
                        config.DeepSeekBaseUrl ?? string.Empty,
                        cancellationToken));
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
            if (PreviewRuntimeActionUiHelper.DrawButton(
                    Resources.Reload,
                    runtimeActionsAvailable))
            {
                LiveModelRefreshCoordinator.ForceRefresh(
                    LiveModelRefreshScope,
                    BuildLiveModelRefreshSignature(config),
                    cancellationToken => DeepSeekModelManager.RefreshAsync(
                        config.DeepSeekTranslatorApiKey ?? string.Empty,
                        config.DeepSeekBaseUrl ?? string.Empty,
                        cancellationToken));
            }
        }

        LiveModelRefreshCoordinator.RequestIfNeeded(
            LiveModelRefreshScope,
            config.UseLiveDeepSeekModelList,
            BuildLiveModelRefreshSignature(config),
            cancellationToken => DeepSeekModelManager.RefreshAsync(
                config.DeepSeekTranslatorApiKey ?? string.Empty,
                config.DeepSeekBaseUrl ?? string.Empty,
                cancellationToken));

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

        var scope = LlmCapabilityPolicyService.CreateScope(
            Echoglossian.TransEngines.DeepSeek,
            "DeepSeek",
            config.DeepSeekBaseUrl ?? "https://api.deepseek.com/v1",
            config.DeepSeekModel ?? "deepseek-chat");
        var sliderState = LlmCapabilityUiHelper.GetTemperatureSliderState(
            scope,
            0.1f,
            1.0f);
        var temp = config.DeepSeekTemperature;
        ImGui.BeginDisabled(!sliderState.IsEnabled);
        if (ImGui.SliderFloat(
                Resources.Temperature,
                ref temp,
                sliderState.MinValue,
                sliderState.MaxValue,
                "%.1f"))
        {
            config.DeepSeekTemperature = temp;
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
            Echoglossian.PromptType.DeepSeek,
            PromptTemplateManager.DefaultPrompt,
            Echoglossian.TransEngines.DeepSeek.ToString());

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
                config.DeepSeekTranslatorApiKey,
                Sensitive: true),
            new LiveModelRefreshSignatureComponent(
                "baseUrl",
                config.DeepSeekBaseUrl));
    }
}
