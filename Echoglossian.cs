// <copyright file="Echoglossian.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.LanguagesHandling;
using Echoglossian.NativeUI.AddonHandlers;
using Echoglossian.NativeUI.Handlers;
using Echoglossian.NativeUI.Helpers;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.Properties;
using Echoglossian.Translators;

namespace Echoglossian
{
  // TODO: implement multiple fallback translation engines.
  public partial class Echoglossian : IDalamudPlugin
  {
    [PluginService]
    public static IDataManager DManager { get; private set; }

    [PluginService]
    public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    public static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    public static IFramework FrameworkInterface { get; private set; } = null!;

    [PluginService]
    public static IGameGui GameGuiInterface { get; private set; } = null!;

    [PluginService]
    public static IChatGui ChatGuiInterface { get; private set; } = null!;

    [PluginService]
    public static IClientState ClientStateInterface { get; private set; } = null!;

    [PluginService]
    public static IToastGui ToastGuiInterface { get; private set; } = null!;

    [PluginService]
    public static IAddonEventManager EventManager { get; private set; } = null!;

    [PluginService]
    public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;

    [PluginService]
    public static IPluginLog PluginLog { get; private set; } = null!;

    [PluginService]
    public static INotificationManager NotificationManager { get; private set; } = null!;

    [PluginService]
    public static ITextureProvider TextureProvider { get; private set; } = null!;

    public string Name => Resources.Name;

    private const string SlashCommand = "/eglo";
    private string configDir;
    public static int LanguageInt = 28;
    private static int fontSize = 24;
    public static int ChosenTransEngine;
    private static string transEngineName;

    public static string ScriptCharList { get; set; }

    public static string SpecialFontFilePath { get; set; }

    public static string FontFilePath { get; set; }

    public static string SymbolsFontFilePath { get; set; }

    public static string DummyFontFilePath { get; set; }

    public static string LangComboFontFilePath { get; set; }

    public static string ComplementaryFont3FilePath { get; set; }

    public static string ComplementaryFont4FilePath { get; set; }

    public static string ComplementaryFont5FilePath { get; set; }

    public static string ComplementaryFont6FilePath { get; set; }

    public static string ComplementaryFont7FilePath { get; set; }

    public string LangToTranslateTo = string.Empty;

    private bool pluginAssetsState;
    public static Dictionary<int, LanguageInfo> LangDict;
    private bool config;

    private Config configuration;

    public static UINewFontHandler UINewFontHandler;

    public static LanguageInfo SelectedLanguage { get; set; }

    private readonly SemaphoreSlim toastTranslationSemaphore;
    private readonly SemaphoreSlim talkTranslationSemaphore;
    private readonly SemaphoreSlim nameTranslationSemaphore;
    private readonly SemaphoreSlim battleTalkTranslationSemaphore;
    private readonly SemaphoreSlim talkSubtitleTranslationSemaphore;
    private readonly SemaphoreSlim senderTranslationSemaphore;
    private readonly SemaphoreSlim errorToastTranslationSemaphore;
    private readonly SemaphoreSlim classChangeToastTranslationSemaphore;
    private readonly SemaphoreSlim areaToastTranslationSemaphore;
    private readonly SemaphoreSlim wideTextToastTranslationSemaphore;
    private readonly SemaphoreSlim questToastTranslationSemaphore;

    private readonly IDalamudTextureWrap pixImage;
    private readonly IDalamudTextureWrap choiceImage;
    private readonly IDalamudTextureWrap cutsceneChoiceImage;
    private readonly IDalamudTextureWrap talkImage;
    private readonly IDalamudTextureWrap logo;

    private readonly CultureInfo cultureInfo;

    public static Sanitizer Sanitizer;

    private AtkTextNodeBufferWrapper atkTextNodeBufferWrapper;

    /// <summary>
    /// List of registered addon handlers for the plugin.
    /// </summary>
    private List<(string AddonName, IAddonTranslationHandler Handler)> registeredAddonHandlers;


