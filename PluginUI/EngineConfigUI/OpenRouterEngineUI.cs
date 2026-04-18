// <copyright file="OpenRouterEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.Translators.OpenAI;
using Echoglossian.Translators.OpenRouter;

namespace Echoglossian.PluginUI.EngineConfigUI;

public static class OpenRouterEngineUI
{
    private static List<OpenAITextModel> models =
        OpenRouterModelManager.CurrentModelList;

    public static bool Draw(Config config, PromptTemplateManager promptManager)
    {
        var changed = false;

        ImGui.TextWrapped(Resources.SettingsForOpenRouterText);
        ImGui.Spacing();

        bool isApiKeyInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.APIKey,
            ref config.OpenRouterApiKey,
            300,
            out isApiKeyInvalid);

        bool isBaseUrlInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.ModelEndpoint,
            ref config.OpenRouterBaseUrl,
            400,
            out isBaseUrlInvalid);

        // Live model list toggle
        var newToggle = config.UseLiveOpenRouterModelList;
        if (ImGui.Checkbox(Resources.FetchLiveModels, ref newToggle))
        {
            config.UseLiveOpenRouterModelList = newToggle;
            changed = true;

            if (newToggle)
            {
                Task.Run(async () =>
                {
                    await OpenRouterModelManager.RefreshAsync(
                        config.OpenRouterApiKey,
                        config.OpenRouterBaseUrl);
                    models = OpenRouterModelManager.CurrentModelList;
                });
            }
            else
            {
                OpenRouterModelManager.ResetToDefault();
                models = OpenRouterModelManager.CurrentModelList;
            }
        }

        // Dropdown model selection
        changed |= ModelDropdownUI.Draw(
            Resources.LLMModel,
            ref config.OpenRouterModel,
            models,
            "OpenRouter");

        var temp = config.OpenRouterTemperature;
        if (ImGui.SliderFloat(Resources.Temperature, ref temp, 0.1f, 1.0f, "%.1f"))
        {
            config.OpenRouterTemperature = temp;
            changed = true;
        }

        PromptEditorUI.Draw(
            promptManager,
            Echoglossian.PromptType.OpenRouter,
            PromptTemplateManager.DefaultPrompt,
            Echoglossian.TransEngines.OpenRouter.ToString());

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }
}
