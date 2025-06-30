// <copyright file="OpenLlamaEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;

namespace Echoglossian.PluginUI.EngineConfigUI;
public static class OpenLlamaEngineUI
{
  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForOpenLlamaText);

    bool isEndpointInvalid;
    changed |= FieldValidationHelper.ValidatedInputText("Model Endpoint", ref config.OpenLlamaUrl, 400, out isEndpointInvalid);

    PromptEditorUI.Draw(promptManager, PromptType.OpenLlama, PromptTemplateManager.DefaultPrompt, TransEngines.OpenLlama.ToString());

    if (changed)
    {
      FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
      SaveConfig(config);
    }

    return changed;
  }
}
