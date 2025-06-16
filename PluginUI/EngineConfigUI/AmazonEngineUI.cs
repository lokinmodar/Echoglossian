// <copyright file="AmazonEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>
namespace Echoglossian.PluginUI.EngineConfigUI;
public static class AmazonEngineUI
{
  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForAmazonTranslateText);

    bool isAccessKeyInvalid;
    changed |= FieldValidationHelper.ValidatedInputText(Resources.AWSAccessKey, ref config.AwsAccessKey, 200, out isAccessKeyInvalid);

    bool isSecretKeyInvalid;
    changed |= FieldValidationHelper.ValidatedInputText(Resources.AWSSecretKey, ref config.AwsSecretKey, 200, out isSecretKeyInvalid);

    bool isRegionInvalid;
    changed |= FieldValidationHelper.ValidatedInputText(Resources.Region, ref config.AwsRegion, 100, out isRegionInvalid);

    PromptEditorUI.Draw(promptManager, PromptType.Amazon, DefaultPrompt, TransEngines.Amazon.ToString());

    if (ImGui.Button(Resources.Save))
    {
      FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
      SaveConfig(config);
    }

    return changed;
  }
}
