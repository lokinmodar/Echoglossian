// <copyright file="Echoglossian.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.NativeUI.AddonHandlers.Common;
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

#if DEBUG
  private const string AddonProbeCommand = "/egloaddonprobe";

  private const string QuestProbeCommand = "/egloquestprobe";
#endif

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
#if DEBUG
  private AddonStructureProbe.AddonStructureProbeWatch? addonProbeWatch;
#endif

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

  private readonly IDalamudTextureWrap choiceImage;

  private readonly Config configuration;

  private readonly CultureInfo cultureInfo;
  private readonly IDalamudTextureWrap cutsceneChoiceImage;
  private readonly IDalamudTextureWrap logo;
  private QueuedTranslationBroker queuedTranslationBroker;
  private readonly HoverTooltipManager hoverTooltipManager;

  private readonly IDalamudTextureWrap pixImage;
  private readonly IDalamudTextureWrap cryptoImage;

  private readonly bool pluginAssetsState;
  private QuestToastRuntime questToastRuntime;
  private readonly IDalamudTextureWrap talkImage;
  private static Echoglossian? activeInstance;
  private string? addonHandlerRegistrationSignature;
  private string? translationActivationBlockedNotificationSignature;
  private string? translationRuntimeSignature;
  private bool runtimeConfigurationDirty;
  private bool runtimeConfigurationReady;

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
    var persistedConfig = PluginInterface.GetPluginConfig() as Config;
    this.configuration = persistedConfig ?? new Config();
    if (persistedConfig == null)
    {
      PluginInterface.SavePluginConfig(this.configuration);
    }

    var loadedConfigVersion = this.configuration.Version;
    var currentConfigVersion = new Config().Version;
    this.DisableStructuredTooltipTranslationForRelease();

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

