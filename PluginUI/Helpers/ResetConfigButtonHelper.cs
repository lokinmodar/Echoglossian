// <copyright file="ResetConfigButtonHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Helpers;

public static class ResetConfigButtonHelper
{
    private static bool showResetPopup;

    /// <summary>
    ///     Draws a button that resets the plugin config to default values, with a
    ///     confirmation popup.
    /// </summary>
    /// <param name="config">The plugin config instance to reset.</param>
    /// <param name="saveCallback">Callback to persist the config after reset.</param>
    /// <param name="buttonLabel">
    ///     Optional label for the button (defaults to "Reset
    ///     Settings to Default").
    /// </param>
    public static void Draw(
        Config config,
        Action saveCallback,
        string? buttonLabel = null)
    {
        buttonLabel ??= Resources.ResetSettingsButtonText;
        var popupId = "ConfirmResetSettingsPopup##" + buttonLabel;

        ImGui.PushID(19);
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);
        if (ImGui.Button(buttonLabel))
        {
            showResetPopup = true;
            ImGui.OpenPopup(popupId);
        }

        ImGui.PopStyleColor(3);
        ImGui.PopID();

        if (showResetPopup)
        {
            var open = true;
            if (ImGui.BeginPopupModal(
                    popupId,
                    ref open,
                    ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text(
                    Resources.AreYouSureYouWantToResetAllSettingsToDefault);
                ImGui.Separator();

                ImGui.PushID(9);
                ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
                ImGui.PushStyleColor(
                    ImGuiCol.ButtonActive,
                    0xDD000000 | 0x005E5BFF);
                ImGui.PushStyleColor(
                    ImGuiCol.ButtonHovered,
                    0xAA000000 | 0x005E5BFF);

                if (ImGui.Button(Resources.YesReset))
                {
                    Echoglossian.ResetSettings(config, saveCallback);
                    ImGui.CloseCurrentPopup();
                    showResetPopup = false;
                }

                ImGui.PopStyleColor(3);
                ImGui.PopID();

                ImGui.SameLine();

                if (ImGui.Button(Resources.Cancel))
                {
                    ImGui.CloseCurrentPopup();
                    showResetPopup = false;
                }

                ImGui.EndPopup();
            }
        }
    }
}
