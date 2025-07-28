// <copyright file="Echoglossian.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;

namespace Echoglossian;

/// <summary>
///     Represents the Echoglossian plugin, which provides translation services and
///     UI enhancements for Dalamud-based applications. This class integrates
///     various plugin services and manages translation operations for different UI
///     components.
/// </summary>
/// <remarks>
///     The Echoglossian class is responsible for initializing and managing
///     the plugin's configuration, handling commands, and registering UI overlays
///     and translation handlers. It uses several Dalamud services to interact
///     with the game client and perform translations. Ensure that all required
///     services are properly initialized before using this class.
/// </remarks>
public partial class Echoglossian : IDalamudPlugin
{
  private const string SlashCommand = "/eglo";
  public static int LanguageInt = 28;
  private static int fontSize = 24;
  public static int ChosenTransEngine;
  private static string transEngineName;
  public static Dictionary<int, LanguageInfo> LangDict;

  public static UINewFontHandler UINewFontHandler;

  public static Sanitizer Sanitizer;

  /// <summary>
  ///     Provides access to the translation service for converting text between
  ///     different languages.
  /// </summary>
  /// <remarks>
  ///     This static field holds an instance of the
  ///     <see cref="TranslationService" /> class, which can be used to perform
  ///     language translations. Ensure that the service is properly initialized
  ///     before use.
  /// </remarks>
  public static TranslationService TranslationService;
  public static string ConfigDirectory;

  private readonly SemaphoreSlim areaToastTranslationSemaphore;
  private readonly SemaphoreSlim battleTalkTranslationSemaphore;
  private readonly IDalamudTextureWrap choiceImage;
  private readonly SemaphoreSlim classChangeToastTranslationSemaphore;

  private readonly Config configuration;

  private readonly CultureInfo cultureInfo;
  private readonly IDalamudTextureWrap cutsceneChoiceImage;
  private readonly SemaphoreSlim errorToastTranslationSemaphore;
  private readonly IDalamudTextureWrap logo;
  private readonly SemaphoreSlim nameTranslationSemaphore;

  private readonly IDalamudTextureWrap pixImage;

  private readonly bool pluginAssetsState;
  private readonly SemaphoreSlim questToastTranslationSemaphore;
  private readonly SemaphoreSlim senderTranslationSemaphore;
  private readonly IDalamudTextureWrap talkImage;
  private readonly SemaphoreSlim talkSubtitleTranslationSemaphore;
  private readonly SemaphoreSlim talkTranslationSemaphore;

  private readonly SemaphoreSlim toastTranslationSemaphore;
  private readonly SemaphoreSlim wideTextToastTranslationSemaphore;

  private AtkTextNodeBufferWrapper atkTextNodeBufferWrapper;
  private bool config;

  public string LangToTranslateTo = string.Empty;

  /// <summary>
  ///     List of registered addon handlers for the plugin.
  /// </summary>
  private List<(string AddonName, IAddonTranslationHandler Handler)>
      registeredAddonHandlers;

