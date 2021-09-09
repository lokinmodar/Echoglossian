// <copyright file="PluginUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Dalamud.Logging;
using Echoglossian.Properties;
using ImGuiNET;

using Num = System.Numerics;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public bool FontLoaded;
    public bool FontLoadFailed;
    public ImFontPtr UiFont;

    public string[] FontSizes = Array.ConvertAll(Enumerable.Range(4, 72).ToArray(), x => x.ToString());

    private void BuildFont()
    {
      var fontFile = $@"{Path.GetFullPath(Path.GetDirectoryName(this.AssemblyLocation)!)}\Font\NotoSans-Regular.ttf";
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

            ImGui.Checkbox("Translate talk", ref this.configuration.TranslateTalk);
            if (this.configuration.TranslateTalk)
            {
              ImGui.Checkbox(
                  "Display translated text overlay above game UI dialogue box instead of replacing its text with the translation",
                  ref this.configuration.UseImGui);
              if (this.configuration.UseImGui)
              {
                ImGui.Separator();
                if (ImGui.Combo("Overlay Font Size", ref this.configuration.FontSize, this.FontSizes, this.FontSizes.Length))
                {
                  this.SaveConfig();
                }

                ImGui.SameLine();
                ImGui.Text("(?)");
                if (ImGui.IsItemHovered())
                {
                  ImGui.SetTooltip("Please select overlay font size.");
                }

                ImGui.Separator();
                ImGui.DragFloat("Width multiplier", ref this.configuration.ImGuiWindowWidthMult, 0.001f, 0.1f, 2f);
                ImGui.Separator();
                ImGui.TextWrapped("Please adjust position if your overlay is not centered relative to talk popup.");
                ImGui.DragFloat2("Position adjustment", ref this.configuration.ImGuiWindowPosCorrection);
              }
            }

            ImGui.Checkbox("Translate battle talk", ref this.configuration.TranslateBattleTalk);

            ImGui.EndTabItem();
          }

          ImGui.EndTabBar();
        }

        if (ImGui.Button("Save and Close Config Window"))
        {
          this.SaveConfig();
          this.config = false;
        }

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);

        if (ImGui.Button("Buy lokinmodar a Coffee"))
        {
          Process.Start("https://ko-fi.com/lokinmodar");
        }

        ImGui.PopStyleColor(3);

        ImGui.End();
      }
    }
  }
}