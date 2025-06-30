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
  private static List<OpenAITextModel> models = OpenRouterModelManager.CurrentModelList;

  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForOpenRouterText);
    ImGui.Spacing();

    bool isApiKeyInvalid;
    changed |= FieldValidationHelper.ValidatedInputText("API Key", ref config.OpenRouterApiKey, 300, out isApiKeyInvalid);

    bool isBaseUrlInvalid;
    changed |= FieldValidationHelper.ValidatedInputText("Model Endpoint", ref config.OpenRouterBaseUrl, 400, out isBaseUrlInvalid);

    // Live model list toggle
    bool newToggle = config.UseLiveOpenRouterModelList;
    if (ImGui.Checkbox("Use Live Model List", ref newToggle))
    {
      config.UseLiveOpenRouterModelList = newToggle;
      changed = true;

      if (newToggle)
      {
        Task.Run(async () =>
        {
          await OpenRouterModelManager.RefreshAsync(config.OpenRouterApiKey, config.OpenRouterBaseUrl);
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
    changed |= ModelDropdownUI.Draw("LLM Model", ref config.OpenRouterModel, models, "OpenRouter");

    float temp = config.OpenRouterTemperature;
    if (ImGui.SliderFloat("Temperature", ref temp, 0.1f, 1.0f, "%.1f"))
    {
      config.OpenRouterTemperature = temp;
      changed = true;
    }

    PromptEditorUI.Draw(promptManager, PromptType.OpenRouter, PromptTemplateManager.DefaultPrompt, TransEngines.OpenRouter.ToString());

    if (changed)
    {
      FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
      SaveConfig(config);
    }

    return changed;
  }
}
