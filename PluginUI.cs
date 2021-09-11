// <copyright file="PluginUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using Dalamud.Logging;
using Echoglossian.Properties;
using ImGuiNET;
using ImGuiScene;

using Num = System.Numerics;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public bool FontLoaded;
    public bool FontLoadFailed;
    public ImFontPtr UiFont;
    /*private TextureWrap? pixImage;
    private SimpleImGuiScene? scene;*/


    public string[] FontSizes = Array.ConvertAll(Enumerable.Range(4, 72).ToArray(), x => x.ToString());

    private void BuildFont(string fontFileName)
    {
      var fontFile = $@"{Path.GetFullPath(Path.GetDirectoryName(this.AssemblyLocation)!)}\Font\{fontFileName}";
      this.FontLoaded = false;
      if (File.Exists(fontFile))
      {
        try
        {
          this.UiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFile, this.configuration.FontSize);
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

    private void EchoglossianConfigUi()
    {
      if (this.config)
      {
        ImGui.SetNextWindowSizeConstraints(new Num.Vector2(500, 500), new Num.Vector2(1920, 1080));
        ImGui.Begin(Resources.ConfigWindowTitle, ref this.config);
        if (ImGui.BeginTabBar("Tabs", ImGuiTabBarFlags.None))
        {
          if (ImGui.BeginTabItem(Resources.ConfigTab1Name))
          {
            ImGui.Text(Resources.WhatToTranslateText);
            ImGui.Checkbox(Resources.TranslateTalkToggleLabel, ref this.configuration.TranslateTalk);
            ImGui.Checkbox(Resources.TransLateBattletalkToggle, ref this.configuration.TranslateBattleTalk);
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

            if (this.configuration.TranslateTalk)
            {
              ImGui.Checkbox(Resources.OverlayToggleLabel, ref this.configuration.UseImGui);
              if (this.configuration.UseImGui)
              {
                ImGui.Separator();
                if (ImGui.Combo(Resources.OverlayFontSizeLabel, ref this.configuration.FontSize, this.FontSizes, this.FontSizes.Length))
                {
                  this.SaveConfig();
                }

                ImGui.SameLine();
                ImGui.Text(Resources.HoverTooltipIndicator);
                if (ImGui.IsItemHovered())
                {
                  ImGui.SetTooltip(Resources.OverlayFontSizeOrientations);
                }

                ImGui.Separator();
                ImGui.DragFloat(Resources.OverlayWidthScrollLabel, ref this.configuration.ImGuiWindowWidthMult, 0.001f, 0.1f, 2f);
                ImGui.SameLine();
                ImGui.Text(Resources.HoverTooltipIndicator);
                if (ImGui.IsItemHovered())
                {
                  ImGui.SetTooltip(Resources.OverlayWidthMultiplierOrientations);
                }

                ImGui.Separator();
                ImGui.Spacing();
                ImGui.DragFloat2(Resources.OverlayPositionAdjustmentLabel, ref this.configuration.ImGuiWindowPosCorrection);
                ImGui.SameLine();
                ImGui.Text(Resources.HoverTooltipIndicator);
                if (ImGui.IsItemHovered())
                {
                  ImGui.SetTooltip(Resources.OverlayAdjustmentOrientations);
                }
              }
            }

            ImGui.EndTabItem();
          }

          ImGui.EndTabBar();
        }

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
        ImGui.PushStyleColor(ImGuiCol.Button, new Num.Vector4(4, 7.0f, 0.6f, 0.6f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Num.Vector4(4, 7.0f, 0.7f, 0.7f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Num.Vector4(4, 7.0f, 0.8f, 0.8f));
        if (ImGui.Button(Resources.SendPixButton))
        {
          ImGui.OpenPopup("test");
        }

        // Always center this window when appearing
        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Num.Vector2(0.5f, 0.5f));
        if (ImGui.BeginPopupModal("test"))
        {
          ImGui.Text("Scan the QR code to send me a PIX - only valid in Brazil");
          // ImGui.Image(this.pixImage!.ImGuiHandle, new Num.Vector2(512, 512));
          if (ImGui.Button("Ok!"))
          {
            ImGui.CloseCurrentPopup();
          }

          ImGui.EndPopup();
          ImGui.SetItemDefaultFocus();
        }

        ImGui.PopStyleColor(3);
        ImGui.PopID();
        ImGui.End();
      }
    }
  }
}