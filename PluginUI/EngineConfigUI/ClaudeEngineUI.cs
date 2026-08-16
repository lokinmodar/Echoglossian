// <copyright file="ClaudeEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators.Capabilities;
using Echoglossian.Translators.Claude;

namespace Echoglossian.PluginUI.EngineConfigUI;

/// <summary>
///     UI for configuring the Anthropic Claude translation engine.
/// </summary>
public static class ClaudeEngineUI
{
    private const string LiveModelRefreshScope = "Claude";

    /// <summary>
    ///     Draws the Claude configuration panel.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="promptManager">The shared prompt template manager.</param>
    /// <returns><see langword="true"/> when any setting changed.</returns>
    public static bool Draw(
        Config config,
        PromptTemplateManager promptManager,
        bool runtimeActionsAvailable = true)
    {
        var changed = false;

        ImGui.TextWrapped(Resources.SettingsForClaudeText);
        ImGui.Spacing();

        if (PreviewRuntimeActionUiHelper.DrawButton(
                Resources.OpenAnthropicApiKeys,
                runtimeActionsAvailable))
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = "https://console.anthropic.com/settings/keys",
                    UseShellExecute = true,
                });
        }

        bool isApiKeyInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.APIKey,
            ref config.ClaudeApiKey,
            400,
            out isApiKeyInvalid);

        bool isBaseUrlInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.ModelEndpoint,
            ref config.ClaudeBaseUrl,
            400,
            out isBaseUrlInvalid);

        var previous = config.UseLiveClaudeModelList;
        if (ImGui.Checkbox(Resources.FetchLiveModels, ref config.UseLiveClaudeModelList))
        {
            changed = true;
            if (config.UseLiveClaudeModelList && !previous)
            {
                LiveModelRefreshCoordinator.ForceRefresh(
                    LiveModelRefreshScope,
                    BuildLiveModelRefreshSignature(config),
                    cancellationToken => ClaudeModelManager.RefreshAsync(
                        config.ClaudeApiKey,
                        config.ClaudeBaseUrl,
                        cancellationToken));
            }
            else if (!config.UseLiveClaudeModelList)
            {
                ClaudeModelManager.ResetToDefault();
                LiveModelRefreshCoordinator.Clear(LiveModelRefreshScope);
            }
        }

        if (config.UseLiveClaudeModelList)
        {
            ImGui.SameLine();
            if (PreviewRuntimeActionUiHelper.DrawButton(
                    Resources.Reload,
                    runtimeActionsAvailable))
            {
                LiveModelRefreshCoordinator.ForceRefresh(
                    LiveModelRefreshScope,
                    BuildLiveModelRefreshSignature(config),
                    cancellationToken => ClaudeModelManager.RefreshAsync(
                        config.ClaudeApiKey,
                        config.ClaudeBaseUrl,
                        cancellationToken));
            }
        }

        LiveModelRefreshCoordinator.RequestIfNeeded(
            LiveModelRefreshScope,
            config.UseLiveClaudeModelList,
            BuildLiveModelRefreshSignature(config),
            cancellationToken => ClaudeModelManager.RefreshAsync(
                config.ClaudeApiKey,
                config.ClaudeBaseUrl,
                cancellationToken));

        var tooltips = new Dictionary<string, string>
        {
            ["claude-sonnet-4-20250514"] = Resources.ClaudeModelTooltipSonnet4,
            ["claude-3-7-sonnet-latest"] = Resources.ClaudeModelTooltipSonnet37,
            ["claude-3-5-haiku-latest"] = Resources.ClaudeModelTooltipHaiku35,
            ["claude-opus-4-1-20250805"] = Resources.ClaudeModelTooltipOpus41,
        };

        var models = config.UseLiveClaudeModelList
            ? ClaudeModelManager.CurrentModelList
            : ClaudeTextModelDefaults.PredefinedModels;

        changed |= ModelDropdownUI.Draw(
            Resources.LLMModel,
            ref config.ClaudeModel,
            models,
            "Claude",
            tooltips);

        var scope = LlmCapabilityPolicyService.CreateScope(
            Echoglossian.TransEngines.Claude,
            "Anthropic",
            string.IsNullOrWhiteSpace(config.ClaudeBaseUrl)
                ? "https://api.anthropic.com"
                : config.ClaudeBaseUrl,
            string.IsNullOrWhiteSpace(config.ClaudeModel)
                ? "claude-sonnet-4-20250514"
                : config.ClaudeModel);
        var sliderState = LlmCapabilityUiHelper.GetTemperatureSliderState(
            scope,
            0.1f,
            1.0f);
        var temp = config.ClaudeTemperature;
        ImGui.BeginDisabled(!sliderState.IsEnabled);
        if (ImGui.SliderFloat(
                Resources.Temperature,
                ref temp,
                sliderState.MinValue,
                sliderState.MaxValue,
                "%.1f"))
        {
            config.ClaudeTemperature = temp;
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
            Echoglossian.PromptType.Claude,
            PromptTemplateManager.DefaultPrompt,
            Echoglossian.TransEngines.Claude.ToString());

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
                config.ClaudeApiKey,
                Sensitive: true),
            new LiveModelRefreshSignatureComponent(
                "baseUrl",
                config.ClaudeBaseUrl));
    }
}
