// <copyright file="Echoglossian.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.Sanitizer;
using Dalamud.IoC;
using Dalamud.Plugin;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Properties;
using FFXIVClientStructs;
using ImGuiScene;
using XivCommon;

namespace Echoglossian
{
  // TODO: implement multiple fallback translation engines.
  public partial class Echoglossian : IDalamudPlugin
  {
    private const string SlashCommand = "/eglo";
    private string configDir;
    private static int languageInt = 16;
    private static int fontSize = 20;
    private static int chosenTransEngine;
    private static string transEngineName;
    private static Dictionary<int, Language> langDict;

    private readonly CommandManager commandManager;
    private bool config;

    private readonly Config configuration;

    private readonly Framework framework;
    private readonly GameGui gameGui;
    private readonly ClientState ci;

    private readonly SemaphoreSlim toastTranslationSemaphore;
    private readonly SemaphoreSlim talkTranslationSemaphore;
    private readonly SemaphoreSlim nameTranslationSemaphore;
    private readonly SemaphoreSlim battleTalkTranslationSemaphore;
    private readonly SemaphoreSlim senderTranslationSemaphore;
    private readonly SemaphoreSlim errorToastTranslationSemaphore;
    private readonly SemaphoreSlim classChangeToastTranslationSemaphore;
    private readonly SemaphoreSlim areaToastTranslationSemaphore;
    private readonly SemaphoreSlim wideTextToastTranslationSemaphore;
    private readonly SemaphoreSlim questToastTranslationSemaphore;

    private readonly DalamudPluginInterface pluginInterface;
    private readonly TextureWrap pixImage;
    private readonly TextureWrap choiceImage;
    private readonly TextureWrap cutsceneChoiceImage;
    private readonly TextureWrap talkImage;
    private readonly TextureWrap logo;
    private readonly ToastGui toastGui;
    private readonly CultureInfo cultureInfo;

    private static Sanitizer sanitizer;

    public List<ToastMessage> ErrorToastsCache { get; set; }

    /// <summary>
    ///   Initializes a new instance of the <see cref="Echoglossian" /> class.
    /// </summary>
    /// <param name="dalamudPluginInterface">Plugin Interface.</param>
    /// <param name="pframework">Framework.</param>
    /// <param name="pCommandManager">Command Manager.</param>
    /// <param name="pGameGui">Game Gui object.</param>
    /// <param name="pToastGui">Toast Gui Object.</param>
    /// <param name="clientState">This class represents the state of the game client at the time of access.</param>
    public Echoglossian(
      [RequiredVersion("1.0")] DalamudPluginInterface dalamudPluginInterface,
      Framework pframework,
      [RequiredVersion("1.0")] CommandManager pCommandManager,
      GameGui pGameGui,
      ToastGui pToastGui,
      ClientState clientState)
    {
      this.framework = pframework;
      this.ci = clientState;

      this.pluginInterface = dalamudPluginInterface;

      this.configuration = this.pluginInterface.GetPluginConfig() as Config ?? new Config();

      this.FixConfig();

      this.configDir = this.pluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar;

      sanitizer = this.pluginInterface.Sanitizer as Sanitizer;

      langDict = this.LanguagesDictionary;

      this.cultureInfo = new CultureInfo(this.configuration.DefaultPluginCulture);

      this.commandManager = pCommandManager;
      this.commandManager.AddHandler(SlashCommand, new CommandInfo(this.Command)
      {
        HelpMessage = Resources.HelpMessage,
      });

      this.gameGui = pGameGui;

      this.toastGui = pToastGui;

      Common = new XivCommonBase(Hooks.Talk | Hooks.BattleTalk | Hooks.ChatBubbles | Hooks.Tooltips);

      Resolver.Initialize();

      this.CreateOrUseDb();

      this.pluginInterface.UiBuilder.BuildFonts += this.LoadConfigFont;
      this.pluginInterface.UiBuilder.BuildFonts += this.LoadFont;

      // this.ListCultureInfos();
      this.pixImage = this.pluginInterface.UiBuilder.LoadImage(Resources.pix);
      this.choiceImage = this.pluginInterface.UiBuilder.LoadImage(Resources.choice);
      this.cutsceneChoiceImage = this.pluginInterface.UiBuilder.LoadImage(Resources.cutscenechoice);
      this.talkImage = this.pluginInterface.UiBuilder.LoadImage(Resources.prttws);
      this.logo = this.pluginInterface.UiBuilder.LoadImage(Resources.logo);

      this.pluginInterface.UiBuilder.DisableCutsceneUiHide = this.configuration.ShowInCutscenes;

      this.pluginInterface.UiBuilder.OpenConfigUi += this.ConfigWindow;

      languageInt = this.configuration.Lang;

      fontSize = this.configuration.FontSize;

      chosenTransEngine = this.configuration.ChosenTransEngine;

      TransEngines t = (TransEngines)chosenTransEngine;
      transEngineName = t.ToString();

      identifier = Factory.Load($"{this.pluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Wiki82.profile.xml");

      this.LoadAllErrorToasts();

      this.framework.Update += this.Tick;

      this.talkTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.nameTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.battleTalkTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.senderTranslationSemaphore = new SemaphoreSlim(1, 1);

      this.toastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.errorToastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.classChangeToastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.areaToastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.wideTextToastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.questToastTranslationSemaphore = new SemaphoreSlim(1, 1);

      this.toastGui.Toast += this.OnToast;
      this.toastGui.ErrorToast += this.OnErrorToast;
      this.toastGui.QuestToast += this.OnQuestToast;

      // Common.Functions.ChatBubbles.OnChatBubble += this.ChatBubblesOnOnChatBubble;
      // Common.Functions.Tooltips.OnActionTooltip += this.TooltipsOnActionTooltip;

      Common.Functions.Talk.OnTalk += this.GetTalk;
      Common.Functions.BattleTalk.OnBattleTalk += this.GetBattleTalk;
      this.pluginInterface.UiBuilder.Draw += this.BuildUi;
    }

