// <copyright file="Utils.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     General utility methods for the Echoglossian plugin.
/// </summary>
public partial class Echoglossian
{
  /// <summary>
  ///     Enum for the different prompt types.
  /// </summary>
  public enum PromptType
  {
    Claude,
    DeepSeek,
    Gemini,
    OpenRouter,
    Microsoft,
    Amazon,
    ChatGPT,
    YandexCloud,
    LibreTranslate,
    Ollama,
    LmStudio,
  }

  /// <summary>
  ///     Enum for the different translation engines.
  /// </summary>
  [Flags]
  public enum TransEngines
  {
    Google = 0, // Google Translator (free engine)
    Deepl = 1, // DeepL Translator
    ChatGPT = 2, // Chat GPT
    YandexCloud = 3, // Yandex Translator
    GTranslate = 4, // Uses Google, Bing and Yandex (free engines)
    DeepSeek = 5,
    Ollama = 6,
    LibreTranslate = 7,
    Microsoft = 8, // Microsoft Bing Translator (free engine)
    Amazon = 9, // Amazon Translate
    Gemini = 10, // Google Cloud Translate
    YandexPublic = 11, // Yandex Public Translator
    OpenRouter = 12, // OpenRouter Translator
    LmStudio = 13, // LM Studio Translator
    Claude = 14, // Anthropic Claude Translator

    All = Google | Deepl | YandexCloud | GTranslate | Amazon | Microsoft |
          ChatGPT | Gemini | DeepSeek | Ollama | LibreTranslate |
          YandexPublic | OpenRouter | LmStudio | Claude,
  }

  /// <summary>
  ///     Regex used to help filtering out numeric-like strings.
  /// </summary>
  public static readonly Regex NumericLikePattern = new(
      @"^\s*([€£$¥]?\s*\d+([.,]\d+)?\s*[%€£$¥]?\s*|(\d+/\d+))\s*$",
      RegexOptions.Compiled);

#if DEBUG
  /// <summary>
  ///     Lists all available culture information and writes it to a file.
  /// </summary>
  public void ListCultureInfos()
  {
    using StreamWriter logStream = new(
        ConfigDirectory + "CultureInfos.txt",
        true);

    var cus = CultureInfo.GetCultures(CultureTypes.AllCultures);
    foreach (var cu in cus)
    {
      logStream.WriteLine(cu.ToString());
    }
  }
#endif

  /// <summary>
  ///     Moves the given path up by the specified number of levels.
  /// </summary>
  /// <param name="path">Path.</param>
  /// <param name="noOfLevels"># of levels to move up.</param>
  /// <returns>Parent path.</returns>
  public string MovePathUp(string path, int noOfLevels)
  {
    var parentPath = path.TrimEnd('/', '\\');
    for (var i = 0; i < noOfLevels; i++)
    {
      if (parentPath != null)
      {
        parentPath = Directory.GetParent(parentPath)?.ToString();
      }
    }

    return parentPath;
  }

  /// <summary>
  ///     Fully resets the plugin configuration to its default values,
  ///     including all fields and properties. Prompts are explicitly assigned
  ///     from <see cref="PromptTemplateManager.DefaultPrompt" />.
  ///     Metadata like
  ///     <c>PluginVersion</c> and <c>FontChangeTime</c> are preserved or refreshed.
  /// </summary>
  /// <param name="config">The config instance to reset.</param>
  /// <param name="saveCallback">A callback that saves the config.</param>
  public static void ResetSettings(Config config, Action saveCallback)
  {
    var defaultConfig = new Config();
    var configType = typeof(Config);

    // Reset all fields
    foreach (var field in configType.GetFields(
                 BindingFlags.Instance | BindingFlags.Public |
                 BindingFlags.NonPublic))
    {
      if (Attribute.IsDefined(field, typeof(NonSerializedAttribute)))
      {
        continue;
      }

      field.SetValue(config, field.GetValue(defaultConfig));
    }

    // Reset all properties
    foreach (var prop in configType.GetProperties(
                 BindingFlags.Instance | BindingFlags.Public |
                 BindingFlags.NonPublic))
    {
      if (!prop.CanRead || !prop.CanWrite)
      {
        continue;
      }

      prop.SetValue(config, prop.GetValue(defaultConfig));
    }

    // Manually assign prompts with fallback to PromptTemplateManager.DefaultPrompt
    void SetPromptIfEmpty(string fieldName)
    {
      var field = configType.GetField(fieldName);
      if (field is { } f &&
          string.IsNullOrWhiteSpace(f.GetValue(config) as string))
      {
        f.SetValue(config, PromptTemplateManager.DefaultPrompt);
      }
    }

    SetPromptIfEmpty(nameof(Config.ChatGptPrompt));
    SetPromptIfEmpty(nameof(Config.ClaudePrompt));
    SetPromptIfEmpty(nameof(Config.DeepSeekPrompt));
    SetPromptIfEmpty(nameof(Config.GeminiPrompt));
    SetPromptIfEmpty(nameof(Config.OpenRouterPrompt));
    SetPromptIfEmpty(nameof(Config.MicrosoftTranslatorPrompt));
    SetPromptIfEmpty(nameof(Config.AmazonPrompt));
    SetPromptIfEmpty(nameof(Config.YandexCloudPrompt));
    SetPromptIfEmpty(nameof(Config.OllamaPrompt));
    SetPromptIfEmpty(nameof(Config.LmStudioPrompt));

    // Restore runtime-mutable metadata
    config.FontChangeTime = DateTime.Now.Ticks;
    config.PluginVersion = ResolvePluginVersion() ?? config.PluginVersion;
    config.Version =
        TranslationEngineSelectionMigrationHelper.TranslationEngineSchemaVersion;

    // Persist config
    saveCallback?.Invoke();

    // Show notification
    var settingsResetNotification = new Notification
    {
      Content = Resources.SettingsReset,
      Title = Resources.Name,
      Icon = FontAwesomeIcon.Cog.ToNotificationIcon(),
      Type = NotificationType.Info,
    };

    NotificationManager.AddNotification(settingsResetNotification);
  }

