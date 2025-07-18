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

        bool isGeminiApiKeyInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            "Gemini API Key",
            ref config.GeminiTranslatorApiKey,
            300,
            out isGeminiApiKeyInvalid);

        // Optional: Live fetch toggle

        if (ImGui.Checkbox(
                "Fetch model list live",
                ref config.UseLiveGeminiModelList))
        {
            changed = true;
            if (config.UseLiveGeminiModelList)
            {
                _ = Task.Run(() =>
                    GeminiModelManager.RefreshAsync(
                        config.GeminiTranslatorApiKey));
            }
            else
            {
                GeminiModelManager.ResetToDefault();
            }
        }

        // Tooltip info per model
        var tooltips = new Dictionary<string, string>
        {
            ["gemini-pro"] = "🔷 Legacy Gemini Pro model (default)",
            ["gemini-1.5-pro"] = "🟢 Large context window and high accuracy",
            ["gemini-1.5-flash"] = "⚡ Fastest and cheapest Gemini model",
        };

        // Use either GeminiModelManager.CurrentModels if live, or static:
        var models = GeminiTextModelDefaults.PredefinedModels;

        changed |= ModelDropdownUI.Draw(
            "Gemini Model",
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