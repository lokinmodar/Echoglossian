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
  public static bool Draw(Config config)
  {
    bool changed = false;

    ImGui.TextWrapped("Settings for OpenLlama translator.");
    changed |= ImGui.InputText("Model Endpoint", ref config.OpenLlamaBaseUrl, 400);
    if (string.IsNullOrWhiteSpace(config.OpenLlamaBaseUrl))
      FieldValidationHelper.ShowFieldRequiredWarning("Model Endpoint");

    PromptTemplateManager.DrawPromptEditor(config, PromptType.OpenLlama, PromptTemplateManager.DefaultPrompt, "OpenLlama");

    return changed;
  }
}
