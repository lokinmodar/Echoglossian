// <copyright file="LmStudioEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators.Capabilities;
using Echoglossian.Translators.LmStudio;
using Echoglossian.Translators.OpenAI;

namespace Echoglossian.PluginUI.EngineConfigUI;

/// <summary>
///     UI for configuring LM Studio translator engine.
/// </summary>
public static class LmStudioEngineUI
{
    private const string LiveModelRefreshScope = "LmStudio";

    /// <summary>
    ///     Draws the LM Studio engine configuration panel.
    /// </summary>
    /// <param name="config">Current plugin configuration.</param>
    /// <param name="promptManager">Prompt manager instance.</param>
    /// <returns>True if any settings changed.</returns>
    public static bool Draw(
        Config config,
        PromptTemplateManager promptManager,
        bool runtimeActionsAvailable = true)
    {
        var changed = false;

        ImGui.TextWrapped(Resources.SettingsForLmStudioText);

        bool isEndpointInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.APIEndpoint,
            ref config.LmStudioBaseUrl,
            300,
            out isEndpointInvalid);

        ImGui.Checkbox(Resources.UseAuthentication, ref config.UseLmStudioAuth);

        if (config.UseLmStudioAuth)
        {
            bool isApiKeyInvalid;
            changed |= FieldValidationHelper.ValidatedInputText(
                Resources.APIKey,
                ref config.LmStudioApiKey,
                300,
                out isApiKeyInvalid);
        }

        var previousUseLiveModelList = config.UseLiveLmStudioModelList;
        if (ImGui.Checkbox(
                Resources.FetchLiveModels,
                ref config.UseLiveLmStudioModelList))
        {
            changed = true;
            if (config.UseLiveLmStudioModelList && !previousUseLiveModelList)
            {
                LiveModelRefreshCoordinator.ForceRefresh(
                    LiveModelRefreshScope,
                    BuildLiveModelRefreshSignature(config),
                    () => LmStudioModelManager.RefreshAsync(
                        config.LmStudioBaseUrl ?? string.Empty,
                        config.UseLmStudioAuth ? config.LmStudioApiKey : null));
            }
            else if (!config.UseLiveLmStudioModelList)
            {
                LmStudioModelManager.ResetToDefault();
                LiveModelRefreshCoordinator.Clear(LiveModelRefreshScope);
            }
        }

        var models = config.UseLiveLmStudioModelList
            ? LmStudioModelManager.CurrentModelList
            : LmStudioTextModelDefaults.PredefinedModels;

        if (config.UseLiveLmStudioModelList)
        {
            ImGui.SameLine();
            if (PreviewRuntimeActionUiHelper.DrawButton(
                    Resources.Reload,
                    runtimeActionsAvailable))
            {
                LiveModelRefreshCoordinator.ForceRefresh(
                    LiveModelRefreshScope,
                    BuildLiveModelRefreshSignature(config),
                    () => LmStudioModelManager.RefreshAsync(
                        config.LmStudioBaseUrl ?? string.Empty,
                        config.UseLmStudioAuth ? config.LmStudioApiKey : null));
            }
        }

        LiveModelRefreshCoordinator.RequestIfNeeded(
            LiveModelRefreshScope,
            config.UseLiveLmStudioModelList,
            BuildLiveModelRefreshSignature(config),
            () => LmStudioModelManager.RefreshAsync(
                config.LmStudioBaseUrl ?? string.Empty,
                config.UseLmStudioAuth ? config.LmStudioApiKey : null));

        changed |= ModelDropdownUI.Draw(
            Resources.Model,
            ref config.LmStudioModel,
            models,
            "LmStudio");

        var scope = LlmCapabilityPolicyService.CreateScope(
            Echoglossian.TransEngines.LmStudio,
            "LmStudio",
            config.LmStudioBaseUrl?.TrimEnd('/') ?? "http://localhost:1234/v1",
            config.LmStudioModel ?? "llama3");
        var sliderState = LlmCapabilityUiHelper.GetTemperatureSliderState(
            scope,
            0.1f,
            1.0f);
        var temp = config.LmStudioTemperature;
        ImGui.BeginDisabled(!sliderState.IsEnabled);
        if (ImGui.SliderFloat(
                Resources.Temperature,
                ref temp,
                sliderState.MinValue,
                sliderState.MaxValue,
                "%.1f"))
        {
            config.LmStudioTemperature = temp;
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
            Echoglossian.PromptType.LmStudio,
            PromptTemplateManager.GetDefaultPrompt(Echoglossian.PromptType.LmStudio),
            Echoglossian.TransEngines.LmStudio.ToString());

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
                "baseUrl",
                config.LmStudioBaseUrl),
            new LiveModelRefreshSignatureComponent(
                "useAuth",
                config.UseLmStudioAuth.ToString()),
            new LiveModelRefreshSignatureComponent(
                "apiKeyHash",
                config.UseLmStudioAuth ? config.LmStudioApiKey : string.Empty,
                Sensitive: true));
    }
}
