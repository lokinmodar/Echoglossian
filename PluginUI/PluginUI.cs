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

  public bool SaveConfigValue = false;
  private List<string> languageList;

  private List<string> enginesList = new()
  {
    // extract to Resources
    "Google",
    "DeepL",
    "ChatGPT",
    "YandexCloud",
    "GTranslate",
    "DeepSeek",
    "OpenLlama",
    "LibreTranslate",
    "Microsoft",
    "Amazon",
    "Gemini",
    "YandexPublic",
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

    ImGui.BeginGroup();

    UINewFontHandler.GeneralFontHandle.Push();

    var langToRemoveDiacritics = this.configuration.Lang is 24 or 25 or 44 or 60 or 61 or 80 or 83 or 87 or 91 or 104 or 105 or 109 or 110;

    if (ImGui.Combo(Resources.LanguageSelectLabelText, ref languageInt, this.languageList.ToArray(), this.languageList.ToArray().Length))
    {
      this.configuration.Lang = languageInt;
      SpecialFontFileName = langDict[this.configuration.Lang].FontName;
      SelectedLanguage = this.languagesDictionary[this.configuration.Lang];

      var languageNotSupported = this.configuration.Lang is 2 or 3 or 5 or 6 or 11 or 13 or 40 or 42 or 57 or 78 or 82 or 106 or 108 or 111 or 112 or 116;
      var languageOnlySupportedThruOverlay = this.configuration.Lang is 4 or 8 or 9 or 10 or 12 or 14 or 15 or 16 or 18 or 19 or 21 or 22 /*or 24 or 25*/ or 29 or 35 or 37 or 38 or 41 or 43 or 45 or 46 or 51 or 52 or 53 or 55 or 56 or 58 /*or 60 or 61*/ or 64 or 67 or 69 or 70 or 71 or 72 or 76 or 77 /*or 80 or 83 */ or 85 or 86 or 89 or 90 /*or 91*/ or 92 or 99 or 100 or 101 or 102 or 103 /*or 104 or 105*/ or 107/* or 109 or 110*/;

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
        // use Google Translate as default
        this.configuration.ChosenTransEngine = 0;
        this.translationService = new TranslationService(this.configuration, PluginLog, sanitizer);
      }

      this.SaveConfigValue = true;

      PluginLog.Debug("Language selected: " + langDict[this.configuration.Lang].LanguageName);

      PluginLog.Debug("Language font: " + langDict[this.configuration.Lang].FontName);

      MountFontPaths();
      PluginInterface.UiBuilder.FontAtlas.BuildFontsAsync();
    }

    Echoglossian.UINewFontHandler.GeneralFontHandle.Pop();

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

    ImGui.Spacing();

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
        this.SaveConfigValue |= Tabs.JournalTab.Draw(this.configuration);
        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab6Name))
      {
        this.SaveConfigValue |= Tabs.OtherSettingsTab.Draw(this.configuration);
        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab7Name))
      {
        this.SaveConfigValue |= Tabs.TranslationEnginesTab.Draw(this.configuration);
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
        this.SaveConfigValue |= Tabs.AboutTab.Draw(this.configuration);
        ImGui.EndTabItem();
      }

      ImGui.EndTabBar();
    }

    ImGui.EndGroup();

    // Window Footer Begin
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
      this.SaveConfig();
    }
  }

  private bool DisableAllToastTranslations()
  {
    this.configuration.TranslateAreaToast = false;
    this.configuration.TranslateClassChangeToast = false;
    this.configuration.TranslateErrorToast = false;
    this.configuration.TranslateQuestToast = false;
    this.configuration.TranslateWideTextToast = false;
    this.SaveConfig();
    return true;
  }
}