// <copyright file="LibreTranslateEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Helpers;
using ImGuiNET;

namespace Echoglossian.PluginUI.EngineConfigUI;

/// <summary>
/// Renders the configuration UI for LibreTranslate translator.
/// </summary>
public static class LibreTranslateEngineUI
{
  public static bool Draw(Config config)
  {
    bool changed = false;

    ImGui.TextWrapped("Settings for LibreTranslate.");
    changed |= ImGui.InputText("LibreTranslate API Endpoint", ref config.LibreTranslateUrl, 300);
    if (string.IsNullOrWhiteSpace(config.LibreTranslateUrl))
      FieldValidationHelper.ShowFieldRequiredWarning("LibreTranslate API Endpoint");

    PromptTemplateManager.DrawPromptEditor(config, PromptType.LibreTranslate, PromptTemplateManager.DefaultPrompt, "LibreTranslate");

    return changed;
  }
}
