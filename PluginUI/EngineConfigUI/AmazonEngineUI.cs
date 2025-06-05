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
  public static bool Draw(Config config)
  {
    bool changed = false;

    ImGui.TextWrapped("Settings for Amazon Translate.");
    changed |= ImGui.InputText("AWS Access Key", ref config.AwsAccessKey, 200);
    if (string.IsNullOrWhiteSpace(config.AwsAccessKey))
      FieldValidationHelper.ShowFieldRequiredWarning("AWS Access Key");

    changed |= ImGui.InputText("AWS Secret Key", ref config.AwsSecretKey, 200);
    if (string.IsNullOrWhiteSpace(config.AwsSecretKey))
      FieldValidationHelper.ShowFieldRequiredWarning("AWS Secret Key");

    changed |= ImGui.InputText("Region", ref config.AwsRegion, 100);
    if (string.IsNullOrWhiteSpace(config.AwsRegion))
      FieldValidationHelper.ShowFieldRequiredWarning("Region");

    PromptTemplateManager.DrawPromptEditor(config, PromptType.Amazon, PromptTemplateManager.DefaultPrompt, "Amazon");

    return changed;
  }
}
