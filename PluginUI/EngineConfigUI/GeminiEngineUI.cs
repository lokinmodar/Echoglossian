// <copyright file="GeminiEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.Translators.Gemini;

namespace Echoglossian.PluginUI.EngineConfigUI;

public static class GeminiEngineUI
{
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

        // Optional: Live fetch toggle

        if (ImGui.Checkbox(
                Resources.FetchLiveModels,
                ref config.UseLiveGeminiModelList))
        {
            changed = true;
            if (config.UseLiveGeminiModelList)
            {
                _ = Task.Run(() =>
                    GeminiModelManager.RefreshAsync(
                        config.GeminiTranslatorApiKey ?? string.Empty));
            }
            else
            {
                GeminiModelManager.ResetToDefault();
            }
        }

        // Tooltip info per model
        var tooltips = new Dictionary<string, string>
        {
            ["gemini-pro"] = Resources.ResourceManager.GetString("GeminiModelTooltipPro", Resources.Culture) ??
                             "🔷 Legacy Gemini Pro model (default)",
            ["gemini-1.5-pro"] = Resources.ResourceManager.GetString("GeminiModelTooltip15Pro", Resources.Culture) ??
                                 "🟢 Large context window and high accuracy",
            ["gemini-1.5-flash"] = Resources.ResourceManager.GetString("GeminiModelTooltip15Flash", Resources.Culture) ??
                                   "⚡ Fastest and cheapest Gemini model",
        };

        // Use either GeminiModelManager.CurrentModels if live, or static:
        var models = GeminiTextModelDefaults.PredefinedModels;

        changed |= ModelDropdownUI.Draw(
            Resources.LLMModel,
            ref config.GeminiModelId,
            models,
            "Gemini",
            tooltips);

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
}
