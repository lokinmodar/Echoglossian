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
  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForMicrosoftText);

    changed |= ImGui.InputText("Microsoft Translator API Key", ref config.MicrosoftTranslatorApiKey, 200);
    if (string.IsNullOrWhiteSpace(config.MicrosoftTranslatorApiKey))
      FieldValidationHelper.ShowFieldRequiredWarningIfEmpty("API Key");

    changed |= ImGui.InputText("Region", ref config.MicrosoftTranslatorRegion, 100);
    if (string.IsNullOrWhiteSpace(config.MicrosoftTranslatorRegion))
      FieldValidationHelper.ShowFieldRequiredWarningIfEmpty("Region");

    PromptEditorUI.Draw(promptManager, PromptType.Microsoft, DefaultPrompt, TransEngines.Microsoft.ToString());

    return changed;
  }
}