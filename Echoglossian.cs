// <copyright file="Echoglossian.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.NativeUI.Helpers;

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
  /// <summary>
  /// The command used to invoke the plugin config UI.
  /// </summary>
  private const string SlashCommand = "/eglo";

  private const string DBManagerWindowCommand = "/eglodbmanager";

  private const string AddonProbeCommand = "/egloaddonprobe";

  private const string QuestProbeCommand = "/egloquestprobe";

  /// <summary>
  /// The language ID to translate to.
  /// </summary>
  public static int LanguageInt = 28;

  /// <summary>
  /// The font size for the plugin's UI elements.
  /// </summary>
  private static int fontSize = 24;

  /// <summary>
  /// The chosen translation engine.
  /// </summary>
  public static int ChosenTransEngine;

  /// <summary>
  /// The name of the chosen translation engine.
  /// </summary>
  private static string transEngineName;

  /// <summary>
  /// Holds the languages dictionary for the plugin.
  /// </summary>
  public static Dictionary<int, LanguageInfo> LangDict;

  /// <summary>
  /// Holds the main text for glyph range configuration.
  /// </summary>
  public static UINewFontHandler UINewFontHandler;

  /// <summary>
  /// Holds the database editor window instance.
  /// </summary>
  private DbEditorWindow? dbEditorWindow;

  /// <summary>
  /// Holds the currently active addon structure probe watch, if any.
  /// </summary>
  private AddonStructureProbe.AddonStructureProbeWatch? addonProbeWatch;

  /// <summary>
  /// Holds the sanitizer instance for cleaning up text input.
  /// </summary>
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

  /// <summary>
  /// The directory where the plugin's configuration files are stored.
  /// </summary>
  public static string ConfigDirectory;

  /// <summary>
  /// Holds the list of StringArrayData names to block translation for
  /// </summary>
  public static List<string> ArraysToBlock;

  private readonly SemaphoreSlim areaToastTranslationSemaphore;
  private readonly SemaphoreSlim battleTalkTranslationSemaphore;
  private readonly IDalamudTextureWrap choiceImage;
  private readonly SemaphoreSlim classChangeToastTranslationSemaphore;

  private readonly Config configuration;

  private readonly CultureInfo cultureInfo;
  private readonly IDalamudTextureWrap cutsceneChoiceImage;
  private readonly SemaphoreSlim errorToastTranslationSemaphore;
  private readonly IDalamudTextureWrap logo;
  private readonly QueuedTranslationBroker queuedTranslationBroker;
  private readonly HoverTooltipManager hoverTooltipManager;
  private readonly SemaphoreSlim nameTranslationSemaphore;

  private readonly IDalamudTextureWrap pixImage;
  private readonly IDalamudTextureWrap cryptoImage;

  private readonly bool pluginAssetsState;
  private readonly SemaphoreSlim questToastTranslationSemaphore;
  private readonly QuestToastRuntime questToastRuntime;
  private readonly SemaphoreSlim senderTranslationSemaphore;
  private readonly IDalamudTextureWrap talkImage;
  private readonly SemaphoreSlim talkSubtitleTranslationSemaphore;
  private readonly SemaphoreSlim talkTranslationSemaphore;

  private readonly SemaphoreSlim toastTranslationSemaphore;
  private readonly SemaphoreSlim wideTextToastTranslationSemaphore;

  private AtkTextNodeBufferWrapper atkTextNodeBufferWrapper;

  /// <summary>
  /// Tggle the configuration UI visibility.
  /// </summary>
  private bool config;

  /// <summary>
  /// The language code to translate to.
  /// </summary>
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

    CommandManager.AddHandler(DBManagerWindowCommand, new CommandInfo(this.OnEgloDbEditorCommand)
    {
      HelpMessage = Resources.OpensTheEchoglossianDBEditor
    });

    CommandManager.AddHandler(
        AddonProbeCommand,
        new CommandInfo(this.OnEgloAddonProbeCommand)
        {
            HelpMessage = Resources.AddonProbeHelpMessage,
        });

    CommandManager.AddHandler(
        QuestProbeCommand,
        new CommandInfo(this.OnEgloQuestProbeCommand)
        {
            HelpMessage = Resources.QuestProbeHelpMessage,
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
    AssetsManager.AssetFiles =
        [
            "NotoSansCJKhk-Regular.otf",
            "NotoSansCJKjp-Regular.otf",
            "NotoSansCJKkr-Regular.otf",
            "NotoSansCJKsc-Regular.otf",
            "NotoSansCJKtc-Regular.otf",
        ];

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

    this.MigrateOverlayStyleSettings();

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
    this.cryptoImage =
        TextureProvider.CreateFromImageAsync(Resources.crypto).Result;
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

    this.queuedTranslationBroker = new QueuedTranslationBroker();
    this.hoverTooltipManager = new HoverTooltipManager();

    this.atkTextNodeBufferWrapper = new AtkTextNodeBufferWrapper();

    this.LoadAllErrorToasts();
    this.LoadAllOtherToasts();

    ArraysToBlock = ["ChatLog", "CharaSelect", "PartyList", "NamePlate", "ActionBar", "Inventory", "CharacterItems", "Trade", "PartyMemberList", "LinkShell", "BlackList", "FriendList", "Letter", "SocialList", "EnemyList", "CastBar", "Journal", "RecipeNote", "FlyText", "InventoryRetainer", "MiniTalk", "CommonCurrencies", "ItemSearch", "ArmouryBoard", "FreeCompanyMember", "HousingBlackListSetting", "LegacyItemStorage", "FreeCompanyApplication", "GearSetList", "FreeCompanyRights", "CabinetStore", "CabinetWithdraw", "FreeCompanyActivity", "FreeCompanyExchange", "FreeCompanyStatus", "ContentsFinderConfirm", "FreeCompanyChest", "Buddy", "FreeCompanyAction", "FishingNote", "FishGuide", "GearSetView", "HousingSignBoard", "Housing", "AllianceList", "LookingForGroup", "HousingTravellersNote", "DTR", "RetainerCharacter", "AdventureNoteBook", "HousingChocoboList", "TripleTriad", "LimitBreak", "RaceChocobo", "Currency", "BeginnerChannelMentorList", "BeginnerChannelBeginnerList", "PvPDuelRequest", "JobHud", "PvPTeam", "PvPTeamMember", "PvPTeamResult", "PvPTeamActivity", "ContentMemberList", "CrossWorldLinkShell", "LovmNamePlate", "LovmActionDetail", "LovmResult", "Lovm", "PvPProfile", "Orchestrion", "OrchestrionPlayListSelect", "RetainerTask", "YKWNote", "DeepDungeonNaviMap", "DeepDungeonStatus", "GcArmyExpedition", "GcArmyTraining", "GcArmyCapture", "PvPMKS", "PvPSpectatorList", "LFGRecruiterNameSearch", "Snipe", "Performance", "ContentsReplayPlayer", "SatisfactionSupplyChangeMiragePrism", "SatisfactionSupplyMiragePrism", "Alarm", "Merchant", "MerchantEquipSelect", "EurekaLogosShardList", "RhythmAction", "WorldTranslate", "PVPSimulationHeader2", "PVPSimulationDisplay", "Emj", "WeeklyPuzzle", "MYCInfo", "TeleportTown", "MJIHousingGoods"];

    this.StringArrayDataHandler = new StringArrayDataHandler(ArraysToBlock, this.configuration, TranslationService);

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

    this.questToastRuntime = new QuestToastRuntime(
        this.configuration,
        TranslationService,
        this.FindAndReturnToastMessage,
        toastMessage => Task.Run(() => this.InsertToastMessageData(toastMessage)),
        (translatedName, translatedText, originalName) =>
            this.UpdateOverlayContent(
                this.questToastOverlay,
                translatedName,
                translatedText,
                originalName),
        () => this.ClearOverlay(this.questToastOverlay, clearText: true),
        text => this.RemoveDiacritics(
            text,
            this.SpecialCharsSupportedByGameFont));

    ToastGuiInterface.QuestToast += this.questToastRuntime.HandleQuestToast;

    this.EgloAddonHandler();

    this.RegisterOverlays();

    this.dbEditorWindow = new DbEditorWindow(new EchoglossianDbContext(ConfigDirectory));
    // Subscribe to draw it:
    PluginInterface.UiBuilder.Draw += this.DrawDbEditorWindow;

    PluginInterface.UiBuilder.Draw += this.BuildUi;


    // ClientStateInterface.Login += this.StringArrayDataHandler.LoadAndTranslateStringArrayDatas;
    this.StringArrayDataHandler.LoadAndTranslateStringArrayDatas();
    /*if (ClientStateInterface.IsLoggedIn)
    {
      // this.ParseUi();
      this.StringArrayDataHandler.LoadAndTranslateStringArrayDatas();
    }*/

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

  [PluginService]
  public static ISeStringEvaluator SeStringEvaluator { get; set; } = null!;

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
    AddonLifecycle.UnLogAddon("CutSceneSelectString");
    QuestUiTranslationCache.Clear();
    QuestHoverTranslationCache.Clear();
    QuestLuminaResolver.Clear();
    QuestProgressResolver.Clear();
    QuestTodoProgressResolver.Clear();

      this.addonProbeWatch?.Dispose();
      this.addonProbeWatch = null;

      ToastGuiInterface.QuestToast -= this.questToastRuntime.HandleQuestToast;
      this.queuedTranslationBroker.Dispose();

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
    PluginInterface.UiBuilder.Draw -= this.DrawDbEditorWindow;

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
    this.cutSceneSelectStringOverlay.Dispose();
    this.DisposeMiniTalkBubbleOverlays();

    // ClientStateInterface.Login -= this.StringArrayDataHandler.LoadAndTranslateStringArrayDatas;

    if (disposing && this.registeredAddonHandlers != null)
    {
      AddonHandlerRegistrar.UnregisterMany(
          this.registeredAddonHandlers,
          AddonLifecycle);
    }

    if (this.configuration.TranslateTalk)
    {
      // Talk now unregisters through the addon-handler registrar.
    }

    if (this.configuration.TranslateJournal)
    {
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreUpdate,
          "Journal",
          this.UiJournalQuestHandler);
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreRequestedUpdate,
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
    }

    if (this.configuration.TranslateJournalAccept)
    {
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreSetup,
          "JournalAccept",
          this.UiJournalAcceptHandler);
    }

    if (this.configuration.TranslateJournalResult)
    {
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreSetup,
          "JournalResult",
          this.UiJournalResultHandler);
    }

    if (this.configuration.TranslateRecommendList)
    {
      AddonLifecycle.UnregisterListener(
          AddonEvent.PostReceiveEvent,
          "RecommendList",
          this.UiRecommendListHandler);
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreRequestedUpdate,
          "RecommendList",
          this.UiRecommendListHandler);
      AddonLifecycle.UnregisterListener(
          AddonEvent.PostRequestedUpdate,
          "RecommendList",
          this.UiRecommendListHandlerAsync);
    }

    if (this.configuration.TranslateAreaMap)
    {
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreRefresh,
          "AreaMap",
          this.UiAreaMapHandler);
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreRequestedUpdate,
          "AreaMap",
          this.UiAreaMapHandler);
    }

    if (this.configuration.TranslateScenarioTree)
    {
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreRefresh,
          "ScenarioTree",
          this.UiScenarioTreeHandler);
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreRequestedUpdate,
          "ScenarioTree",
          this.UiScenarioTreeHandler);
    }

    if (this.configuration.TranslateToDoList)
    {
      AddonLifecycle.UnregisterListener(
          AddonEvent.PostRequestedUpdate,
          "_ToDoList",
          this.UiToDoListHandler);
      AddonLifecycle.UnregisterListener(
          AddonEvent.PreRequestedUpdate,
          "_ToDoList",
          this.UiToDoListHandler);
    }

    FrameworkInterface.Update -= this.Tick;

    this.GlyphRangeConfigText?.Free();
    this.GlyphRangeMainText = null;
    this.GlyphRangeConfigText = null;

    CommandManager.RemoveHandler(SlashCommand);
    CommandManager.RemoveHandler(DBManagerWindowCommand);
    CommandManager.RemoveHandler(AddonProbeCommand);
    CommandManager.RemoveHandler(QuestProbeCommand);
  }

}
