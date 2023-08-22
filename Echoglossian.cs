// <copyright file="Echoglossian.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

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

using FFXIVClientStructs;
using FFXIVClientStructs.Interop;
using ImGuiScene;
using XivCommon;

namespace Echoglossian
{
  // TODO: implement multiple fallback translation engines.
  public partial class Echoglossian : IDalamudPlugin
  {
    [PluginService]
    public static DataManager DManager { get; private set; }

    [PluginService]
    public static DalamudPluginInterface PluginInterface { get; private set; } = null!;

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

    private static XivCommonBase Common { get; set; }

    public string Name => Resources.Name;

    private const string SlashCommand = "/eglo";
    private string configDir;
    private static int languageInt = 28;
    private static int fontSize = 24;
    private static int chosenTransEngine;
    private static string transEngineName;

    private bool PluginAssetsState;
    private static Dictionary<int, LanguageInfo> langDict;
    private bool config;


    private Config configuration;

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

    private readonly TextureWrap pixImage;
    private readonly TextureWrap choiceImage;
    private readonly TextureWrap cutsceneChoiceImage;
    private readonly TextureWrap talkImage;
    private readonly TextureWrap logo;

    private readonly CultureInfo cultureInfo;

    private static Sanitizer sanitizer;

    public List<ToastMessage> ErrorToastsCache { get; set; }

    public List<ToastMessage> QuestToastsCache { get; set; }

    public List<ToastMessage> OtherToastsCache { get; set; }

    public Echoglossian()
    {
      this.configuration = PluginInterface.GetPluginConfig() as Config ?? new Config();

      this.configDir = PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar;

      CommandManager.AddHandler(SlashCommand, new CommandInfo(this.Command)
      {
        HelpMessage = Resources.HelpMessage,
      });

      sanitizer = PluginInterface.Sanitizer as Sanitizer;

      // Resolver.Initialize();
      // Resolver.GetInstance.SetupSearchSpace();
      // Resolver.GetInstance.Resolve(); 

      langDict = this.LanguagesDictionary;
      identifier = Factory.Load($"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Wiki82.profile.xml");

      Common = new XivCommonBase(Hooks.Talk | Hooks.BattleTalk);

      this.CreateOrUseDb();

      this.cultureInfo = new CultureInfo(this.configuration.DefaultPluginCulture);
      this.AssetsPath = $"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}";

      this.AssetFiles.Add("NotoSansCJKhk-Regular.otf");
      this.AssetFiles.Add("NotoSansCJKjp-Regular.otf");
      this.AssetFiles.Add("NotoSansCJKkr-Regular.otf");
      this.AssetFiles.Add("NotoSansCJKsc-Regular.otf");
      this.AssetFiles.Add("NotoSansCJKtc-Regular.otf");

#if DEBUG
      // PluginLog.LogWarning($"Assets state config: {JsonConvert.SerializeObject(this.configuration, Formatting.Indented)}");
#endif
      this.configuration.PluginVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
      if (this.configuration.Version < 5)
      {
        this.FixConfig();
      }

      this.PluginAssetsState = this.configuration.PluginAssetsDownloaded;
#if DEBUG
      PluginLog.LogWarning($"Assets state config: {this.configuration.PluginAssetsDownloaded}");
      PluginLog.LogWarning($"Assets state var: {this.PluginAssetsState}");
#endif
      if (!this.PluginAssetsState)
      {
        this.PluginAssetsChecker();
      }

      PluginInterface.UiBuilder.BuildFonts += this.LoadLanguageComboFont;
      PluginInterface.UiBuilder.BuildFonts += this.LoadFont;

      // this.ListCultureInfos();
      this.pixImage = PluginInterface.UiBuilder.LoadImage(Resources.pix);
      this.choiceImage = PluginInterface.UiBuilder.LoadImage(Resources.choice);
      this.cutsceneChoiceImage = PluginInterface.UiBuilder.LoadImage(Resources.cutscenechoice);
      this.talkImage = PluginInterface.UiBuilder.LoadImage(Resources.prttws);
      this.logo = PluginInterface.UiBuilder.LoadImage(Resources.logo);

      PluginInterface.UiBuilder.DisableCutsceneUiHide = this.configuration.ShowInCutscenes;

      PluginInterface.UiBuilder.OpenConfigUi += this.ConfigWindow;

      languageInt = this.configuration.Lang;

      fontSize = this.configuration.FontSize;

      chosenTransEngine = this.configuration.ChosenTransEngine;

      TransEngines t = (TransEngines)chosenTransEngine;
      transEngineName = t.ToString();

      this.LoadAllErrorToasts();
      this.LoadAllOtherToasts();

      Framework.Update += this.Tick;

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

      ToastGui.Toast += this.OnToast;
      ToastGui.ErrorToast += this.OnErrorToast;
      ToastGui.QuestToast += this.OnQuestToast;

      //this.HandleTalkAsync();

      // Common.Functions.ChatBubbles.OnChatBubble += this.ChatBubblesOnChatBubble;
      // Common.Functions.Tooltips.OnActionTooltip += this.TooltipsOnActionTooltip;
      Common.Functions.Talk.OnTalk += this.GetTalk;
      Common.Functions.BattleTalk.OnBattleTalk += this.GetBattleTalk;
      PluginInterface.UiBuilder.Draw += this.BuildUi;

      /* if (ClientState.IsLoggedIn)
       {
         this.ParseUi();
       }*/
    }

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
      // Common.Functions.ChatBubbles.OnChatBubble -= this.ChatBubblesOnChatBubble;
      // Common.Functions.Tooltips.OnActionTooltip -= this.TooltipsOnActionTooltip;
      Common?.Dispose();