    private static XivCommonBase Common { get; set; }

    public string Name => Resources.Name;

    private Task GameTask;

    /// <inheritdoc />
    public void Dispose()
    {
      this.Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
      Common.Functions.Talk.OnTalk -= this.GetTalk;
      Common.Functions.BattleTalk.OnBattleTalk -= this.GetBattleTalk;
      // Common.Functions.ChatBubbles.OnChatBubble -= this.ChatBubblesOnOnChatBubble;
      // Common.Functions.Tooltips.OnActionTooltip -= this.TooltipsOnActionTooltip;
      Common?.Functions.Dispose();
      Common?.Dispose();

      this.toastGui.Toast -= this.OnToast;
      this.toastGui.ErrorToast -= this.OnErrorToast;
      this.toastGui.QuestToast -= this.OnQuestToast;

      this.pluginInterface.UiBuilder.OpenConfigUi -= this.ConfigWindow;

      this.nameTranslationSemaphore?.Dispose();
      this.talkTranslationSemaphore?.Dispose();
      this.battleTalkTranslationSemaphore?.Dispose();
      this.senderTranslationSemaphore?.Dispose();

      this.toastTranslationSemaphore?.Dispose();
      this.errorToastTranslationSemaphore?.Dispose();
      this.areaToastTranslationSemaphore?.Dispose();
      this.wideTextToastTranslationSemaphore?.Dispose();
      this.questToastTranslationSemaphore?.Dispose();

      this.pluginInterface.UiBuilder.Draw -= this.BuildUi;

      this.pixImage?.Dispose();
      this.choiceImage?.Dispose();
      this.cutsceneChoiceImage?.Dispose();
      this.talkImage?.Dispose();

      this.framework.Update -= this.Tick;

      this.pluginInterface.UiBuilder.BuildFonts -= this.LoadFont;
      this.pluginInterface.UiBuilder.BuildFonts -= this.LoadConfigFont;

      this.pluginInterface.UiBuilder.RebuildFonts();

      this.commandManager.RemoveHandler(SlashCommand);
    }