  /// <summary>
  ///     Initializes a new instance of the <see cref="Echoglossian" /> class.
  /// </summary>
  public Echoglossian()
  {
    this.configuration = PluginInterface.GetPluginConfig() as Config ??
                         new Config();

    ConfigDirectory = PluginInterface.GetPluginConfigDirectory() +
                      Path.DirectorySeparatorChar;

    CommandManager.AddHandler(
        SlashCommand,
        new CommandInfo(this.Command)
        {
          HelpMessage = Resources.HelpMessage,
        });

    Sanitizer = PluginInterface.Sanitizer as Sanitizer;

    LangDict = this.languagesDictionary;

    LanguageEngineSupport.ApplySupportTo(LangDict);

    try
    {
      this.CreateOrUseDb();
      PluginLog.Debug("Eglo database created or used successfully.");
    }
    catch (Exception e)
    {
      PluginLog.Error($"Error creating or using database: {e}");
    }

    this.cultureInfo =
        new CultureInfo(this.configuration.DefaultPluginCulture);
    AssetsManager.AssetsPath =
        $"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}";
    AssetsManager.AssetFiles = new List<string>
        {
            "NotoSansCJKhk-Regular.otf",
            "NotoSansCJKjp-Regular.otf",
            "NotoSansCJKkr-Regular.otf",
            "NotoSansCJKsc-Regular.otf",
            "NotoSansCJKtc-Regular.otf",
        };

    ComplementaryFont3FilePath =
        $"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSansJP-VF-3.ttf";
    ComplementaryFont4FilePath =
        $"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSansJP-VF-4.ttf";
    ComplementaryFont5FilePath =
        $"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSansJP-VF-5.ttf";
    ComplementaryFont6FilePath =
        $"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSansJP-VF-6.ttf";
    ComplementaryFont7FilePath =
        $"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSansJP-VF-7.ttf";

    this.configuration.PluginVersion = Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString();
    if (this.configuration.Version < 5)
    {
      this.FixConfig();
    }

    this.pluginAssetsState = this.configuration.PluginAssetsDownloaded;

    PluginLog.Debug(
        $"Assets state config: {this.configuration.PluginAssetsDownloaded}");
    PluginLog.Debug($"Assets state var: {this.pluginAssetsState}");

    if (!this.pluginAssetsState)
    {
      AssetsManager.PluginAssetsChecker();
    }

    SelectedLanguage = this.languagesDictionary[this.configuration.Lang];

    // this.ListCultureInfos();
    this.pixImage =
        TextureProvider.CreateFromImageAsync(Resources.pix).Result;
    this.choiceImage = TextureProvider
        .CreateFromImageAsync(Resources.choice).Result;
    this.cutsceneChoiceImage = TextureProvider
        .CreateFromImageAsync(Resources.cutscenechoice).Result;
    this.talkImage = TextureProvider.CreateFromImageAsync(Resources.prttws)
        .Result;
    this.logo = TextureProvider.CreateFromImageAsync(Resources.logo).Result;

    PluginInterface.UiBuilder.DisableCutsceneUiHide =
        this.configuration.ShowInCutscenes;

    PluginInterface.UiBuilder.OpenConfigUi += this.ConfigWindow;

    LanguageInt = this.configuration.Lang;

    fontSize = this.configuration.FontSize;

    ChosenTransEngine = this.configuration.ChosenTransEngine;

    this.LangToTranslateTo = LangDict[LanguageInt].Code;

    MountFontPaths();

    UINewFontHandler = new UINewFontHandler(this.configuration);

    var t = (TransEngines)ChosenTransEngine;
    transEngineName = t.ToString();
    TranslationService = new TranslationService(
        this.configuration,
        PluginLog,
        Sanitizer);

    this.atkTextNodeBufferWrapper = new AtkTextNodeBufferWrapper();

    this.LoadAllErrorToasts();
    this.LoadAllOtherToasts();

    var arraysToBlock = new List<string> { "ChatLog", "CharaSelect", "PartyList", "NamePlate", "ActionBar", "Inventory", "CharacterItems", "Trade", "PartyMemberList", "LinkShell", "BlackList", "FriendList", "Letter", "SocialList", "EnemyList", "CastBar", "Journal", "RecipeNote", "FlyText", "InventoryRetainer", "MiniTalk", "CommonCurrencies", "ItemSearch", "ArmouryBoard", "FreeCompanyMember", "HousingBlackListSetting", "LegacyItemStorage", "FreeCompanyApplication", "GearSetList", "FreeCompanyRights", "CabinetStore", "CabinetWithdraw", "FreeCompanyActivity", "FreeCompanyExchange", "FreeCompanyStatus", "AreaMap2", "ContentsFinderConfirm", "FreeCompanyChest", "Buddy", "FreeCompanyAction", "FishingNote", "FishGuide", "GearSetView", "HousingSignBoard", "Housing", "AllianceList", "LookingForGroup", "HousingTravellersNote", "DTR", "RetainerCharacter", "AdventureNoteBook", "HousingChocoboList", "TripleTriad", "LimitBreak", "RaceChocobo", "Currency", "BeginnerChannelMentorList", "BeginnerChannelBeginnerList", "PvPDuelRequest", "JobHud", "PvPTeam", "PvPTeamMember", "PvPTeamResult", "PvPTeamActivity", "ContentMemberList", "CrossWorldLinkShell", "LovmNamePlate", "LovmActionDetail", "LovmResult", "Lovm", "PvPProfile", "Orchestrion", "OrchestrionPlayListSelect", "RetainerTask", "YKWNote", "DeepDungeonNaviMap", "DeepDungeonStatus", "GcArmyExpedition", "GcArmyTraining", "GcArmyCapture", "PvPMKS", "PvPSpectatorList", "LFGRecruiterNameSearch", "Snipe", "Performance", "ContentsReplayPlayer", "SatisfactionSupplyChangeMiragePrism", "SatisfactionSupplyMiragePrism", "Alarm", "Merchant", "MerchantEquipSelect", "EurekaLogosShardList", "RhythmAction", "WorldTranslate", "PVPSimulationHeader2", "PVPSimulationDisplay", "Emj", "WeeklyPuzzle", "MYCInfo", "TeleportTown", "MJIHousingGoods" };

    this.StringArrayDataHandler = new StringArrayDataHandler(arraysToBlock, this.configuration, TranslationService);

    GameWindowCacheManager.Preload(ConfigDirectory);

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


    // ClientStateInterface.Login += this.StringArrayDataHandler.LoadAndTranslateStringArrayDatas;
    this.StringArrayDataHandler.LoadAndTranslateStringArrayDatas();
    /*if (ClientStateInterface.IsLoggedIn)
    {
      // this.ParseUi();
      this.StringArrayDataHandler.LoadAndTranslateStringArrayDatas();
    }*/

    // Disabling BattleTalk translation by default if the language is not supported by the game font while we fix the overlays
    this.configuration.TranslateBattleTalk =
        this.configuration.OverlayOnlyLanguage ? false : true;
    this.configuration.UseImGuiForBattleTalk = false;

    // Fix wrong chatgpt base url in v3.17
    // TODO: remove it in later versions
    if (this.configuration.ChatGPTBaseUrl ==
        "https://api.openai.com/v1/chat/completions")
    {
      this.configuration.ChatGPTBaseUrl = "https://api.openai.com/v1";
      PluginInterface.SavePluginConfig(this.configuration);
    }
  }