      ToastGui.Toast -= this.OnToast;
      ToastGui.ErrorToast -= this.OnErrorToast;
      ToastGui.QuestToast -= this.OnQuestToast;

      PluginInterface.UiBuilder.OpenConfigUi -= this.ConfigWindow;

      this.nameTranslationSemaphore?.Dispose();
      this.talkTranslationSemaphore?.Dispose();
      this.battleTalkTranslationSemaphore?.Dispose();
      this.senderTranslationSemaphore?.Dispose();

      this.toastTranslationSemaphore?.Dispose();
      this.errorToastTranslationSemaphore?.Dispose();
      this.areaToastTranslationSemaphore?.Dispose();
      this.wideTextToastTranslationSemaphore?.Dispose();
      this.questToastTranslationSemaphore?.Dispose();

      PluginInterface.UiBuilder.Draw -= this.BuildUi;

      this.pixImage?.Dispose();
      this.choiceImage?.Dispose();
      this.cutsceneChoiceImage?.Dispose();
      this.talkImage?.Dispose();

      Framework.Update -= this.Tick;

      PluginInterface.UiBuilder.BuildFonts -= this.LoadFont;
      PluginInterface.UiBuilder.BuildFonts -= this.LoadLanguageComboFont;
      this.glyphRangeMainText?.Free();
      this.glyphRangeConfigText?.Free();
      this.glyphRangeMainText = null;
      this.glyphRangeConfigText = null;

      CommandManager.RemoveHandler(SlashCommand);
    }

    private void Tick(Framework tFramework)
    {
      if (!this.configuration.Translate)
      {
#if DEBUG
        // PluginLog.Log("Translations are disabled!");
#endif
        return;
      }

      switch (this.configuration.UseImGuiForTalk || this.configuration.UseImGuiForBattleTalk ||
              this.configuration.UseImGuiForToasts)
      {
        case true when !this.FontLoaded || this.FontLoadFailed:
          return;
        case true:
          {
            switch (ClientState.IsLoggedIn)
            {
              case true:

                this.TalkHandler("Talk", 1);

                // this.TalkSubtitleHandler("TalkSubtitle", 1);
                this.BattleTalkHandler("_BattleTalk", 1);

                this.TextErrorToastHandler("_TextError", 1);

                this.ToastHandler("_WideText", 1);

                // this.ClassChangeToastHandler("_WideText", 1);
                //
                // this.ClassChangeToastHandler("_WideText", 2);
                this.ToastHandler("_TextClassChange", 1);
                this.ToastHandler("_AreaText", 1);

                // this.QuestToastHandler("_ScreenText", 1);
                break;
            }

            break;
          }

        default:
          //this.DisableAllTranslations();
          break;
      }
    }

    private void BuildUi()
    {
      if (!this.configuration.PluginAssetsDownloaded)
      {
        // this.PluginAssetsChecker();
        return;
      }

      if (!this.LanguageComboFontLoaded && !this.LanguageComboFontLoadFailed)
      {
        PluginInterface.UiBuilder.RebuildFonts();
        return;
      }

      if (!this.FontLoaded && !this.FontLoadFailed)
      {
        PluginInterface.UiBuilder.RebuildFonts();
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

          PluginInterface.UiBuilder.RebuildFonts();
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

      if (this.configuration.UseImGuiForTalk && this.configuration.TranslateTalk && this.talkDisplayTranslation)
      {
        this.DrawTranslatedDialogueWindow();
#if DEBUG
        // PluginLog.LogVerbose("Showing Talk Translation Overlay.");
#endif
      }

#if DEBUG
      /* PluginLog.LogWarning($"Toast Draw Vars: !UseImGuiForToasts - {!this.configuration.UseImGuiForToasts}" +
                    $", TranslateErrorToast - {this.configuration.TranslateErrorToast}" +
                    $", errorToastDisplayTranslation - {this.errorToastDisplayTranslation}" +
                    $" equals? {!this.configuration.UseImGuiForToasts && this.configuration.TranslateErrorToast && this.errorToastDisplayTranslation}");*/
#endif
      if (this.configuration.UseImGuiForToasts && this.configuration.TranslateErrorToast && this.errorToastDisplayTranslation)
      {
        this.DrawTranslatedErrorToastWindow();
#if DEBUG
        // PluginLog.LogWarning("Showing Error Toast Translation Overlay.");
#endif
      }

      if (this.configuration.UseImGuiForToasts && this.configuration.TranslateToast && this.toastDisplayTranslation)
      {
        this.DrawTranslatedToastWindow();
#if DEBUG
        // PluginLog.LogWarning("Showing Error Toast Translation Overlay.");
#endif
      }

      /*if (!this.configuration.UseImGuiForToasts && this.configuration.TranslateClassChangeToast && this.classChangeToastDisplayTranslation)
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
