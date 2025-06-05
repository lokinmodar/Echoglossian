// <copyright file="GeminiEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Helpers;
using ImGuiNET;

namespace Echoglossian.PluginUI.EngineConfigUI;


/// <summary>
/// Renders the configuration UI for Google Gemini.
/// </summary>
public static class GeminiEngineUI
{
  public static bool Draw(Config config)
  {
    bool changed = false;

    ImGui.TextWrapped("Settings for Gemini Translator.");
    changed |= ImGui.InputText("Gemini API Key", ref config.GeminiApiKey, 300);
    if (string.IsNullOrWhiteSpace(config.GeminiApiKey))
      FieldValidationHelper.ShowFieldRequiredWarning("Gemini API Key");

    PromptTemplateManager.DrawPromptEditor(config, PromptType.Gemini, PromptTemplateManager.DefaultPrompt, "Gemini");

    return changed;
  }
}
