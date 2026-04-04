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
  /// <summary>
  /// The command used to invoke the plugin config UI.
  /// </summary>
  private const string SlashCommand = "/eglo";

  private const string DBManagerWindowCommand = "/eglodbmanager";

  private const string AddonProbeCommand = "/egloaddonprobe";

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
            HelpMessage =
              "Dumps a recursive addon structure probe to the log. Usage: /egloaddonprobe <addon name> [index] or /egloaddonprobe stop",
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
    this.addonProbeWatch?.Dispose();
    this.addonProbeWatch = null;

    ToastGuiInterface.QuestToast -= this.questToastRuntime.HandleQuestToast;

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
    CommandManager.RemoveHandler(DBManagerWindowCommand);
    CommandManager.RemoveHandler(AddonProbeCommand);
  }

  /// <summary>
  /// Updates the plugin's state on each tick.
  /// </summary>
  private void Tick(IFramework tFramework)
  {
    this.addonProbeWatch?.Tick();
    if (this.addonProbeWatch?.IsDisposed == true)
    {
      this.addonProbeWatch = null;
    }

    if (!this.configuration.Translate)
    {
      return;
    }

    switch (this.configuration.UseImGuiForTalk ||
            this.configuration.UseImGuiForBattleTalk ||
            this.configuration.OverlayOnlyLanguage ||
            this.configuration.UseImGuiForWideTextToast ||
            this.configuration.UseImGuiForErrorToast ||
            this.configuration.UseImGuiForAreaToast ||
            this.configuration.UseImGuiForClassChangeToast ||
            this.configuration.UseImGuiForQuestToast)
    {
      case true when !this.FontLoaded || this.FontLoadFailed:
        return;
      case true:
        return;
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
      if (overlayRegistration.IsEnabled is not null &&
          !overlayRegistration.IsEnabled())
      {
        continue;
      }

      if (overlayRegistration.SyncBeforeDraw is not null &&
          !overlayRegistration.SyncBeforeDraw())
      {
        continue;
      }

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
          overlayRegistration.Config,
          overlayRegistration.CustomTitleGetter?.Invoke());
    }
  }

  /// <summary>
  /// Draws the database editor window.
  /// </summary>
  private void DrawDbEditorWindow()
  {
    this.dbEditorWindow?.Draw();
  }

  /// <summary>
  /// Open the Echoglossian DB Editor window when the command is executed.
  /// </summary>
  /// <param name="command">Command name.</param>
  /// <param name="args">Command arguments.</param>
  private void OnEgloDbEditorCommand(string command, string args)
  {
    this.dbEditorWindow?.IsOpen = true;
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

  /// <summary>
  /// Dumps a recursive probe of the requested addon to the log so we can
  /// inspect its live node tree, component roots, and likely overlay anchors.
  /// </summary>
  /// <param name="command">Command name.</param>
  /// <param name="args">Command arguments.</param>
  private void OnEgloAddonProbeCommand(string command, string args)
  {
    var trimmedArgs = args.Trim();
    if (trimmedArgs.Equals("stop", StringComparison.OrdinalIgnoreCase) ||
        trimmedArgs.Equals("cancel", StringComparison.OrdinalIgnoreCase))
    {
      if (this.addonProbeWatch == null)
      {
        ChatGuiInterface.Print("No active addon probe watch to stop.");
        return;
      }

      this.addonProbeWatch.Stop();
      this.addonProbeWatch = null;

      ChatGuiInterface.Print("Addon probe watch stopped.");
      return;
    }

    var (addonName, addonIndex) = this.ParseAddonProbeArguments(args);
    if (string.IsNullOrWhiteSpace(addonName))
    {
      ChatGuiInterface.Print(
          "Usage: /egloaddonprobe <addon name> [index] or /egloaddonprobe stop");
      return;
    }

    this.addonProbeWatch?.Dispose();
    this.addonProbeWatch = AddonStructureProbe.StartWatch(
        GameGuiInterface,
        PluginLog,
        addonName,
        addonIndex);

    ChatGuiInterface.Print(
        $"Addon probe watch started for '{addonName}'[{addonIndex}] for 60 seconds. Check the Dalamud log for event and tree dumps.");
  }

  /// <summary>
  /// Parses the addon probe command arguments into an addon name and optional index.
  /// </summary>
  /// <param name="args">The raw command arguments.</param>
  /// <returns>The addon name and index to probe.</returns>
  private (string AddonName, int Index) ParseAddonProbeArguments(string args)
  {
    var trimmedArgs = args.Trim();
    if (trimmedArgs.Length == 0)
    {
      return (string.Empty, 0);
    }

    var lastSpace = trimmedArgs.LastIndexOf(' ');
    if (lastSpace > 0 &&
        int.TryParse(trimmedArgs[(lastSpace + 1)..], out var parsedIndex))
    {
      var parsedName = trimmedArgs[..lastSpace].Trim();
      if (parsedName.Length > 0)
      {
        return (parsedName, parsedIndex);
      }
    }

    return (trimmedArgs, 0);
  }

  /// <summary>
  /// Updates the shared toast overlay bounds using a live "_WideText" addon
  /// instance received from AddonLifecycle.
  /// </summary>
  /// <param name="addon">The live "_WideText" addon.</param>
  private unsafe void SyncWideTextToastOverlayBounds(
      AtkUnitBase* addon,
      AtkTextNode* textNode)
  {
    this.UpdateToastOverlayBounds(this.toastOverlay, addon, textNode);
  }

  /// <summary>
  /// Updates the error toast overlay bounds using a live "_TextError" addon
  /// instance received from AddonLifecycle.
  /// </summary>
  /// <param name="addon">The live "_TextError" addon.</param>
  private unsafe void SyncErrorToastOverlayBounds(
      AtkUnitBase* addon,
      AtkTextNode* textNode)
  {
    this.UpdateToastOverlayBounds(this.errorToastOverlay, addon, textNode);
  }

  /// <summary>
  /// Updates the area toast overlay bounds using a live "_AreaText" addon
  /// instance received from AddonLifecycle.
  /// </summary>
  /// <param name="addon">The live "_AreaText" addon.</param>
  private unsafe void SyncAreaToastOverlayBounds(
      AtkUnitBase* addon,
      AtkTextNode* textNode)
  {
    this.UpdateToastOverlayBounds(this.areaToastOverlay, addon, textNode);
  }

  /// <summary>
  /// Updates the class/job change toast overlay bounds using a live
  /// "_TextClassChange" addon instance received from AddonLifecycle.
  /// </summary>
  /// <param name="addon">The live "_TextClassChange" addon.</param>
  private unsafe void SyncClassChangeToastOverlayBounds(
      AtkUnitBase* addon,
      AtkTextNode* textNode)
  {
    this.UpdateToastOverlayBounds(this.classChangeToastOverlay, addon, textNode);
  }

  /// <summary>
  /// Updates the text gimmick hint overlay bounds using a live addon instance
  /// received from AddonLifecycle.
  /// </summary>
  /// <param name="addon">The live "_TextGimmickHint" addon.</param>
  private unsafe void SyncTextGimmickHintToastOverlayBounds(
      AtkUnitBase* addon,
      AtkTextNode* textNode)
  {
    this.UpdateToastOverlayBounds(this.textGimmickHintOverlay, addon, textNode);
  }

  /// <summary>
  /// Persists a toast row into the correct historical cache/table according to
  /// its toast type.
  /// </summary>
  /// <param name="toastMessage">The translated toast row to persist.</param>
  /// <returns>The persistence result message.</returns>
  private string InsertToastMessageData(ToastMessage toastMessage)
  {
    return string.Equals(
            toastMessage.ToastType,
            "Error",
            StringComparison.OrdinalIgnoreCase)
        ? this.InsertErrorToastMessageData(toastMessage)
        : this.InsertOtherToastMessageData(toastMessage);
  }

  /// <summary>
  /// Handles the registration of addon handlers.
  /// </summary>
  private unsafe void EgloAddonHandler()
  {
    PluginLog.Debug("EgloAddonHandler called.");

    this.registeredAddonHandlers =
        [
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
        ];

    if (this.configuration.TranslateTalk)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "Talk",
              Handler: new TalkHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnTalkMessage,
                  InsertTalkData,
                  (translatedName, translatedText, originalName) =>
                      this.UpdateOverlayContent(
                          this.talkOverlay,
                          translatedName,
                          translatedText,
                          originalName),
                  () => this.ClearOverlay(this.talkOverlay, clearText: true),
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
    }

    if (this.configuration.TranslateBattleTalk)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "_BattleTalk",
              Handler: new BattleTalkHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnBattleTalkMessage,
                  battleTalkMessage => Task.FromResult(
                      InsertBattleTalkData(battleTalkMessage)),
                  (translatedName, translatedText, originalName) =>
                      this.UpdateOverlayContent(
                          this.battleTalkOverlay,
                          translatedName,
                          translatedText,
                          originalName),
                  () => this.ClearOverlay(
                      this.battleTalkOverlay,
                      clearText: true),
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
    }

    if (this.configuration.TranslateTalkSubtitle)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "TalkSubtitle",
              Handler: new TalkSubtitleHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnTalkSubtitleMessage,
                  talkSubtitleMessage => Task.Run(
                      () => InsertTalkSubtitleData(talkSubtitleMessage)),
                  (translatedName, translatedText, originalName) =>
                      this.UpdateOverlayContent(
                          this.talkSubtitleOverlay,
                          translatedName,
                          translatedText,
                          originalName),
                  () => this.ClearOverlay(
                      this.talkSubtitleOverlay,
                      clearText: true),
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
    }

    if (this.configuration.TranslateToast &&
        this.configuration.TranslateWideTextToast)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "_WideText",
              Handler: new WideTextToastHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnToastMessage,
                  toastMessage => Task.Run(
                      () => this.InsertToastMessageData(toastMessage)),
                  (translatedName, translatedText, originalName) =>
                      this.UpdateOverlayContent(
                          this.toastOverlay,
                          translatedName,
                          translatedText,
                          originalName),
                  () => this.ClearOverlay(this.toastOverlay, clearText: true),
                  this.SyncWideTextToastOverlayBounds,
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
    }

    if (this.configuration.TranslateToast &&
        this.configuration.TranslateErrorToast)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "_TextError",
              Handler: new ErrorToastHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnToastMessage,
                  toastMessage => Task.Run(
                      () => this.InsertToastMessageData(toastMessage)),
                  (translatedName, translatedText, originalName) =>
                      this.UpdateOverlayContent(
                          this.errorToastOverlay,
                          translatedName,
                          translatedText,
                          originalName),
                  () => this.ClearOverlay(
                      this.errorToastOverlay,
                      clearText: true),
                  this.SyncErrorToastOverlayBounds,
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
    }

    if (this.configuration.TranslateToast &&
        this.configuration.TranslateAreaToast)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "_AreaText",
              Handler: new AreaToastHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnToastMessage,
                  toastMessage => Task.Run(
                      () => this.InsertToastMessageData(toastMessage)),
                  (translatedName, translatedText, originalName) =>
                      this.UpdateOverlayContent(
                          this.areaToastOverlay,
                          translatedName,
                          translatedText,
                          originalName),
                  () => this.ClearOverlay(
                      this.areaToastOverlay,
                      clearText: true),
                  this.SyncAreaToastOverlayBounds,
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
    }

    if (this.configuration.TranslateToast &&
        this.configuration.TranslateClassChangeToast)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "_TextClassChange",
              Handler: new ClassChangeToastHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnToastMessage,
                  toastMessage => Task.Run(
                      () => this.InsertToastMessageData(toastMessage)),
                  (translatedName, translatedText, originalName) =>
                      this.UpdateOverlayContent(
                          this.classChangeToastOverlay,
                          translatedName,
                          translatedText,
                          originalName),
                  () => this.ClearOverlay(
                      this.classChangeToastOverlay,
                      clearText: true),
                  this.SyncClassChangeToastOverlayBounds,
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
    }

    if (this.configuration.TranslateTextGimmickHint)
    {
      this.registeredAddonHandlers.Add(
          (AddonName: "_TextGimmickHint",
              Handler: new TextGimmickHintHandler(
                  this.configuration,
                  TranslationService,
                  this.FindAndReturnTextGimmickHintMessage,
                  textGimmickHintMessage => InsertTextGimmickHintData(
                      textGimmickHintMessage),
                  (translatedName, translatedText, originalName) =>
                      this.UpdateOverlayContent(
                          this.textGimmickHintOverlay,
                          translatedName,
                          translatedText,
                          originalName),
                  () => this.ClearOverlay(
                      this.textGimmickHintOverlay,
                      clearText: true),
                  this.SyncTextGimmickHintToastOverlayBounds,
                  text => this.RemoveDiacritics(
                      text,
                      this.SpecialCharsSupportedByGameFont))));
    }

    AddonHandlerRegistrar.RegisterMany(
        this.registeredAddonHandlers,
        AddonLifecycle);

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

    // tracking addon lifecycle for debug
    AddonEvent[] lifecycleLogEventsWithoutUpdatesAndDraws =
    [
      AddonEvent.PreSetup,
      AddonEvent.PreFinalize,
      AddonEvent.PreRequestedUpdate,
      AddonEvent.PreRefresh,
      AddonEvent.PreReceiveEvent,
      AddonEvent.PreOpen,
      AddonEvent.PreClose,
      AddonEvent.PreShow,
      AddonEvent.PreHide,
      AddonEvent.PreMove,
      AddonEvent.PreMouseOver,
      AddonEvent.PreMouseOut,
      AddonEvent.PreFocus,
      AddonEvent.PostSetup,
      AddonEvent.PostRequestedUpdate,
      AddonEvent.PostRefresh,
      AddonEvent.PostReceiveEvent,
      AddonEvent.PostOpen,
      AddonEvent.PostClose,
      AddonEvent.PostShow,
      AddonEvent.PostHide,
      AddonEvent.PostMove,
      AddonEvent.PostMouseOver,
      AddonEvent.PostMouseOut,
      AddonEvent.PostFocus,
    ];

    AddonEvent[] lifecycleLogEventsWithoutUpdates =
    [
      AddonEvent.PreSetup,
      AddonEvent.PreDraw,
      AddonEvent.PreFinalize,
      AddonEvent.PreRequestedUpdate,
      AddonEvent.PreRefresh,
      AddonEvent.PreReceiveEvent,
      AddonEvent.PreOpen,
      AddonEvent.PreClose,
      AddonEvent.PreShow,
      AddonEvent.PreHide,
      AddonEvent.PreMove,
      AddonEvent.PreMouseOver,
      AddonEvent.PreMouseOut,
      AddonEvent.PreFocus,
      AddonEvent.PostSetup,
      AddonEvent.PostDraw,
      AddonEvent.PostRequestedUpdate,
      AddonEvent.PostRefresh,
      AddonEvent.PostReceiveEvent,
      AddonEvent.PostOpen,
      AddonEvent.PostClose,
      AddonEvent.PostShow,
      AddonEvent.PostHide,
      AddonEvent.PostMove,
      AddonEvent.PostMouseOver,
      AddonEvent.PostMouseOut,
      AddonEvent.PostFocus,
    ];

    // AddonLifecycleExtensions.LogAddon(
    //     AddonLifecycle,
    //     "Talk",
    //     lifecycleLogEventsWithoutUpdatesAndDraws);
      // AddonLifecycleExtensions.LogAddon(
      //   AddonLifecycle,
      //   "_BattleTalk",
      //   lifecycleLogEventsWithoutUpdatesAndDraws);

  }
}
