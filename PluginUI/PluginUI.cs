// <copyright file="PluginUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.PluginUI.Tabs;

namespace Echoglossian;

public partial class Echoglossian
{
  public static bool LangToRemoveDiacritics;

  private readonly List<string> enginesList = new()
    {
        "Google",
        "DeepL",
        "ChatGPT",
        "YandexCloud",
        "GTranslate",
        "DeepSeek",
        "Ollama",
        "LibreTranslate",
        "Microsoft",
        "Amazon",
        "Gemini",
        "YandexPublic",
        "OpenRouter",
        "LmStudio",
        "Claude",
    };

  private List<string> languageList;
  public bool SaveConfigValue;
  private bool showResetPopup = false;

  /// <summary>
  ///     Draws the Echoglossian configuration UI.
  /// </summary>
  private void EchoglossianConfigUi()
  {
    LanguageDropdownHelper.Initialize(this.languagesDictionary);

    ImGui.SetNextWindowSizeConstraints(
        new Vector2(900, 900),
        new Vector2(1920, 1080));
    ImGui.Begin(
        $"{Resources.ConfigWindowTitle} - Plugin Version: {this.configuration.PluginVersion}",
        ref this.config);

    // Header
    ImGui.BeginGroup();
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

      if (!LangDict[LanguageInt].SupportedEngines
              .Contains(this.configuration.ChosenTransEngine))
      {
        this.configuration.ChosenTransEngine = 0;
        this.RebuildTranslationServiceSafely();
      }

      this.SaveConfigValue = true;
      PluginRuntimeLog.Debug(
          "Language selected: " +
          LangDict[this.configuration.Lang].LanguageName);
      PluginRuntimeLog.Debug(
          "Language font: " + LangDict[this.configuration.Lang].FontName);

      MountFontPaths();
      PluginInterface.UiBuilder.FontAtlas.BuildFontsAsync();
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
      this.SaveConfigValue |= AssignIfChanged(
          ref this.configuration.Translate,
          false);
    }

    if (this.configuration.OverlayOnlyLanguage)
    {
      ImGui.Text(Resources.LanguageOnlySupportedUsingOverlay);
    }

    ImGui.EndGroup();
    ImGui.Spacing();

    if (!this.configuration.UnsupportedLanguage)
    {
      this.SaveConfigValue |= ImGui.Checkbox(
          Resources.EnableTranslation,
          ref this.configuration.Translate);
    }

    if (this.configuration.Translate)
    {
      ImGui.SameLine();
      ImGui.TextColored(
          new Vector4(0, 255, 0, 255),
          Resources.TranslationEnabled);
    }
    else
    {
      ImGui.SameLine();
      ImGui.TextColored(
          new Vector4(255, 255, 0, 255),
          Resources.TranslationDisabled);
    }

    if (this.configuration.Translate)
    {
      ImGui.BeginGroup();
      ImGui.Text(Resources.WhatToTranslateText);
      ImGui.EndGroup();
    }

    ImGui.Spacing();
    ImGui.BeginGroup();

    if (ImGui.BeginTabBar(
            "TabBar",
            ImGuiTabBarFlags.NoCloseWithMiddleMouseButton))
    {
      if (ImGui.BeginTabItem(Resources.ConfigTabGeneralName))
      {
        this.SaveConfigValue |= GeneralTab.Draw(this.configuration);
        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab0Name))
      {
        this.SaveConfigValue |= OverlayTab.Draw(this.configuration);
        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab7Name))
      {
        this.SaveConfigValue |= TranslationEnginesTab.Draw(
            this.configuration,
            LanguageInt,
            LanguageDropdownHelper.GetDisplayNames().ToList(),
            this.enginesList,
            LangDict,
            this.RebuildTranslationServiceSafely);
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
        () => SaveConfig(this.configuration),
        this.pixImage.Handle,
        this.cryptoImage.Handle);

    ImGui.End();

    if (this.SaveConfigValue)
    {
      SaveConfig(this.configuration);
    }
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


