// <copyright file="PluginUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Diagnostics;
using System.Numerics;
using Echoglossian.PluginUI;
using Echoglossian.Properties;
using Echoglossian.Translators;
using ImGuiNET;
using Dalamud.Interface.Utility.Raii;

namespace Echoglossian;

public partial class Echoglossian
{
  public bool SaveConfigValue = false;
  private List<string> languageList;

  private readonly List<string> enginesList = new()
    {
        "Google", "DeepL", "ChatGPT", "YandexCloud", "GTranslate",
        "DeepSeek", "OpenLlama", "LibreTranslate", "Microsoft",
        "Amazon", "Gemini", "YandexPublic"
    };

  private void EchoglossianConfigUi()
  {
    this.languageList = new List<string>();

    foreach (var l in this.languagesDictionary)
    {
      this.languageList.Add(l.Value.LanguageName);
    }

    ImGui.SetNextWindowSizeConstraints(new Vector2(900, 700), new Vector2(1920, 1080));
    ImGui.Begin($"{Resources.ConfigWindowTitle} - Plugin Version: {this.configuration.PluginVersion}", ref this.config);

    // Header
    ImGui.BeginGroup();
    UINewFontHandler.GeneralFontHandle.Push();

    var langToRemoveDiacritics = this.configuration.Lang is 24 or 25 or 44 or 60 or 61 or 80 or 83 or 87 or 91 or 104 or 105 or 109 or 110;

    if (ImGui.Combo(Resources.LanguageSelectLabelText, ref languageInt, this.languageList.ToArray(), this.languageList.Count))
    {
      this.configuration.Lang = languageInt;
      SpecialFontFileName = langDict[this.configuration.Lang].FontName;
      SelectedLanguage = this.languagesDictionary[this.configuration.Lang];

      var languageNotSupported = this.configuration.Lang is 2 or 3 or 5 or 6 or 11 or 13 or 40 or 42 or 57 or 78 or 82 or 106 or 108 or 111 or 112 or 116;
      var languageOnlySupportedThruOverlay = this.configuration.Lang is 4 or 8 or 9 or 10 or 12 or 14 or 15 or 16 or 18 or 19 or 21 or 22 or 29 or 35 or 37 or 38 or 41 or 43 or 45 or 46 or 51 or 52 or 53 or 55 or 56 or 58 or 64 or 67 or 69 or 70 or 71 or 72 or 76 or 77 or 85 or 86 or 89 or 90 or 92 or 99 or 100 or 101 or 102 or 103 or 107;

      if (languageNotSupported)
      {
        this.configuration.UnsupportedLanguage = true;
      }
      else
      {
        this.configuration.UnsupportedLanguage = false;
        this.configuration.OverlayOnlyLanguage = languageOnlySupportedThruOverlay;
      }

      if (!langDict[languageInt].SupportedEngines.Contains(this.configuration.ChosenTransEngine))
      {
        this.configuration.ChosenTransEngine = 0;
        this.translationService = new TranslationService(this.configuration, PluginLog, sanitizer);
      }

      this.SaveConfigValue = true;
      PluginLog.Debug("Language selected: " + langDict[this.configuration.Lang].LanguageName);
      PluginLog.Debug("Language font: " + langDict[this.configuration.Lang].FontName);

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
      this.SaveConfigValue |= AssignIfChanged(ref this.configuration.Translate, false);
    }

    if (this.configuration.OverlayOnlyLanguage)
    {
      ImGui.Text(Resources.LanguageOnlySupportedUsingOverlay);
    }

    ImGui.EndGroup();
    ImGui.Spacing();

    if (!this.configuration.UnsupportedLanguage)
    {
      this.SaveConfigValue |= ImGui.Checkbox(Resources.EnableTranslation, ref this.configuration.Translate);
    }

    if (this.configuration.Translate)
    {
      ImGui.SameLine();
      ImGui.TextColored(new Vector4(0, 255, 0, 255), Resources.TranslationEnabled);
    }
    else
    {
      ImGui.SameLine();
      ImGui.TextColored(new Vector4(255, 255, 0, 255), Resources.TranslationDisabled);
    }

    if (this.configuration.Translate)
    {
      ImGui.BeginGroup();
      ImGui.Text(Resources.WhatToTranslateText);
      ImGui.EndGroup();
    }

    ImGui.Spacing();
    ImGui.BeginGroup();

    if (ImGui.BeginTabBar("TabBar", ImGuiTabBarFlags.NoCloseWithMiddleMouseButton))
    {
      if (ImGui.BeginTabItem(Resources.ConfigTab0Name))
      {
        this.SaveConfigValue |= Tabs.GeneralTab.Draw(this.configuration);
        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab1Name))
      {
        this.SaveConfigValue |= Tabs.OverlayTab.Draw(this.configuration);
        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab4Name))
      {
        this.SaveConfigValue |= Tabs.JournalTab.Draw(this.configuration, langToRemoveDiacritics);
        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab6Name))
      {
        this.SaveConfigValue |= Tabs.OtherSettingsTab.Draw(this.configuration);
        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab7Name))
      {
        this.DrawTranslationEnginesTab();
        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab8Name))
      {
        this.SaveConfigValue |= Tabs.TroubleshootingTab.Draw(this.configuration);
        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab9Name))
      {
        this.SaveConfigValue |= Tabs.MiscTab.Draw(this.configuration);
        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTabAbout))
      {
        this.SaveConfigValue |= Tabs.AboutTab.Draw(this.configuration, this.logo.ImGuiHandle);
        ImGui.EndTabItem();
      }

      ImGui.EndTabBar();
    }

    ImGui.EndGroup();

    PluginConfigWindowFooter.DrawFooter(
        ref this.config,
        ref this.SaveConfigValue,
        () => Echoglossian.SaveConfig(this.configuration),
        this.pixImage.ImGuiHandle
    );

    ImGui.End();

    if (this.SaveConfigValue)
    {
      Echoglossian.SaveConfig(this.configuration);
    }
  }

  private bool DisableAllToastTranslations()
  {
    this.configuration.TranslateAreaToast = false;
    this.configuration.TranslateClassChangeToast = false;
    this.configuration.TranslateErrorToast = false;
    this.configuration.TranslateQuestToast = false;
    this.configuration.TranslateWideTextToast = false;
    Echoglossian.SaveConfig(this.configuration);
    return true;
  }
}