  /// <summary>
  ///     Fixes the configuration file if it is missing or has an incorrect version.
  /// </summary>
  public void FixConfig()
  {
    if (!File.Exists(PluginInterface.ConfigFile.FullName))
    {
#if DEBUG
      PluginRuntimeLog.Debug(
          $"Inside config file fixer - Config File Info: {PluginInterface.ConfigFile.FullName}");
#endif
      SaveConfig(this.configuration);
      return;
    }

    if (this.configuration.Version >= 5)
    {
      return;
    }

    PluginInterface.ConfigFile.Delete();
    SaveConfig(this.configuration);
    ResetSettings(this.configuration, () => SaveConfig(this.configuration));

    PluginInterface.GetPluginConfig();
  }

  /// <summary>
  ///     Resolves the current plugin version from the loaded assembly
  ///     metadata.
  /// </summary>
  /// <returns>The best available version string, or null when unavailable.</returns>
  private static string? ResolvePluginVersion()
  {
    var fileVersion = System.Diagnostics.FileVersionInfo
        .GetVersionInfo(PluginInterface.AssemblyLocation.FullName)
        .FileVersion;
    if (!string.IsNullOrWhiteSpace(fileVersion))
    {
      return fileVersion;
    }

    return Assembly.GetExecutingAssembly().GetName().Version?.ToString();
  }

