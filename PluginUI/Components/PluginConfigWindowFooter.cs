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
  /// <param name="pixImageHandle">The ImGui texture handle for the Pix QR image.</param>
  public static void DrawFooter(ref bool config, ref bool saveConfigValue, ImTextureID pixImageHandle, ImTextureID cryptoImageHandle)
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
    }

    // Patron Button
    ImGui.SameLine();
    ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
    ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);

    if (ImGui.Button(Resources.PatronButtonLabel))
    {
      Process.Start(new ProcessStartInfo
      {
        FileName = "https://ko-fi.com/lokinmodar",
        UseShellExecute = true,
      });
      config = false;
    }

    ImGui.PopStyleColor(3);
    ImGui.SameLine();

    // Send crypto button
    ImGui.PushID(3);
    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.95f, 0.5f, 0.90f)); // light green
    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.60f, 1.0f, 0.60f, 0.95f)); // brighter on hover
    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.45f, 0.87f, 0.45f, 1.00f)); // slightly deeper for active

    if (ImGui.Button(Resources.SendCryptoButton))
    {
      ImGui.OpenPopup(Resources.CryptoQrWindowLabel);
    }

    // Always center this window when appearing
    var centerbtn = ImGui.GetMainViewport().GetCenter();
    ImGui.SetNextWindowPos(centerbtn, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

    if (ImGui.BeginPopupModal(Resources.CryptoQrWindowLabel))
    {
      ImGui.Text(Resources.CryptoQRCodeInstructionsText);
      ImGui.Image(cryptoImageHandle, new Vector2(450, 512));

      if (ImGui.Button(Resources.CloseButtonLabel))
      {
        ImGui.CloseCurrentPopup();
      }

      ImGui.EndPopup();
      ImGui.SetItemDefaultFocus();
    }

    ImGui.PopStyleColor(3);
    ImGui.PopID();

    ImGui.SameLine();

    // Pix Button
    ImGui.PushID(4);
    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.12f, 0.18f, 0.32f, 1.0f)); // dark blue
    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.18f, 0.26f, 0.45f, 1.0f)); // lighter blue on hover
    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.10f, 0.23f, 0.42f, 1.0f)); // accent/active blue

    if (ImGui.Button(Resources.SendPixButton))
    {
      ImGui.OpenPopup(Resources.PixQrWindowLabel);
    }

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

    ImGui.PopStyleColor(3);
    ImGui.PopID();

    ImGui.EndGroup();
  }
}
