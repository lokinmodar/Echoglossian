// <copyright file="LmStudioEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.Translators.LmStudio;
using Echoglossian.Translators.OpenAI;

namespace Echoglossian.PluginUI.EngineConfigUI;

/// <summary>
///     UI for configuring LM Studio translator engine.
/// </summary>
public static class LmStudioEngineUI
{
    private static List<LlmTextModel> staticModels =
        LmStudioTextModelDefaults.PredefinedModels;

    private static List<LlmTextModel> liveModels = new();
    private static bool modelsFetched;

    /// <summary>
    ///     Draws the LM Studio engine configuration panel.
    /// </summary>
    /// <param name="config">Current plugin configuration.</param>
    /// <param name="promptManager">Prompt manager instance.</param>
    /// <returns>True if any settings changed.</returns>
    public static bool Draw(Config config, PromptTemplateManager promptManager)
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

        ImGui.Checkbox(
            Resources.FetchLiveModels,
            ref config.UseLiveLmStudioModelList);

        var models = config.UseLiveLmStudioModelList
            ? LmStudioModelManager.CurrentModelList
            : LmStudioTextModelDefaults.PredefinedModels;

        if (config.UseLiveLmStudioModelList && !modelsFetched)
        {
            _ = Task.Run(async () =>
            {
                await LmStudioModelManager.RefreshAsync(
                    config.LmStudioBaseUrl ?? string.Empty,
                    config.UseLmStudioAuth ? config.LmStudioApiKey : null);
                modelsFetched = true;
            });
        }

        changed |= ModelDropdownUI.Draw(
            Resources.Model,
            ref config.LmStudioModel,
            models,
            "LmStudio");

        var temp = config.LmStudioTemperature;
        if (ImGui.SliderFloat(
                Resources.Temperature,
                ref temp,
                0.1f,
                1.0f,
                "%.1f"))
        {
            config.LmStudioTemperature = temp;
            changed = true;
        }

        PromptEditorUI.Draw(
            promptManager,
            Echoglossian.PromptType.LmStudio,
            PromptTemplateManager.DefaultPrompt,
            Echoglossian.TransEngines.LmStudio.ToString());

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }
}