  /// <summary>
  ///     Migrates legacy shared overlay style settings and NPC-name settings into
  ///     the per-overlay/per-addon settings introduced for newer configs.
  /// </summary>
  public void MigrateOverlayStyleSettings()
  {
    if (this.configuration.Version >= 10)
    {
      return;
    }

    if (this.configuration.Version < 6)
    {
      this.configuration.TalkForceShowTitle = this.configuration.ForceShowTitle;
      this.configuration.BattleTalkForceShowTitle =
          this.configuration.ForceShowTitle;
      this.configuration.ToastForceShowTitle = this.configuration.ForceShowTitle;
      this.configuration.TalkSubtitleForceShowTitle =
          this.configuration.ForceShowTitle;
    }

    this.configuration.TranslateTalkNpcNames =
        this.configuration.TranslateNpcNames;
    this.configuration.TranslateBattleTalkNpcNames =
        this.configuration.TranslateNpcNames;
    this.configuration.MiniTalkFontScale =
        this.configuration.TalkSubtitleFontScale;
    this.configuration.MiniTalkBackgroundOpacity = 1f;
    this.configuration.ImGuiMiniTalkWindowWidthMult =
        this.configuration.ImGuiTalkSubtitleWindowWidthMult;
    this.configuration.ImGuiMiniTalkWindowPosCorrection =
        this.configuration.ImGuiTalkSubtitleWindowPosCorrection;
    this.configuration.OverlayMiniTalkTextColor =
        this.configuration.OverlayTalkSubtitleTextColor;

    this.configuration.WideTextToastFontScale = this.configuration.ToastFontScale;
    this.configuration.ErrorToastFontScale = this.configuration.ToastFontScale;
    this.configuration.AreaToastFontScale = this.configuration.ToastFontScale;
    this.configuration.ClassChangeToastFontScale =
        this.configuration.ToastFontScale;
    this.configuration.QuestToastFontScale = this.configuration.ToastFontScale;

    this.configuration.WideTextToastForceShowTitle =
        this.configuration.ToastForceShowTitle;
    this.configuration.ErrorToastForceShowTitle =
        this.configuration.ToastForceShowTitle;
    this.configuration.AreaToastForceShowTitle =
        this.configuration.ToastForceShowTitle;
    this.configuration.ClassChangeToastForceShowTitle =
        this.configuration.ToastForceShowTitle;
    this.configuration.QuestToastForceShowTitle =
        this.configuration.ToastForceShowTitle;

    this.configuration.ImGuiWideTextToastWindowWidthMult =
        this.configuration.ImGuiToastWindowWidthMult;
    this.configuration.ImGuiErrorToastWindowWidthMult =
        this.configuration.ImGuiToastWindowWidthMult;
    this.configuration.ImGuiAreaToastWindowWidthMult =
        this.configuration.ImGuiToastWindowWidthMult;
    this.configuration.ImGuiClassChangeToastWindowWidthMult =
        this.configuration.ImGuiToastWindowWidthMult;
    this.configuration.ImGuiQuestToastWindowWidthMult =
        this.configuration.ImGuiToastWindowWidthMult;

    this.configuration.ImGuiWideTextToastWindowPosCorrection =
        this.configuration.ImGuiToastWindowPosCorrection;
    this.configuration.ImGuiErrorToastWindowPosCorrection =
        this.configuration.ImGuiToastWindowPosCorrection;
    this.configuration.ImGuiAreaToastWindowPosCorrection =
        this.configuration.ImGuiToastWindowPosCorrection;
    this.configuration.ImGuiClassChangeToastWindowPosCorrection =
        this.configuration.ImGuiToastWindowPosCorrection;
    this.configuration.ImGuiQuestToastWindowPosCorrection =
        this.configuration.ImGuiToastWindowPosCorrection;

    this.configuration.OverlayWideTextToastTextColor =
        this.configuration.OverlayToastTextColor;
    this.configuration.OverlayErrorToastTextColor =
        this.configuration.OverlayToastTextColor;
    this.configuration.OverlayAreaToastTextColor =
        this.configuration.OverlayToastTextColor;
    this.configuration.OverlayClassChangeToastTextColor =
        this.configuration.OverlayToastTextColor;
    this.configuration.OverlayQuestToastTextColor =
        this.configuration.OverlayToastTextColor;

    this.configuration.WideTextToastBackgroundOpacity = 1f;
    this.configuration.ErrorToastBackgroundOpacity = 0f;
    this.configuration.AreaToastBackgroundOpacity = 1f;
    this.configuration.ClassChangeToastBackgroundOpacity = 1f;
    this.configuration.QuestToastBackgroundOpacity = 1f;

    this.configuration.UseImGuiForWideTextToast =
        this.configuration.UseImGuiForToasts;
    this.configuration.UseImGuiForErrorToast =
        this.configuration.UseImGuiForToasts;
    this.configuration.UseImGuiForAreaToast =
        this.configuration.UseImGuiForToasts;
    this.configuration.UseImGuiForClassChangeToast =
        this.configuration.UseImGuiForToasts;
    this.configuration.UseImGuiForQuestToast =
        this.configuration.UseImGuiForToasts;

    this.configuration.Version = 10;

    SaveConfig(this.configuration);
  }

  /// <summary>
  ///     Migrates legacy overlay booleans and the shared swap toggle into
  ///     per-surface translation display modes.
  /// </summary>
  public void MigrateOverlayDisplayModes()
  {
    if (this.configuration.Version >= 13)
    {
      return;
    }

    this.configuration.TalkTranslationDisplayMode =
        ResolveLegacyOverlayDisplayMode(
            this.configuration.UseImGuiForTalk,
            this.configuration.SwapTextsUsingImGui);
    this.configuration.BattleTalkTranslationDisplayMode =
        ResolveLegacyOverlayDisplayMode(
            this.configuration.UseImGuiForBattleTalk,
            this.configuration.SwapTextsUsingImGui);
    this.configuration.TalkSubtitleTranslationDisplayMode =
        ResolveLegacyOverlayDisplayMode(
            this.configuration.UseImGuiForTalkSubtitle,
            this.configuration.SwapTextsUsingImGui);
    this.configuration.MiniTalkTranslationDisplayMode =
        ResolveLegacyOverlayDisplayMode(
            this.configuration.UseImGuiForMiniTalk,
            this.configuration.SwapTextsUsingImGui);
    this.configuration.CutSceneSelectStringTranslationDisplayMode =
        ResolveLegacyOverlayDisplayMode(
            this.configuration.UseImGuiForCutSceneSelectString,
            this.configuration.SwapTextsUsingImGui);
    this.configuration.WideTextToastTranslationDisplayMode =
        ResolveLegacyOverlayDisplayMode(
            this.configuration.UseImGuiForWideTextToast,
            this.configuration.SwapTextsUsingImGui);
    this.configuration.ErrorToastTranslationDisplayMode =
        ResolveLegacyOverlayDisplayMode(
            this.configuration.UseImGuiForErrorToast,
            this.configuration.SwapTextsUsingImGui);
    this.configuration.AreaToastTranslationDisplayMode =
        ResolveLegacyOverlayDisplayMode(
            this.configuration.UseImGuiForAreaToast,
            this.configuration.SwapTextsUsingImGui);
    this.configuration.ClassChangeToastTranslationDisplayMode =
        ResolveLegacyOverlayDisplayMode(
            this.configuration.UseImGuiForClassChangeToast,
            this.configuration.SwapTextsUsingImGui);
    this.configuration.TextGimmickHintTranslationDisplayMode =
        ResolveLegacyOverlayDisplayMode(
            this.configuration.UseImGuiForTextGimmickHint,
            this.configuration.SwapTextsUsingImGui);
    this.configuration.QuestToastTranslationDisplayMode =
        ResolveLegacyOverlayDisplayMode(
            this.configuration.UseImGuiForQuestToast,
            this.configuration.SwapTextsUsingImGui);

    this.configuration.Version = 13;
    SaveConfig(this.configuration);
  }