    public static TranslationService TranslationService;

    // private CharacterWindowHandler characterWindowHandler;

    public List<ToastMessage> ErrorToastsCache { get; set; }

    public List<ToastMessage> QuestToastsCache { get; set; }

    public List<ToastMessage> OtherToastsCache { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Echoglossian"/> class.
    /// </summary>
    public Echoglossian()
    {
      this.configuration = PluginInterface.GetPluginConfig() as Config ?? new Config();

      this.configDir = PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar;

      CommandManager.AddHandler(SlashCommand, new CommandInfo(this.Command)
      {
        HelpMessage = Resources.HelpMessage,
      });

      Sanitizer = PluginInterface.Sanitizer as Sanitizer;

      LangDict = this.languagesDictionary;

      LanguageEngineSupport.ApplySupportTo(LangDict);

      /*      identifier = Factory.Load($"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Wiki82.profile.xml");*/

      try
      {
        this.CreateOrUseDb();
      }
      catch (Exception e)
      {
        PluginLog.Error($"Error creating or using database: {e}");
      }
      finally
      {
        PluginLog.Debug("Eglo database created or used successfully.");
      }

      this.cultureInfo = new CultureInfo(this.configuration.DefaultPluginCulture);
      AssetsManager.AssetsPath = $"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}";
      AssetsManager.AssetFiles = new List<string>
{
  "NotoSansCJKhk-Regular.otf",
  "NotoSansCJKjp-Regular.otf",
  "NotoSansCJKkr-Regular.otf",
  "NotoSansCJKsc-Regular.otf",
  "NotoSansCJKtc-Regular.otf",
};

      ComplementaryFont3FilePath = $"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSansJP-VF-3.ttf";
      ComplementaryFont4FilePath = $"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSansJP-VF-4.ttf";
      ComplementaryFont5FilePath = $"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSansJP-VF-5.ttf";
      ComplementaryFont6FilePath = $"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSansJP-VF-6.ttf";
      ComplementaryFont7FilePath = $"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSansJP-VF-7.ttf";

#if DEBUG
      // PluginLog.Debug($"Assets state config: {JsonConvert.SerializeObject(this.configuration, Formatting.Indented)}");
#endif
      this.configuration.PluginVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
      if (this.configuration.Version < 5)
      {
        this.FixConfig();
      }

      this.pluginAssetsState = this.configuration.PluginAssetsDownloaded;
#if DEBUG
      PluginLog.Debug($"Assets state config: {this.configuration.PluginAssetsDownloaded}");
      PluginLog.Debug($"Assets state var: {this.pluginAssetsState}");
#endif
      if (!this.pluginAssetsState)
      {
        AssetsManager.PluginAssetsChecker();
      }

      SelectedLanguage = this.languagesDictionary[this.configuration.Lang];

      // this.ListCultureInfos();
      this.pixImage = TextureProvider.CreateFromImageAsync(Resources.pix).Result;
      this.choiceImage = TextureProvider.CreateFromImageAsync(Resources.choice).Result;
      this.cutsceneChoiceImage = TextureProvider.CreateFromImageAsync(Resources.cutscenechoice).Result;
      this.talkImage = TextureProvider.CreateFromImageAsync(Resources.prttws).Result;
      this.logo = TextureProvider.CreateFromImageAsync(Resources.logo).Result;

      PluginInterface.UiBuilder.DisableCutsceneUiHide = this.configuration.ShowInCutscenes;

      PluginInterface.UiBuilder.OpenConfigUi += this.ConfigWindow;

      LanguageInt = this.configuration.Lang;

      fontSize = this.configuration.FontSize;

      ChosenTransEngine = this.configuration.ChosenTransEngine;

      this.LangToTranslateTo = LangDict[LanguageInt].Code;

      MountFontPaths();

      UINewFontHandler = new UINewFontHandler(this.configuration);

      TransEngines t = (TransEngines)ChosenTransEngine;
      transEngineName = t.ToString();
      TranslationService = new TranslationService(this.configuration, PluginLog, Sanitizer);

      this.atkTextNodeBufferWrapper = new AtkTextNodeBufferWrapper();

      // this.characterWindowHandler = new CharacterWindowHandler(this.configuration, TranslationService);

      this.LoadAllErrorToasts();
      this.LoadAllOtherToasts();

      FrameworkInterface.Update += this.Tick;

      this.talkTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.nameTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.battleTalkTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.senderTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.talkSubtitleTranslationSemaphore = new SemaphoreSlim(1, 1);

      this.toastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.errorToastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.classChangeToastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.areaToastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.wideTextToastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.questToastTranslationSemaphore = new SemaphoreSlim(1, 1);

      ToastGuiInterface.Toast += this.OnToast;
      ToastGuiInterface.ErrorToast += this.OnErrorToast;
      ToastGuiInterface.QuestToast += this.OnQuestToast;

      this.EgloAddonHandler();

      this.RegisterOverlays();

      PluginInterface.UiBuilder.Draw += this.BuildUi;

      /* if (ClientStateInterface.IsLoggedIn)
       {
         this.ParseUi();
       }*/

      // Disabling BattleTalk translation by default if the language is not supported by the game font while we fix the overlays
      this.configuration.TranslateBattleTalk = this.configuration.OverlayOnlyLanguage ? false : true;
      this.configuration.UseImGuiForBattleTalk = false;

      // Fix wrong chatgpt base url in v3.17
      // TODO: remove it in later versions
      if (this.configuration.ChatGPTBaseUrl == "https://api.openai.com/v1/chat/completions")
      {
        this.configuration.ChatGPTBaseUrl = "https://api.openai.com/v1";
        PluginInterface.SavePluginConfig(this.configuration);
      }
    }

    /// <inheritdoc />
    public void Dispose()
    {
      this.Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
      ToastGuiInterface.Toast -= this.OnToast;
      ToastGuiInterface.ErrorToast -= this.OnErrorToast;
      ToastGuiInterface.QuestToast -= this.OnQuestToast;

      PluginInterface.UiBuilder.OpenConfigUi -= this.ConfigWindow;

      this.nameTranslationSemaphore?.Dispose();
      this.talkTranslationSemaphore?.Dispose();
      this.battleTalkTranslationSemaphore?.Dispose();
      this.senderTranslationSemaphore?.Dispose();
      this.talkSubtitleTranslationSemaphore?.Dispose();
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
      this.logo?.Dispose();

      this.talkOverlay.Dispose();
      this.battleTalkOverlay.Dispose();
      this.talkSubtitleOverlay.Dispose();
      this.toastOverlay.Dispose();
      this.errorToastOverlay.Dispose();
      this.chatBubbleOverlay.Dispose();

      /*if (this.configuration.TranslateCharacterWindow)
      {
        AddonLifecycle.UnregisterListener(AddonEvent.PreSetup, "Character", this.characterWindowHandler.ExtractAndTranslateValues);
        AddonLifecycle.UnregisterListener(AddonEvent.PreRefresh, "Character", this.characterWindowHandler.ApplyTranslatedValues);
        AddonLifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, "Character", this.characterWindowHandler.ApplyTranslatedValues);
      }*/

      if (disposing && this.registeredAddonHandlers != null)
      {
        AddonHandlerRegistrar.UnregisterMany(this.registeredAddonHandlers, AddonLifecycle);
      }

      if (this.configuration.TranslateTalk)
      {
        AddonLifecycle.UnregisterListener(AddonEvent.PreRefresh, "Talk", this.UiTalkAsyncHandler);
        AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, "Talk", this.UiTalkAsyncHandler);
        AddonLifecycle.UnregisterListener(AddonEvent.PreReceiveEvent, "Talk", this.UiTalkAsyncHandler);
      }

      if (this.configuration.TranslateBattleTalk)
      {
        AddonLifecycle.UnregisterListener(AddonEvent.PreRefresh, "_BattleTalk", this.UiBattleTalkAsyncHandler);
        AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, "_BattleTalk", this.UiBattleTalkAsyncHandler);
        AddonLifecycle.UnregisterListener(AddonEvent.PreReceiveEvent, "_BattleTalk", this.UiBattleTalkAsyncHandler);
      }