  [PluginService] public static IDataManager DManager { get; set; }

  [PluginService]
  public static IDalamudPluginInterface PluginInterface { get; set; } = null!;

  [PluginService]
  public static ICommandManager CommandManager { get; set; } = null!;

  [PluginService]
  public static IFramework FrameworkInterface { get; set; } = null!;

  [PluginService] public static IGameGui GameGuiInterface { get; set; } = null!;

  [PluginService]
  public static IChatGui ChatGuiInterface { get; set; } = null!;

  [PluginService]
  public static IClientState ClientStateInterface { get; set; } = null!;

  [PluginService] public static IToastGui ToastGuiInterface { get; set; } = null!;

  [PluginService]
  public static IAddonEventManager EventManager { get; set; } = null!;

  [PluginService]
  public static IAddonLifecycle AddonLifecycle { get; set; } = null!;

  [PluginService] public static IPluginLog PluginLog { get; set; } = null!;

  [PluginService]
  public static INotificationManager NotificationManager { get; set; } = null!;

  [PluginService]
  public static ITextureProvider TextureProvider { get; set; } = null!;

  public string Name => Resources.Name;

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

  public static LanguageInfo SelectedLanguage { get; set; }

  public List<ToastMessage> ErrorToastsCache { get; set; }

  public List<ToastMessage> QuestToastsCache { get; set; }

  public List<ToastMessage> OtherToastsCache { get; set; }

  public StringArrayDataHandler StringArrayDataHandler { get; set; }



  /// <inheritdoc />
  public void Dispose()
  {
    this.Dispose(true);
    GC.SuppressFinalize(this);
  }

  /// <summary>
  ///     Disposes of the resources used by the Echoglossian plugin.
  /// </summary>
  /// <param name="disposing">Indicates whether the method was called from managed code.</param>
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

    // ClientStateInterface.Login -= this.StringArrayDataHandler.LoadAndTranslateStringArrayDatas;

    if (disposing && this.registeredAddonHandlers != null)
    {
      AddonHandlerRegistrar.UnregisterMany(
          this.registeredAddonHandlers,
          AddonLifecycle);
    }

    if (this.configuration.TranslateTalk)
    {
      /*      AddonLifecycle.UnregisterListener(
                AddonEvent.PreRefresh,
                "Talk",
                this.UiTalkAsyncHandler);
            AddonLifecycle.UnregisterListener(
                AddonEvent.PreDraw,
                "Talk",
                this.UiTalkAsyncHandler);
            AddonLifecycle.UnregisterListener(
                AddonEvent.PreReceiveEvent,
                "Talk",
                this.UiTalkAsyncHandler);*/
    }