  /// <summary>
  ///     Migrates legacy persisted translation engine ids and stamps the config
  ///     with the current engine-selection schema version.
  /// </summary>
  /// <param name="loadedConfigVersion">
  ///     The configuration version loaded from disk
  ///     before migrations.
  /// </param>
  public void MigrateTranslationEngineSelection(int loadedConfigVersion)
  {
    var changed = TranslationEngineSelectionMigrationHelper
        .NormalizeAndSyncSelection(
            this.configuration,
            loadedConfigVersion,
            LangDict.TryGetValue(this.configuration.Lang, out var language)
                ? language.SupportedEngines
                : null);

    if (changed)
    {
      SaveConfig(this.configuration);
    }
  }

  /// <summary>
  ///     Migrates legacy separate MainCommand and AddonContextMenuTitle
  ///     translation settings into one unified game-main-menu scope while
  ///     preserving the existing persisted config fields.
  /// </summary>
  public void MigrateGameMainMenuTranslationSettings()
  {
    if (this.configuration.NormalizeGameMainMenuTranslationSettings())
    {
      SaveConfig(this.configuration);
    }
  }

  /// <summary>
  ///     Saves the current configuration to the plugin config file.
  /// </summary>
  public static void SaveConfig(Config config)
  {
    config.NormalizeGameMainMenuTranslationSettings();
    TranslationEngineSelectionMigrationHelper.NormalizeAndSyncSelection(
        config,
        config.Version);

    try
    {
      PluginInterface.SavePluginConfig(config);
    }
    catch (Exception ex)
    {
      PluginRuntimeLog.Error(
          "Config",
          $"Failed to save plugin configuration: {ex}");
    }

    activeInstance?.OnConfigurationSaved(config);
  }

  /// <summary>
  ///     Rebuilds the shared translation service using the current config while
  ///     preventing engine-constructor failures from tearing down the plugin.
  /// </summary>
  public void RebuildTranslationServiceSafely()
  {
    this.configuration.NormalizeGameMainMenuTranslationSettings();
    TranslationEngineSelectionMigrationHelper.NormalizeAndSyncSelection(
        this.configuration,
        this.configuration.Version,
        LangDict.TryGetValue(this.configuration.Lang, out var language)
            ? language.SupportedEngines
            : null);
    TranslationService = this.CreateTranslationServiceSafely();
    ChosenTransEngine = this.configuration.ChosenTransEngine;
    transEngineName = ((TransEngines)ChosenTransEngine).ToString();
  }

  /// <summary>
  ///     Creates a translation service using the current config, retrying known
  ///     legacy engine-collision repairs and falling back to a safe no-op
  ///     translator when instantiation still fails.
  /// </summary>
  /// <returns>The created translation service.</returns>
  private TranslationService CreateTranslationServiceSafely()
  {
    try
    {
      return new TranslationService(
          this.configuration,
          PluginLog,
          Sanitizer);
    }
    catch (Exception ex)
    {
      PluginRuntimeLog.Error(
          $"Failed to initialize translation service for engine id {this.configuration.ChosenTransEngine}: {ex}");

      PluginRuntimeLog.Warning(
          "Falling back to an unavailable translator to keep the plugin loaded.");
      return new TranslationService(
          Sanitizer.Sanitize,
          new UnavailableTranslator(),
          this.configuration.ChosenTransEngine);
    }
  }

