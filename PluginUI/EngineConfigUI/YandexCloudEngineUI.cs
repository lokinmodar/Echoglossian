// <copyright file="YandexCloudEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Helpers;
using Echoglossian.Properties;
using ImGuiNET;

namespace Echoglossian.PluginUI.EngineConfigUI;


/// <summary>
/// Renders the configuration UI for Yandex Cloud Translator.
/// </summary>
public static class YandexCloudEngineUI
{
  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForYandexCloudText);

    changed |= ImGui.InputText(Resources.YandexCloudFolderId, ref config.YandexFolderId, 200);
    if (string.IsNullOrWhiteSpace(config.YandexFolderId))
      FieldValidationHelper.ShowFieldRequiredWarningIfEmpty(Resources.YandexCloudFolderId);

    changed |= ImGui.InputText(Resources.YandexCloudApiKey, ref config.YandexPaidApiKey, 300);
    if (string.IsNullOrWhiteSpace(config.YandexPaidApiKey))
      FieldValidationHelper.ShowFieldRequiredWarningIfEmpty(Resources.YandexCloudApiKey);

    PromptEditorUI.Draw(promptManager, PromptType.YandexCloud, DefaultPrompt, TransEngines.YandexCloud.ToString());

    return changed;
  }
}
