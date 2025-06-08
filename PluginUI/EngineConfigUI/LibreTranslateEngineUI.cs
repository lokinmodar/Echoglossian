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
  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForLibreTranslateText);

    changed |= ImGui.InputText("LibreTranslate API Endpoint", ref config.LibreTranslateUrl, 300);
    if (string.IsNullOrWhiteSpace(config.LibreTranslateUrl))
      FieldValidationHelper.ShowFieldRequiredWarningIfEmpty("LibreTranslate API Endpoint");

    PromptEditorUI.Draw(promptManager, PromptType.LibreTranslate, DefaultPrompt, TransEngines.LibreTranslate.ToString());

    return changed;
  }
}
