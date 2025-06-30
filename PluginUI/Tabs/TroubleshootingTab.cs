// <copyright file="TroubleshootingTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Plugin.Services;
using ImGuiNET;

using System.Diagnostics;
using System.Numerics;

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
/// Renders the troubleshooting tab for plugin assets management and resetting settings.
/// Fully self-contained using static Echoglossian methods.
/// </summary>
public static class TroubleshootingTab
{
  public static bool Draw(Config config)
  {
    bool changed = false;

    var pluginAssetsStatus = config.PluginAssetsDownloaded;

    ImGui.BeginGroup();
    ImGui.TextWrapped(Resources.CurrentPluginAssetsStatus + ": " + pluginAssetsStatus);
    ImGui.TextWrapped(Resources.PluginAssetsNotDownloadedText);

    ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
    ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);

    if (ImGui.Button(Resources.DownloadPluginAssetsButtonText))
    {
      AssetsManager.DownloadAssets(0);
      AssetsManager.DownloadAssets(1);
      AssetsManager.DownloadAssets(2);
      AssetsManager.DownloadAssets(3);
      AssetsManager.DownloadAssets(4);
      AssetsManager.PluginAssetsChecker();
      changed = true;
    }

    ImGui.PopStyleColor(3);
    ImGui.EndGroup();

    ImGui.Spacing();

    ImGui.BeginGroup();
    ImGui.TextWrapped(Resources.ResetSettingsMessageText);

    ResetConfigButtonHelper.Draw(config, () => SaveConfig(config));

    ImGui.EndGroup();

    if (changed)
    {
      FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
      SaveConfig(config);
    }

    return changed;
  }
}
