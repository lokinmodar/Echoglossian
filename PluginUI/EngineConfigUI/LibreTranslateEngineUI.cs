// <copyright file="LibreTranslateEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>
public static class LibreTranslateEngineUI
{
  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForLibreTranslateText);

    bool isEndpointInvalid;
    changed |= FieldValidationHelper.ValidatedInputText(Resources.LibreTranslateAPIEndpoint, ref config.LibreTranslateUrl, 300, out isEndpointInvalid);

    PromptEditorUI.Draw(promptManager, PromptType.LibreTranslate, DefaultPrompt, TransEngines.LibreTranslate.ToString());

    if (ImGui.Button(Resources.Save))
    {
      FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
      SaveConfig(config);
    }

    return changed;
  }
}
