// <copyright file="PluginUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

using Dalamud.Logging;
using Echoglossian.Properties;
using ImGuiNET;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    // public string[] FontSizes = Array.ConvertAll(Enumerable.Range(4, 72).ToArray(), x => x.ToString());
    private List<string> LanguageList;

    internal void ReloadFont()
    {
      this.pluginInterface.UiBuilder.RebuildFonts();
#if DEBUG
      PluginLog.LogVerbose("Font atlas rebuilt!");
#endif
    }

    private void EchoglossianConfigUi()
    {
      this.LanguageList = new List<string>();

      foreach (KeyValuePair<int, Language> l in this.LanguagesDictionary)
      {
        this.LanguageList.Add(l.Value.LanguageName);
      }

      bool languageNotSupported = this.configuration.Lang is 3 or 5 or 11 or 13 or 111 or 112;
      bool languageOnlySupportedThruOverlay =
        this.configuration.Lang is 2 or 4 or 6 or 8 or 9 or 10 or 12 or 14 or 15 or 16 or 18 or 19 or 21 or 22 or 24 or 25 or 29 or 35 or 37 or 38 or 40 or 41 or 42 or 43 or 45 or 46 or 51 or 52 or 53 or 55 or 56 or 57 or 58 or 60 or 61 or 64 or 67 or 69 or 70 or 71 or 72 or 76 or 77 or 78 or 80 or 82 or 83 or 85 or 86 or 89 or 90 or 91 or 92 or 99 or 100 or 101 or 102 or 103 or 104 or 105 or 106 or 107 or 108 or 109 or 110 or 116;
      ImGui.SetNextWindowSizeConstraints(new Vector2(600, 600), new Vector2(1920, 1080));

      ImGui.Begin(Resources.ConfigWindowTitle, ref this.config);
      if (ImGui.BeginTabBar("TabBar", ImGuiTabBarFlags.None))
      {
        if (ImGui.BeginTabItem(Resources.ConfigTab0Name))
        {
          if (ImGui.Checkbox(Resources.EnableTranslation, ref this.configuration.Translate))
          {
            if (!this.configuration.Translate)
            {
              bool disabledTranslations = this.DisableAllTranslations();
            }

            this.SaveConfig();
          }
          else
          {
            if (this.ConfigFontLoaded)
            {
#if DEBUG
              // PluginLog.LogVerbose("Pushing font");
#endif
              ImGui.PushFont(this.ConfigUiFont);
            }

            if (ImGui.Combo(Resources.LanguageSelectLabelText, ref languageInt, this.LanguageList.ToArray(), this.LanguageList.ToArray().Length))
            {
              this.configuration.Lang = languageInt;
              this.LoadFont();
              this.ReloadFont();
              this.SaveConfig();
            }

            if (this.ConfigFontLoaded)
            {
#if DEBUG
              // PluginLog.LogVerbose("Popping font!");
#endif
              ImGui.PopFont();
            }

            ImGui.SameLine();
            ImGui.Text(Resources.HoverTooltipIndicator);
            if (ImGui.IsItemHovered())
            {
              ImGui.SetTooltip(Resources.LanguageSelectionTooltip);
            }

            if (languageNotSupported)
            {
              ImGui.Text(Resources.LanguageNotSupportedText);
              bool translationsDisabled = this.DisableAllTranslations();

              if (translationsDisabled)
              {
                this.SaveConfig();
              }
            }
            else
            {
              if (languageOnlySupportedThruOverlay)
              {
                ImGui.Text(Resources.LanguageOnlySupportedUsingOverlay);
                this.configuration.UseImGui = true;
                this.configuration.UseImGuiForBattleTalk = true;
                this.configuration.DoNotUseImGuiForToasts = false;

                this.SaveConfig();
              }

              ImGui.Spacing();
              ImGui.Separator();
              ImGui.Text(Resources.WhatToTranslateText);
              if (ImGui.Checkbox(Resources.TranslateTalkToggleLabel, ref this.configuration.TranslateTalk))
              {
                this.SaveConfig();
              }

              if (ImGui.Checkbox(Resources.TransLateBattletalkToggle, ref this.configuration.TranslateBattleTalk))
              {
                this.SaveConfig();
              }

              if (ImGui.Checkbox(Resources.TranslateToastToggleText, ref this.configuration.TranslateToast))
              {
                if (!this.configuration.TranslateToast)
                {
                  bool toastTranslationsDisabled = this.DisableAllToastTranslations();
                }

                this.SaveConfig();
              }
            }
          }

          ImGui.EndTabItem();
          this.SaveConfig();
        }

        if (ImGui.BeginTabItem(Resources.ConfigTab1Name, ref this.configuration.TranslateTalk))
        {
          if (!languageOnlySupportedThruOverlay && !this.configuration.UseImGui)
          {
            ImGui.IsItemDeactivated();
            if (ImGui.Checkbox(
              Resources.OverlayToggleLabel,
              ref this.configuration.UseImGui))
            {
              this.SaveConfig();
            }
          }

          if (languageOnlySupportedThruOverlay || this.configuration.UseImGui)
          {
            ImGui.Text(Resources.ImguiAdjustmentsLabel);
            if (ImGui.Checkbox(Resources.TranslateNpcNamesToggle, ref this.configuration.TranslateNpcNames))
            {
              this.SaveConfig();
            }

            ImGui.Spacing();
            ImGui.Separator();

            if (ImGui.SliderFloat(
              Resources.OverlayFontScaleLabel,
              ref this.configuration.FontScale,
              -3f,
              3f,
              "%.2f"))
            {
              this.configuration.FontChangeTime = DateTime.Now.Ticks;
              this.SaveConfig();
            }

            ImGui.SameLine();
            ImGui.Text(Resources.HoverTooltipIndicator);
            if (ImGui.IsItemHovered())
            {
              ImGui.SetTooltip(Resources.OverlayFontSizeOrientations);
            }

            ImGui.Separator();
            ImGui.Spacing();
            ImGui.SameLine();
            ImGui.Text(Resources.FontColorSelectLabel);
            ImGui.SameLine();
            if (ImGui.ColorEdit3(
              Resources.OverlayColorSelectName,
              ref this.configuration.OverlayTextColor,
              ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
            {
              this.SaveConfig();
            }

            ImGui.SameLine();
            ImGui.Text(Resources.HoverTooltipIndicator);
            if (ImGui.IsItemHovered())
            {
              ImGui.SetTooltip(Resources.OverlayFontColorOrientations);
            }

            ImGui.Spacing();
            ImGui.Separator();
            if (ImGui.DragFloat(
              Resources.OverlayWidthScrollLabel,
              ref this.configuration.ImGuiTalkWindowWidthMult,
              0.001f, 0.01f, 3f))
            {
              this.SaveConfig();
            }

            ImGui.Separator();
            if (ImGui.DragFloat(
              Resources.OverlayHeightScrollLabel,
              ref this.configuration.ImGuiTalkWindowHeightMult,
              0.001f, 0.01f, 3f))
            {
              this.SaveConfig();
            }

            ImGui.Separator();
            ImGui.Spacing();
            if (ImGui.DragFloat2(
              Resources.OverlayPositionAdjustmentLabel,
              ref this.configuration.ImGuiWindowPosCorrection))
            {
              this.SaveConfig();
            }

            ImGui.SameLine();
            ImGui.Text(Resources.HoverTooltipIndicator);
            if (ImGui.IsItemHovered())
            {
              ImGui.SetTooltip(Resources.OverlayAdjustmentOrientations);
            }
          }

          ImGui.Spacing();
          ImGui.Separator();
          if (!languageOnlySupportedThruOverlay && this.configuration.UseImGui)
          {
            if (ImGui.Checkbox(Resources.SwapTranslationTextToggle, ref this.configuration.SwapTextsUsingImGui))
            {
              this.SaveConfig();
            }
          }

          ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(Resources.ConfigTab2Name, ref this.configuration.TranslateBattleTalk))
        {
          if (!languageOnlySupportedThruOverlay && !this.configuration.UseImGui)
          {
            ImGui.IsItemDeactivated();
            if (ImGui.Checkbox(
              Resources.OverlayToggleLabel,
              ref this.configuration.UseImGuiForBattleTalk))
            {
              this.SaveConfig();
            }
          }

          if (languageOnlySupportedThruOverlay || this.configuration.UseImGuiForBattleTalk)
          {
            ImGui.Text(Resources.ImguiAdjustmentsLabel);
            if (ImGui.Checkbox(Resources.TranslateNpcNamesToggle, ref this.configuration.TranslateNpcNames))
            {
              this.SaveConfig();
            }

            ImGui.Spacing();
            ImGui.Separator();

            if (ImGui.SliderFloat(
              Resources.OverlayFontScaleLabel,
              ref this.configuration.BattleTalkFontScale,
              -3f,
              3f,
              "%.2f"))
            {
              this.configuration.FontChangeTime = DateTime.Now.Ticks;
              this.SaveConfig();
            }

            ImGui.SameLine();
            ImGui.Text(Resources.HoverTooltipIndicator);
            if (ImGui.IsItemHovered())
            {
              ImGui.SetTooltip(Resources.OverlayFontSizeOrientations);
            }

            ImGui.Separator();
            ImGui.Spacing();
            ImGui.SameLine();
            ImGui.Text(Resources.FontColorSelectLabel);
            ImGui.SameLine();
            if (ImGui.ColorEdit3(
              Resources.OverlayColorSelectName,
              ref this.configuration.OverlayBattleTalkTextColor,
              ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
            {
              this.SaveConfig();
            }

            ImGui.SameLine();
            ImGui.Text(Resources.HoverTooltipIndicator);
            if (ImGui.IsItemHovered())
            {
              ImGui.SetTooltip(Resources.OverlayFontColorOrientations);
            }

            ImGui.Spacing();
            ImGui.Separator();
            if (ImGui.DragFloat(
              Resources.OverlayWidthScrollLabel,
              ref this.configuration.ImGuiBattleTalkWindowWidthMult,
              0.001f, 0.01f, 3f))
            {
              this.SaveConfig();
            }

            ImGui.Separator();
            if (ImGui.DragFloat(
              Resources.OverlayHeightScrollLabel,
              ref this.configuration.ImGuiBattleTalkWindowHeightMult,
              0.001f, 0.01f, 3f))
            {
              this.SaveConfig();
            }

            ImGui.Separator();
            ImGui.Spacing();
            if (ImGui.DragFloat2(
              Resources.OverlayPositionAdjustmentLabel,
              ref this.configuration.ImGuiBattleTalkWindowPosCorrection))
            {
              this.SaveConfig();
            }

            ImGui.SameLine();
            ImGui.Text(Resources.HoverTooltipIndicator);
            if (ImGui.IsItemHovered())
            {
              ImGui.SetTooltip(Resources.OverlayAdjustmentOrientations);
            }
          }

          ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(Resources.ConfigTab3Name, ref this.configuration.TranslateToast))
        {
          if (this.configuration.TranslateToast)
          {
            ImGui.Separator();
            ImGui.Text(Resources.WhichToastsToTranslateText);
            ImGui.Checkbox(Resources.TranslateErrorToastToggleText, ref this.configuration.TranslateErrorToast);
            ImGui.Checkbox(Resources.TranslateQuestToastToggleText, ref this.configuration.TranslateQuestToast);
            ImGui.Checkbox(Resources.TranslateAreaToastToggleText, ref this.configuration.TranslateAreaToast);
            ImGui.Checkbox(
              Resources.TranslateClassChangeToastToggleText,
              ref this.configuration.TranslateClassChangeToast);
            ImGui.Checkbox(
              Resources.TranslateScreenInfoToastToggleText,
              ref this.configuration.TranslateWideTextToast);
            ImGui.Separator();
            if (!languageOnlySupportedThruOverlay)
            {
              this.configuration.DoNotUseImGuiForToasts = true;
              if (ImGui.Checkbox(
                Resources.DoNotUseImGuiForToastsToggle,
                ref this.configuration.DoNotUseImGuiForToasts))
              {
                this.SaveConfig();
              }
            }
            else
            {
              this.configuration.DoNotUseImGuiForToasts = false;
              this.SaveConfig();
            }

            ImGui.Separator();
            if (this.configuration.UseImGui && !this.configuration.DoNotUseImGuiForToasts)
            {
              ImGui.Separator();
              if (ImGui.DragFloat(
                Resources.ToastOverlayWidthScrollLabel,
                ref this.configuration.ImGuiToastWindowWidthMult, 0.001f, 0.01f, 3f))
              {
                this.SaveConfig();
              }

              ImGui.SameLine();
              ImGui.Text(Resources.HoverTooltipIndicator);
              if (ImGui.IsItemHovered())
              {
                ImGui.SetTooltip(Resources.ToastOverlayWidthMultiplierOrientations);
              }
            }
          }

          ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(Resources.ConfigTab4Name, ref this.configuration.TranslateJournal))
        {
          ImGui.Text("This is the Cucumber tab!\nblah blah blah blah blah");
          ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(Resources.ConfigTab5Name, ref this.configuration.TranslateTooltips))
        {
          ImGui.Text("This is the Cucumber tab!\nblah blah blah blah blah");
          ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(Resources.ConfigTab6Name, ref this.configuration.TranslateToDoList))
        {
          ImGui.Text("This is the Onion tab!\nblah blah blah blah blah");
          ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(Resources.ConfigTab7Name))
        {
          var transEngine = this.configuration.ChosenTransEngine;

          ImGui.BeginGroup();
          if (transEngine == 0)
          {
            ImGui.Text(Resources.SettingsForGTransText);
            ImGui.Text(Resources.TranslationEngineSettingsNotRequired);
          }

          ImGui.EndGroup();
          ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(Resources.ConfigTabAbout))
        {
          if (ImGui.BeginTable("columns", 2))
          {
            ImGui.TableNextColumn();
            ImGui.BeginGroup();
            ImGui.TextColored(new Vector4(247, 247, 7, 1), Resources.DisclaimerTitle);
            ImGui.Spacing();
            ImGui.TextWrapped(Resources.DisclaimerText1);
            ImGui.TextWrapped(Resources.DisclaimerText2);
            ImGui.TextWrapped(Resources.ContribText);
            ImGui.EndGroup();

            ImGui.TableNextColumn();
            var posLogo = new Vector2(
              ImGui.GetWindowContentRegionMax().X - 300,
              ImGui.GetWindowContentRegionMin().Y + 50);
            ImGui.SetCursorPos(posLogo);
            ImGui.Image(this.logo.ImGuiHandle, new Vector2(300, 300));
            ImGui.EndTable();
          }

          ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
      }

      ImGui.Spacing();
      var pos = new Vector2(ImGui.GetWindowContentRegionMin().X, ImGui.GetWindowContentRegionMax().Y - 100);

      ImGui.SetCursorPos(pos);
      ImGui.Separator();
      ImGui.BeginGroup();
      ImGui.TextWrapped(Resources.NEListText);
      if (ImGui.Button(Resources.TodoUrl))
      {
        Process.Start(new ProcessStartInfo
        {
          FileName = "https://github.com/lokinmodar/Echoglossian/projects/1",
          UseShellExecute = true,
        });
        this.SaveConfig();
        this.config = false;
      }

      ImGui.Spacing();
      if (ImGui.Button(Resources.SaveCloseButtonLabel))
      {
        this.SaveConfig();
        this.config = false;
      }

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
        this.SaveConfig();
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
        ImGui.OpenPopup(Resources.PixQrWindowLabel);
        this.SaveConfig();
      }

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
    }

    private bool DisableAllToastTranslations()
    {
      this.configuration.TranslateAreaToast = false;
      this.configuration.TranslateClassChangeToast = false;
      this.configuration.TranslateErrorToast = false;
      this.configuration.TranslateQuestToast = false;
      this.configuration.TranslateWideTextToast = false;
      return true;
    }

    private bool DisableAllTranslations()
    {
      this.configuration.TranslateTalk = false;
      this.configuration.TranslateTalkSubtitle = false;
      this.configuration.TranslateBattleTalk = false;
      this.configuration.TranslateToast = false;
      this.configuration.TranslateNpcNames = false;
      this.configuration.TranslateErrorToast = false;
      this.configuration.TranslateQuestToast = false;
      this.configuration.TranslateAreaToast = false;
      this.configuration.TranslateClassChangeToast = false;
      this.configuration.TranslateWideTextToast = false;
      this.configuration.TranslateYesNoScreen = false;
      this.configuration.TranslateCutSceneSelectString = false;
      this.configuration.TranslateSelectString = false;
      this.configuration.TranslateSelectOk = false;
      this.configuration.TranslateToDoList = false;
      this.configuration.TranslateScenarioTree = false;

      return true;
    }
  }
}