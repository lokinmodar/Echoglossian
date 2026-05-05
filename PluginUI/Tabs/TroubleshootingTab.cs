// <copyright file="TroubleshootingTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
///     Renders the troubleshooting tab for plugin assets management and resetting
///     settings.
///     Fully self-contained using static Echoglossian methods.
/// </summary>
public static class TroubleshootingTab
{
    public static bool Draw(Config config)
    {
        var changed = false;

        AssetsManager.RefreshPluginAssetsState(Echoglossian.SelectedLanguage);
        config.PluginAssetsDownloaded = AssetsManager.PluginAssetsDownloaded;
        var pluginAssetsStatus = AssetsManager.PluginAssetsDownloaded;

        ImGui.BeginGroup();
        ImGui.TextWrapped(
            Resources.CurrentPluginAssetsStatus + ": " + pluginAssetsStatus);
        ImGui.TextWrapped(Resources.PluginAssetsNotDownloadedText);

        if (AssetsManager.RequiresDownloadedAssets(Echoglossian.SelectedLanguage))
        {
            ImGui.Spacing();
            ImGui.TextWrapped(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.LanguageAssetsTroubleshootingSummaryFormat,
                    Echoglossian.SelectedLanguage.LanguageName,
                    AssetsManager.AssetsPath));

            if (ImGui.Button(Resources.ManageLanguageAssetsButtonText))
            {
                PluginAssetRequirementUiHelper.RequestForSelectedLanguage();
            }
        }

        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);

        if (ImGui.Button(Resources.DownloadPluginAssetsButtonText))
        {
            AssetsManager.PluginAssetsChecker(Echoglossian.SelectedLanguage);
            config.PluginAssetsDownloaded = AssetsManager.PluginAssetsDownloaded;
            changed = true;
        }

        ImGui.PopStyleColor(3);
        ImGui.EndGroup();

        ImGui.Spacing();

        ImGui.BeginGroup();
        ImGui.TextWrapped(Resources.ResetSettingsMessageText);

        ResetConfigButtonHelper.Draw(
            config,
            () => Echoglossian.SaveConfig(config));

        ImGui.EndGroup();

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }
}
