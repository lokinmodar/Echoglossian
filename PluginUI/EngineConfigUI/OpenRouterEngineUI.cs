// <copyright file="OpenRouterEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Diagnostics;
using System.Numerics;
using Echoglossian.Helpers;
using Echoglossian.Properties;
using ImGuiNET;

namespace Echoglossian.PluginUI.Helpers;

/// <summary>
/// Renders the OpenRouter engine settings UI.
/// </summary>
public static class OpenRouterEngineUI
{
  /// <summary>
  /// Draws the settings UI for the OpenRouter translation engine.
  /// </summary>
  /// <param name="config">The plugin configuration.</param>
  /// <param name="promptManager">Prompt management helper.</param>
  /// <returns>True if any value changed.</returns>
  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForOpenRouterText); // Add this to Resources.resx if missing
    ImGui.Spacing();

    // API Key
    bool isApiKeyInvalid;
    changed |= FieldValidationHelper.ValidatedInputText("API Key", ref config.OpenRouterApiKey, 300, out isApiKeyInvalid);

    // Base URL
    bool isBaseUrlInvalid;
    changed |= FieldValidationHelper.ValidatedInputText("Model Endpoint", ref config.OpenRouterBaseUrl, 400, out isBaseUrlInvalid);

    // Model Name
    bool isModelInvalid;
    changed |= FieldValidationHelper.ValidatedInputText("LLM Model", ref config.OpenRouterModel, 200, out isModelInvalid);

    // Temperature
    float temp = config.OpenRouterTemperature;
    if (ImGui.SliderFloat("Temperature", ref temp, 0.1f, 1.0f, "%.1f"))
    {
      config.OpenRouterTemperature = temp;
      changed = true;
    }

    ImGui.Separator();

    // Prompt Editor
    PromptEditorUI.Draw(promptManager, PromptType.OpenRouter, DefaultPrompt, TransEngines.OpenRouter.ToString());

    return changed;
  }

}
