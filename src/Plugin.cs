// <copyright file="Echoglossian.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.Sanitizer;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using Echoglossian.EFCoreSqlite.Models;

using Echoglossian.Properties;

namespace Echoglossian
{
  public class Plugin : IDalamudPlugin
  {
    public string Name => Resources.Name;

    private const string SlashCommand = "/eglo";
    private string configDir;

    [PluginService]
    private DalamudPluginInterface PluginInterface { get; set; }
    
    [PluginService]
    public static DataManager DManager { get; private set; }

    private Config configuration;
    private PluginUI ui;
    
    [PluginService]
    public static CommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    public static Framework Framework { get; private set; } = null!;

    [PluginService]
    public static GameGui GameGui { get; private set; } = null!;

    [PluginService]
    public static ClientState ClientState { get; private set; } = null!;

    [PluginService]
    public static ToastGui ToastGui { get; private set; } = null!;
    
    public List<ToastMessage> ErrorToastsCache { get; set; }

    public List<ToastMessage> QuestToastsCache { get; set; }

    public List<ToastMessage> OtherToastsCache { get; set; }
    
    public Plugin()
    {
    
      this.configuration = PluginInterface.GetPluginConfig() as Config ?? new Config();

      this.configDir = PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar;
      
      CommandManager.AddHandler(SlashCommand, new CommandInfo(this.Command)
      {
        HelpMessage = Resources.HelpMessage,
      });

      this.ui = new PluginUI(this.PluginInterface, this.configuration, this);
      this.pluginInterface.UiBuilder.Draw += this.ui.Draw;
      this.pluginInterface.UiBuilder.OpenConfigUi += this.ui.OpenConfig;
    }

    public void Dispose()
    {
      this.pluginInterface.UiBuilder.Draw -= this.ui.Draw;
      this.pluginInterface.UiBuilder.OpenConfigUi -= this.ui.OpenConfig;
      this.ui.Dispose();
    }
  }
}