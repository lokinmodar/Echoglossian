// <copyright file="TranslationEnginesTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using ImGuiNET;
using Echoglossian.Properties;

namespace Echoglossian.Tabs;

/// <summary>
/// Renders the translation engines tab.
/// </summary>
public static class TranslationEnginesTab
{
  /// <summary>
  /// Draws the translation engine tab by invoking the plugin's shared draw method.
  /// </summary>
  /// <param name="config">The current plugin configuration.</param>
  /// <returns>Always returns false, since this tab's draw state is handled internally.</returns>
  public static bool Draw(Config config)
  {
    if (ImGui.BeginTabItem(Resources.ConfigTab7Name))
    {
      Echoglossian.DrawTranslationEnginesTab(config); // TODO: FIx this. It is broken now
      ImGui.EndTabItem();
    }

    return false;
  }
}