      if (this.configuration.TranslateTalkSubtitle)
      {
        AddonLifecycle.UnregisterListener(AddonEvent.PreSetup, "TalkSubtitle", this.UiTalkSubtitleAsyncHandler);
        AddonLifecycle.UnregisterListener(AddonEvent.PreRefresh, "TalkSubtitle", this.UiTalkSubtitleAsyncHandler);
      }

      if (this.configuration.TranslateJournal)
      {
        AddonLifecycle.UnregisterListener(AddonEvent.PreSetup, "JournalResult", this.UiJournalResultHandler);
        AddonLifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, "RecommendList", this.UiRecommendListHandler);
        AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "RecommendList", this.UiRecommendListHandlerAsync);
        AddonLifecycle.UnregisterListener(AddonEvent.PreRefresh, "AreaMap", this.UiAreaMapHandler);
        AddonLifecycle.UnregisterListener(AddonEvent.PreRefresh, "ScenarioTree", this.UiScenarioTreeHandler);
        AddonLifecycle.UnregisterListener(AddonEvent.PreUpdate, "Journal", this.UiJournalQuestHandler);
        AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "Journal", this.UiJournalDetailHandler);
        AddonLifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, "JournalDetail", this.UiJournalDetailHandler);
        AddonLifecycle.UnregisterListener(AddonEvent.PreSetup, "JournalAccept", this.UiJournalAcceptHandler);
        AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_ToDoList", this.UiToDoListHandler);
      }

      FrameworkInterface.Update -= this.Tick;

      this.GlyphRangeConfigText?.Free();
      this.GlyphRangeMainText = null;
      this.GlyphRangeConfigText = null;

      CommandManager.RemoveHandler(SlashCommand);
    }

    private void Tick(IFramework tFramework)
    {
      if (!this.configuration.Translate)
      {
        return;
      }

      switch (this.configuration.UseImGuiForTalk || this.configuration.UseImGuiForBattleTalk ||
              this.configuration.UseImGuiForToasts)
      {
        case true when !this.FontLoaded || this.FontLoadFailed:
          return;
        case true:
          {
            switch (ClientStateInterface.IsLoggedIn)
            {
              case true:
                this.TextErrorToastHandler("_TextError", 1);
                this.ToastHandler("_WideText", 1);
                this.ToastHandler("_TextClassChange", 1);
                this.ToastHandler("_AreaText", 1);
                break;
            }

            break;
          }

        default:
          // this.DisableAllTranslations();
          break;
      }
    }

    /// <summary>
    /// Builds the UI for the plugin.
    /// </summary>
    private void BuildUi()
    {
      if (!this.configuration.PluginAssetsDownloaded)
      {
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
        }
      }

      if (!this.configuration.Translate)
      {
        return;
      }

      foreach (var overlayRegistration in this.registeredOverlays)
      {
        overlayRegistration.Overlay.Semaphore.Wait();
        bool shouldDisplay = overlayRegistration.Overlay.Display;
        overlayRegistration.Overlay.Semaphore.Release();

        if (!shouldDisplay)
        {
          continue;
        }

        // Title is now resolved inside DrawTranslationWindow, so no need to pass customTitle
        this.DrawTranslationWindow(
            overlayRegistration.Overlay,
            overlayRegistration.Config);
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

    private void EgloAddonHandler()
    {
      PluginLog.Debug("EgloAddonHandler called.");

      /*      if (ClientStateInterface.IsLoggedIn && !this.GatheringCharacterWindowAtkValuesComplete)
            {
              this.TranslateCharacterWindow();
            }*/

      this.registeredAddonHandlers = new List<(string AddonName, IAddonTranslationHandler Handler)>
      {
        (AddonName: "Character", Handler: new CharacterWindowHandler(this.configuration, TranslationService)),
      };

      AddonHandlerRegistrar.RegisterMany(this.registeredAddonHandlers, AddonLifecycle);

      /*if (this.configuration.TranslateCharacterWindow)
      {
        PluginLog.Debug("Registering CharacterWindowHandler listeners.");
        AddonLifecycle.RegisterListener(AddonEvent.PreSetup, "Character", this.characterWindowHandler.ExtractAndTranslateValues);
        AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, "Character", this.characterWindowHandler.ApplyTranslatedValues);
        AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, "Character", this.characterWindowHandler.ApplyTranslatedValues);
      }*/

      if (this.configuration.TranslateTalk)
      {
        PluginLog.Debug("Registering Talk addon listeners for translation.");
        // this.EgloNeutralAddonHandler("Talk", new string[] {  /* "PreUpdate", "PostUpdate",*/ "PreDraw",/* "PostDraw",  "PreReceiveEvent", "PostReceiveEvent", "PreRequestedUpdate", "PostRequestedUpdate" ,*/ "PreRefresh",/* "PostRefresh"*/ });
        AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, "Talk", this.UiTalkAsyncHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "Talk", this.UiTalkAsyncHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, "Talk", this.UiTalkAsyncHandler);
      }

      if (this.configuration.TranslateBattleTalk)
      {
        PluginLog.Debug("Registering _BattleTalk addon listeners for translation.");
        // this.EgloNeutralAddonHandler("_BattleTalk", new string[] { /* "PreUpdate", "PostUpdate",*/ "PreDraw",/* "PostDraw",  "PreReceiveEvent", "PostReceiveEvent", "PreRequestedUpdate", "PostRequestedUpdate" ,*/ "PreRefresh",/* "PostRefresh"*/});
        AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, "_BattleTalk", this.UiBattleTalkAsyncHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "_BattleTalk", this.UiBattleTalkAsyncHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, "_BattleTalk", this.UiBattleTalkAsyncHandler);
      }

      if (this.configuration.TranslateTalkSubtitle)
      {
        PluginLog.Debug("Registering TalkSubtitle addon listeners for translation.");
        // this.EgloNeutralAddonHandler("TalkSubtitle", new string[] {/* "PreUpdate", "PostUpdate",*/ "PreDraw",/* "PostDraw",  "PreReceiveEvent", "PostReceiveEvent", "PreRequestedUpdate", "PostRequestedUpdate" ,*/ "PreRefresh",/* "PostRefresh"*/});
        AddonLifecycle.RegisterListener(AddonEvent.PreSetup, "TalkSubtitle", this.UiTalkSubtitleAsyncHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, "TalkSubtitle", this.UiTalkSubtitleAsyncHandler);
      }

      if (this.configuration.TranslateJournal)
      {
        PluginLog.Debug("Registering Journal addon listeners for translation.");
        AddonLifecycle.RegisterListener(AddonEvent.PreSetup, "JournalResult", this.UiJournalResultHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "RecommendList", this.UiRecommendListHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "RecommendList", this.UiRecommendListHandlerAsync);
        AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, "AreaMap", this.UiAreaMapHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, "ScenarioTree", this.UiScenarioTreeHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreUpdate, "Journal", this.UiJournalQuestHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "Journal", this.UiJournalDetailHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, "JournalDetail", this.UiJournalDetailHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreSetup, "JournalAccept", this.UiJournalAcceptHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_ToDoList", this.UiToDoListHandler);
      }

      /*"PreSetup","PostSetup", "PreUpdate", "PostUpdate", "PreDraw", "PostDraw", "PreFinalize", "PreReceiveEvent", "PostReceiveEvent", "PreRequestedUpdate", "PostRequestedUpdate", "PreRefresh", "PostRefresh" */
    }
  }
}
