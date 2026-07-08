// <copyright file="ClaudeEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
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
    public static bool Draw(Config config, PromptTemplateManager promptManager)
    {
        var changed = false;

        ImGui.TextWrapped(Resources.SettingsForClaudeText);
        ImGui.Spacing();

        if (ImGui.Button(Resources.OpenAnthropicApiKeys))
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
                    () => ClaudeModelManager.RefreshAsync(
                        config.ClaudeApiKey,
                        config.ClaudeBaseUrl));
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
            if (ImGui.Button(Resources.Reload))
            {
                LiveModelRefreshCoordinator.ForceRefresh(
                    LiveModelRefreshScope,
                    BuildLiveModelRefreshSignature(config),
                    () => ClaudeModelManager.RefreshAsync(
                        config.ClaudeApiKey,
                        config.ClaudeBaseUrl));
            }
        }

        LiveModelRefreshCoordinator.RequestIfNeeded(
            LiveModelRefreshScope,
            config.UseLiveClaudeModelList,
            BuildLiveModelRefreshSignature(config),
            () => ClaudeModelManager.RefreshAsync(
                config.ClaudeApiKey,
                config.ClaudeBaseUrl));

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

        var temp = config.ClaudeTemperature;
        if (ImGui.SliderFloat(Resources.Temperature, ref temp, 0.1f, 1.0f, "%.1f"))
        {
            config.ClaudeTemperature = temp;
            changed = true;
        }

        PromptEditorUI.Draw(
            promptManager,
            Echoglossian.PromptType.Claude,
            PromptTemplateManager.DefaultPrompt,
            Echoglossian.TransEngines.Claude.ToString());

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
                config.ClaudeApiKey,
                Sensitive: true),
            new LiveModelRefreshSignatureComponent(
                "baseUrl",
                config.ClaudeBaseUrl));
    }
}
