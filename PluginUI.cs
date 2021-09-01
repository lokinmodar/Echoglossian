using System;
using System.Diagnostics;
using ImGuiNET;
using Num = System.Numerics;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private void EchoglossianConfigUi()
    {
      if (_config)
      {
        ImGui.SetNextWindowSizeConstraints(new Num.Vector2(500, 500), new Num.Vector2(1920, 1080));
        ImGui.Begin("Echoglossian Configuration", ref _config);
        if (ImGui.BeginTabBar("Tabs", ImGuiTabBarFlags.None))
        {
          if (ImGui.BeginTabItem("Configuration"))
          {
            if (ImGui.Combo("Language to translate to", ref _languageInt, _languages, _languages.Length))
              SaveConfig();

            ImGui.SameLine();
            ImGui.Text("(?)");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Which language to translate to.");

            ImGui.Checkbox("Translate talk", ref _configuration.TranslateTalk);
            if (_configuration.TranslateTalk)
            {
              ImGui.Checkbox("Display translated text above message instead of replacing it",
                ref _configuration.UseImGui);
              if (_configuration.UseImGui)
              {
                ImGui.DragFloat("Width multiplier", ref _configuration.ImGuiWindowWidthMult, 0.001f, 0.1f, 2f);
                ImGui.TextWrapped("Please adjust position if your overlay is not centered relative to talk popup.");
                ImGui.DragFloat2("Position adjustment", ref _configuration.ImGuiWindowPosCorrection);
              }
            }

            ImGui.Checkbox("Translate battle talk", ref _configuration.TranslateBattleTalk);

            ImGui.EndTabItem();
          }


          ImGui.EndTabBar();
        }


        if (ImGui.Button("Save and Close Config Window"))
        {
          SaveConfig();
          _config = false;
        }

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);

        if (ImGui.Button("Buy lokinmodar a Coffee")) Process.Start("https://ko-fi.com/lokinmodar");

        ImGui.PopStyleColor(3);

        ImGui.End();
      }

      if (!_picker) return;
      ImGui.SetNextWindowSizeConstraints(new Num.Vector2(320, 440), new Num.Vector2(640, 880));
      ImGui.Begin("UIColor Picker", ref _picker);
      ImGui.Columns(10, "##columnsID", false);
      foreach (var z in _uiColours)
      {
        var temp = BitConverter.GetBytes(z.UIForeground);
        if (ImGui.ColorButton(z.RowId.ToString(), new Num.Vector4(
          (float) temp[3] / 255,
          (float) temp[2] / 255,
          (float) temp[1] / 255,
          (float) temp[0] / 255)))
        {
          _chooser.Choice = z.UIForeground;
          _chooser.Option = z.RowId;
          _picker = false;
          SaveConfig();
        }

        ImGui.NextColumn();
      }

      ImGui.Columns(1);
      ImGui.End();
    }
  }
}