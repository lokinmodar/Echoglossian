// <copyright file="GoogleEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.EngineConfigUI;

/// <summary>
/// Renders the configuration UI for the Google Translate engine.
/// </summary>
public static class GoogleEngineUI
{
  /// <summary>
  /// Draws the Google Translate engine settings.
  /// </summary>
  /// <param name="config">The plugin configuration.</param>
  /// <returns>True if any setting was changed.</returns>
  public static bool Draw(Config config)
  {
    ImGui.TextWrapped(Resources.SettingsForGTransText);
    ImGui.TextWrapped(Resources.TranslationEngineSettingsNotRequired);
    return false;
  }
}
