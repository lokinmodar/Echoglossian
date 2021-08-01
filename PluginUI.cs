﻿using System;
using Dalamud.Game.Text;
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
                        {
                            SaveConfig();
                        }
                        ImGui.SameLine();
                        ImGui.Text("(?)"); if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Which language to translate to."); }
                        /*if (ImGui.Combo("Mode", ref _tranMode, _tranModeOptions, _tranModeOptions.Length))
                        {
                            SaveConfig();
                        }
                        ImGui.SameLine();*/
                        /*ImGui.Text("(?)"); if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Which method of displaying the translated text."); }
                        var textColour = BitConverter.GetBytes(_textColour[0].Choice);
                        if (ImGui.ColorButton("Translated Text Colour", new Num.Vector4(
                            (float)textColour[3] / 255,
                            (float)textColour[2] / 255,
                            (float)textColour[1] / 255,
                            (float)textColour[0] / 255)))
                        {
                            _chooser = _textColour[0];
                            _picker = true;
                        }
                        ImGui.SameLine();
                        ImGui.Text("Translated text colour");
                        ImGui.SameLine();
                        ImGui.Text("(?)"); if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Colour the translated text this colour."); }
                        ImGui.Separator();
                        ImGui.Checkbox("Exclude self", ref _notSelf);
                        ImGui.SameLine();
                        ImGui.Text("(?)"); if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Do not translate your own text."); }

                        ImGui.Checkbox("Send Translations to one channel", ref _oneChan);
                        ImGui.SameLine();
                        ImGui.Text("(?)"); if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Only works for the 'Additional' mode.'"); }
                        if (_oneChan)
                        {
                            if (ImGui.Combo("Channel", ref _oneInt, _orderString, _orderString.Length))
                            {
                                _tranMode = 2;
                                SaveConfig();
                            }
                        }*/
                        
                        ImGui.EndTabItem();
                    }
                    
                    /*if (ImGui.BeginTabItem("Channels"))
                    {
                        var i = 0;
                        ImGui.Text("Enabled channels:");
                        ImGui.SameLine();
                        ImGui.Text("(?)"); if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Which chat channels to translate."); }
                        ImGui.Columns(2);

                        foreach (var e in (XivChatType[])Enum.GetValues(typeof(XivChatType)))
                        {
                            if (_yesNo[i])
                            {
                                var enabled = _channels.Contains(e);
                                if (ImGui.Checkbox($"{e}", ref enabled))
                                {
                                    if (enabled) _channels.Add(e);
                                    else _channels.Remove(e);
                                    SaveConfig();
                                }
                                ImGui.NextColumn();
                            }
                            i++;
                        }
                        ImGui.Columns(1);
                        ImGui.EndTabItem();
                    }*/

                    /*if (ImGui.BeginTabItem("Whitelist"))
                    {
                        ImGui.Checkbox("Enable Whitelist", ref _whitelist);
                        ImGui.SameLine();
                        ImGui.Text("(?)"); if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Only translate languages detected in specific languages"); }
                        if (_whitelist)
                        {
                            var remove = -1;
                            for (var j = 0; j < _chosenLanguages.Count; j++)
                            {
                                ImGui.Text($"{j}: {_languages[_chosenLanguages[j]]}");
                                ImGui.SameLine();
                                if (ImGui.Button($"Remove##{j}"))
                                {
                                    remove = j;
                                }
                            }
                            if (ImGui.Combo("Add Language", ref _languageInt2, _languages, _languages.Length))
                            {
                                _chosenLanguages.Add(_languageInt2);
                                SaveConfig();
                            }
                            if (remove != -1)
                            {
                                _chosenLanguages.RemoveAt(remove);
                                SaveConfig();
                            }
                        }
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Blacklist"))
                    {
                        ImGui.Text("Blacklisted messages:");
                        var removeTwo = -1;
                        for (var j = 0; j < _blacklist.Count; j++)
                        {
                            ImGui.Text($"- {_blacklist[j]}");
                            ImGui.SameLine();
                            if (ImGui.Button($"Remove##{j}"))
                            {
                                removeTwo = j;
                            }
                        }

                        if (removeTwo != -1)
                        {
                            _blacklist.RemoveAt(removeTwo);
                            SaveConfig();
                        }
                    
                        ImGui.Separator();
                        ImGui.Text("Recently translated messages");
                        for (var j = 0; j < _lastTranslations.Count; j++)
                        {
                            ImGui.Text($"{j+1}: {_lastTranslations[j]}");
                            ImGui.SameLine();
                            if (ImGui.Button($"Add##{j}"))
                            {
                                _blacklist.Add(_lastTranslations[j]);
                                SaveConfig();
                            }
                        }
                        ImGui.EndTabItem();
                    }*/
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

                if (ImGui.Button("Buy lokinmodar a Coffee"))
                {
                    System.Diagnostics.Process.Start("https://ko-fi.com/lokinmodar");
                }
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
                    (float)temp[3] / 255,
                    (float)temp[2] / 255,
                    (float)temp[1] / 255,
                    (float)temp[0] / 255)))
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