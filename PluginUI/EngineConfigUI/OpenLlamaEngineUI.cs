// <copyright file="OpenLlamaEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Helpers;
using ImGuiNET;

namespace Echoglossian.PluginUI.EngineConfigUI;


/// <summary>
/// Renders the configuration UI for OpenLlama translator.
/// </summary>
public static class OpenLlamaEngineUI
{
  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForOpenLlamaText);

    changed |= ImGui.InputText("Model Endpoint", ref config.OpenLlamaBaseUrl, 400);
    if (string.IsNullOrWhiteSpace(config.OpenLlamaBaseUrl))
      FieldValidationHelper.ShowFieldRequiredWarningIfEmpty("Model Endpoint");

    PromptEditorUI.Draw(promptManager, PromptType.OpenLlama, DefaultPrompt, TransEngines.OpenLlama.ToString());

    return changed;
  }
}