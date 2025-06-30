// <copyright file="YandexPublicEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.EngineConfigUI;

/// <summary>
/// Renders the configuration UI for the Yandex Public Translator (no custom prompts).
/// </summary>
public static class YandexPublicEngineUI
{
  public static bool Draw(Config config)
  {
    ImGui.TextWrapped(Resources.SettingsForYandexPublic);
    ImGui.TextWrapped(Resources.TranslationEngineSettingsNotRequired);
    return false;
  }
}
