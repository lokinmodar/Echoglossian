// <copyright file="GeminiEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators.Capabilities;
using Echoglossian.Translators.Gemini;

namespace Echoglossian.PluginUI.EngineConfigUI;

public static class GeminiEngineUI
{
    private const string LiveModelRefreshScope = "Gemini";

    public static bool Draw(
        Config config,
        PromptTemplateManager promptManager,
        bool runtimeActionsAvailable = true)
    {
        var changed = false;

        ImGui.TextWrapped(Resources.SettingsForGeminiText);

        var geminiApiKey = config.GeminiTranslatorApiKey ?? string.Empty;
        bool isGeminiApiKeyInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.GeminiAPIKey,
            ref geminiApiKey,
            300,
            out isGeminiApiKeyInvalid);
        config.GeminiTranslatorApiKey = geminiApiKey;

        if (ImGui.Checkbox(
                Resources.FetchLiveModels,
                ref config.UseLiveGeminiModelList))
        {
            changed = true;
            if (config.UseLiveGeminiModelList)
            {
                LiveModelRefreshCoordinator.ForceRefresh(
                    LiveModelRefreshScope,
                    BuildLiveModelRefreshSignature(config),
                    cancellationToken => GeminiModelManager.RefreshAsync(
                        config.GeminiTranslatorApiKey ?? string.Empty,
                        cancellationToken));
            }
            else
            {
                GeminiModelManager.ResetToDefault();
                LiveModelRefreshCoordinator.Clear(LiveModelRefreshScope);
            }
        }

        if (config.UseLiveGeminiModelList)
        {
            ImGui.SameLine();
            if (PreviewRuntimeActionUiHelper.DrawButton(
                    Resources.Reload,
                    runtimeActionsAvailable))
            {
                LiveModelRefreshCoordinator.ForceRefresh(
                    LiveModelRefreshScope,
                    BuildLiveModelRefreshSignature(config),
                    cancellationToken => GeminiModelManager.RefreshAsync(
                        config.GeminiTranslatorApiKey ?? string.Empty,
                        cancellationToken));
            }
        }

        LiveModelRefreshCoordinator.RequestIfNeeded(
            LiveModelRefreshScope,
            config.UseLiveGeminiModelList,
            BuildLiveModelRefreshSignature(config),
            cancellationToken => GeminiModelManager.RefreshAsync(
                config.GeminiTranslatorApiKey ?? string.Empty,
                cancellationToken));

        var tooltips = new Dictionary<string, string>
        {
            ["gemini-2.5-flash"] = Resources.GeminiModelTooltip15Flash,
            ["gemini-2.5-flash-lite"] = Resources.GeminiModelTooltipPro,
            ["gemini-2.5-pro"] = Resources.GeminiModelTooltip15Pro,
        };

        var models = config.UseLiveGeminiModelList
            ? GeminiModelManager.CurrentModelList
            : GeminiTextModelDefaults.PredefinedModels;

        changed |= ModelDropdownUI.Draw(
            Resources.LLMModel,
            ref config.GeminiModelId,
            models,
            "Gemini",
            tooltips);
        config.GeminiModel = config.GeminiModelId;

        var scope = LlmCapabilityPolicyService.CreateScope(
            Echoglossian.TransEngines.Gemini,
            "Gemini",
            "https://generativelanguage.googleapis.com",
            config.GeminiModel ?? "gemini-2.5-flash");
        var sliderState = LlmCapabilityUiHelper.GetTemperatureSliderState(
            scope,
            0.1f,
            1.0f);
        var temp = config.GeminiTemperature;
        ImGui.BeginDisabled(!sliderState.IsEnabled);
        if (ImGui.SliderFloat(
                Resources.Temperature,
                ref temp,
                sliderState.MinValue,
                sliderState.MaxValue,
                "%.1f"))
        {
            config.GeminiTemperature = temp;
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
            Echoglossian.PromptType.Gemini,
            PromptTemplateManager.DefaultPrompt,
            Echoglossian.TransEngines.Gemini.ToString());

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
                config.GeminiTranslatorApiKey,
                Sensitive: true));
    }
}
