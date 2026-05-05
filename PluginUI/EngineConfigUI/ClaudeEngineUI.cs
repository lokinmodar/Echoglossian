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

        if (ImGui.Button(
                Resources.ResourceManager.GetString(
                    "OpenAnthropicApiKeys",
                    Resources.Culture) ??
                "Open Anthropic API Keys"))
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
                _ = Task.Run(() => ClaudeModelManager.RefreshAsync(
                    config.ClaudeApiKey,
                    config.ClaudeBaseUrl));
            }
            else if (!config.UseLiveClaudeModelList)
            {
                ClaudeModelManager.ResetToDefault();
            }
        }

        var tooltips = new Dictionary<string, string>
        {
            ["claude-sonnet-4-20250514"] = Resources.ResourceManager.GetString("ClaudeModelTooltipSonnet4", Resources.Culture) ??
                                           "Balanced quality, latency, and cost for general translation.",
            ["claude-3-7-sonnet-latest"] = Resources.ResourceManager.GetString("ClaudeModelTooltipSonnet37", Resources.Culture) ??
                                           "Strong general-purpose Claude alias with broad capability.",
            ["claude-3-5-haiku-latest"] = Resources.ResourceManager.GetString("ClaudeModelTooltipHaiku35", Resources.Culture) ??
                                          "Fastest Claude tier for lower latency and lower cost.",
            ["claude-opus-4-1-20250805"] = Resources.ResourceManager.GetString("ClaudeModelTooltipOpus41", Resources.Culture) ??
                                           "Highest-tier Claude model when you want maximum quality.",
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
}
