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
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Interface;
using Dalamud.IoC;
using Dalamud.Plugin;
using Echoglossian.Properties;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using ImGuiScene;
using XivCommon;

namespace Echoglossian
{
  // TODO: Handle weird alphabets correctly.
  // TODO: Add translation database history.
  // TODO: implement multiple fallback translation engines.
  public partial class Echoglossian : IDalamudPlugin
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

    private readonly CommandManager commandManager;
    private bool config;
    private readonly Config configuration;

    private readonly Framework framework;
    private readonly GameGui gameGui;

    private readonly DalamudPluginInterface pluginInterface;
    private TextureWrap pixImage;

    private ToastGui toastGui;

    private readonly SemaphoreSlim TranslationSemaphore;
    private readonly SemaphoreSlim talkTranslationSemaphore;

    /// <summary>
    ///   Initializes a new instance of the <see cref="Echoglossian" /> class.
    /// </summary>
    /// <param name="dalamudPluginInterface">Plugin Interface.</param>
    /// <param name="pframework">Framework.</param>
    /// <param name="pCommandManager">Command Manager.</param>
    /// <param name="pGameGui">Game Gui object.</param>
    /// <param name="pToastGui">Toast Gui Object</param>
    public Echoglossian([RequiredVersion("1.0")] DalamudPluginInterface dalamudPluginInterface, Framework pframework,
      [RequiredVersion("1.0")] CommandManager pCommandManager, GameGui pGameGui, ToastGui pToastGui)
    {
      this.framework = pframework;

      this.pluginInterface = dalamudPluginInterface;
      this.configuration = this.pluginInterface.GetPluginConfig() as Config ?? new Config();

      Common = new XivCommonBase(Hooks.Talk | Hooks.BattleTalk);

      this.commandManager = pCommandManager;
      this.commandManager.AddHandler(SlashCommand, new CommandInfo(this.Command)
      {
        HelpMessage = Resources.HelpMessage,
      });

      this.gameGui = pGameGui;

      this.toastGui = pToastGui;

      this.pixImage = this.pluginInterface.UiBuilder.LoadImage(Resources.pix);

      this.pluginInterface.UiBuilder.DisableCutsceneUiHide = this.configuration.ShowInCutscenes;

      this.pluginInterface.UiBuilder.Draw += this.EchoglossianConfigUi;
      this.pluginInterface.UiBuilder.OpenConfigUi += this.ConfigWindow;

      languageInt = this.configuration.Lang;

      fontSize = this.configuration.FontSize;

      this.framework.Update += this.Tick;

      this.TranslationSemaphore = new SemaphoreSlim(1, 1);
      this.talkTranslationSemaphore = new SemaphoreSlim(1, 1);

      this.toastGui.Toast += this.OnToast;
      this.toastGui.ErrorToast += this.OnToast;
      this.toastGui.QuestToast += this.OnToast;

      Common.Functions.Talk.OnTalk += this.GetText;
      Common.Functions.BattleTalk.OnBattleTalk += this.GetBattleText;
      this.pluginInterface.UiBuilder.Draw += this.DrawTranslatedDialogueWindow;
      this.pluginInterface.UiBuilder.Draw += this.DrawTranslatedToastWindow;

    }

    /// <summary>
    ///   Gets or sets AssemblyLocation. When loaded by LivePluginLoader, the executing assembly will be wrong. Supplying this
    ///   property allows LivePluginLoader to supply the correct location, so that you have full compatibility when loaded
    ///   normally and through LPL.
    /// </summary>
    public string AssemblyLocation { get; set; } = Assembly.GetExecutingAssembly().Location;

    private static XivCommonBase Common { get; set; }

    public string Name => Resources.Name;

    /// <inheritdoc />
    public void Dispose()
    {
      GC.SuppressFinalize(this);

      Common.Functions.Talk.OnTalk -= this.GetText;
      Common.Functions.BattleTalk.OnBattleTalk -= this.GetBattleText;
      Common?.Dispose();

      this.toastGui.Toast -= this.OnToast;
      this.toastGui.ErrorToast -= this.OnToast;
      this.toastGui.QuestToast -= this.OnToast;

      this.pluginInterface.UiBuilder.OpenConfigUi -= this.ConfigWindow;

      this.commandManager.RemoveHandler(SlashCommand);

      this.toastGui?.Dispose();
      this.gameGui?.Dispose();

      this.TranslationSemaphore?.Dispose();
      this.talkTranslationSemaphore?.Dispose();

      this.pluginInterface.UiBuilder.Draw -= this.DrawTranslatedDialogueWindow;
      this.pluginInterface.UiBuilder.Draw -= this.DrawTranslatedToastWindow;
      this.pluginInterface.UiBuilder.Draw -= this.EchoglossianConfigUi;

      this.pixImage?.Dispose();

      this.framework.Update -= this.Tick;


      this.pluginInterface?.Dispose();
    }

    private void Tick(Framework tFramework)
    {
      if (this.configuration.UseImGui)
      {
        this.AddonHandlers("Talk", 1);
        this.AddonHandlers("_TextError", 1);
        this.AddonHandlers("_TextError", 2);
        this.AddonHandlers("_WideText", 1);
        this.AddonHandlers("_WideText", 2);
        this.AddonHandlers("_ScreenText", 1);
        this.AddonHandlers("_ScreenText", 2);
        this.AddonHandlers("_AreaText", 1);
        this.AddonHandlers("_AreaText", 2);
      }
      else
      {
        this.addonDisplayTranslation = false;
        this.talkDisplayTranslation = false;
      }
    }

    private unsafe void AddonHandlers(string addonName, int index)
    {
      var addonByName = this.gameGui.GetAddonByName(addonName, index);
      if (addonByName != IntPtr.Zero)
      {
        var addonByNameMaster = (AtkUnitBase*)addonByName;
        if (addonByNameMaster->IsVisible)
        {
          if (addonName == "Talk")
          {
            this.talkDisplayTranslation = true;
            this.talkTextDimensions.X = addonByNameMaster->RootNode->Width * addonByNameMaster->Scale;
            this.talkTextDimensions.Y = addonByNameMaster->RootNode->Height * addonByNameMaster->Scale;
            this.talkTextPosition.X = addonByNameMaster->RootNode->X;
            this.talkTextPosition.Y = addonByNameMaster->RootNode->Y;
          }
          else
          {
            this.addonDisplayTranslation = true;
            this.translationTextDimensions.X = addonByNameMaster->RootNode->Width * addonByNameMaster->Scale;
            this.translationTextDimensions.Y = addonByNameMaster->RootNode->Height * addonByNameMaster->Scale;
            this.translationTextPosition.X = addonByNameMaster->RootNode->X;
            this.translationTextPosition.Y = addonByNameMaster->RootNode->Y;
          }
        }
        else
        {
          this.talkDisplayTranslation = false;
          this.addonDisplayTranslation = false;
        }
      }
      else
      {
        this.addonDisplayTranslation = false;
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