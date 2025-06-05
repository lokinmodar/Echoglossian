// <copyright file="MicrosoftEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Helpers;
using ImGuiNET;

namespace Echoglossian.PluginUI.EngineConfigUI;

/// <summary>
/// Renders the configuration UI for Microsoft Translator.
/// </summary>
public static class MicrosoftEngineUI
{
  public static bool Draw(Config config)
  {
    bool changed = false;

    ImGui.TextWrapped("Settings for Microsoft Translator.");
    changed |= ImGui.InputText("Microsoft Translator API Key", ref config.MicrosoftApiKey, 200);
    if (string.IsNullOrWhiteSpace(config.MicrosoftApiKey))
      FieldValidationHelper.ShowFieldRequiredWarning("API Key");

    changed |= ImGui.InputText("Region", ref config.MicrosoftRegion, 100);
    if (string.IsNullOrWhiteSpace(config.MicrosoftRegion))
      FieldValidationHelper.ShowFieldRequiredWarning("Region");

    PromptTemplateManager.DrawPromptEditor(config, PromptType.Microsoft, PromptTemplateManager.DefaultPrompt, "Microsoft");

    return changed;
  }
}