  /// <summary>
  ///     Shows a persistent notification recommending that the user review the
  ///     plugin configuration after a config version upgrade migration.
  /// </summary>
  /// <param name="loadedConfigVersion">
  ///     The configuration version loaded from disk before migrations.
  /// </param>
  /// <param name="currentConfigVersion">
  ///     The current configuration schema version supported by the plugin.
  /// </param>
  private void TryShowConfigVersionUpgradeNotification(
      int loadedConfigVersion,
      int currentConfigVersion)
  {
    if (loadedConfigVersion >= currentConfigVersion)
    {
      return;
    }

    var notification = new Notification
    {
      Title = Resources.Name,
      Content = Resources.ResourceManager.GetString(
                    nameof(Resources.ConfigVersionUpgradeRecommendedMessage),
                    this.cultureInfo) ??
                Resources.ConfigVersionUpgradeRecommendedMessage,
      Icon = FontAwesomeIcon.ExclamationTriangle.ToNotificationIcon(),
      Type = NotificationType.Warning,
      UserDismissable = true,
      InitialDuration = TimeSpan.MaxValue,
      HardExpiry = DateTime.MaxValue,
    };

    var activeNotification = NotificationManager.AddNotification(notification);
    Action<Dalamud.Interface.ImGuiNotification.EventArgs.INotificationDrawArgs>?
        drawActions = null;
    var openConfigurationLabel = Resources.ResourceManager.GetString(
                                     nameof(Resources.OpenConfigurationButtonLabel),
                                     this.cultureInfo) ??
                                 Resources.OpenConfigurationButtonLabel;

    drawActions = _ =>
    {
      if (!ImGui.Button(openConfigurationLabel))
      {
        return;
      }

      this.ConfigWindow();
      if (drawActions is not null)
      {
        activeNotification.DrawActions -= drawActions;
      }

      activeNotification.DismissNow();
    };

    activeNotification.DrawActions += drawActions;
  }

  /// <summary>
  /// Forces translation off when the selected language or engine cannot support
  /// an active translated runtime state.
  /// </summary>
  private void EnforceTranslationActivationConstraints()
  {
    var blockReason = TranslationActivationGuard.GetBlockReason(
        this.configuration,
        SelectedLanguage);
    if (blockReason == TranslationActivationGuard.BlockReason.None ||
        !this.configuration.Translate)
    {
      return;
    }

    this.configuration.Translate = false;
    SaveConfig(this.configuration);
  }

  /// <summary>
  /// Shows a persistent notification when translation is blocked by missing
  /// required assets or incomplete engine configuration.
  /// </summary>
  private void TryShowTranslationActivationBlockedNotification()
  {
    var blockReason = TranslationActivationGuard.GetBlockReason(
        this.configuration,
        SelectedLanguage);
    if (blockReason is not TranslationActivationGuard.BlockReason
        .MissingRequiredAssets &&
        blockReason is not TranslationActivationGuard.BlockReason
        .EngineConfigurationIncomplete)
    {
      this.translationActivationBlockedNotificationSignature = null;
      return;
    }

    var signature =
        $"{(int)blockReason}:{this.configuration.Lang}:{this.configuration.ChosenTransEngine}";
    if (string.Equals(
            signature,
            this.translationActivationBlockedNotificationSignature,
            StringComparison.Ordinal))
    {
      return;
    }

    this.translationActivationBlockedNotificationSignature = signature;

    var notification = new Notification
    {
      Title = Resources.Name,
      Content = blockReason ==
                TranslationActivationGuard.BlockReason.MissingRequiredAssets
          ? Resources.ResourceManager.GetString(
                nameof(Resources
                    .TranslationBlockedByMissingAssetsNotificationText),
                this.cultureInfo) ??
            Resources.TranslationBlockedByMissingAssetsNotificationText
          : Resources.ResourceManager.GetString(
                nameof(Resources
                    .TranslationBlockedByEngineConfigurationNotificationText),
                this.cultureInfo) ??
            Resources.TranslationBlockedByEngineConfigurationNotificationText,
      Icon = FontAwesomeIcon.ExclamationTriangle.ToNotificationIcon(),
      Type = NotificationType.Warning,
      UserDismissable = true,
      InitialDuration = TimeSpan.MaxValue,
      HardExpiry = DateTime.MaxValue,
    };

    var activeNotification = NotificationManager.AddNotification(notification);
    Action<Dalamud.Interface.ImGuiNotification.EventArgs.INotificationDrawArgs>?
        drawActions = null;
    var openConfigurationLabel = Resources.ResourceManager.GetString(
                                     nameof(Resources.OpenConfigurationButtonLabel),
                                     this.cultureInfo) ??
                                 Resources.OpenConfigurationButtonLabel;

    drawActions = _ =>
    {
      if (!ImGui.Button(openConfigurationLabel))
      {
        return;
      }

      this.ConfigWindow();
      if (drawActions is not null)
      {
        activeNotification.DrawActions -= drawActions;
      }

      activeNotification.DismissNow();
    };

    activeNotification.DrawActions += drawActions;
  }

