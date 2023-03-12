// <copyright file="PluginUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Numerics;
using Dalamud.Plugin;
using ImGuiNET;

namespace Echoglossian;

public class PluginUI
{
    private readonly DalamudPluginInterface pluginInterface;
    private readonly Config config;
    private readonly Plugin plugin;

    public PluginUI(DalamudPluginInterface pluginInterface, Config config, Plugin plugin)
    {
        this.pluginInterface = pluginInterface;
        this.config = config;
        this.plugin = plugin;
    }

    public void Draw()
    {
        ImGui.SetNextWindowSize(new Vector2(400, 400), ImGuiCond.FirstUseEver);
        ImGui.Begin("Echoglossian", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        ImGui.Text("Echoglossian is a plugin that translates in-game text to English.");
        ImGui.Text("It is currently in beta, so please report any issues you find.");
        ImGui.Text("You can find the source code on GitHub at");
        ImGui.End();
    }