#if DEBUG
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
#endif

    Sanitizer = PluginInterface.Sanitizer as Sanitizer;

    LangDict = this.languagesDictionary;

    LanguageEngineSupport.ApplySupportTo(LangDict);

    try
    {
      this.CreateOrUseDb();
      PluginRuntimeLog.Debug("Eglo database created or used successfully.");
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Error($"Error creating or using database: {e}");
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

    this.configuration.PluginVersion =
        ResolvePluginVersion() ?? this.configuration.PluginVersion;
    if (this.configuration.Version < 5)
    {
      this.FixConfig();
    }

    this.MigrateOverlayStyleSettings();
    this.MigrateOverlayDisplayModes();
    this.MigrateGameMainMenuTranslationSettings();
    this.MigrateTranslationEngineSelection(loadedConfigVersion);

    SelectedLanguage = this.languagesDictionary[this.configuration.Lang];
    AssetsManager.RefreshPluginAssetsState(SelectedLanguage);
    this.configuration.PluginAssetsDownloaded = AssetsManager.PluginAssetsDownloaded;

    this.pluginAssetsState = this.configuration.PluginAssetsDownloaded;

    PluginRuntimeLog.Debug(
        $"Assets state config: {this.configuration.PluginAssetsDownloaded}");
    PluginRuntimeLog.Debug($"Assets state var: {this.pluginAssetsState}");

    if (!this.pluginAssetsState &&
        AssetsManager.RequiresDownloadedAssets(SelectedLanguage))
    {
      AssetsManager.PluginAssetsChecker(SelectedLanguage);
    }

    this.EnforceTranslationActivationConstraints();

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

    PluginInterface.UiBuilder.OpenMainUi += this.ConfigWindow;
    PluginInterface.UiBuilder.OpenConfigUi += this.ConfigWindow;
    this.TryShowConfigVersionUpgradeNotification(
        loadedConfigVersion,
        currentConfigVersion);

    LanguageInt = this.configuration.Lang;

    fontSize = this.configuration.FontSize;

    this.LangToTranslateTo = LangDict[LanguageInt].Code;

    MountFontPaths();

    UINewFontHandler = new UINewFontHandler(this.configuration);

    this.RebuildTranslationServiceSafely();

    this.queuedTranslationBroker = new QueuedTranslationBroker(
        (TransEngines)this.configuration.ChosenTransEngine,
        message => PluginRuntimeLog.Warning(message),
        message => PluginRuntimeLog.Error(message));
    this.hoverTooltipManager = new HoverTooltipManager(
        this.configuration,
        UINewFontHandler);
    this.RegisterStructuredTooltipLifecycleHandlers();

    this.atkTextNodeBufferWrapper = new AtkTextNodeBufferWrapper();

    this.LoadAllErrorToasts();
    this.LoadAllOtherToasts();

    DbFirstGameWindowAddonHandler.ClearSessionCaches();
    GameWindowCacheManager.Preload(ConfigDirectory);
    StringArrayDataCacheManager.Preload(ConfigDirectory);
    TranslationFailureCacheManager.Preload(ConfigDirectory);
    ActionTooltipCacheManager.Preload(ConfigDirectory);
    TraitCacheManager.Preload(ConfigDirectory);
    ReferenceTextCacheRegistry.PreloadAll(ConfigDirectory);
    ItemTooltipCacheManager.Preload(ConfigDirectory);

    FrameworkInterface.Update += this.Tick;

    this.questToastRuntime = this.CreateQuestToastRuntime();
    this.RegisterQuestToastRuntime();

    this.EgloAddonHandler();

    this.RegisterOverlays();

    this.dbEditorWindow = new DbEditorWindow(new EchoglossianDbContext(ConfigDirectory));
    // Subscribe to draw it:
    PluginInterface.UiBuilder.Draw += this.DrawDbEditorWindow;

    PluginInterface.UiBuilder.Draw += this.BuildUi;
    activeInstance = this;
    this.translationRuntimeSignature =
        this.ComputeTranslationRuntimeSignature();
    this.addonHandlerRegistrationSignature =
        this.ComputeAddonHandlerRegistrationSignature();
    this.runtimeConfigurationReady = true;
    this.TryShowTranslationActivationBlockedNotification();
  }

  [PluginService] public static IDataManager DManager { get; set; }

  [PluginService]
  public static IDalamudPluginInterface PluginInterface { get; set; } = null!;

  [PluginService]
  public static ICommandManager CommandManager { get; set; } = null!;

  /// <summary>
  ///     Temporarily disables the structured tooltip feature family for release
  ///     builds while <c>ActionDetail</c> and <c>ItemDetail</c> remain unstable.
  /// </summary>
  private void DisableStructuredTooltipTranslationForRelease()
  {
    if (!this.configuration.TranslateTooltips)
    {
      return;
    }

    this.configuration.TranslateTooltips = false;
    PluginInterface.SavePluginConfig(this.configuration);
  }

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
  public static IUnlockState UnlockStateInterface { get; set; } = null!;

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
    if (this.registeredAddonHandlers != null)
    {
      foreach (var (_, handler) in this.registeredAddonHandlers)
      {
        if (handler is IPluginUnloadAwareAddonHandler unloadAwareHandler)
        {
          unloadAwareHandler.OnPluginUnload();
        }
      }
    }

    this.ResetStructuredTooltipUiRuntime();
    this.UnregisterStructuredTooltipLifecycleHandlers();

    AddonLifecycle.UnLogAddon("CutSceneSelectString");
      this.hoverTooltipManager.Clear();
      QuestUiTranslationCache.Clear();
      QuestHoverTranslationCache.Clear();
      StringArrayDataCacheManager.Clear();
      GameWindowCacheManager.Clear();
      TranslationFailureCacheManager.Clear();
      ActionTooltipCacheManager.Clear();
      TraitCacheManager.Clear();
      ReferenceTextCacheRegistry.ClearAll();
      ItemTooltipCacheManager.Clear();
      DbFirstGameWindowAddonHandler.ClearSessionCaches();
      QuestLuminaResolver.Clear();
    QuestProgressResolver.Clear();
    QuestTodoProgressResolver.Clear();
    this.ClearAcceptedQuestPrefetchState();
    this.ClearTraitDetailPrefetchState();
    this.ClearReferenceTextPrefetchState();

#if DEBUG
    this.addonProbeWatch?.Dispose();
    this.addonProbeWatch = null;
#endif

      this.UnregisterQuestToastRuntime();
      this.queuedTranslationBroker.Dispose();

      PluginInterface.UiBuilder.OpenMainUi -= this.ConfigWindow;
      PluginInterface.UiBuilder.OpenConfigUi -= this.ConfigWindow;

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
    this.actionDetailOverlay.Dispose();
    this.itemDetailOverlay.Dispose();
    this.DisposeMiniTalkBubbleOverlays();

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

    FrameworkInterface.Update -= this.Tick;

    this.GlyphRangeConfigText?.Free();
    this.GlyphRangeMainText = null;
    this.GlyphRangeConfigText = null;

    CommandManager.RemoveHandler(SlashCommand);
    CommandManager.RemoveHandler(DBManagerWindowCommand);
#if DEBUG
    CommandManager.RemoveHandler(AddonProbeCommand);
    CommandManager.RemoveHandler(QuestProbeCommand);
#endif

    if (ReferenceEquals(activeInstance, this))
    {
      activeInstance = null;
    }
  }

}


