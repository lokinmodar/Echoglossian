// <copyright file="PluginConfigWindowFooter.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Components;

/// <summary>
/// Renders the footer of the Plugin Configuration Window.
/// Includes Save, Donation, Project Links, and Pix QR popup.
/// </summary>
public static class PluginConfigWindowFooter
{
  /// <summary>
  /// Draws the footer section with buttons and Pix QR popup.
  /// </summary>
  /// <param name="config">The window open/close flag.</param>
  /// <param name="saveConfigValue">Reference to the SaveConfigValue flag.</param>
  /// <param name="saveCallback">Callback function to invoke when saving.</param>
  /// <param name="pixImageHandle">The ImGui texture handle for the Pix QR image.</param>
  public static void DrawFooter(ref bool config, ref bool saveConfigValue, Action saveCallback, nint pixImageHandle)
  {
    var windowSize = ImGui.GetWindowContentRegionMax();

    ImGui.SetCursorPosY(windowSize.Y - 100);
    ImGui.Separator();

    ImGui.BeginGroup();

    ImGui.TextWrapped(Resources.NEListText);

    // Project Link
    ImGui.PushID(1);
    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1 / 7.0f, 0.6f, 0.6f, 1f));
    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1 / 7.0f, 0.7f, 0.7f, 1f));
    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1 / 7.0f, 0.8f, 0.8f, 1f));

    if (ImGui.Button(Resources.TodoUrl))
    {
      saveConfigValue = true;
      Process.Start(new ProcessStartInfo
      {
        FileName = "https://github.com/users/lokinmodar/projects/2",
        UseShellExecute = true,
      });
      config = false;
    }

    ImGui.PopStyleColor(3);
    ImGui.PopID();

    ImGui.Spacing();

    // Save Button
    if (ImGui.Button(Resources.SaveCloseButtonLabel))
    {
      saveConfigValue = true;
      config = false;
      saveCallback?.Invoke();
    }

    // Patron Button
    ImGui.SameLine();
    ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
    ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);

    if (ImGui.Button(Resources.PatronButtonLabel))
    {
      saveConfigValue = true;
      Process.Start(new ProcessStartInfo
      {
        FileName = "https://ko-fi.com/lokinmodar",
        UseShellExecute = true,
      });
      config = false;
    }

    ImGui.PopStyleColor(3);

    // Pix Button
    ImGui.SameLine();
    ImGui.PushID(4);
    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(4, 7.0f, 0.6f, 0.6f));
    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(4, 7.0f, 0.7f, 0.7f));
    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(4, 7.0f, 0.8f, 0.8f));

    if (ImGui.Button(Resources.SendPixButton))
    {
      saveConfigValue = true;
      ImGui.OpenPopup(Resources.PixQrWindowLabel);
    }

    ImGui.PopStyleColor(3);
    ImGui.PopID();

    ImGui.EndGroup();

    // Pix QR Popup
    var center = ImGui.GetMainViewport().GetCenter();
    ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

    if (ImGui.BeginPopupModal(Resources.PixQrWindowLabel))
    {
      ImGui.Text(Resources.QRCodeInstructionsText);
      ImGui.Image(pixImageHandle, new Vector2(512, 512));

      if (ImGui.Button(Resources.CloseButtonLabel))
      {
        ImGui.CloseCurrentPopup();
      }

      ImGui.EndPopup();
      ImGui.SetItemDefaultFocus();
    }
  }
}
