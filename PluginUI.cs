// <copyright file="PluginUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;

using Dalamud.Logging;
using Echoglossian.Properties;
using ImGuiNET;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public string[] FontSizes = Array.ConvertAll(Enumerable.Range(4, 72).ToArray(), x => x.ToString());

    internal void ReloadFont()
    {
      this.pluginInterface.UiBuilder.RebuildFonts();
#if DEBUG
      PluginLog.LogVerbose("Font atlas rebuilt!");
#endif
    }

    private void EchoglossianConfigUi()
    {
      this.configuration.ConfigurationDirectory =
        $"{this.pluginInterface.ConfigDirectory}{Path.DirectorySeparatorChar}";
      ImGui.SetNextWindowSizeConstraints(new Vector2(600, 500), new Vector2(1920, 1080));
      ImGui.Begin(Resources.ConfigWindowTitle, ref this.config);
      if (ImGui.BeginTabBar("Tabs", ImGuiTabBarFlags.None))
      {
        if (ImGui.BeginTabItem(Resources.ConfigTab1Name))
        {
          /*ImGui.Text(Resources.PluginInterfaceLanguage);
          if (ImGui.Combo(Resources.PluginInterfaceLanguageSelectorText, ref this.configuration.PluginCultureInt, this.languages, this.languages.Length))
          {
            this.SaveConfig();
          }*/

          ImGui.Text(Resources.WhatToTranslateText);
          if (ImGui.Checkbox(Resources.TranslateTalkToggleLabel, ref this.configuration.TranslateTalk))
          {
            this.SaveConfig();
          }

          if (ImGui.Checkbox(Resources.TransLateBattletalkToggle, ref this.configuration.TranslateBattleTalk))
          {
            this.SaveConfig();
          }

          if (ImGui.Checkbox(Resources.TranslateNpcNamesToggle, ref this.configuration.TranslateNPCNames))
          {
            this.SaveConfig();
          }

          if (ImGui.Checkbox(Resources.TranslateToastToggleText, ref this.configuration.TranslateToast))
          {
            if (!this.configuration.TranslateToast)
            {
              this.configuration.TranslateAreaToast = false;
              this.configuration.TranslateClassChangeToast = false;
              this.configuration.TranslateErrorToast = false;
              this.configuration.TranslateQuestToast = false;
              this.configuration.TranslateWideTextToast = false;
            }

            this.SaveConfig();
          }

          ImGui.Spacing();
          ImGui.Separator();
          if (ImGui.Combo(Resources.LanguageSelectLabelText, ref languageInt, this.languages, this.languages.Length))
          {
            this.SaveConfig();
          }

          ImGui.SameLine();
          ImGui.Text(Resources.HoverTooltipIndicator);
          if (ImGui.IsItemHovered())
          {
            ImGui.SetTooltip(Resources.LanguageSelectionTooltip);
          }

          ImGui.Spacing();
          ImGui.Separator();

          if (this.configuration.TranslateTalk || this.configuration.TranslateToast)
          {
            ImGui.Checkbox(Resources.OverlayToggleLabel, ref this.configuration.UseImGui);
            if (this.configuration.UseImGui)
            {
              /*ImGui.Separator();
              if (ImGui.Combo(Resources.OverlayFontSizeLabel, ref this.configuration.FontSize, this.FontSizes, this.FontSizes.Length))
              {
                this.SaveConfig();
                //this.LoadFont();
                if (this.FontLoaded)
                {
                  this.pluginInterface.UiBuilder.RebuildFonts();
                }
              }*/

              ImGui.Separator();
              if (ImGui.SliderFloat(Resources.OverlayFontScaleLabel, ref this.configuration.FontScale, -3f, 3f, "%.2f"))
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

              /*ImGui.SameLine();
              if (ImGui.SmallButton("Reload Font"))
              {
                this.ReloadFont();
              }*/

              ImGui.Separator();
              ImGui.SameLine();
              ImGui.Text(Resources.FontColorSelectLabel); 
              ImGui.SameLine();
              if (ImGui.ColorEdit3(Resources.OverlayColorSelectName, ref this.configuration.OverlayTextColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
              {
#if DEBUG
                PluginLog.Information($"Color selected before save: {this.configuration.OverlayTextColor}");
#endif
                this.SaveConfig();
#if DEBUG
                PluginLog.Information($"Color selected after save: {this.configuration.OverlayTextColor}");
#endif
              }

              ImGui.SameLine();
              ImGui.Text(Resources.HoverTooltipIndicator);
              if (ImGui.IsItemHovered())
              {
                ImGui.SetTooltip(Resources.OverlayFontColorOrientations);
              }

              ImGui.Separator();
              if (ImGui.DragFloat(Resources.OverlayWidthScrollLabel, ref this.configuration.ImGuiWindowWidthMult, 0.001f, 0.01f, 3f))
              {
                this.SaveConfig();
              }

              ImGui.SameLine();
              ImGui.Text(Resources.HoverTooltipIndicator);
              if (ImGui.IsItemHovered())
              {
                ImGui.SetTooltip(Resources.OverlayWidthMultiplierOrientations);
              }

              ImGui.Separator();
              ImGui.Spacing();
              if (ImGui.DragFloat2(Resources.OverlayPositionAdjustmentLabel, ref this.configuration.ImGuiWindowPosCorrection))
              {
                this.SaveConfig();
              }

              ImGui.SameLine();
              ImGui.Text(Resources.HoverTooltipIndicator);
              if (ImGui.IsItemHovered())
              {
                ImGui.SetTooltip(Resources.OverlayAdjustmentOrientations);
              }

              if (this.configuration.TranslateTalk)
              {
                if (ImGui.Checkbox(Resources.SwapTranslationTextToggle, ref this.configuration.SwapTextsUsingImGui))
                {
                  this.SaveConfig();
                }
              }
            }
          }

          ImGui.EndTabItem();
        }

        if (this.configuration.TranslateToast)
        {
          if (ImGui.BeginTabItem(Resources.ConfigTab2Name))
          {
            ImGui.Separator();
            ImGui.Text(Resources.WhichToastsToTranslateText);
            ImGui.Checkbox(Resources.TranslateErrorToastToggleText, ref this.configuration.TranslateErrorToast);
            ImGui.Checkbox(Resources.TranslateQuestToastToggleText, ref this.configuration.TranslateQuestToast);
            ImGui.Checkbox(Resources.TranslateAreaToastToggleText, ref this.configuration.TranslateAreaToast);
            ImGui.Checkbox(Resources.TranslateClassChangeToastToggleText, ref this.configuration.TranslateClassChangeToast);
            ImGui.Checkbox(Resources.TranslateScreenInfoToastToggleText, ref this.configuration.TranslateWideTextToast);
            ImGui.Separator();
            if (ImGui.Checkbox(Resources.DoNotUseImGuiForToastsToggle, ref this.configuration.DoNotUseImGuiForToasts))
            {
              this.SaveConfig();
            }

            ImGui.Separator();
            if (this.configuration.UseImGui && !this.configuration.DoNotUseImGuiForToasts)
            {
              ImGui.Separator();
              if (ImGui.DragFloat(Resources.ToastOverlayWidthScrollLabel, ref this.configuration.ImGuiToastWindowWidthMult, 0.001f, 0.01f, 3f))
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

            ImGui.EndTabItem();
          }
        }

        ImGui.EndTabBar();
      }

      var pos = new Vector2(ImGui.GetWindowContentRegionMin().X, ImGui.GetWindowContentRegionMax().Y - 30);
      ImGui.Separator();
      ImGui.SetCursorPos(pos);
      ImGui.BeginGroup();
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
        Process.Start("https://ko-fi.com/lokinmodar");
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
      /*}*/
    }
  }
}