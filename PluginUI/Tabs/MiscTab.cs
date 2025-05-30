// <copyright file="MiscTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Properties;
using ImGuiNET;

namespace Echoglossian.Tabs;

/// <summary>
/// Renders the miscellaneous settings tab in the plugin configuration UI.
/// </summary>
public static class MiscTab
{
  /// <summary>
  /// Draws the miscellaneous settings UI.
  /// </summary>
  /// <param name="config">The plugin configuration.</param>
  /// <returns>True if any value has changed, otherwise false.</returns>
  public static bool Draw(Config config)
  {
    bool changed = false;

    ImGui.Text(Resources.ConfigTab9Text);

    if (ImGui.Checkbox(Resources.ConfigTab9CheckboxClipboardText, ref config.CopyTranslationToClipboard))
    {
      changed = true;
    }

    ImGui.TextWrapped(Resources.ConfigTab9CheckboxClipboardTooltipText);

    return changed;
  }
}
