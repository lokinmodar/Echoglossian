// <copyright file="PluginUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.PluginUI.Tabs;

namespace Echoglossian;

public partial class Echoglossian
{
  public static bool LangToRemoveDiacritics;

  public bool SaveConfigValue;

  /// <summary>
  ///     Draws the Echoglossian configuration UI.
  /// </summary>
  private void EchoglossianConfigUi()
  {
    this.SaveConfigValue = false;
    LanguageDropdownHelper.Initialize(this.languagesDictionary);

    ImGui.SetNextWindowSizeConstraints(
        new Vector2(900, 900),
        new Vector2(1920, 1080));
    ImGui.Begin(
        $"{Resources.ConfigWindowTitle} - Plugin Version: {this.configuration.PluginVersion}",
        ref this.config);

    this.DrawTranslationStatusHeader();
    ImGui.Spacing();
    ImGui.BeginGroup();

    if (ImGui.BeginTabBar(
            "TabBar",
            ImGuiTabBarFlags.NoCloseWithMiddleMouseButton))
    {
      if (ImGui.BeginTabItem(Resources.ConfigTab7Name))
      {
        this.SaveConfigValue |= this.DrawTranslationSetupTab();
        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab0Name))
      {
        this.SaveConfigValue |= OverlayTab.Draw(this.configuration);
        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab8Name))
      {
        this.SaveConfigValue |=
            TroubleshootingTab.Draw(this.configuration);
        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTabAbout))
      {
        this.SaveConfigValue |= AboutTab.Draw(
            this.configuration,
            this.logo.Handle);
        ImGui.EndTabItem();
      }

      ImGui.EndTabBar();
    }

    ImGui.EndGroup();

    PluginConfigWindowFooter.DrawFooter(
        ref this.config,
        ref this.SaveConfigValue,
        this.pixImage.Handle,
        this.cryptoImage.Handle);

    this.SaveConfigValue |=
        PluginAssetRequirementUiHelper.DrawMissingAssetsPopup(
            this.configuration);

    ImGui.End();

    if (this.SaveConfigValue)
    {
      SaveConfig(this.configuration);
      this.SaveConfigValue = false;
    }
  }

  /// <summary>
  /// Draws the compact translation status line that stays outside the tab
  /// content.
  /// </summary>
  private void DrawTranslationStatusHeader()
  {
    var blockReason = TranslationActivationGuard.GetBlockReason(
        this.configuration,
        SelectedLanguage);
    var translationBlockedByMissingAssets =
        blockReason == TranslationActivationGuard.BlockReason
            .MissingRequiredAssets;
    var translationBlockedByEngineConfiguration =
        blockReason == TranslationActivationGuard.BlockReason
            .EngineConfigurationIncomplete;

    if (translationBlockedByMissingAssets)
    {
      ImGui.TextColored(
          new Vector4(255, 165, 0, 255),
          Resources.TranslationBlockedByMissingAssetsStatusText);
      return;
    }

    if (translationBlockedByEngineConfiguration)
    {
      ImGui.TextColored(
          new Vector4(255, 165, 0, 255),
          Resources.TranslationBlockedByEngineConfigurationText);
      return;
    }

    if (this.configuration.Translate)
    {
      ImGui.TextColored(
          new Vector4(0, 255, 0, 255),
          Resources.TranslationEnabled);
      return;
    }

    ImGui.TextColored(
        new Vector4(255, 255, 0, 255),
        Resources.TranslationDisabled);
  }

  /// <summary>
  /// Draws the first configuration tab that combines language selection, engine
  /// configuration, activation, and the legacy general settings.
  /// </summary>
  /// <returns><c>true</c> when the configuration changed.</returns>
  private bool DrawTranslationSetupTab()
  {
    var changed = false;

    using var scrollingChild = ImRaii.Child(
        "TranslationSetupSettings",
        new Vector2(-1, -100),
        false,
        ImGuiWindowFlags.NoBackground);

    if (!scrollingChild)
    {
      return false;
    }

    changed |= this.DrawTranslationLanguageSelectionSection();
    ImGui.Separator();

    changed |= TranslationEnginesTab.Draw(
        this.configuration,
        LanguageInt,
        LangDict,
        this.RebuildTranslationServiceSafely);

    ImGui.Separator();
    changed |= this.DrawTranslationActivationSection();

    ImGui.Separator();
    ImGui.Text(Resources.ConfigTabGeneralName);
    ImGui.Spacing();
    changed |= GeneralTab.Draw(this.configuration);

    return changed;
  }

  /// <summary>
  /// Draws the language-selection section and applies the related runtime side
  /// effects when the target language changes.
  /// </summary>
  /// <returns><c>true</c> when the configuration changed.</returns>
  private bool DrawTranslationLanguageSelectionSection()
  {
    var changed = false;

    UINewFontHandler.GeneralFontHandle.Push();

    LangToRemoveDiacritics = this.configuration.Lang is 24 or 25 or 44 or 60
        or 61 or 80 or 83 or 87 or 91 or 104 or 105 or 109 or 110;

    if (LanguageDropdownHelper.DrawLanguageDropdown(
            ref this.configuration.Lang,
            Resources.LanguageSelectLabelText))
    {
      LanguageInt = this.configuration.Lang;
      SpecialFontFileName = LangDict[this.configuration.Lang].FontName;
      SelectedLanguage =
          this.languagesDictionary[this.configuration.Lang];

      var languageNotSupported = this.configuration.Lang is 2 or 3 or 5
          or 6 or 11 or 13 or 40 or 42 or 57 or 78 or 82 or 106 or 108
          or 111 or 112 or 116;
      var languageOnlySupportedThruOverlay = this.configuration.Lang is 4
          or 8 or 9 or 10 or 12 or 14 or 15 or 16 or 18 or 19 or 21 or 22
          or 29 or 35 or 37 or 38 or 41 or 43 or 45 or 46 or 51 or 52
          or 53 or 55 or 56 or 58 or 64 or 67 or 69 or 70 or 71 or 72
          or 76 or 77 or 85 or 86 or 89 or 90 or 92 or 99 or 100 or 101
          or 102 or 103 or 107;

      this.configuration.UnsupportedLanguage = languageNotSupported;
      this.configuration.OverlayOnlyLanguage = !languageNotSupported &&
          languageOnlySupportedThruOverlay;

      if (TranslationEngineSelectionMigrationHelper.NormalizeAndSyncSelection(
              this.configuration,
              this.configuration.Version,
              LangDict[LanguageInt].SupportedEngines))
      {
        this.RebuildTranslationServiceSafely();
        changed = true;
      }

      changed = true;
      PluginRuntimeLog.Debug(
          "Language selected: " +
          LangDict[this.configuration.Lang].LanguageName);
      PluginRuntimeLog.Debug(
          "Language font: " + LangDict[this.configuration.Lang].FontName);

      AssetsManager.RefreshPluginAssetsState(SelectedLanguage);
      this.configuration.PluginAssetsDownloaded =
          AssetsManager.PluginAssetsDownloaded;
      MountFontPaths();
      if (!this.configuration.PluginAssetsDownloaded &&
          AssetsManager.RequiresDownloadedAssets(SelectedLanguage))
      {
        AssetsManager.PluginAssetsChecker(SelectedLanguage);
        PluginAssetRequirementUiHelper.RequestForSelectedLanguage();
      }
      else
      {
        PluginInterface.UiBuilder.FontAtlas.BuildFontsAsync();
      }
    }

    UINewFontHandler.GeneralFontHandle.Pop();

    ImGui.SameLine();
    ImGui.Text(Resources.HoverTooltipIndicator);
    if (ImGui.IsItemHovered())
    {
      ImGui.SetTooltip(Resources.LanguageSelectionTooltip);
    }

    if (this.configuration.UnsupportedLanguage)
    {
      ImGui.Text(Resources.LanguageNotSupportedText);
    }

    if (this.configuration.OverlayOnlyLanguage)
    {
      ImGui.Text(Resources.LanguageOnlySupportedUsingOverlay);
    }

    return changed;
  }

  /// <summary>
  /// Draws the translation activation section and blocks activation while the
  /// selected language or engine is not ready.
  /// </summary>
  /// <returns><c>true</c> when the configuration changed.</returns>
  private bool DrawTranslationActivationSection()
  {
    var changed = false;

    var blockReason = TranslationActivationGuard.GetBlockReason(
        this.configuration,
        SelectedLanguage);
    var translationBlockedByMissingAssets =
        blockReason == TranslationActivationGuard.BlockReason
            .MissingRequiredAssets;
    var translationBlockedByEngineConfiguration =
        blockReason == TranslationActivationGuard.BlockReason
            .EngineConfigurationIncomplete;
    var translationShouldBeBlocked =
        blockReason != TranslationActivationGuard.BlockReason.None;

    if (translationShouldBeBlocked)
    {
      changed |= AssignIfChanged(ref this.configuration.Translate, false);
    }

    if (!this.configuration.UnsupportedLanguage)
    {
      if (translationBlockedByMissingAssets ||
          translationBlockedByEngineConfiguration)
      {
        ImGui.BeginDisabled();
      }

      if (ImGui.Checkbox(
              Resources.EnableTranslation,
              ref this.configuration.Translate))
      {
        changed = true;
      }

      if (translationBlockedByMissingAssets ||
          translationBlockedByEngineConfiguration)
      {
        ImGui.EndDisabled();
      }

      if ((translationBlockedByMissingAssets ||
              translationBlockedByEngineConfiguration) &&
          ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
      {
        ImGui.SetTooltip(
            translationBlockedByMissingAssets
                ? Resources.TranslationRequiresDownloadedAssetsText
                : Resources.TranslationBlockedByEngineConfigurationText);
      }
    }

    if (translationBlockedByMissingAssets)
    {
      ImGui.SameLine();
      ImGui.TextColored(
          new Vector4(255, 165, 0, 255),
          Resources.TranslationBlockedByMissingAssetsStatusText);
      changed |= PluginAssetRequirementUiHelper.DrawInlineWarning(
          this.configuration);
    }
    else if (translationBlockedByEngineConfiguration)
    {
      ImGui.TextWrapped(
          Resources.TranslationBlockedByEngineConfigurationText);
    }

    return changed;
  }

  private bool DisableAllToastTranslations()
  {
    this.configuration.TranslateAreaToast = false;
    this.configuration.TranslateClassChangeToast = false;
    this.configuration.TranslateErrorToast = false;
    this.configuration.TranslateQuestToast = false;
    this.configuration.TranslateWideTextToast = false;
    SaveConfig(this.configuration);
    return true;
  }
}


