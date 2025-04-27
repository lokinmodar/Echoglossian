// <copyright file="Echoglossian.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>


using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.LanguagesHandling;
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
    private static int languageInt = 28;
    private static int fontSize = 24;
    private static int chosenTransEngine;
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
    private static Dictionary<int, LanguageInfo> langDict;
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

    private static Sanitizer sanitizer;

    private AtkTextNodeBufferWrapper AtkTextNodeBufferWrapper;

    private UiAddonHandler uiBattleTalkAddonHandler;
    private UiAddonHandler uiTalkAddonHandler;
    private UiAddonHandler uiTalkSubtitleHandler;

    private TranslationService translationService;

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

      sanitizer = PluginInterface.Sanitizer as Sanitizer;

      langDict = this.languagesDictionary;

      LanguageEngineSupport.ApplySupportTo(langDict);

      identifier = Factory.Load($"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Wiki82.profile.xml");

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
      this.assetsPath = $"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}";

      this.AssetFiles.Add("NotoSansCJKhk-Regular.otf");
      this.AssetFiles.Add("NotoSansCJKjp-Regular.otf");
      this.AssetFiles.Add("NotoSansCJKkr-Regular.otf");
      this.AssetFiles.Add("NotoSansCJKsc-Regular.otf");
      this.AssetFiles.Add("NotoSansCJKtc-Regular.otf");

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
        this.PluginAssetsChecker();
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

      languageInt = this.configuration.Lang;

      fontSize = this.configuration.FontSize;

      chosenTransEngine = this.configuration.ChosenTransEngine;

      this.LangToTranslateTo = langDict[languageInt].Code;

      MountFontPaths();

      Echoglossian.UINewFontHandler = new UINewFontHandler(this.configuration);

      TransEngines t = (TransEngines)chosenTransEngine;
      transEngineName = t.ToString();
      this.translationService = new TranslationService(this.configuration, PluginLog, sanitizer);

      this.AtkTextNodeBufferWrapper = new AtkTextNodeBufferWrapper();

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

      this.uiTalkAddonHandler = new UiAddonHandler(this.configuration, this.UiFont, this.FontLoaded, this.LangToTranslateTo);
      this.uiBattleTalkAddonHandler = new UiAddonHandler(this.configuration, this.UiFont, this.FontLoaded, this.LangToTranslateTo);
      this.uiTalkSubtitleHandler = new UiAddonHandler(this.configuration, this.UiFont, this.FontLoaded, this.LangToTranslateTo);

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

      this.uiTalkAddonHandler?.Dispose();
      this.uiBattleTalkAddonHandler?.Dispose();
      this.uiTalkSubtitleHandler?.Dispose();

      this.pixImage?.Dispose();
      this.choiceImage?.Dispose();
      this.cutsceneChoiceImage?.Dispose();
      this.talkImage?.Dispose();
      this.logo?.Dispose();

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
#if DEBUG
        // PluginLog.Debug("Translations are disabled!");
#endif
        return;
      }

      if (ClientStateInterface.IsLoggedIn && !this.GatheringCharacterWindowAtkValuesComplete)
      {
        this.TranslateCharacterWindow();
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

    private void BuildUi()
    {
      if (!this.configuration.PluginAssetsDownloaded)
      {
        // this.PluginAssetsChecker();
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

          /* PluginInterface.UiBuilder.RebuildFonts();*/
        }
      }

      if (!this.configuration.Translate)
      {
        return;
      }

      foreach (var overlayRegistration in this.registeredOverlays)
      {
        if (!overlayRegistration.Overlay.Display)
        {
          continue;
        }

        string? customTitle = overlayRegistration.CustomTitleGetter?.Invoke();

        this.DrawTranslationWindow(
            overlayRegistration.Overlay,
            overlayRegistration.Config,
            customTitle
        );
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
#if DEBUG
      PluginLog.Debug("EgloAddonHandler called.");
#endif

      if (this.configuration.TranslateTalk)
      {
        // this.EgloNeutralAddonHandler("Talk", new string[] {  /* "PreUpdate", "PostUpdate",*/ "PreDraw",/* "PostDraw",  "PreReceiveEvent", "PostReceiveEvent", "PreRequestedUpdate", "PostRequestedUpdate" ,*/ "PreRefresh",/* "PostRefresh"*/ });
        AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, "Talk", this.UiTalkAsyncHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "Talk", this.UiTalkAsyncHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, "Talk", this.UiTalkAsyncHandler);
      }

      if (this.configuration.TranslateBattleTalk)
      {
        // this.EgloNeutralAddonHandler("_BattleTalk", new string[] { /* "PreUpdate", "PostUpdate",*/ "PreDraw",/* "PostDraw",  "PreReceiveEvent", "PostReceiveEvent", "PreRequestedUpdate", "PostRequestedUpdate" ,*/ "PreRefresh",/* "PostRefresh"*/});
        AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, "_BattleTalk", this.UiBattleTalkAsyncHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "_BattleTalk", this.UiBattleTalkAsyncHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, "_BattleTalk", this.UiBattleTalkAsyncHandler);
      }

      if (this.configuration.TranslateTalkSubtitle)
      {
        // this.EgloNeutralAddonHandler("TalkSubtitle", new string[] {/* "PreUpdate", "PostUpdate",*/ "PreDraw",/* "PostDraw",  "PreReceiveEvent", "PostReceiveEvent", "PreRequestedUpdate", "PostRequestedUpdate" ,*/ "PreRefresh",/* "PostRefresh"*/});
        AddonLifecycle.RegisterListener(AddonEvent.PreSetup, "TalkSubtitle", this.UiTalkSubtitleAsyncHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, "TalkSubtitle", this.UiTalkSubtitleAsyncHandler);
      }

      if (this.configuration.TranslateJournal)
      {
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
