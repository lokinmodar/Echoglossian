// <copyright file="AmazonEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Helpers;
using ImGuiNET;

namespace Echoglossian.PluginUI.EngineConfigUI;

/// <summary>
/// Renders the configuration UI for Amazon Translate.
/// </summary>
public static class AmazonEngineUI
{
  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForAmazonTranslateText);

    changed |= ImGui.InputText("AWS Access Key", ref config.AwsAccessKey, 200);
    if (string.IsNullOrWhiteSpace(config.AwsAccessKey))
      FieldValidationHelper.ShowFieldRequiredWarningIfEmpty("AWS Access Key");

    changed |= ImGui.InputText("AWS Secret Key", ref config.AwsSecretKey, 200);
    if (string.IsNullOrWhiteSpace(config.AwsSecretKey))
      FieldValidationHelper.ShowFieldRequiredWarningIfEmpty("AWS Secret Key");

    changed |= ImGui.InputText("Region", ref config.AwsRegion, 100);
    if (string.IsNullOrWhiteSpace(config.AwsRegion))
      FieldValidationHelper.ShowFieldRequiredWarningIfEmpty("Region");

    PromptEditorUI.Draw(promptManager, PromptType.Amazon, DefaultPrompt, TransEngines.Amazon.ToString());


    return changed;
  }
}