  /// <summary>
  ///     Resolves a per-surface translation display mode from the historical
  ///     overlay boolean plus the shared swap toggle.
  /// </summary>
  /// <param name="useOverlay">Whether the surface used its overlay path.</param>
  /// <param name="swapTexts">
  ///     Whether the overlay showed the original while native UI received the
  ///     translation.
  /// </param>
  /// <returns>The migrated display mode.</returns>
  private static JournalTranslationDisplayMode ResolveLegacyOverlayDisplayMode(
      bool useOverlay,
      bool swapTexts)
  {
    if (!useOverlay)
    {
      return JournalTranslationDisplayMode.NativeUiTranslation;
    }

    return swapTexts
        ? JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips
        : JournalTranslationDisplayMode.TooltipTranslation;
  }

  /// <summary>
  ///     Creates an image containing the given text.
  ///     NOTE: the image should be disposed after use.
  /// </summary>
  /// <param name="text">Text to draw.</param>
  /// <param name="fontOptional">Font to use, defaults to Control.DefaultFont.</param>
  /// <param name="textColorOptional">Text color, defaults to Black.</param>
  /// <param name="backColorOptional">Background color, defaults to white.</param>
  /// <param name="minSizeOptional">
  ///     Minimum image size, defaults the size required to
  ///     display the text.
  /// </param>
  /// <returns>The image containing the text, which should be disposed after use.</returns>
  public Image DrawText(
      string text,
      Font? fontOptional = null,
      Color? textColorOptional = null,
      Color? backColorOptional = null,
      Size? minSizeOptional = null)
  {
    PluginRuntimeLog.Debug("Inside image creation method");

    PrivateFontCollection pfc = new();
    pfc.AddFontFile(
        $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}{SpecialFontFileName}");

    Font font = new(
        pfc.Families[0],
        this.configuration.FontSize,
        FontStyle.Regular);
    if (fontOptional != null)
    {
      font = fontOptional;
    }

    var textColor = Color.White;
    if (textColorOptional != null)
    {
      textColor = (Color)textColorOptional;
    }

    var backColor = Color.Black;
    if (backColorOptional != null)
    {
      backColor = (Color)backColorOptional;
    }

    var minSize = Size.Empty;
    if (minSizeOptional != null)
    {
      minSize = (Size)minSizeOptional;
    }

    // first, create a dummy bitmap just to get a graphics object
    SizeF textSize;
    using (Image img = new Bitmap(1, 1))
    {
      using (var drawing = Graphics.FromImage(img))
      {
        // measure the string to see how big the image needs to be
        textSize = drawing.MeasureString(text, font);
        if (!minSize.IsEmpty)
        {
          textSize.Width = textSize.Width > minSize.Width
              ? textSize.Width
              : minSize.Width;
          textSize.Height = textSize.Height > minSize.Height
              ? textSize.Height
              : minSize.Height;
        }
      }
    }

    // create a new image of the right size
    Image textAsImage = new Bitmap(
        (int)textSize.Width,
        (int)textSize.Height);
    using (var drawing = Graphics.FromImage(textAsImage))
    {
      // paint the background
      drawing.Clear(backColor);

      // create a brush for the text
      using (Brush textBrush = new SolidBrush(textColor))
      {
        drawing.DrawString(text, font, textBrush, 0, 0);
        drawing.Save();
      }
    }

    PluginRuntimeLog.Debug("Before returning the image created");

    return textAsImage;
  }

  /// <summary>
  ///     Converts Image to byte array.
  /// </summary>
  /// <param name="image">Image to be converted.</param>
  /// <returns>Byte array to be used elsewhere.</returns>
  private byte[] TranslationImageConverter(Image image)
  {
    PluginRuntimeLog.Debug("Conversion to byte");

    var imageConverter = new ImageConverter();
    return (byte[])imageConverter.ConvertTo(image, typeof(byte[]));
  }

  /// <summary>
  /// Assigns a new value to the target variable if it has changed.
  /// </summary>
  /// <typeparam name="T">The type of the variable to be assigned.</typeparam>
  /// <param name="target">The reference to the target variable.</param>
  /// <param name="newValue">The new value to assign.</param>
  /// <returns>True if the value has changed; otherwise, false.</returns>
  private static bool AssignIfChanged<T>(ref T target, T newValue)
      where T : IEquatable<T>
  {
    if (target.Equals(newValue))
    {
      return false;
    }

    target = newValue;
    return true;
  }

  /// <summary>
  ///     Checks if the given time string is in a valid format (e.g., "123:45").
  /// </summary>
  /// <param name="time">Time in string format.</param>
  /// <returns>Returns true if the input is valide time information.</returns>
  internal static bool IsValidTimeFormat(string time)
  {
    // PluginRuntimeLog.Debug($"Checking time format: {time}");
    var pattern = @"(\d{1,3}):(\d{2})";
    var match = Regex.Match(time, pattern);

    if (match.Success)
    {
      var minutes = int.Parse(match.Groups[1].Value);
      var seconds = int.Parse(match.Groups[2].Value);
      return minutes < 1000 && seconds < 60;
    }

    return false;
  }

