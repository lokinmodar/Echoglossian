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
  public static bool Draw(Config config)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForYandexCloudText);
    ImGui.Spacing();

    changed |= ImGui.InputText(Resources.YandexCloudFolderId, ref config.YandexFolderId, 200);
    if (string.IsNullOrWhiteSpace(config.YandexFolderId))
      FieldValidationHelper.ShowFieldRequiredWarning(Resources.YandexCloudFolderId);

    changed |= ImGui.InputText(Resources.YandexCloudApiKey, ref config.YandexApiKey, 300);
    if (string.IsNullOrWhiteSpace(config.YandexApiKey))
      FieldValidationHelper.ShowFieldRequiredWarning(Resources.YandexCloudApiKey);

    return changed;
  }
}
