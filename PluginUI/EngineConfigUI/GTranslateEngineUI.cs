// <copyright file="GTranslateEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Helpers;
using Echoglossian.Properties;
using ImGuiNET;

namespace Echoglossian.PluginUI.EngineConfigUI;


/// <summary>
/// Renders the configuration UI for GTranslate.
/// </summary>
public static class GTranslateEngineUI
{
  public static bool Draw(Config config)
  {
    ImGui.TextWrapped(Resources.SettingsForGTranslateText);

    return true;
  }
}
