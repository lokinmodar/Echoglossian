// <copyright file="PluginUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;

using Dalamud.Interface;
using Dalamud.Logging;
using Dalamud.Utility;
using Echoglossian.Properties;
using ImGuiNET;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public bool FontLoaded;
    public bool FontLoadFailed;
    public ImFontPtr UiFont;

    private bool talkDisplayTranslation;

    private string currentNameTranslation = string.Empty;
    private volatile int currentNameTranslationId;

    private string currentTalkTranslation = string.Empty;
    private volatile int currentTalkTranslationId;

    private Vector2 talkTextDimensions = Vector2.Zero;
    private Vector2 talkTextImguiSize = Vector2.Zero;
    private Vector2 talkTextPosition = Vector2.Zero;

    private bool battleTalkDisplayTranslation;

    private string currentSenderTranslation = string.Empty;
    private volatile int currentSenderTranslationId;

    private string currentBattleTalkTranslation = string.Empty;
    private volatile int currentBattleTalkTranslationId;

    private Vector2 battleTalkTextDimensions = Vector2.Zero;
    private Vector2 battleTalkTextImguiSize = Vector2.Zero;
    private Vector2 battleTalkTextPosition = Vector2.Zero;

    private bool addonDisplayTranslation;

    private Vector2 addonTranslationTextDimensions = Vector2.Zero;
    private Vector2 addonTranslationTextImguiSize = Vector2.Zero;
    private Vector2 addonTranslationTextPosition = Vector2.Zero;

    private bool toastDisplayTranslation;

    private string currentToastTranslation = string.Empty;
    private volatile int currentToastTranslationId;

    private Vector2 toastTranslationTextDimensions = Vector2.Zero;
    private Vector2 toastTranslationTextImguiSize = Vector2.Zero;
    private Vector2 toastTranslationTextPosition = Vector2.Zero;

    private string currentQuestToastTranslation = string.Empty;
    private volatile int currentQuestToastTranslationId;

    private bool questToastDisplayTranslation;

    private Vector2 questToastTranslationTextDimensions = Vector2.Zero;
    private Vector2 questToastTranslationTextImguiSize = Vector2.Zero;
    private Vector2 questToastTranslationTextPosition = Vector2.Zero;

    private string currentClassChangeToastTranslation = string.Empty;
    private volatile int currentClassChangeToastTranslationId;

    private bool classChangeToastDisplayTranslation;

    private Vector2 classChangeToastTranslationTextDimensions = Vector2.Zero;
    private Vector2 classChangeToastTranslationTextImguiSize = Vector2.Zero;
    private Vector2 classChangeToastTranslationTextPosition = Vector2.Zero;

    private string currentWideTextToastTranslation = string.Empty;
    private volatile int currentWideTextToastTranslationId;

    private bool wideTextToastDisplayTranslation;

    private Vector2 wideTextToastTranslationTextDimensions = Vector2.Zero;
    private Vector2 wideTextToastTranslationTextImguiSize = Vector2.Zero;
    private Vector2 wideTextToastTranslationTextPosition = Vector2.Zero;

    private string currentAreaToastTranslation = string.Empty;
    private volatile int currentAreaToastTranslationId;

    private bool areaToastDisplayTranslation;

    private Vector2 areaToastTranslationTextDimensions = Vector2.Zero;
    private Vector2 areaToastTranslationTextImguiSize = Vector2.Zero;
    private Vector2 areaToastTranslationTextPosition = Vector2.Zero;

    private string currentErrorToastTranslation = string.Empty;
    private volatile int currentErrorToastTranslationId;

    private bool errorToastDisplayTranslation;

    private Vector2 errorToastTranslationTextDimensions = Vector2.Zero;
    private Vector2 errorToastTranslationTextImguiSize = Vector2.Zero;
    private Vector2 errorToastTranslationTextPosition = Vector2.Zero;

    public string[] FontSizes = Array.ConvertAll(Enumerable.Range(4, 72).ToArray(), x => x.ToString());

    public string GetTranslatedNpcNameForWindow()
    {
      string name;

      if (this.nameTranslationSemaphore.Wait(0))
      {
        name = this.currentNameTranslation;

        this.nameTranslationSemaphore.Release();
      }
      else
      {
        name = Resources.WaitingForTranslation;
      }

      return name;
    }

    public string GetTranslatedSenderNameForWindow()
    {
      string name;

      if (this.senderTranslationSemaphore.Wait(0))
      {
        name = this.currentSenderTranslation;

        this.senderTranslationSemaphore.Release();
      }
      else
      {
        name = Resources.WaitingForTranslation;
      }

      return name;
    }

    private void LoadFont(/*string fontFileName,int imguiFontSize */)
    {
      // TODO: Get font by languageint
      var fontFile = $@"{Path.GetFullPath(Path.GetDirectoryName(this.AssemblyLocation) !)}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Regular.ttf";
      this.FontLoaded = false;
      if (File.Exists(fontFile))
      {
        try
        {
          this.UiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFile, this.configuration.FontSize /*imguiFontSize*/);
          this.FontLoaded = true;
        }
        catch (Exception ex)
        {
          PluginLog.Log($"Font failed to load. {fontFile}");
          PluginLog.Log(ex.ToString());
          this.FontLoadFailed = true;
        }
      }
      else
      {
        PluginLog.Log($"Font doesn't exist. {fontFile}");
        this.FontLoadFailed = true;
      }
    }

    private void DrawTranslatedBattleDialogueWindow()
    {
      if (this.configuration.UseImGui && this.configuration.TranslateBattleTalk && this.battleTalkDisplayTranslation)
      {
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(
          this.battleTalkTextPosition.X + (this.battleTalkTextDimensions.X / 2) - (this.battleTalkTextImguiSize.X / 2),
          this.battleTalkTextPosition.Y - this.battleTalkTextImguiSize.Y - 20) + this.configuration.ImGuiWindowPosCorrection);
        var size = Math.Min(
          this.battleTalkTextDimensions.X * this.configuration.ImGuiWindowWidthMult,
          ImGui.CalcTextSize(this.currentBattleTalkTranslation).X + (ImGui.GetStyle().WindowPadding.X * 2));
        ImGui.SetNextWindowSizeConstraints(new Vector2(size, 0), new Vector2(size, this.battleTalkTextDimensions.Y));

        // ImGui.PushFont(this.UiFont);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(this.configuration.OverlayTextColor, 255));
        if (this.configuration.TranslateNPCNames)
        {
          var name = this.GetTranslatedSenderNameForWindow();
          if (!name.IsNullOrEmpty())
          {
            ImGui.Begin(
              name,
              ImGuiWindowFlags.NoNav
              | ImGuiWindowFlags.NoCollapse
              | ImGuiWindowFlags.AlwaysAutoResize
              | ImGuiWindowFlags.NoFocusOnAppearing
              | ImGuiWindowFlags.NoMouseInputs
              | ImGuiWindowFlags.NoScrollbar);
          }
          else
          {
            ImGui.Begin(
              "BattleTalk translation",
              ImGuiWindowFlags.NoTitleBar
              | ImGuiWindowFlags.NoNav
              | ImGuiWindowFlags.AlwaysAutoResize
              | ImGuiWindowFlags.NoFocusOnAppearing
              | ImGuiWindowFlags.NoMouseInputs
              | ImGuiWindowFlags.NoScrollbar);
          }
        }
        else
        {
          ImGui.Begin(
            "BattleTalk translation",
            ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoNav
            | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoMouseInputs
            | ImGuiWindowFlags.NoScrollbar);
        }

        if (this.battleTalkTranslationSemaphore.Wait(0))
        {
          // ImGui.TextColored(new Vector4(this.configuration.OverlayTextColor, 255), this.currentTalkTranslation);
          ImGui.TextWrapped(this.currentBattleTalkTranslation);

          this.battleTalkTranslationSemaphore.Release();
        }
        else
        {
          ImGui.Text(Resources.WaitingForTranslation);
        }

        this.battleTalkTextImguiSize = ImGui.GetWindowSize();
        ImGui.PopStyleColor(1);

        // ImGui.PopFont();
        ImGui.End();
      }
    }

    private void DrawTranslatedDialogueWindow()
    {
      if (this.configuration.UseImGui && this.configuration.TranslateTalk && this.talkDisplayTranslation)
      {
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(
          this.talkTextPosition.X + (this.talkTextDimensions.X / 2) - (this.talkTextImguiSize.X / 2),
          this.talkTextPosition.Y - this.talkTextImguiSize.Y - 20) + this.configuration.ImGuiWindowPosCorrection);
        var size = Math.Min(
          this.talkTextDimensions.X * this.configuration.ImGuiWindowWidthMult,
          ImGui.CalcTextSize(this.currentTalkTranslation).X + (ImGui.GetStyle().WindowPadding.X * 2));
        ImGui.SetNextWindowSizeConstraints(new Vector2(size, 0), new Vector2(size, this.talkTextDimensions.Y));

        // ImGui.PushFont(this.UiFont);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(this.configuration.OverlayTextColor, 255));
        if (this.configuration.TranslateNPCNames)
        {
          var name = this.GetTranslatedNpcNameForWindow();
          if (!name.IsNullOrEmpty())
          {
            ImGui.Begin(
              name,
              ImGuiWindowFlags.NoNav
              | ImGuiWindowFlags.NoCollapse
              | ImGuiWindowFlags.AlwaysAutoResize
              | ImGuiWindowFlags.NoFocusOnAppearing
              | ImGuiWindowFlags.NoMouseInputs
              | ImGuiWindowFlags.NoScrollbar);
          }
          else
          {
            ImGui.Begin(
              "Talk translation",
              ImGuiWindowFlags.NoTitleBar
              | ImGuiWindowFlags.NoNav
              | ImGuiWindowFlags.AlwaysAutoResize
              | ImGuiWindowFlags.NoFocusOnAppearing
              | ImGuiWindowFlags.NoMouseInputs
              | ImGuiWindowFlags.NoScrollbar);
          }
        }
        else
        {
          ImGui.Begin(
            "Talk translation",
            ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoNav
            | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoMouseInputs
            | ImGuiWindowFlags.NoScrollbar);
        }

        if (this.talkTranslationSemaphore.Wait(0))
        {
          // ImGui.TextColored(new Vector4(this.configuration.OverlayTextColor, 255), this.currentTalkTranslation);
          ImGui.TextWrapped(this.currentTalkTranslation);

          this.talkTranslationSemaphore.Release();
        }
        else
        {
          ImGui.Text(Resources.WaitingForTranslation);
        }

        this.talkTextImguiSize = ImGui.GetWindowSize();
        ImGui.PopStyleColor(1);

        // ImGui.PopFont();
        ImGui.End();
      }
    }

    private void DrawTranslatedToastWindow()
    {
      if (this.configuration.UseImGui && this.configuration.TranslateToast && this.toastDisplayTranslation)
      {
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(
          this.toastTranslationTextPosition.X + (this.toastTranslationTextDimensions.X / 2) - (this.toastTranslationTextImguiSize.X / 2),
          this.toastTranslationTextPosition.Y - this.toastTranslationTextImguiSize.Y - 20) + this.configuration.ImGuiToastWindowPosCorrection);
        var size = Math.Min(
          this.toastTranslationTextDimensions.X * this.configuration.ImGuiToastWindowWidthMult,
          ImGui.CalcTextSize(this.currentToastTranslation).X + (ImGui.GetStyle().WindowPadding.X * 2));
        ImGui.SetNextWindowSizeConstraints(new Vector2(size, 0), new Vector2(size * 1.5f, this.toastTranslationTextDimensions.Y));
        ImGui.Begin(
          "Toast Translation",
          ImGuiWindowFlags.NoTitleBar
          | ImGuiWindowFlags.NoNav
          | ImGuiWindowFlags.AlwaysAutoResize
          | ImGuiWindowFlags.NoFocusOnAppearing
          | ImGuiWindowFlags.NoMouseInputs);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(this.configuration.OverlayTextColor, 255));
        if (this.toastTranslationSemaphore.Wait(0))
        {
          ImGui.Text(this.currentToastTranslation);
          this.toastTranslationSemaphore.Release();
        }
        else
        {
          ImGui.Text(Resources.WaitingForTranslation);
        }

        this.toastTranslationTextImguiSize = ImGui.GetWindowSize();
        ImGui.PopStyleColor(1);
        ImGui.End();
      }
    }

    private void EchoglossianConfigUi()
    {
      if (this.config)
      {
        ImGui.SetNextWindowSizeConstraints(new Vector2(500, 500), new Vector2(1920, 1080));
        ImGui.Begin(Resources.ConfigWindowTitle, ref this.config);
        if (ImGui.BeginTabBar("Tabs", ImGuiTabBarFlags.None))
        {
          if (ImGui.BeginTabItem(Resources.ConfigTab1Name))
          {
            ImGui.Text(Resources.PluginInterfaceLanguage);
            if (ImGui.Combo(Resources.PluginInterfaceLanguageSelectorText, ref this.configuration.PluginCultureInt, this.languages, this.languages.Length))
            {
              this.SaveConfig();
            }

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
                ImGui.Separator();
                if (ImGui.Combo(Resources.OverlayFontSizeLabel, ref this.configuration.FontSize, this.FontSizes, this.FontSizes.Length))
                {
                  this.SaveConfig();
                  this.LoadFont(/*this.configuration.FontSize*/);
                }

                ImGui.SameLine();
                ImGui.Text(Resources.HoverTooltipIndicator);
                if (ImGui.IsItemHovered())
                {
                  ImGui.SetTooltip(Resources.OverlayFontSizeOrientations);
                }

                ImGui.Separator();
                ImGui.SameLine();
                ImGui.Text(Resources.FontColorSelectLabel);
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
                if (ImGui.DragFloat(Resources.OverlayWidthScrollLabel, ref this.configuration.ImGuiWindowWidthMult, 0.001f, 0.1f, 2f))
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
                if (ImGui.DragFloat(Resources.ToastOverlayWidthScrollLabel, ref this.configuration.ImGuiToastWindowWidthMult, 0.001f, 0.1f, 2f))
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
      }
    }
  }
}