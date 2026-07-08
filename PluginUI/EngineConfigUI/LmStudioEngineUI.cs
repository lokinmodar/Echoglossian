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
    private const string LiveModelRefreshScope = "LmStudio";

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
            if (ImGui.Button(Resources.Reload))
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
            PromptTemplateManager.GetDefaultPrompt(Echoglossian.PromptType.LmStudio),
            Echoglossian.TransEngines.LmStudio.ToString());

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
