// <copyright file="Echoglossian.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Threading;

using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina;
using Lumina.Excel;
using XivCommon;

namespace Echoglossian
{
  // TODO: Handle polish alphabet correctly.
  // TODO: Add translation database history.
  // TODO: implement multiple fallback translation engines.
  public unsafe partial class Echoglossian : IDalamudPlugin
  {
    private const string SlashCommand = "/eglo";
    private static int languageInt = 16;
    private static int fontSize = 20;

    private static readonly string[] Codes =
    {
        "af", "an", "ar", "az", "be_x_old",
        "bg", "bn", "br", "bs",
        "ca", "ceb", "cs", "cy", "da",
        "de", "el", "en", "eo", "es",
        "et", "eu", "fa", "fi", "fr",
        "gl", "he", "hi", "hr", "ht",
        "hu", "hy", "id", "is", "it",
        "ja", "jv", "ka", "kk", "ko",
        "la", "lb", "lt", "lv",
        "mg", "mk", "ml", "mr", "ms",
        "new", "nl", "nn", "no", "oc",
        "pl", "pt", "ro", "roa_rup",
        "ru", "sk", "sl",
        "sq", "sr", "sv", "sw", "ta",
        "te", "th", "tl", "tr", "uk",
        "ur", "vi", "vo", "war", "zh",
        "zh_classical", "zh_yue",
    };

    private readonly string[] languages =
    {
        "Afrikaans", "Aragonese", "Arabic", "Azerbaijani", "Belarusian",
        "Bulgarian", "Bengali", "Breton", "Bosnian",
        "Catalan; Valencian", "Cebuano", "Czech", "Welsh", "Danish",
        "German", "Greek, Modern", "English", "Esperanto", "Spanish; Castilian",
        "Estonian", "Basque", "Persian", "Finnish", "French",
        "Galician", "Hebrew", "Hindi", "Croatian", "Haitian; Haitian Creole",
        "Hungarian", "Armenian", "Indonesian", "Icelandic", "Italian",
        "Japanese", "Javanese", "Georgian", "Kazakh", "Korean",
        "Latin", "Luxembourgish; Letzeburgesch", "Lithuanian", "Latvian",
        "Malagasy", "Macedonian", "Malayalam", "Marathi", "Malay",
        "Nepal Bhasa; Newari", "Dutch; Flemish", "Norwegian Nynorsk; Nynorsk, Norwegian", "Norwegian",
        "Occitan (post 1500)",
        "Polish", "Portuguese", "Romanian; Moldavian; Moldovan", "Romance languages",
        "Russian", "Slovak", "Slovenian",
        "Albanian", "Serbian", "Swedish", "Swahili", "Tamil",
        "Telugu", "Thai", "Tagalog", "Turkish", "Ukrainian",
        "Urdu", "Vietnamese", "Volapük", "Waray", "Chinese",
        "Chinese Classical", "Chinese yue",
    };

    private Framework framework;
    private GameGui gameGui;
    // private GameData gameData;
    // private readonly UiColorPick chooser;
    private List<int> chosenLanguages;
    private bool config;
    private Config configuration;
    // private bool picker;
    private DalamudPluginInterface pluginInterface;
    // private ExcelSheet<UIColor> uiColours;
    private string talkCurrentTranslation = string.Empty;
    private volatile int talkCurrentTranslationId;
    private bool talkDisplayTranslation;
    private Vector2 talkTextDimensions = Vector2.Zero;
    private Vector2 talkTextImguiSize = Vector2.Zero;
    private Vector2 talkTextPosition = Vector2.Zero;
    private CommandManager commandManager;

    private SemaphoreSlim talkTranslationSemaphore;

    public string Name => "Echoglossian";

    /// <summary>
    /// Gets or sets AssemblyLocation. When loaded by LivePluginLoader, the executing assembly will be wrong. Supplying this property allows LivePluginLoader to supply the correct location, so that you have full compatibility when loaded normally and through LPL.
    /// </summary>
    public string AssemblyLocation { get; set; } = Assembly.GetExecutingAssembly().Location;

