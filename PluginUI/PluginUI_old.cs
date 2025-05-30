// <copyright file="PluginUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Diagnostics;

using Echoglossian.Properties;
using Echoglossian.Translators;
using ImGuiNET;

namespace Echoglossian;
public partial class Echoglossian
{
  // public string[] FontSizes = Array.ConvertAll(Enumerable.Range(4, 72).ToArray(), x => x.ToString());



  private void EchoglossianConfigUiOld()
  {













    if (ImGui.BeginTabBar(
      "TabBar",
      ImGuiTabBarFlags.NoCloseWithMiddleMouseButton))
    {

      ImGui.EndTabBar();
    }

    ImGui.EndGroup();


    ImGui.BeginGroup();
    ImGui.Spacing();

    var windowSize = ImGui.GetWindowContentRegionMax();
    /*var pos = new Vector2(ImGui.GetWindowContentRegionMin().X, ImGui.GetWindowContentRegionMax().Y - 100);*/
    //var zero = Vector2.Zero;

    // var placeholderButtonSize = ImGuiHelpers.GetButtonSize("placeholder");

    ImGui.SetCursorPosY(windowSize.Y - 100/* placeholderButtonSize.Y*/);
    ImGui.Separator();
    ImGui.BeginGroup();
    ImGui.TextWrapped(Resources.NEListText);

    ImGui.PushID(1);
    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1 / 7.0f, 0.6f, 0.6f, 1f));
    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1 / 7.0f, 0.7f, 0.7f, 1f));
    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1 / 7.0f, 0.8f, 0.8f, 1f));
    if (ImGui.Button(Resources.TodoUrl))
    {
      this.SaveConfigValue = true;
      Process.Start(new ProcessStartInfo
      {
        FileName = "https://github.com/users/lokinmodar/projects/2",
        UseShellExecute = true,
      });
      this.config = false;
    }

    ImGui.PopStyleColor(3);
    ImGui.PopID();

    ImGui.Spacing();

    if (ImGui.Button(Resources.SaveCloseButtonLabel))
    {
      this.SaveConfigValue = true;
      this.config = false;
    }

    ImGui.SameLine();
    ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
    ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);
    if (ImGui.Button(Resources.PatronButtonLabel))
    {
      this.SaveConfigValue = true;
      Process.Start(new ProcessStartInfo
      {
        FileName = "https://ko-fi.com/lokinmodar",
        UseShellExecute = true,
      });
      this.config = false;
    }

    ImGui.PopStyleColor(3);
    ImGui.SameLine();
    ImGui.PushID(4);
    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(4, 7.0f, 0.6f, 0.6f));
    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(4, 7.0f, 0.7f, 0.7f));
    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(4, 7.0f, 0.8f, 0.8f));
    if (ImGui.Button(Resources.SendPixButton))
    {
      this.SaveConfigValue = true;
      ImGui.OpenPopup(Resources.PixQrWindowLabel);
    }

    ImGui.EndGroup();

    // Always center this window when appearing
    var center = ImGui.GetMainViewport().GetCenter();
    ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
    if (ImGui.BeginPopupModal(Resources.PixQrWindowLabel))
    {
      ImGui.Text(Resources.QRCodeInstructionsText);
      ImGui.Image(this.pixImage.ImGuiHandle, new Vector2(512, 512));
      if (ImGui.Button(Resources.CloseButtonLabel))
      {
        ImGui.CloseCurrentPopup();
      }

      ImGui.EndPopup();
      ImGui.SetItemDefaultFocus();
    }

    ImGui.PopStyleColor(3);
    ImGui.PopID();
    ImGui.EndGroup();
    ImGui.End();

    if (this.SaveConfigValue)
    {
      //this.SaveConfig();
    }
  }

}