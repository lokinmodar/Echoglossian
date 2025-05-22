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



  private void EchoglossianConfigUi()
  {













    if (ImGui.BeginTabBar(
      "TabBar",
      ImGuiTabBarFlags.NoCloseWithMiddleMouseButton))
    {
      if (ImGui.BeginTabItem(Resources.ConfigTab1Name))
      {
        /* - talk - */
        if (this.configuration.Translate)
        {
          this.SaveConfigValue |= ImGui.Checkbox(
            Resources.TranslateTalkToggleLabel,
            ref this.configuration.TranslateTalk);

          if (this.configuration.TranslateTalk)
          {
            if (this.configuration.OverlayOnlyLanguage)
            {
              this.SaveConfigValue |=
                AssignIfChanged(ref this.configuration.UseImGuiForTalk, true);
              this.SaveConfigValue |=
                AssignIfChanged(
                  ref this.configuration.SwapTextsUsingImGui,
                  false);
            }
            else
            {
              this.SaveConfigValue |= ImGui.Checkbox(
                Resources.OverlayToggleLabel,
                ref this.configuration.UseImGuiForTalk);
            }

            this.SaveConfigValue |= ImGui.Checkbox(
              Resources.TranslateNpcNamesToggle,
              ref this.configuration.TranslateNpcNames);

            ImGui.Spacing();
            ImGui.Separator();

            if (this.configuration.UseImGuiForTalk)
            {
              ImGui.Text(Resources.ImguiAdjustmentsLabel);
              if (ImGui.SliderFloat(
                Resources.OverlayFontScaleLabel,
                ref this.configuration.FontScale, -3f, 3f, "%.2f"))
              {
                this.SaveConfigValue = true;
                this.configuration.FontChangeTime = DateTime.Now.Ticks;
              }

              ImGui.SameLine();
              ImGui.Text(Resources.HoverTooltipIndicator);
              if (ImGui.IsItemHovered())
              {
                ImGui.SetTooltip(Resources.OverlayFontSizeOrientations);
              }

              ImGui.Text(Resources.FontColorSelectLabel);
              ImGui.SameLine();
              this.SaveConfigValue |= ImGui.ColorEdit3(
                Resources.OverlayColorSelectName,
                ref this.configuration.OverlayTalkTextColor,
                ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);

              ImGui.SameLine();
              ImGui.Text(Resources.HoverTooltipIndicator);
              if (ImGui.IsItemHovered())
              {
                ImGui.SetTooltip(Resources.OverlayFontColorOrientations);
              }

              ImGui.Spacing();
              ImGui.Separator();
              this.SaveConfigValue |= ImGui.DragFloat(
                Resources.OverlayWidthScrollLabel,
                ref this.configuration.ImGuiTalkWindowWidthMult, 0.001f, 0.01f,
                3f);

              ImGui.Separator();
              this.SaveConfigValue |= ImGui.DragFloat(
                Resources.OverlayHeightScrollLabel,
                ref this.configuration.ImGuiTalkWindowHeightMult, 0.001f, 0.01f,
                3f);

              ImGui.Separator();
              ImGui.Spacing();
              this.SaveConfigValue |= ImGui.DragFloat2(
                Resources.OverlayPositionAdjustmentLabel,
                ref this.configuration.ImGuiWindowPosCorrection);

              ImGui.SameLine();
              ImGui.Text(Resources.HoverTooltipIndicator);
              if (ImGui.IsItemHovered())
              {
                ImGui.SetTooltip(Resources.OverlayAdjustmentOrientations);
              }
            }

            ImGui.Spacing();
            ImGui.Separator();
            if (!this.configuration.OverlayOnlyLanguage &&
                this.configuration.UseImGuiForTalk)
            {
              this.SaveConfigValue |= ImGui.Checkbox(
                Resources.SwapTranslationTextToggle,
                ref this.configuration.SwapTextsUsingImGui);

              if (this.configuration.SwapTextsUsingImGui && langToRemoveDiacritics)
              {
                this.SaveConfigValue |= ImGui.Checkbox(
                                   Resources.RemoveDiacriticsToggle,
                                   ref this.configuration.RemoveDiacriticsWhenUsingReplacementTalkBTalk);
              }
            }
          }
        }

        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab2Name))
      {
        if (this.configuration.Translate)
        {
          /* - battle talk - */
          this.SaveConfigValue |= ImGui.Checkbox(
            Resources.TransLateBattletalkToggle,
            ref this.configuration.TranslateBattleTalk);

          ImGui.BeginGroup();
          /*if (this.configuration.OverlayOnlyLanguage)
          {
            this.configuration.TranslateBattleTalk = false; // had disabled so no texts are lost while we fix this
            saveConfig |=
              AssignIfChanged(
                ref this.configuration.UseImGuiForBattleTalk,
                true);
          }
          else
          {
            this.configuration.UseImGuiForBattleTalk = false; // had disabled so no texts are lost while we fix this
            saveConfig |= ImGui.Checkbox(
              Resources.OverlayToggleLabel,
              ref this.configuration.UseImGuiForBattleTalk);
          }*/

          this.SaveConfigValue |= ImGui.Checkbox(
            Resources.TranslateNpcNamesToggle,
            ref this.configuration.TranslateNpcNames);

          ImGui.EndGroup();
        }

        ImGui.Spacing();
        ImGui.Separator();

        if (this.configuration.UseImGuiForBattleTalk)
        {
          ImGui.Text(Resources.ImguiAdjustmentsLabel);
          if (ImGui.SliderFloat(
            Resources.OverlayFontScaleLabel,
            ref this.configuration.BattleTalkFontScale, -3f, 3f, "%.2f"))
          {
            this.SaveConfigValue = true;
            this.configuration.FontChangeTime = DateTime.Now.Ticks;
          }

          ImGui.SameLine();
          ImGui.Text(Resources.HoverTooltipIndicator);
          if (ImGui.IsItemHovered())
          {
            ImGui.SetTooltip(Resources.OverlayFontSizeOrientations);
          }

          ImGui.Spacing();
          ImGui.Text(Resources.FontColorSelectLabel);
          ImGui.SameLine();
          this.SaveConfigValue |= ImGui.ColorEdit3(
            Resources.OverlayColorSelectName,
            ref this.configuration.OverlayBattleTalkTextColor,
            ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);

          ImGui.SameLine();
          ImGui.Text(Resources.HoverTooltipIndicator);
          if (ImGui.IsItemHovered())
          {
            ImGui.SetTooltip(Resources.OverlayFontColorOrientations);
          }

          ImGui.Spacing();
          ImGui.Separator();
          this.SaveConfigValue |= ImGui.DragFloat(
            Resources.OverlayWidthScrollLabel,
            ref this.configuration.ImGuiBattleTalkWindowWidthMult, 0.001f,
            0.01f, 3f);

          ImGui.Separator();
          this.SaveConfigValue |= ImGui.DragFloat(
            Resources.OverlayHeightScrollLabel,
            ref this.configuration.ImGuiBattleTalkWindowHeightMult, 0.001f,
            0.01f, 3f);

          ImGui.Separator();
          ImGui.Spacing();
          this.SaveConfigValue |= ImGui.DragFloat2(
            Resources.OverlayPositionAdjustmentLabel,
            ref this.configuration.ImGuiBattleTalkWindowPosCorrection);

          ImGui.SameLine();
          ImGui.Text(Resources.HoverTooltipIndicator);
          if (ImGui.IsItemHovered())
          {
            ImGui.SetTooltip(Resources.OverlayAdjustmentOrientations);
          }
        }

        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab3Name))
      {
        if (this.configuration.Translate)
        {
          /* - Toast messages - */
          this.SaveConfigValue |= ImGui.Checkbox(
            Resources.TranslateToastToggleText,
            ref this.configuration.TranslateToast);

          ImGui.BeginGroup();
          if (this.configuration.OverlayOnlyLanguage)
          {
            this.SaveConfigValue |=
              AssignIfChanged(ref this.configuration.UseImGuiForToasts, true);
          }
          else
          {
            this.SaveConfigValue |= ImGui.Checkbox(
              Resources.UseImGuiForToastsToggle,
              ref this.configuration.UseImGuiForToasts);
          }
        }

        ImGui.EndGroup();

        if (this.configuration.TranslateToast)
        {
          ImGui.Separator();
          ImGui.Text(Resources.WhichToastsToTranslate);
          this.SaveConfigValue |= ImGui.Checkbox(
            Resources.TranslateErrorToastToggleText,
            ref this.configuration.TranslateErrorToast);
          this.SaveConfigValue |= ImGui.Checkbox(
            Resources.TranslateQuestToastToggleText,
            ref this.configuration.TranslateQuestToast);
          this.SaveConfigValue |= ImGui.Checkbox(
            Resources.TranslateAreaToastToggleText,
            ref this.configuration.TranslateAreaToast);
          this.SaveConfigValue |=
            ImGui.Checkbox(
              Resources.TranslateClassChangeToastToggleText,
              ref this.configuration.TranslateClassChangeToast);
          this.SaveConfigValue |=
            ImGui.Checkbox(
              Resources.TranslateScreenInfoToastToggleText,
              ref this.configuration.TranslateWideTextToast);
        }

        ImGui.Separator();
        if (this.configuration.UseImGuiForToasts)
        {
          ImGui.Text(Resources.ImguiAdjustmentsLabel);

          this.SaveConfigValue |= ImGui.DragFloat(
            Resources.ToastOverlayWidthScrollLabel,
            ref this.configuration.ImGuiToastWindowWidthMult, 0.001f, 0.01f,
            3f);

          ImGui.SameLine();
          ImGui.Text(Resources.HoverTooltipIndicator);
          if (ImGui.IsItemHovered())
          {
            ImGui.SetTooltip(Resources.ToastOverlayWidthMultiplierOrientations);
          }
        }

        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab4Name))
      {
        if (this.configuration.Translate)
        {
          /* - Journal - */
          this.SaveConfigValue |= ImGui.Checkbox(
            Resources.TranslateJournalToggle,
            ref this.configuration.TranslateJournal);
        }

        if (langToRemoveDiacritics)
        {
          this.SaveConfigValue |= ImGui.Checkbox(
                             Resources.RemoveDiacriticsToggle,
                             ref this.configuration.RemoveDiacriticsWhenUsingReplacementQuest);
        }

        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab5Name))
      {
        /* - talk subtitle - */
        if (this.configuration.Translate)
        {
          this.SaveConfigValue |= ImGui.Checkbox(
            Resources.TranslateTalkSubtitleToggleLabel,
            ref this.configuration.TranslateTalkSubtitle);

          if (this.configuration.TranslateTalkSubtitle)
          {
            if (this.configuration.OverlayOnlyLanguage)
            {
              this.SaveConfigValue |=
                AssignIfChanged(ref this.configuration.UseImGuiForTalkSubtitle, true);
              this.SaveConfigValue |=
                AssignIfChanged(
                  ref this.configuration.SwapTextsUsingImGui,
                  false);
            }
            else
            {
              this.SaveConfigValue |= ImGui.Checkbox(
                Resources.OverlayToggleLabel,
                ref this.configuration.UseImGuiForTalkSubtitle);
            }

            ImGui.Spacing();
            ImGui.Separator();

            if (this.configuration.UseImGuiForTalkSubtitle)
            {
              ImGui.Text(Resources.ImguiAdjustmentsLabel);
              if (ImGui.SliderFloat(
                Resources.OverlayFontScaleLabel,
                ref this.configuration.FontScale, -3f, 3f, "%.2f"))
              {
                this.SaveConfigValue = true;
                this.configuration.FontChangeTime = DateTime.Now.Ticks;
              }

              ImGui.SameLine();
              ImGui.Text(Resources.HoverTooltipIndicator);
              if (ImGui.IsItemHovered())
              {
                ImGui.SetTooltip(Resources.OverlayFontSizeOrientations);
              }

              ImGui.Text(Resources.FontColorSelectLabel);
              ImGui.SameLine();
              this.SaveConfigValue |= ImGui.ColorEdit3(
                Resources.OverlayColorSelectName,
                ref this.configuration.OverlayTalkTextColor,
                ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);

              ImGui.SameLine();
              ImGui.Text(Resources.HoverTooltipIndicator);
              if (ImGui.IsItemHovered())
              {
                ImGui.SetTooltip(Resources.OverlayFontColorOrientations);
              }

              ImGui.Spacing();
              ImGui.Separator();
              this.SaveConfigValue |= ImGui.DragFloat(
                Resources.OverlayWidthScrollLabel,
                ref this.configuration.ImGuiTalkSubtitleWindowWidthMult, 0.001f, 0.01f,
                3f);

              ImGui.Separator();
              this.SaveConfigValue |= ImGui.DragFloat(
                Resources.OverlayHeightScrollLabel,
                ref this.configuration.ImGuiTalkSubtitleWindowHeightMult, 0.001f, 0.01f,
                3f);

              ImGui.Separator();
              ImGui.Spacing();
              this.SaveConfigValue |= ImGui.DragFloat2(
                Resources.OverlayPositionAdjustmentLabel,
                ref this.configuration.ImGuiTalkSubtitleWindowPosCorrection);

              ImGui.SameLine();
              ImGui.Text(Resources.HoverTooltipIndicator);
              if (ImGui.IsItemHovered())
              {
                ImGui.SetTooltip(Resources.OverlayAdjustmentOrientations);
              }
            }

            ImGui.Spacing();
            ImGui.Separator();
            if (!this.configuration.OverlayOnlyLanguage &&
                this.configuration.UseImGuiForTalkSubtitle)
            {
              this.SaveConfigValue |= ImGui.Checkbox(
                Resources.SwapTranslationTextToggle,
                ref this.configuration.SwapTextsUsingImGui);
            }

            if (this.configuration.SwapTextsUsingImGui && langToRemoveDiacritics)
            {
              this.SaveConfigValue |= ImGui.Checkbox(
                                 Resources.RemoveDiacriticsToggle,
                                 ref this.configuration.RemoveDiacriticsWhenUsingReplacementTalkBTalk);
            }
          }
        }

        ImGui.EndTabItem();
      }

      /*if (ImGui.BeginTabItem(Resources.ConfigTab6Name))
      {
        ImGui.Text("This is the Onion tab!\nblah blah blah blah blah");
        ImGui.EndTabItem();
      }*/

      if (ImGui.BeginTabItem(Resources.ConfigTab7Name))
      {
        /* - Translation engine - */
        this.DrawTranslationEnginesTab();
        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab8Name))
      {
        var pluginAssetsStatus = this.configuration.PluginAssetsDownloaded;

        ImGui.BeginGroup();
        /*if (pluginAssetsStatus)
        {
          // TODO: Add a button to re-download the assets
          ImGui.Text(Resources.PluginAssetsDownloadedText);
        }
        else
        {*/
        ImGui.TextWrapped(Resources.CurrentPluginAssetsStatus + ": " + pluginAssetsStatus);
        ImGui.TextWrapped(Resources.PluginAssetsNotDownloadedText);
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);
        if (ImGui.Button(Resources.DownloadPluginAssetsButtonText))
        {
          this.DownloadAssets(0);
          this.DownloadAssets(1);
          this.DownloadAssets(2);
          this.DownloadAssets(3);
          this.DownloadAssets(4);
          this.PluginAssetsChecker();
          this.SaveConfigValue = true;
        }



        ImGui.PopStyleColor(3);
        ImGui.EndGroup();

        ImGui.Spacing();

        ImGui.BeginGroup();
        ImGui.TextWrapped(Resources.ResetSettingsMessageText);
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);
        if (ImGui.Button(Resources.ResetSettingsButtonText))
        {
          //TODO: Add button logic
          PluginLog.Debug("Resetting settings to default");
          this.ResetSettings();
        }

        ImGui.PopStyleColor(3);
        ImGui.EndGroup();

        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTab9Name))
      {
        ImGui.Text(Resources.ConfigTab9Text);

        ImGui.Checkbox(Resources.ConfigTab9CheckboxClipboardText, ref this.configuration.CopyTranslationToClipboard);
        ImGui.TextWrapped(Resources.ConfigTab9CheckboxClipboardTooltipText);

        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem(Resources.ConfigTabAbout))
      {
        if (ImGui.BeginTable("columns", 2))
        {
          ImGui.TableNextColumn();
          ImGui.BeginGroup();
          ImGui.TextColored(new Vector4(247, 247, 7, 255), Resources.DisclaimerTitle);
          ImGui.Spacing();
          ImGui.TextWrapped(Resources.DisclaimerText1);
          ImGui.TextWrapped(Resources.DisclaimerText2);
          ImGui.TextWrapped(Resources.ContribText);
          ImGui.EndGroup();

          ImGui.TableNextColumn();
          var posLogo = new Vector2(ImGui.GetWindowContentRegionMax().X - 300, ImGui.GetWindowContentRegionMin().Y + 150);
          ImGui.SetCursorPos(posLogo);
          ImGui.Image(this.logo.ImGuiHandle, new Vector2(300, 300));
          ImGui.EndTable();
        }

        ImGui.EndTabItem();
      }

      ImGui.EndTabBar();
    }

    ImGui.EndGroup();


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