using System;
using Dalamud.Game.Text;
using ImGuiNET;
using Num = System.Numerics;
using XivCommon.Functions;


namespace Echoglossian
{
    public class PluginUi : IDisposable
    {
        private const string DefaultLabel = "<default>";
        //public bool IsVisible { get; set; }

        private Plugin Plugin { get; }
        private bool _config;
        private int _languageInt;
        private Configuration _configuration;

        public PluginUi(Plugin plugin)
        {
            Plugin = plugin;
            _config = plugin._config;
            _languageInt = plugin._languageInt;
            _configuration = plugin.Config;
        }

        private void TranslatorConfigUi()
        {
            if (_config)
            {
                ImGui.SetNextWindowSizeConstraints(new Num.Vector2(500, 500), new Num.Vector2(1920, 1080));
                ImGui.Begin("Chat Translator Config", ref _config);
                if (ImGui.BeginTabBar("Tabs", ImGuiTabBarFlags.None))
                {
                    if (ImGui.BeginTabItem("Config"))
                    {
                        if (ImGui.Combo("Language to translate to", ref _languageInt, this.Plugin.Echoglossian._languages, this.Plugin.Echoglossian._languages.Length))
                        {
                            _configuration.Save();
                        }
                        ImGui.SameLine();
                        ImGui.Text("(?)"); if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Which language to translate to."); }
                        if (ImGui.Combo("Mode", ref this.Plugin._tranMode ,this.Plugin._tranModeOptions, this.Plugin._tranModeOptions.Length))
                        {
                            _configuration.Save();
                        }
                        ImGui.SameLine();
                        ImGui.Text("(?)"); if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Which method of displaying the translated text."); }
                        var textColour = BitConverter.GetBytes(this.Plugin._textColour[0].Choice);
                        if (ImGui.ColorButton("Translated Text Colour", new Num.Vector4(
                            (float)textColour[3] / 255,
                            (float)textColour[2] / 255,
                            (float)textColour[1] / 255,
                            (float)textColour[0] / 255)))
                        {
                            this.Plugin._chooser = this.Plugin._textColour[0];
                            this.Plugin._picker = true;
                        }
                        ImGui.SameLine();
                        ImGui.Text("Translated text colour");
                        ImGui.SameLine();
                        ImGui.Text("(?)"); if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Colour the translated text this colour."); }
                        ImGui.Separator();
                        ImGui.Checkbox("Exclude self", ref this.Plugin._notSelf);
                        ImGui.SameLine();
                        ImGui.Text("(?)"); if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Do not translate your own text."); }

                        ImGui.Checkbox("Send Translations to one channel", ref this.Plugin._oneChan);
                        ImGui.SameLine();
                        ImGui.Text("(?)"); if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Only works for the 'Additional' mode.'"); }
                        if (this.Plugin._oneChan)
                        {
                            if (ImGui.Combo("Channel", ref this.Plugin._oneInt, this.Plugin.Echoglossian._orderString, this.Plugin.Echoglossian._orderString.Length))
                            {
                                this.Plugin._tranMode = 2;
                                _configuration.Save();
                            }
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Channels"))
                    {
                        var i = 0;
                        ImGui.Text("Enabled channels:");
                        ImGui.SameLine();
                        ImGui.Text("(?)"); if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Which chat channels to translate."); }
                        ImGui.Columns(2);

                        foreach (var e in (XivChatType[])Enum.GetValues(typeof(XivChatType)))
                        {
                            if (this.Plugin.Echoglossian._yesNo[i])
                            {
                                var enabled = this.Plugin._channels.Contains(e);
                                if (ImGui.Checkbox($"{e}", ref enabled))
                                {
                                    if (enabled) this.Plugin._channels.Add(e);
                                    else this.Plugin._channels.Remove(e);
                                    _configuration.Save();
                                }
                                ImGui.NextColumn();
                            }
                            i++;
                        }
                        ImGui.Columns(1);
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Whitelist"))
                    {
                        ImGui.Checkbox("Enable Whitelist", ref this.Plugin._whitelist);
                        ImGui.SameLine();
                        ImGui.Text("(?)"); if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Only translate languages detected in specific languages"); }
                        if (this.Plugin._whitelist)
                        {
                            var remove = -1;
                            for (var j = 0; j < this.Plugin._chosenLanguages.Count; j++)
                            {
                                ImGui.Text($"{j}: {this.Plugin.Echoglossian._languages[this.Plugin._chosenLanguages[j]]}");
                                ImGui.SameLine();
                                if (ImGui.Button($"Remove##{j}"))
                                {
                                    remove = j;
                                }
                            }
                            if (ImGui.Combo("Add Language", ref this.Plugin._languageInt2, this.Plugin.Echoglossian._languages, this.Plugin.Echoglossian._languages.Length))
                            {
                                this.Plugin._chosenLanguages.Add(this.Plugin._languageInt2);
                                _configuration.Save();
                            }
                            if (remove != -1)
                            {
                                this.Plugin._chosenLanguages.RemoveAt(remove);
                                _configuration.Save();
                            }
                        }
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Blacklist"))
                    {
                        ImGui.Text("Blacklisted messages:");
                        var removeTwo = -1;
                        for (var j = 0; j < this.Plugin._blacklist.Count; j++)
                        {
                            ImGui.Text($"- {this.Plugin._blacklist[j]}");
                            ImGui.SameLine();
                            if (ImGui.Button($"Remove##{j}"))
                            {
                                removeTwo = j;
                            }
                        }

                        if (removeTwo != -1)
                        {
                            this.Plugin._blacklist.RemoveAt(removeTwo);
                            _configuration.Save();
                        }

                        ImGui.Separator();
                        ImGui.Text("Recently translated messages");
                        for (var j = 0; j < this.Plugin._lastTranslations.Count; j++)
                        {
                            ImGui.Text($"{j + 1}: {this.Plugin._lastTranslations[j]}");
                            ImGui.SameLine();
                            if (ImGui.Button($"Add##{j}"))
                            {
                                this.Plugin._blacklist.Add(this.Plugin._lastTranslations[j]);
                                _configuration.Save();
                            }
                        }
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }


                if (ImGui.Button("Save and Close Config"))
                {
                    _configuration.Save();
                    _config = false;
                }

                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);

                if (ImGui.Button("Buy Haplo a Hot Chocolate"))
                {
                    System.Diagnostics.Process.Start("https://ko-fi.com/haplo");
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
                    _configuration.Save();
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
            TranslatorConfigUi();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