    private static XivCommonBase Common { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Echoglossian"/> class.
    /// </summary>
    /// <param name="dalamudPluginInterface">Plugin Interface.</param>
    /// <param name="pframework">Framework.</param>
    /// <param name="pCommandManager">Command Manager.</param>
    /// <param name="pGameGui">Game Gui object.</param>
    public Echoglossian(DalamudPluginInterface dalamudPluginInterface, Framework pframework, CommandManager pCommandManager, GameGui pGameGui)
    {
      this.framework = pframework;
      this.pluginInterface = dalamudPluginInterface;
      this.configuration = this.pluginInterface.GetPluginConfig() as Config ?? new Config();
      // this.gameData = pGameData;
      this.gameGui = pGameGui;
      Common = new XivCommonBase(Hooks.Talk | Hooks.BattleTalk);
      this.commandManager = pCommandManager;

      Common.Functions.Talk.OnTalk += this.GetText;
      Common.Functions.BattleTalk.OnBattleTalk += this.GetBattleText;

      this.pluginInterface.UiBuilder.Draw += this.EchoglossianConfigUi;
      this.pluginInterface.UiBuilder.OpenConfigUi += this.ConfigWindow;
      // this.pluginInterface.UiBuilder.OnOpenConfigUi += this.EchoglossianConfig;
      this.commandManager.AddHandler(SlashCommand, new CommandInfo(this.Command)
      {
        HelpMessage = "Opens Echoglossian config window",
      });
      // this.uiColours = this.gameData.Excel.GetSheet<UIColor>();
      languageInt = this.configuration.Lang;
      this.chosenLanguages = this.configuration.ChosenLanguages;
      fontSize = this.configuration.FontSize;

      this.framework.Update += this.Tick;
      this.pluginInterface.UiBuilder.Draw += this.DrawTranslatedText;
      this.talkTranslationSemaphore = new SemaphoreSlim(1, 1);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      GC.SuppressFinalize(this);

      Common.Functions.Talk.OnTalk -= this.GetText;
      Common.Functions.BattleTalk.OnBattleTalk -= this.GetBattleText;

      // _pluginInterface.Framework.Gui.Chat.OnChatMessage -= Chat_OnChatMessage;
      this.pluginInterface.UiBuilder.OpenConfigUi -= this.EchoglossianConfigUi;
      // this.pluginInterface.UiBuilder.OpenConfigUi -= this.EchoglossianConfig;
      this.commandManager.RemoveHandler(SlashCommand);

      this.framework.Update -= this.Tick;
      this.pluginInterface.UiBuilder.Draw -= this.DrawTranslatedText;
      this.pluginInterface.Dispose();
    }

    private void DrawTranslatedText()
    {
      if (this.configuration.UseImGui && this.configuration.TranslateTalk && this.talkDisplayTranslation)
      {
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(
          this.talkTextPosition.X + (this.talkTextDimensions.X / 2) - (this.talkTextImguiSize.X / 2),
          this.talkTextPosition.Y - this.talkTextImguiSize.Y - 20) + this.configuration.ImGuiWindowPosCorrection);
        var size = Math.Min(
            this.talkTextDimensions.X * this.configuration.ImGuiWindowWidthMult,
            ImGui.CalcTextSize(this.talkCurrentTranslation).X + (ImGui.GetStyle().WindowPadding.X * 2));
        ImGui.SetNextWindowSizeConstraints(new Vector2(size, 0), new Vector2(size, this.talkTextDimensions.Y));
        ImGui.Begin(
            "Battle talk translation",
            ImGuiWindowFlags.NoTitleBar
          | ImGuiWindowFlags.NoNav
          | ImGuiWindowFlags.AlwaysAutoResize
          | ImGuiWindowFlags.NoFocusOnAppearing
          | ImGuiWindowFlags.NoMouseInputs);

        if (this.talkTranslationSemaphore.Wait(0))
        {
          ImGui.TextWrapped(this.talkCurrentTranslation);
          this.talkTranslationSemaphore.Release();
        }
        else
        {
          ImGui.Text("Awaiting translation...");
        }

        this.talkTextImguiSize = ImGui.GetWindowSize();
        ImGui.End();
      }
    }

    private void Tick(Framework tFramework)
    {
      if (this.configuration.UseImGui)
      {
        var talk = this.gameGui.GetAddonByName("Talk", 1);
        if (talk != IntPtr.Zero)
        {
          var talkMaster = (AtkUnitBase*)talk;
          if (talkMaster->IsVisible)
          {
            this.talkDisplayTranslation = true;
            this.talkTextDimensions.X = talkMaster->RootNode->Width * talkMaster->Scale;
            this.talkTextDimensions.Y = talkMaster->RootNode->Height * talkMaster->Scale;
            this.talkTextPosition.X = talkMaster->RootNode->X;
            this.talkTextPosition.Y = talkMaster->RootNode->Y;
          }
          else
          {
            this.talkDisplayTranslation = false;
          }
        }
        else
        {
          this.talkDisplayTranslation = false;
        }
      }
      else
      {
        this.talkDisplayTranslation = false;
      }
    }

    private void ConfigWindow()
    {
      this.config = true;
    }

    private void Command(string command, string arguments)
    {
      this.config = true;
    }
  }
}