// <copyright file="GeminiEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Helpers;
using ImGuiNET;


namespace Echoglossian.PluginUI.Helpers.Engines;

public static class GeminiEngineUI
{
  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForGeminiText);

    changed |= ImGui.InputText("Gemini API Key", ref config.GeminiTranslatorApiKey, 300);
    if (string.IsNullOrWhiteSpace(config.GeminiTranslatorApiKey))
      FieldValidationHelper.ShowFieldRequiredWarningIfEmpty("Gemini API Key");

    PromptEditorUI.Draw(promptManager, PromptType.Gemini, DefaultPrompt, TransEngines.Gemini.ToString());

    return changed;
  }
}