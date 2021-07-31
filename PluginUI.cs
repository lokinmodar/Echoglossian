using System;
using Dalamud.Game.Text;
using ImGuiNET;
using Num = System.Numerics;
using XivCommon.Functions;


namespace Echoglossian
{
    public class PluginUi : IDisposable
    {
        private string DefaultLabel = "<default>";
        //public bool IsVisible { get; set; }

        private Plugin Plugin { get; }
        private bool _config;
        private int _languageInt;
        private Configuration _configuration;
        private Echoglossian _echoglossian;
        private bool _picker;

        public PluginUi(Plugin plugin)
        {
            this.Plugin = plugin;
            this.DefaultLabel = plugin.Name;
            this._picker = plugin._picker;
            this._echoglossian = Glossian._echoglossian;
            this._config = plugin._config;
            this._languageInt = plugin.Config.Lang;
            this._configuration = plugin.Config;
        }

        private void EchoglossianConfigUi()
        {
            if (_config)
            {
                ImGui.SetNextWindowSizeConstraints(new Num.Vector2(500, 500), new Num.Vector2(1920, 1080));
                ImGui.Begin($"{DefaultLabel} Config", ref _config);
                if (ImGui.BeginTabBar("Tabs", ImGuiTabBarFlags.None))
                {
                    if (ImGui.BeginTabItem("Config"))
                    {
                        if (ImGui.Combo("Language to translate to", ref _languageInt, this._echoglossian.Languages, this._echoglossian.Languages.Length))
                        {
                            this.Plugin.SaveConfig();
                        }
                        ImGui.SameLine();
                        ImGui.Text("(?)"); if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Which language to translate to."); }

                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }


                if (ImGui.Button("Save and Close Config"))
                {
                    this.Plugin.SaveConfig();
                    this._config = false;
                }

                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);

                if (ImGui.Button("Buy Dante a Coffee"))
                {
                    System.Diagnostics.Process.Start("https://ko-fi.com/lokinmodar");
                }
                ImGui.PopStyleColor(3);
                ImGui.End();
            }

            if (!this.Plugin._picker) return;
            ImGui.SetNextWindowSizeConstraints(new Num.Vector2(320, 440), new Num.Vector2(640, 880));
            ImGui.Begin("UIColor Picker", ref this.Plugin._picker);
            ImGui.Columns(10, "##columnsID", false);
            foreach (var z in this.Plugin._uiColours)
            {
                var temp = BitConverter.GetBytes(z.UIForeground);
                if (ImGui.ColorButton(z.RowId.ToString(), new Num.Vector4(
                    (float)temp[3] / 255,
                    (float)temp[2] / 255,
                    (float)temp[1] / 255,
                    (float)temp[0] / 255)))
                {
                    this.Plugin._chooser.Choice = z.UIForeground;
                    this.Plugin._chooser.Option = z.RowId;
                    this.Plugin._picker = false;
                    this.Plugin.SaveConfig();
                }
                ImGui.NextColumn();
            }
            ImGui.Columns(1);
            ImGui.End();
        }



        public void Draw()
        {
            /*if (!IsVisible)
                return;*/
            this.EchoglossianConfigUi();
        }

        public void Dispose()
        {
            this._echoglossian.Dispose();
            this.Plugin?.Dispose();
            //throw new NotImplementedException();
        }
    }
}