    if (this.configuration.TranslateBattleTalk)
    {
      /*      AddonLifecycle.UnregisterListener(
                AddonEvent.PreRefresh,
                "_BattleTalk",
                this.UiBattleTalkAsyncHandler);
            AddonLifecycle.UnregisterListener(
                AddonEvent.PreDraw,
                "_BattleTalk",
                this.UiBattleTalkAsyncHandler);
            AddonLifecycle.UnregisterListener(
                AddonEvent.PreReceiveEvent,
                "_BattleTalk",
                this.UiBattleTalkAsyncHandler);*/
    }

    if (this.configuration.TranslateTalkSubtitle)
    {
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreSetup,
          "TalkSubtitle",
          this.UiTalkSubtitleAsyncHandler);
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreRefresh,
          "TalkSubtitle",
          this.UiTalkSubtitleAsyncHandler);
    }

    if (this.configuration.TranslateJournal)
    {
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreSetup,
          "JournalResult",
          this.UiJournalResultHandler);
      AddonLifecycle.UnregisterListener(
          AddonEvent.PostReceiveEvent,
          "RecommendList",
          this.UiRecommendListHandler);
      AddonLifecycle.UnregisterListener(
          AddonEvent.PostRequestedUpdate,
          "RecommendList",
          this.UiRecommendListHandlerAsync);
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreRefresh,
          "AreaMap",
          this.UiAreaMapHandler);
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreRefresh,
          "ScenarioTree",
          this.UiScenarioTreeHandler);
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreUpdate,
          "Journal",
          this.UiJournalQuestHandler);
      AddonLifecycle.UnregisterListener(
          AddonEvent.PostRequestedUpdate,
          "Journal",
          this.UiJournalDetailHandler);
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreRequestedUpdate,
          "JournalDetail",
          this.UiJournalDetailHandler);
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreSetup,
          "JournalAccept",
          this.UiJournalAcceptHandler);
      AddonLifecycle.UnregisterListener(
          AddonEvent.PostRequestedUpdate,
          "_ToDoList",
          this.UiToDoListHandler);
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

    switch (this.configuration.UseImGuiForTalk ||
            this.configuration.UseImGuiForBattleTalk ||
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
    }
  }

  /// <summary>
  ///     Builds the UI for the plugin.
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
      if (DateTime.Now.Ticks - 10000000 >
          this.configuration.FontChangeTime)
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
      var shouldDisplay = overlayRegistration.Overlay.Display;
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

  /// <summary>
  /// Sets the configuration flag to true when the config window is opened.
  /// </summary>
  private void ConfigWindow()
  {
    this.config = true;
  }

  /// <summary>
  /// Sets the configuration flag to true when the command is executed.
  /// </summary>
  /// <param name="command">The command that triggered the execution.</param>
  /// <param name="arguments">Arguments associated with the command.</param>
  private void Command(string command, string arguments)
  {
    this.config = true;
  }

  private void EgloAddonHandler()
  {
    PluginLog.Debug("EgloAddonHandler called.");

    this.registeredAddonHandlers =
        new List<(string AddonName, IAddonTranslationHandler Handler)>
        {
               /* (AddonName: "_MainCommand",
                    Handler: new MainCommandHandler(
                        this.configuration,
                        TranslationService)),
                (AddonName: "AddonContextMenuTitle",
                    Handler: new AddonContextMenuTitleHandler(
                        this.configuration,
                        TranslationService)),*/
                (AddonName: "Character",
                    Handler: new CharacterWindowHandler(
                        this.configuration,
                        TranslationService)),
               /* (AddonName: "OperationGuide",
                    Handler: new OperationGuideHandler(
                        this.configuration,
                        TranslationService)),
                (AddonName: "Hud",
                    Handler: new HudWindowHandler(
                        this.configuration,
                        TranslationService)),
                (AddonName: "Hud2",
                    Handler: new Hud2WindowHandler(
                        this.configuration,
                        TranslationService)),
                (AddonName: "CharacterClass",
                    Handler: new CharacterClassSubWindowHandler(
                        this.configuration,
                        TranslationService)),
                (AddonName: "CharacterRepute",
                    Handler: new CharacterReputeSubWindowHandler(
                        this.configuration,
                        TranslationService)),
                (AddonName: "CharacterProfile",
                    Handler: new CharacterProfileSubWindowHandler(
                        this.configuration,
                        TranslationService)),
                (AddonName: "CharacterStatus",
                    Handler: new CharacterStatusSubWindowHandler(
                        this.configuration,
                        TranslationService)),*/
                this.configuration.TranslateTalk
                    ? (AddonName: "Talk",
                        Handler: new TalkHandler(
                            this.configuration,
                            TranslationService))
                    : default,
                this.configuration.TranslateBattleTalk
                    ? (AddonName: "_BattleTalk",
                        Handler: new BattleTalkHandler(
                            this.configuration,
                            TranslationService))
                    : default,
        };

    AddonHandlerRegistrar.RegisterMany(
        this.registeredAddonHandlers,
        AddonLifecycle);

    if (this.configuration.TranslateTalk)
    {
      /*PluginLog.Debug(
          "Registering Talk addon listeners for translation.");

      AddonLifecycle.RegisterListener(
          AddonEvent.PreRefresh,
          "Talk",
          this.UiTalkAsyncHandler);
      AddonLifecycle.RegisterListener(
          AddonEvent.PreDraw,
          "Talk",
          this.UiTalkAsyncHandler);
      AddonLifecycle.RegisterListener(
          AddonEvent.PreReceiveEvent,
          "Talk",
          this.UiTalkAsyncHandler);*/
    }

    if (this.configuration.TranslateBattleTalk)
    {
      PluginLog.Debug(
          "Registering _BattleTalk addon listeners for translation.");

      /*      AddonLifecycle.RegisterListener(
                AddonEvent.PreRefresh,
                "_BattleTalk",
                this.UiBattleTalkAsyncHandler);
            AddonLifecycle.RegisterListener(
                AddonEvent.PreDraw,
                "_BattleTalk",
                this.UiBattleTalkAsyncHandler);
            AddonLifecycle.RegisterListener(
                AddonEvent.PreReceiveEvent,
                "_BattleTalk",
                this.UiBattleTalkAsyncHandler);*/
    }

    if (this.configuration.TranslateTalkSubtitle)
    {
      PluginLog.Debug(
          "Registering TalkSubtitle addon listeners for translation.");

      AddonLifecycle.RegisterListener(
          AddonEvent.PreSetup,
          "TalkSubtitle",
          this.UiTalkSubtitleAsyncHandler);
      AddonLifecycle.RegisterListener(
          AddonEvent.PreRefresh,
          "TalkSubtitle",
          this.UiTalkSubtitleAsyncHandler);
    }

    if (this.configuration.TranslateJournal)
    {
      PluginLog.Debug(
          "Registering Journal addon listeners for translation.");
      AddonLifecycle.RegisterListener(
          AddonEvent.PreSetup,
          "JournalResult",
          this.UiJournalResultHandler);
      AddonLifecycle.RegisterListener(
          AddonEvent.PostReceiveEvent,
          "RecommendList",
          this.UiRecommendListHandler);
      AddonLifecycle.RegisterListener(
          AddonEvent.PostRequestedUpdate,
          "RecommendList",
          this.UiRecommendListHandlerAsync);
      AddonLifecycle.RegisterListener(
          AddonEvent.PreRefresh,
          "AreaMap",
          this.UiAreaMapHandler);
      AddonLifecycle.RegisterListener(
          AddonEvent.PreRefresh,
          "ScenarioTree",
          this.UiScenarioTreeHandler);
      AddonLifecycle.RegisterListener(
          AddonEvent.PreUpdate,
          "Journal",
          this.UiJournalQuestHandler);
      AddonLifecycle.RegisterListener(
          AddonEvent.PostRequestedUpdate,
          "Journal",
          this.UiJournalDetailHandler);
      AddonLifecycle.RegisterListener(
          AddonEvent.PreRequestedUpdate,
          "JournalDetail",
          this.UiJournalDetailHandler);
      AddonLifecycle.RegisterListener(
          AddonEvent.PreSetup,
          "JournalAccept",
          this.UiJournalAcceptHandler);
      AddonLifecycle.RegisterListener(
          AddonEvent.PostRequestedUpdate,
          "_ToDoList",
          this.UiToDoListHandler);
    }

    /*"PreSetup","PostSetup", "PreUpdate", "PostUpdate", "PreDraw", "PostDraw", "PreFinalize", "PreReceiveEvent", "PostReceiveEvent", "PreRequestedUpdate", "PostRequestedUpdate", "PreRefresh", "PostRefresh" */
  }
}