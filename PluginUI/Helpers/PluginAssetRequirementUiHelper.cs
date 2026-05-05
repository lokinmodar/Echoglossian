// <copyright file="PluginAssetRequirementUiHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Helpers;

/// <summary>
/// Renders the language-asset requirement UX for languages that depend on
/// downloaded font assets.
/// </summary>
public static class PluginAssetRequirementUiHelper
{
  private const string MissingAssetsPopupId =
      "MissingLanguageAssetsPopup##Echoglossian";

  private static bool openMissingAssetsPopup;
  private static bool showMissingAssetsPopup;

  /// <summary>
  /// Requests that the missing-assets popup be shown for the currently
  /// selected language when it depends on unavailable downloaded assets.
  /// </summary>
  public static void RequestForSelectedLanguage()
  {
    if (!AssetsManager.HasMissingRequiredAssets(Echoglossian.SelectedLanguage))
    {
      return;
    }

    openMissingAssetsPopup = true;
    showMissingAssetsPopup = true;
  }

  /// <summary>
  /// Draws inline warning UI and a management button when the selected
  /// language requires missing downloaded assets.
  /// </summary>
  /// <param name="config">The plugin configuration to update.</param>
  /// <returns><c>true</c> when the UI changed configuration state.</returns>
  public static bool DrawInlineWarning(Config config)
  {
    if (!AssetsManager.HasMissingRequiredAssets(Echoglossian.SelectedLanguage))
    {
      return false;
    }

    ImGui.TextWrapped(Resources.TranslationRequiresDownloadedAssetsText);
    if (ImGui.Button(Resources.ManageLanguageAssetsButtonText))
    {
      RequestForSelectedLanguage();
    }

    config.PluginAssetsDownloaded = false;
    return false;
  }

  /// <summary>
  /// Draws the modal that guides the user through automatic and manual
  /// resolution of required downloaded font assets.
  /// </summary>
  /// <param name="config">The plugin configuration to update.</param>
  /// <returns><c>true</c> when the configuration changed.</returns>
  public static bool DrawMissingAssetsPopup(Config config)
  {
    var changed = false;

    if (!showMissingAssetsPopup && !openMissingAssetsPopup)
    {
      return false;
    }

    AssetsManager.RefreshPluginAssetsState(Echoglossian.SelectedLanguage);
    config.PluginAssetsDownloaded = AssetsManager.PluginAssetsDownloaded;

    if (!AssetsManager.HasMissingRequiredAssets(Echoglossian.SelectedLanguage))
    {
      if (AssetsManager.PluginAssetsDownloaded)
      {
        Echoglossian.MountFontPaths();
        Echoglossian.PluginInterface.UiBuilder.FontAtlas.BuildFontsAsync();
      }

      showMissingAssetsPopup = false;
      openMissingAssetsPopup = false;
      return true;
    }

    if (openMissingAssetsPopup)
    {
      ImGui.OpenPopup(MissingAssetsPopupId);
      openMissingAssetsPopup = false;
    }

    if (!ImGui.BeginPopupModal(
            MissingAssetsPopupId,
            ImGuiWindowFlags.AlwaysAutoResize))
    {
      return changed;
    }

    var requiredAssets =
        AssetsManager.GetRequiredAssetFiles(Echoglossian.SelectedLanguage);

    ImGui.TextWrapped(
        string.Format(
            CultureInfo.CurrentCulture,
            Resources.LanguageAssetsPopupBodyFormat,
            Echoglossian.SelectedLanguage.LanguageName));
    ImGui.TextWrapped(Resources.LanguageAssetsPopupAutoDownloadHint);
    ImGui.Separator();

    ImGui.TextUnformatted(Resources.LanguageAssetsPopupFolderLabel);
    ImGui.TextWrapped(AssetsManager.AssetsPath);
    ImGui.Separator();

    ImGui.TextUnformatted(Resources.LanguageAssetsPopupFilesLabel);
    foreach (var assetFile in requiredAssets)
    {
      ImGui.BulletText(assetFile);
    }

    ImGui.Spacing();
    if (ImGui.Button(Resources.DownloadPluginAssetsButtonText))
    {
      AssetsManager.PluginAssetsChecker(Echoglossian.SelectedLanguage);
      config.PluginAssetsDownloaded = AssetsManager.PluginAssetsDownloaded;
      changed = true;
    }

    ImGui.SameLine();
    if (ImGui.Button(Resources.RecheckAssetsButtonText))
    {
      AssetsManager.RefreshPluginAssetsState(Echoglossian.SelectedLanguage);
      config.PluginAssetsDownloaded = AssetsManager.PluginAssetsDownloaded;
      changed = true;
    }

    ImGui.SameLine();
    if (ImGui.Button(Resources.OpenAssetsFolderButtonText))
    {
      AssetsManager.OpenAssetsDirectory();
    }

    if (ImGui.Button(Resources.OpenManualDownloadLinksButtonText))
    {
      AssetsManager.OpenRequiredAssetDownloadLinks(
          Echoglossian.SelectedLanguage);
    }

    ImGui.SameLine();
    if (ImGui.Button(Resources.CloseButtonLabel))
    {
      showMissingAssetsPopup = false;
      ImGui.CloseCurrentPopup();
    }

    ImGui.EndPopup();
    return changed;
  }
}