    private void Tick(Framework tFramework)
    {
      switch (this.configuration.UseImGui || this.configuration.UseImGuiForBattleTalk || !this.configuration.DoNotUseImGuiForToasts)
      {
        case true when !this.FontLoaded || this.FontLoadFailed:
          return;
        case true:
          {
            switch (this.ci.IsLoggedIn)
            {
              case true:
#if DEBUG
                // PluginLog.LogVerbose("Monitoring Framework Tick");
#endif
                this.TalkHandler("Talk", 1);

                // this.TalkSubtitleHandler("TalkSubtitle", 1);

                this.BattleTalkHandler("_BattleTalk", 1);

                this.TextErrorToastHandler("_TextError", 1);

                this.WideTextToastHandler("_WideText", 1);

                // this.ClassChangeToastHandler("_WideText", 1);
                //
                // this.ClassChangeToastHandler("_WideText", 2);

                // this.ClassChangeToastHandler("_TextClassChange", 1);
                this.AreaToastHandler("_AreaText", 1);

                this.QuestToastHandler("_ScreenText", 1);

                break;
            }

            break;
          }

        default:
          this.toastDisplayTranslation = false;
          this.questToastDisplayTranslation = false;
          this.wideTextToastDisplayTranslation = false;
          this.errorToastDisplayTranslation = false;
          this.areaToastDisplayTranslation = false;
          this.classChangeToastDisplayTranslation = false;
          this.talkDisplayTranslation = false;
          this.battleTalkDisplayTranslation = false;
          break;
      }
    }

    private void BuildUi()
    {
      if (!this.ConfigFontLoaded && !this.ConfigFontLoadFailed)
      {
        this.pluginInterface.UiBuilder.RebuildFonts();
        return;
      }

      if (!this.FontLoaded && !this.FontLoadFailed)
      {
        this.pluginInterface.UiBuilder.RebuildFonts();
        return;
      }

      if (this.config)
      {
        this.EchoglossianConfigUi();
      }

      if (this.configuration.FontChangeTime > 0)
      {
        if (DateTime.Now.Ticks - 10000000 > this.configuration.FontChangeTime)
        {
          this.configuration.FontChangeTime = 0;
          this.FontLoadFailed = false;

          this.ReloadFont();
        }
      }

      if (!this.configuration.Translate)
      {
        return;
      }

      if (this.configuration.UseImGuiForBattleTalk && this.configuration.TranslateBattleTalk && this.battleTalkDisplayTranslation)
      {
        this.DrawTranslatedBattleDialogueWindow();
#if DEBUG
        // PluginLog.LogVerbose("Showing BattleTalk Translation Overlay.");
#endif
      }

      if (this.configuration.UseImGui && this.configuration.TranslateTalk && this.talkDisplayTranslation)
      {
        this.DrawTranslatedDialogueWindow();
#if DEBUG
        // PluginLog.LogVerbose("Showing Talk Translation Overlay.");
#endif
      }

#if DEBUG
      /* PluginLog.LogWarning($"Toast Draw Vars: !DoNotUseImGuiForToasts - {!this.configuration.DoNotUseImGuiForToasts}" +
                    $", TranslateErrorToast - {this.configuration.TranslateErrorToast}" +
                    $", errorToastDisplayTranslation - {this.errorToastDisplayTranslation}" +
                    $" equals? {!this.configuration.DoNotUseImGuiForToasts && this.configuration.TranslateErrorToast && this.errorToastDisplayTranslation}");*/
#endif
      if (!this.configuration.DoNotUseImGuiForToasts && this.configuration.TranslateErrorToast && this.errorToastDisplayTranslation)
      {
        this.DrawTranslatedErrorToastWindow();
#if DEBUG
        // PluginLog.LogWarning("Showing Error Toast Translation Overlay.");
#endif
      }

      /*if (!this.configuration.DoNotUseImGuiForToasts && this.configuration.TranslateClassChangeToast && this.classChangeToastDisplayTranslation)
      {
        this.DrawTranslatedClassChangeToastWindow();
        *//*#if DEBUG
                PluginLog.LogWarning("Showing Error Toast Translation Overlay.");
        #endif*//*
      }*/
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
