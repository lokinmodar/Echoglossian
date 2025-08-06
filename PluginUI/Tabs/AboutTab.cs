// <copyright file="AboutTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>
namespace Echoglossian.PluginUI.Tabs;

/// <summary>
/// Displays the "About Echoglossian" tab content with logo and disclaimer.
/// </summary>
public static class AboutTab
{
  /// <summary>
  /// Draws the About tab.
  /// </summary>
  /// <param name="config">The plugin config object.</param>
  /// <param name="logoHandle">The ImGui handle for the logo image.</param>
  /// <returns>True if any changes were made (always false here).</returns>
  public static bool Draw(Config config, ImTextureID logoHandle)
  {
    bool changed = false;
    if (ImGui.BeginTable("columns", 2))
    {
      ImGui.TableNextColumn();
      ImGui.BeginGroup();
      ImGui.TextColored(new Vector4(247, 247, 7, 255), Resources.DisclaimerTitle);
      ImGui.Spacing();
      ImGui.TextWrapped(Resources.DisclaimerText1);
      ImGui.TextWrapped(Resources.DisclaimerText2);
      ImGui.TextWrapped(Resources.ContribText);
      ImGui.EndGroup();

      ImGui.TableNextColumn();
      var posLogo = new Vector2(ImGui.GetWindowContentRegionMax().X - 300, ImGui.GetWindowContentRegionMin().Y + 150);
      ImGui.SetCursorPos(posLogo);
      ImGui.Image(logoHandle, new Vector2(300, 300));
      ImGui.EndTable();
    }

    return changed;
  }
}