  /// <summary>
  ///     Cleans a string by removing line breaks, carriage returns, and double
  ///     spaces.
  /// </summary>
  /// <param name="input">The string to be cleaned.</param>
  /// <returns>Cleaned string.</returns>
  public static string CleanString(string input)
  {
    PluginRuntimeLog.Debug($"Cleaning string: {input}");
    if (string.IsNullOrEmpty(input))
    {
      PluginRuntimeLog.Debug("Input is null or empty, returning as is.");
      return input;
    }

    // Check if the string ends with exactly 5 spaces
    var endsWithFiveSpaces = input.EndsWith("     ");

    // Remove line breaks and carriage returns
    var result = input.Replace("\r", string.Empty)
        .Replace("\n", string.Empty);

    // Remove double spaces when they are between two letters
    result = Regex.Replace(result, @"(?<=\S) {2,}(?=\S)", " ");

    // Reattach the 5 spaces if they were originally present
    if (endsWithFiveSpaces)
    {
      result += "     ";
    }

    return result;
  }

  /// <summary>
  ///     Removes diacritics from a string based on a set of supported characters.
  /// </summary>
  /// <param name="text">Text to be cleaned of diacritics.</param>
  /// <param name="supportedChars">
  ///     List of chars to be parsed into their plain Latin
  ///     chars.
  /// </param>
  /// <returns>Cleand text.</returns>
  public string RemoveDiacritics(string text, HashSet<char> supportedChars)
  {
    // PluginRuntimeLog.Debug(
    //     $"Removing diacritics from text: {text}, supportedChars count: {supportedChars.Count}");
    if (string.IsNullOrEmpty(text))
    {
      // PluginRuntimeLog.Debug("Text is null or empty, returning as is.");
      return text;
    }

    var stringBuilder = new StringBuilder();

    foreach (var c in text)
    {
      if (supportedChars.Contains(c))
      {
        // Directly append supported characters without alteration
        stringBuilder.Append(c);
      }
      else if (CustomReplacements.ContainsKey(c))
      {
        // Replace with custom replacement if character is not in supportedChars
        stringBuilder.Append(CustomReplacements[c]);
      }
      else
      {
        // Normalize and handle diacritics for the remaining characters
        var normalizedChar =
            c.ToString().Normalize(NormalizationForm.FormD);
        foreach (var nc in normalizedChar)
        {
          var unicodeCategory =
              CharUnicodeInfo.GetUnicodeCategory(nc);
          if (unicodeCategory != UnicodeCategory.NonSpacingMark)
          {
            stringBuilder.Append(nc);
          }
        }
      }
    }

    return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
  }

  /// <summary>
  ///     Gets the game version from the framework.
  /// </summary>
  /// <returns>Game version as a string.</returns>
  public /* void*/ static string? GetGameVersion()
  {
    // var gameVersion = Framework.Instance()->GameVersionString;

    var gv = DManager.GameData.Repositories?["ffxiv"].Version;
    // PluginRuntimeLog.Debug($"Game Version: {gv}");

    return gv;
  }

  /// <summary>
  ///     Parses a string into a dictionary.
  /// </summary>
  /// <param name="input">
  ///     String in this format:
  ///     "key1|value1|key2|value2|key3|value3|..."'.
  /// </param>
  /// <returns>The string converted into a Dictionary.</returns>
  public Dictionary<int, string> ParseStringToDictionary(string input)
  {
    // input string must obey this format "key1|value1|key2|value2|key3|value3|..."
    var dictionary = input.Split('|')
        .Select((value, index) => new { value, index })
        .GroupBy(x => x.index / 2)
        .Where(g => int.TryParse(g.First().value, out _)).ToDictionary(
            g => int.Parse(g.First().value),
            g => g.Skip(1).First().value);

    // Output the dictionary as JSON
    var jsonOutput = JsonConvert.SerializeObject(
        dictionary,
        Formatting.Indented);
    PluginRuntimeLog.Debug($"Parsed Dictionary JSON: {jsonOutput}");

    return dictionary;
  }

  /// <summary>
  ///     Checks if the player is in an instance.
  /// </summary>
  /// <returns>The information of which instance the player is in.</returns>
  public unsafe Tuple<bool, int> IsInInstance()
  {
    if (!FrameworkAccessGuard.TryGetEventFramework(out var eventFramework))
    {
      return new Tuple<bool, int>(false, 0);
    }

    var icDirector = eventFramework->GetInstanceContentDirector();

    var isInstanceContent =
        icDirector != null && icDirector->InstanceContentType != 0;

    if (isInstanceContent)
    {
      PluginRuntimeLog.Debug(
          $"IsInstance: {isInstanceContent}, InstanceContentType: {icDirector->InstanceContentType}");
    }

    return new Tuple<bool, int>(
        isInstanceContent,
        icDirector != null ? (int)icDirector->InstanceContentType : 0);
  }

