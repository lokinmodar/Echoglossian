// <copyright file="GeminiEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.Translators.Gemini;

namespace Echoglossian.PluginUI.EngineConfigUI;

public static class GeminiEngineUI
{
    private const string LiveModelRefreshScope = "Gemini";

    public static bool Draw(Config config, PromptTemplateManager promptManager)
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
                    () => GeminiModelManager.RefreshAsync(
                        config.GeminiTranslatorApiKey ?? string.Empty));
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
            if (ImGui.Button(Resources.Reload))
            {
                LiveModelRefreshCoordinator.ForceRefresh(
                    LiveModelRefreshScope,
                    BuildLiveModelRefreshSignature(config),
                    () => GeminiModelManager.RefreshAsync(
                        config.GeminiTranslatorApiKey ?? string.Empty));
            }
        }

        LiveModelRefreshCoordinator.RequestIfNeeded(
            LiveModelRefreshScope,
            config.UseLiveGeminiModelList,
            BuildLiveModelRefreshSignature(config),
            () => GeminiModelManager.RefreshAsync(
                config.GeminiTranslatorApiKey ?? string.Empty));

        var tooltips = new Dictionary<string, string>
        {
            ["gemini-pro"] = Resources.GeminiModelTooltipPro,
            ["gemini-1.5-pro"] = Resources.GeminiModelTooltip15Pro,
            ["gemini-1.5-flash"] = Resources.GeminiModelTooltip15Flash,
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

        PromptEditorUI.Draw(
            promptManager,
            Echoglossian.PromptType.Gemini,
            PromptTemplateManager.DefaultPrompt,
            Echoglossian.TransEngines.Gemini.ToString());

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
                config.GeminiTranslatorApiKey,
                Sensitive: true));
    }
}