  /// <summary>
  ///     Checks if the translation should be disabled based on the current state.
  /// </summary>
  /// <returns>The player state true if the player is in PVP.</returns>
  public bool DisableTranslationAccordingToState()
  {
    var state = ClientStateInterface.IsPvP ||
                ClientStateInterface.IsPvPExcludingDen;

    return state;
  }

  /// <summary>
  /// Serializes a dictionary of int to byte[] into a single reversible byte array.
  /// </summary>
  public static byte[] SerializeDictionary(Dictionary<int, byte[]> data)
  {
    using var ms = new MemoryStream();
    using var writer = new BinaryWriter(ms);

    writer.Write(data.Count);

    foreach (var (key, value) in data)
    {
      writer.Write(key);
      writer.Write(value.Length);
      writer.Write(value);
    }

    return ms.ToArray();
  }

  /// <summary>
  /// Deserializes a byte array back into a dictionary of int to byte[].
  /// </summary>
  public static Dictionary<int, byte[]> DeserializeDictionary(byte[] rawData)
  {
    var result = new Dictionary<int, byte[]>();

    using var ms = new MemoryStream(rawData);
    using var reader = new BinaryReader(ms);

    var count = reader.ReadInt32();

    for (int i = 0; i < count; i++)
    {
      var key = reader.ReadInt32();
      var length = reader.ReadInt32();
      var value = reader.ReadBytes(length);

      result[key] = value;
    }

    return result;
  }

  /// <summary>
  /// Parses a string in the format "s0|value0|s1|value1|..." into a dictionary of index to value.
  /// </summary>
  /// <param name="input">The input string.</param>
  /// <returns>A dictionary mapping int indices to their respective string values.</returns>
  public static Dictionary<int, string> ParseStringArraySerializedText(string input)
  {
    var result = new Dictionary<int, string>();

    if (string.IsNullOrWhiteSpace(input))
      return result;

    var parts = input.Split('|');

    if (parts.Length % 2 != 0)
    {
      PluginRuntimeLog.Warning($"[ParseStringArraySerializedText] Malformed input string. Odd number of segments: {input}");
      return result;
    }

    for (int i = 0; i < parts.Length; i += 2)
    {
      var keyPart = parts[i];
      var valuePart = parts[i + 1];

      if (!keyPart.StartsWith("s") || !int.TryParse(keyPart[1..], out int index))
      {
        PluginRuntimeLog.Warning($"[ParseStringArraySerializedText] Invalid key '{keyPart}' at position {i}");
        continue;
      }

      result[index] = valuePart;
    }

    return result;
  }

  /// <summary>
  ///     Builds one or more translation chunks using stable numeric indices so
  ///     the translation response can be parsed back into indexed entries.
  /// </summary>
  /// <param name="entries">The ordered entries to batch.</param>
  /// <param name="maxChunkLength">The maximum chunk length before splitting.</param>
  /// <returns>The serialized translation chunks.</returns>
  public static List<string> BuildIndexedTranslationChunks(
      IReadOnlyList<(int Index, string Text)> entries,
      int maxChunkLength = 4000)
  {
    var chunks = new List<string>();
    if (entries.Count == 0)
    {
      return chunks;
    }

    var builder = new StringBuilder();
    foreach (var (index, text) in entries)
    {
      var pair = $"{index}|{text}";
      if (builder.Length > 0 &&
          builder.Length + pair.Length + 1 > maxChunkLength)
      {
        chunks.Add(builder.ToString());
        builder.Clear();
      }

      if (builder.Length > 0)
      {
        builder.Append('|');
      }

      builder.Append(pair);
    }

    if (builder.Length > 0)
    {
      chunks.Add(builder.ToString());
    }

    return chunks;
  }

  /// <summary>
  ///     Parses translated indexed entries encoded as "index|value|index|value|...".
  /// </summary>
  /// <param name="input">The translated payload.</param>
  /// <returns>A dictionary mapping indices to translated strings.</returns>
  public static Dictionary<int, string> ParseIndexedTranslationPairs(string input)
  {
    var result = new Dictionary<int, string>();

    if (string.IsNullOrWhiteSpace(input))
    {
      return result;
    }

    var parts = input.Split('|');
    if (parts.Length < 2)
    {
      return result;
    }

    for (var i = 0; i + 1 < parts.Length; i += 2)
    {
      if (!int.TryParse(parts[i], out var index))
      {
        continue;
      }

      result[index] = parts[i + 1];
    }

    return result;
  }


}


