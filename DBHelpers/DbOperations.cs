// <copyright file="DbOperations.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;

using Dalamud.Game.Gui.NamePlate;

namespace Echoglossian;

/// <summary>
///  Defines operations for managing and retrieving translation data
/// </summary>
public partial class Echoglossian
{
  public static TalkMessage? FoundTalkMessage { get; set; }

  public ToastMessage? FoundToastMessage { get; set; }

  public static NamePlateMessage? FoundNamePlateMessage { get; set; }

  public static BattleTalkMessage? FoundBattleTalkMessage { get; set; }

  public static TalkSubtitleMessage? FoundTalkSubtitleMessage { get; set; }

  public static MiniTalkMessage? FoundMiniTalkMessage { get; set; }

  public static TextGimmickHintMessage? FoundTextGimmickHintMessage { get; set; }

  public static SelectString? FoundSelectStringMessage { get; set; }

  public static SelectionDialogText? FoundSelectionDialogText { get; set; }

  public static GameWindow? FoundGameWindow { get; set; }

  /// <summary>
  ///     Returns the currently loaded live configuration without re-reading the
  ///     persisted plugin config file from disk.
  /// </summary>
  /// <returns>
  ///     The active configuration instance when the plugin is loaded;
  ///     otherwise, <see langword="null" />.
  /// </returns>
  private static Config? GetActiveConfiguration()
  {
    return activeInstance?.configuration;
  }

  /// <summary>
  ///     Returns whether DB lookups should filter by translation engine using
  ///     the current live configuration.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when stored translations should be filtered
  ///     by engine; otherwise, <see langword="false" />.
  /// </returns>
  private static bool ShouldFilterStoredTranslationsByEngine()
  {
    return GetActiveConfiguration()?.TranslateAlreadyTranslatedTexts == true;
  }

  /// <summary>
  ///     Returns whether translated text should be copied to the clipboard
  ///     using the current live configuration.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when clipboard copy is enabled; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private static bool ShouldCopyTranslationToClipboard()
  {
    return GetActiveConfiguration()?.CopyTranslationToClipboard == true;
  }

  /// <summary>
  ///     Creates or uses the database, applying any pending migrations.
  /// </summary>
  public void CreateOrUseDb()
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);
    PluginRuntimeLog.Debug($"Config dir path: {ConfigDirectory}");
    try
    {
      PluginRuntimeLog.Debug($"Config dir path: {ConfigDirectory}");

      var pendingMigrations = context.Database.GetPendingMigrations().ToList();

      if (pendingMigrations.Count != 0)
      {
        PluginRuntimeLog.Debug(
            $"Pending migrations: {pendingMigrations.Count}");
        context.Database.Migrate();
      }

      var appliedMigrations = context.Database.GetAppliedMigrations().ToList();
      if (appliedMigrations.Count != 0)
      {
        PluginRuntimeLog.Debug(
            $"Last applied migration: {appliedMigrations[^1]}");
      }
      else
      {
        PluginRuntimeLog.Debug("No applied migrations found.");
      }
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Error($"Error creating or using Db: {e}");
    }
    finally
    {
      PluginRuntimeLog.Debug("Db created or used successfully");
    }
  }
  /// <summary>
  ///     Finds and returns a TalkMessage from the database.
  /// </summary>
  /// <param name="talkMessage">TalkMessage to be found on the Database.</param>
  /// <returns>The found <see cref="TalkMessage" />.</returns>
  public TalkMessage? FindAndReturnTalkMessage(TalkMessage talkMessage)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (!TranslationReuseScope.TryCreate(
              this.configuration,
              talkMessage.TranslationEngine,
              out var scope))
      {
        return null;
      }

      var existingTalkMessage = context.TalkMessage.AsNoTracking().Where(t =>
          t.SenderName == talkMessage.SenderName &&
          t.OriginalTalkMessage == talkMessage.OriginalTalkMessage &&
          t.TranslationLang == talkMessage.TranslationLang);
      var candidates = existingTalkMessage.AsEnumerable().Where(t =>
          scope.Matches(
              t.OriginalTalkMessageLang,
              t.TranslationLang,
              t.TranslationEngine));

      var localFoundTalkMessage = OrderTalkMessageLookupQuery(
              candidates.AsQueryable())
          .FirstOrDefault();
      if (localFoundTalkMessage == null ||
          localFoundTalkMessage.OriginalTalkMessage !=
              talkMessage.OriginalTalkMessage)
      {
        return null;
      }

      if (!TranslationPersistenceGuard.IsUsableDialogueTranslation(
              localFoundTalkMessage.OriginalTalkMessage,
              localFoundTalkMessage.TranslatedTalkMessage,
              localFoundTalkMessage.OriginalTalkMessageLang,
              localFoundTalkMessage.TranslationLang))
      {
        return null;
      }

      return localFoundTalkMessage;
    }
    catch (Exception e)
    {
      return null;
    }
  }

  /// <summary>
  /// Finds and returns a ToastMessage from the database.
  /// </summary>
  /// <param name="toastMessage">Formatted ToastMessage to be found in the database</param>
  /// <returns></returns>
  public bool FindToastMessage(ToastMessage toastMessage)
  {
    try
    {
      if (!TranslationReuseScope.TryCreate(
              this.configuration,
              toastMessage.TranslationEngine,
              out var scope))
      {
        this.FoundToastMessage = null;
        return false;
      }

      var cache = this.OtherToastsCache;
      if (cache == null || cache.Count == 0)
      {
        this.LoadAllOtherToasts();
        cache = this.OtherToastsCache;

        if (cache == null || cache.Count == 0)
        {
          return false;
        }
      }

      var existingToastMessage = cache.Where(t =>
          t.OriginalToastMessage == toastMessage.OriginalToastMessage &&
          t.TranslationLang == toastMessage.TranslationLang &&
          t.ToastType == toastMessage.ToastType).Where(t =>
          scope.Matches(
              t.OriginalLang,
              t.TranslationLang,
              t.TranslationEngine));

      var localFoundToastMessage = existingToastMessage.FirstOrDefault();

      PluginRuntimeLog.Debug($"localFoundToasMessage: {localFoundToastMessage}");

      if (localFoundToastMessage == null ||
          localFoundToastMessage.OriginalToastMessage !=
          toastMessage.OriginalToastMessage)
      {
        this.FoundToastMessage = null;
        return false;
      }

      this.FoundToastMessage = localFoundToastMessage;
      return true;
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"FindToastMessage exception {e}");
      return false;
    }
  }

  /// <summary>
  /// Finds and returns a non-error ToastMessage using the in-memory toast cache when
  /// available.
  /// </summary>
  /// <param name="toastMessage">Formatted ToastMessage to be found in the database.</param>
  /// <returns>The matching <see cref="ToastMessage" />, or <see langword="null" />.</returns>
  public ToastMessage? FindAndReturnToastMessage(ToastMessage toastMessage)
  {
    try
    {
      if (!TranslationReuseScope.TryCreate(
              this.configuration,
              toastMessage.TranslationEngine,
              out var scope))
      {
        return null;
      }

      var useErrorCache = string.Equals(
          toastMessage.ToastType,
          "Error",
          StringComparison.OrdinalIgnoreCase);
      var cache = useErrorCache ? this.ErrorToastsCache : this.OtherToastsCache;
      if (cache == null || cache.Count == 0)
      {
        if (useErrorCache)
        {
          this.LoadAllErrorToasts();
          cache = this.ErrorToastsCache;
        }
        else
        {
          this.LoadAllOtherToasts();
          cache = this.OtherToastsCache;
        }

        if (cache == null || cache.Count == 0)
        {
          return null;
        }
      }

      var existingToastMessage = cache.Where(t =>
          t.OriginalToastMessage == toastMessage.OriginalToastMessage &&
          t.TranslationLang == toastMessage.TranslationLang &&
          t.ToastType == toastMessage.ToastType &&
          !string.IsNullOrWhiteSpace(t.TranslatedToastMessage)).Where(t =>
          scope.Matches(
              t.OriginalLang,
              t.TranslationLang,
              t.TranslationEngine));

      return existingToastMessage.FirstOrDefault();
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"FindAndReturnToastMessage exception {e}");
      return null;
    }
  }

  /// <summary>
  ///     Finds and returns a translated world-object nameplate row using the
  ///     shared in-memory nameplate cache.
  /// </summary>
  /// <param name="namePlateMessage">Formatted nameplate message to find.</param>
  /// <returns>The matching row, or <see langword="null" />.</returns>
  public NamePlateMessage? FindAndReturnNamePlateMessage(
      NamePlateMessage namePlateMessage)
  {
    try
    {
      if (namePlateMessage.NamePlateKind == null ||
          string.IsNullOrWhiteSpace(namePlateMessage.OriginalNamePlateText) ||
          !TranslationReuseScope.TryCreate(
              this.configuration,
              namePlateMessage.TranslationEngine,
              out var scope))
      {
        FoundNamePlateMessage = null;
        return null;
      }

      FoundNamePlateMessage = NamePlateCacheManager.TryFindMatch(
          (NamePlateKind)namePlateMessage.NamePlateKind.Value,
          namePlateMessage.OriginalNamePlateText,
          scope);
      return FoundNamePlateMessage;
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"FindAndReturnNamePlateMessage exception {e}");
      FoundNamePlateMessage = null;
      return null;
    }
  }

  /// <summary>
  /// Finds and returns an ErrorToastMessage from the database.
  /// </summary>
  /// <param name="toastMessage">Formatted ErrorToastMessage to be found in the database</param>
  /// <returns></returns>
  public bool FindErrorToastMessage(ToastMessage toastMessage)
  {
    try
    {
      if (!TranslationReuseScope.TryCreate(
              this.configuration,
              toastMessage.TranslationEngine,
              out var scope))
      {
        this.FoundToastMessage = null;
        return false;
      }

      var cache = this.ErrorToastsCache;
      if (cache == null || cache.Count == 0)
      {
        this.LoadAllErrorToasts();
        cache = this.ErrorToastsCache;

        if (cache == null || cache.Count == 0)
        {
          return false;
        }
      }

      var existingToastMessage = cache.Where(t =>
          t.OriginalToastMessage == toastMessage.OriginalToastMessage &&
          t.TranslationLang == toastMessage.TranslationLang &&
          t.ToastType == toastMessage.ToastType).Where(t =>
          scope.Matches(
              t.OriginalLang,
              t.TranslationLang,
              t.TranslationEngine));

      var localFoundToastMessage = existingToastMessage.FirstOrDefault();

      if (localFoundToastMessage == null ||
          localFoundToastMessage.OriginalToastMessage !=
          toastMessage.OriginalToastMessage)
      {
        this.FoundToastMessage = null;
        return false;
      }

      this.FoundToastMessage = localFoundToastMessage;
      return true;
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"FindErrorToastMessage exception {e}");
      return false;
    }
  }

  /// <summary>
  ///     Finds and returns a BattleTalkMessage from the database.
  /// </summary>
  /// <param name="battleTalkMessage">Formatted BattleTalkMessage to be found in the database</param>
  /// <returns></returns>
  public BattleTalkMessage? FindAndReturnBattleTalkMessage(
      BattleTalkMessage battleTalkMessage)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (!TranslationReuseScope.TryCreate(
              this.configuration,
              battleTalkMessage.TranslationEngine,
              out var scope))
      {
        return null;
      }

      var existingBattleTalkMessage = context.BattleTalkMessage.AsNoTracking().Where(t =>
          t.SenderName == battleTalkMessage.SenderName &&
          t.OriginalBattleTalkMessage ==
          battleTalkMessage.OriginalBattleTalkMessage &&
          t.TranslationLang == battleTalkMessage.TranslationLang &&
          t.TranslatedBattleTalkMessage != null &&
          t.TranslatedBattleTalkMessage != string.Empty);
      var candidates = existingBattleTalkMessage.AsEnumerable().Where(t =>
          scope.Matches(
              t.OriginalBattleTalkMessageLang,
              t.TranslationLang,
              t.TranslationEngine));

      var localFoundBattleTalkMessage =
          OrderBattleTalkMessageLookupQuery(candidates.AsQueryable())
              .FirstOrDefault();
      if (localFoundBattleTalkMessage == null ||
          localFoundBattleTalkMessage.OriginalBattleTalkMessage !=
              battleTalkMessage.OriginalBattleTalkMessage)
      {
        return null;
      }

      if (!TranslationPersistenceGuard.IsUsableDialogueTranslation(
              localFoundBattleTalkMessage.OriginalBattleTalkMessage,
              localFoundBattleTalkMessage.TranslatedBattleTalkMessage,
              localFoundBattleTalkMessage.OriginalBattleTalkMessageLang,
              localFoundBattleTalkMessage.TranslationLang))
      {
        return null;
      }

      FoundBattleTalkMessage = localFoundBattleTalkMessage;

      return localFoundBattleTalkMessage;
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"FindAndReturnBattleTalkMessage exception {e}");
      return null;
    }
  }

  /// <summary>
  ///     Finds and returns a QuestPlate from the database.
  ///
  ///     Lookup is content-aware: GameVersion is not used as a filter so that
  ///     existing translations survive game patches. When a row is found and its
  ///     <see cref="QuestPlate.SourceContentHash" /> matches the hash on the
  ///     incoming plate, the translation is still valid. In that case the caller
  ///     should call <see cref="UpdateQuestPlateGameVersion" /> to bump the
  ///     GameVersion field without retranslating. When the hashes differ (content
  ///     changed in a patch), this method returns null, signalling that
  ///     retranslation is needed.
  ///     Rows with no stored hash (legacy rows) are treated as hash-mismatch and
  ///     will be retranslated once so they gain a hash on the next save.
  /// </summary>
  /// <param name="questPlate">Formatted QuestPlate to be found in the database.</param>
  /// <returns>
  ///     The matching plate when translation is still valid, or null when a new
  ///     translation run is required.
  /// </returns>
  public QuestPlate? FindQuestPlate(QuestPlate questPlate)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);
    try
    {
      if (!TranslationReuseScope.TryCreate(
              this.configuration,
              questPlate.TranslationEngine,
              out var scope))
      {
        return null;
      }

      questPlate.GameVersion ??= GetGameVersion();
      QuestLuminaResolver.TryPopulateQuestId(questPlate);

      QuestPlate? localFoundQuestPlate = null;
      var hasQuestId = !string.IsNullOrWhiteSpace(questPlate.QuestId);

      // Look up without GameVersion so that cross-patch reuse is possible.
      if (hasQuestId)
      {
        var questIdMatches = context.QuestPlate.AsNoTracking().Where(t =>
            t.QuestId == questPlate.QuestId &&
            t.TranslationLang == scope.TargetLanguageCode);

        localFoundQuestPlate = SelectPreferredQuestPlate(
            questIdMatches.AsEnumerable(),
            questPlate,
            scope);
      }

      if (localFoundQuestPlate == null &&
          !string.IsNullOrWhiteSpace(questPlate.OriginalQuestMessage))
      {
        var questMessageMatches = context.QuestPlate.AsNoTracking().Where(t =>
            t.QuestName == questPlate.QuestName &&
            t.OriginalQuestMessage == questPlate.OriginalQuestMessage &&
            t.TranslationLang == scope.TargetLanguageCode);

        if (hasQuestId)
        {
          questMessageMatches = questMessageMatches.Where(t =>
              t.QuestId == questPlate.QuestId ||
              t.QuestId == null ||
              t.QuestId == string.Empty);
        }

        localFoundQuestPlate = SelectPreferredQuestPlate(
            questMessageMatches.AsEnumerable(),
            questPlate,
            scope);
      }

      if (localFoundQuestPlate == null &&
          !hasQuestId &&
          !string.IsNullOrWhiteSpace(questPlate.QuestName))
      {
        var questNameMatches = context.QuestPlate.AsNoTracking().Where(t =>
            t.QuestName == questPlate.QuestName &&
            t.TranslationLang == scope.TargetLanguageCode);

        localFoundQuestPlate = SelectPreferredQuestPlate(
            questNameMatches.AsEnumerable(),
            questPlate,
            scope);
      }

      if (localFoundQuestPlate == null)
      {
        return null;
      }

      return localFoundQuestPlate;
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"FindQuestPlate exception: {e}");
      return null;
    }
  }

  /// <summary>
  ///     Updates only the <see cref="QuestPlate.GameVersion" /> and
  ///     <see cref="QuestPlate.UpdatedDate" /> of an existing row without touching
  ///     any translated content. Call this when
  ///     <see cref="FindQuestPlate" /> returned a non-null plate (content hash
  ///     matched), meaning the existing translation is still valid but was stored
  ///     under an older game version.
  /// </summary>
  /// <param name="id">Primary key of the row to update.</param>
  /// <param name="newGameVersion">Current game version string.</param>
  public void UpdateQuestPlateGameVersion(int id, string? newGameVersion)
  {
    if (string.IsNullOrWhiteSpace(newGameVersion))
    {
      return;
    }

    using var context = new EchoglossianDbContext(ConfigDirectory);
    try
    {
      context.QuestPlate
        .Where(t => t.Id == id)
        .ExecuteUpdate(setters => setters
          .SetProperty(t => t.GameVersion, newGameVersion)
          .SetProperty(t => t.UpdatedDate, DateTime.Now));
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"UpdateQuestPlateGameVersion exception: {e}");
    }
  }

  /// <summary>
  ///     Finds a QuestPlate by its name and translation language.
  /// </summary>
  /// <param name="questPlate">Formatted QuestPlate to be found in the database</param>
  /// <returns></returns>
  public QuestPlate? FindQuestPlateByName(QuestPlate questPlate)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);
    try
    {
      if (!TranslationReuseScope.TryCreate(
              this.configuration,
              questPlate.TranslationEngine,
              out var scope))
      {
        return null;
      }

      questPlate.GameVersion ??= GetGameVersion();
      QuestLuminaResolver.TryPopulateQuestId(questPlate);

      QuestPlate? localFoundQuestPlate = null;
      var hasQuestId = !string.IsNullOrWhiteSpace(questPlate.QuestId);

      // Prefer QuestId lookup (stable primary key) when available so that two
      // quests sharing a display name are never confused. Fall back to a
      // legacy-compatible name lookup when only pre-canonical rows exist.
      if (hasQuestId)
      {
        var questIdMatches = context.QuestPlate.AsNoTracking().Where(t =>
            t.QuestId == questPlate.QuestId &&
            t.TranslationLang == scope.TargetLanguageCode);

        localFoundQuestPlate = SelectPreferredQuestPlate(
            questIdMatches.AsEnumerable(),
            questPlate,
            scope);
      }

      if (localFoundQuestPlate == null &&
          !string.IsNullOrWhiteSpace(questPlate.QuestName))
      {
        var questNameMatches = context.QuestPlate.AsNoTracking().Where(t =>
            t.QuestName == questPlate.QuestName &&
            t.TranslationLang == scope.TargetLanguageCode);

        if (hasQuestId)
        {
          questNameMatches = questNameMatches.Where(t =>
              t.QuestId == questPlate.QuestId ||
              t.QuestId == null ||
              t.QuestId == string.Empty);
        }

        localFoundQuestPlate = SelectPreferredQuestPlate(
            questNameMatches.AsEnumerable(),
            questPlate,
            scope);
      }

      return localFoundQuestPlate;
    }
    catch (Exception e)
    {
      return null;
    }
  }

  /// <summary>
  ///     Finds dedicated quest-popup text without requiring a canonical quest
  ///     row to exist.
  /// </summary>
  /// <param name="questPopupText">The popup row to look up.</param>
  /// <returns>The matching popup row, if one exists.</returns>
  public QuestPopupText? FindQuestPopupText(QuestPopupText questPopupText)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);
    try
    {
      if (!TranslationReuseScope.TryCreate(
              this.configuration,
              questPopupText.TranslationEngine,
              out var scope) ||
          string.IsNullOrWhiteSpace(questPopupText.SurfaceName))
      {
        return null;
      }

      var surfaceMatches = context.QuestPopupTexts.AsNoTracking().Where(t =>
          t.SurfaceName == questPopupText.SurfaceName &&
          t.TranslationLang == scope.TargetLanguageCode);
      QuestPopupText? localFoundQuestPopupText = null;
      var hasQuestId = !string.IsNullOrWhiteSpace(questPopupText.QuestId);

      if (hasQuestId)
      {
        var questIdMatches = surfaceMatches.Where(t =>
            t.QuestId == questPopupText.QuestId);
        localFoundQuestPopupText = SelectPreferredQuestPopupText(
            questIdMatches.AsEnumerable(),
            questPopupText,
            scope);
      }

      if (localFoundQuestPopupText == null)
      {
        var popupTextMatches = surfaceMatches.Where(t =>
            t.OriginalTitle == questPopupText.OriginalTitle &&
            t.OriginalBody == questPopupText.OriginalBody);

        if (hasQuestId)
        {
          popupTextMatches = popupTextMatches.Where(t =>
              t.QuestId == questPopupText.QuestId ||
              t.QuestId == null ||
              t.QuestId == string.Empty);
        }

        localFoundQuestPopupText = SelectPreferredQuestPopupText(
            popupTextMatches.AsEnumerable(),
            questPopupText,
            scope);
      }

      return localFoundQuestPopupText;
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"FindQuestPopupText exception: {e}");
      return null;
    }
  }

  /// <summary>
  /// Finds and returns a TalkSubtitleMessage from the database.
  /// </summary>
  /// <param name="talkSubtitleMessage">Formatted TalkSubtitleMessage to be found in the database</param>
  /// <returns></returns>
  public TalkSubtitleMessage? FindAndReturnTalkSubtitleMessage(
      TalkSubtitleMessage talkSubtitleMessage)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);
    try
    {
      if (!TranslationReuseScope.TryCreate(
              this.configuration,
              talkSubtitleMessage.TranslationEngine,
              out var scope))
      {
        return null;
      }

      var existingTalkSubtitleMessage =
          context.TalkSubtitleMessage.AsNoTracking().Where(t =>
              t.OriginalTalkSubtitleMessage == talkSubtitleMessage
                  .OriginalTalkSubtitleMessage && t.TranslationLang ==
              talkSubtitleMessage.TranslationLang);

      var localFoundTalkSubtitleMessage =
          existingTalkSubtitleMessage.AsEnumerable().FirstOrDefault(t =>
              scope.Matches(
                  t.OriginalTalkSubtitleMessageLang,
                  t.TranslationLang,
                  t.TranslationEngine));
      if (localFoundTalkSubtitleMessage == null ||
          localFoundTalkSubtitleMessage.OriginalTalkSubtitleMessage !=
              talkSubtitleMessage.OriginalTalkSubtitleMessage ||
          !TranslationPersistenceGuard.IsUsableDialogueTranslation(
              localFoundTalkSubtitleMessage.OriginalTalkSubtitleMessage,
              localFoundTalkSubtitleMessage.TranslatedTalkSubtitleMessage,
              localFoundTalkSubtitleMessage.OriginalTalkSubtitleMessageLang,
              localFoundTalkSubtitleMessage.TranslationLang))
      {
        return null;
      }

      return localFoundTalkSubtitleMessage;
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"FindAndReturnTalkSubtitleMessage exception {e}");
      return null;
    }
  }

  /// <summary>
  /// Finds and returns a MiniTalkMessage from the database.
  /// </summary>
  /// <param name="miniTalkMessage">Formatted MiniTalkMessage to be found in the database.</param>
  /// <returns>The found <see cref="MiniTalkMessage" /> or <see langword="null" />.</returns>
  public MiniTalkMessage? FindAndReturnMiniTalkMessage(
      MiniTalkMessage miniTalkMessage)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);
    try
    {
      if (!TranslationReuseScope.TryCreate(
              this.configuration,
              miniTalkMessage.TranslationEngine,
              out var scope))
      {
        return null;
      }

      var existingMiniTalkMessage =
          context.MiniTalkMessage.AsNoTracking().Where(t =>
              t.OriginalMiniTalkMessage == miniTalkMessage
                  .OriginalMiniTalkMessage && t.TranslationLang ==
              miniTalkMessage.TranslationLang);

      var localFoundMiniTalkMessage =
          existingMiniTalkMessage.AsEnumerable().FirstOrDefault(t =>
              scope.Matches(
                  t.OriginalMiniTalkMessageLang,
                  t.TranslationLang,
                  t.TranslationEngine));
      if (localFoundMiniTalkMessage == null ||
          localFoundMiniTalkMessage.OriginalMiniTalkMessage !=
              miniTalkMessage.OriginalMiniTalkMessage ||
          !TranslationPersistenceGuard.IsUsableDialogueTranslation(
              localFoundMiniTalkMessage.OriginalMiniTalkMessage,
              localFoundMiniTalkMessage.TranslatedMiniTalkMessage,
              localFoundMiniTalkMessage.OriginalMiniTalkMessageLang,
              localFoundMiniTalkMessage.TranslationLang))
      {
        return null;
      }

      FoundMiniTalkMessage = localFoundMiniTalkMessage;
      return localFoundMiniTalkMessage;
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"FindAndReturnMiniTalkMessage exception {e}");
      return null;
    }
  }

  /// <summary>
  /// Finds and returns a TextGimmickHintMessage from the database.
  /// </summary>
  /// <param name="textGimmickHintMessage">Formatted TextGimmickHintMessage to be found in the database</param>
  /// <returns></returns>
  public TextGimmickHintMessage? FindAndReturnTextGimmickHintMessage(
      TextGimmickHintMessage textGimmickHintMessage)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);
    try
    {
      if (!TranslationReuseScope.TryCreate(
              this.configuration,
              textGimmickHintMessage.TranslationEngine,
              out var scope))
      {
        return null;
      }

      var existingTextGimmickHintMessage =
          context.TextGimmickHintMessage.AsNoTracking().Where(t =>
              t.OriginalText == textGimmickHintMessage.OriginalText &&
              t.TranslationLang == textGimmickHintMessage.TranslationLang);

      var localFoundTextGimmickHintMessage =
          existingTextGimmickHintMessage.AsEnumerable().FirstOrDefault(t =>
              scope.Matches(
                  t.OriginalLang,
                  t.TranslationLang,
                  t.TranslationEngine));
      if (localFoundTextGimmickHintMessage == null ||
          localFoundTextGimmickHintMessage.OriginalText !=
          textGimmickHintMessage.OriginalText)
      {
        return null;
      }

      return localFoundTextGimmickHintMessage;
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"FindAndReturnTextGimmickHintMessage exception {e}");
      return null;
    }
  }

  /// <summary>
  /// Finds and returns a SelectString from the database.
  /// </summary>
  /// <param name="selectString">Formatted SelectString to be found in the database.</param>
  /// <returns>The found <see cref="SelectString" /> or <see langword="null" />.</returns>
  public SelectString? FindAndReturnCutSceneSelectStringMessage(
      SelectString selectString)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);
    try
    {
      if (!TranslationReuseScope.TryCreate(
              this.configuration,
              selectString.TranslationEngine,
              out var scope))
      {
        return null;
      }

      var existingSelectString =
          context.SelectString.AsNoTracking().Where(t =>
              t.OriginalSelectString == selectString.OriginalSelectString &&
              t.OriginalOptionsAsText == selectString.OriginalOptionsAsText &&
              t.TranslationLang == selectString.TranslationLang);

      var localFoundSelectString =
          existingSelectString.AsEnumerable().FirstOrDefault(t =>
              scope.Matches(
                  t.OriginalSelectStringLang,
                  t.TranslationLang,
                  t.TranslationEngine));
      if (localFoundSelectString == null ||
          localFoundSelectString.OriginalSelectString !=
          selectString.OriginalSelectString ||
          localFoundSelectString.OriginalOptionsAsText !=
          selectString.OriginalOptionsAsText)
      {
        return null;
      }

      FoundSelectStringMessage = localFoundSelectString;
      return localFoundSelectString;
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"FindAndReturnCutSceneSelectStringMessage exception {e}");
      return null;
    }
  }

  /// <summary>
  ///     Finds and returns a generic selection-dialog payload from the
  ///     dedicated selection-dialog table.
  /// </summary>
  /// <param name="selectionDialogText">
  ///     The formatted selection-dialog payload to find.
  /// </param>
  /// <returns>
  ///     The found <see cref="SelectionDialogText" />, or
  ///     <see langword="null" />.
  /// </returns>
  public SelectionDialogText? FindSelectionDialogText(
      SelectionDialogText selectionDialogText)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (!TranslationReuseScope.TryCreate(
              this.configuration,
              selectionDialogText.TranslationEngine,
              out var scope) ||
          string.IsNullOrWhiteSpace(selectionDialogText.AddonName) ||
          string.IsNullOrWhiteSpace(selectionDialogText.OriginalTextsAsText))
      {
        FoundSelectionDialogText = null;
        return null;
      }

      var candidates = context.SelectionDialogTexts
          .AsNoTracking()
          .Where(t =>
              t.AddonName == selectionDialogText.AddonName &&
              t.OriginalTextsAsText == selectionDialogText.OriginalTextsAsText)
          .AsEnumerable()
          .Where(t =>
              RuntimeLanguageHelper.LanguagesMatch(
                  t.TranslationLang,
                  selectionDialogText.TranslationLang) &&
              scope.Matches(
                  t.OriginalLang,
                  t.TranslationLang,
                  t.TranslationEngine));
      var localFoundSelectionDialogText = SelectPreferredSelectionDialogText(
          candidates,
          selectionDialogText);
      if (localFoundSelectionDialogText == null ||
          !ShouldSaveToDB(localFoundSelectionDialogText.TranslatedTextsAsText))
      {
        FoundSelectionDialogText = null;
        return null;
      }

      FoundSelectionDialogText = localFoundSelectionDialogText;
      return localFoundSelectionDialogText;
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"FindSelectionDialogText exception {e}");
      FoundSelectionDialogText = null;
      return null;
    }
  }

  /// <summary>
  ///     Finds a dedicated ContextMenu payload scoped by addon, source hash,
  ///     game version, target language, and translation engine.
  /// </summary>
  /// <param name="contextMenuText">The ContextMenu payload to find.</param>
  /// <returns>
  ///     The matching <see cref="ContextMenuText" />, or
  ///     <see langword="null" />.
  /// </returns>
  public ContextMenuText? FindContextMenuText(ContextMenuText contextMenuText)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (string.IsNullOrWhiteSpace(contextMenuText.AddonName) ||
          string.IsNullOrWhiteSpace(contextMenuText.OriginalTextsAsText) ||
          string.IsNullOrWhiteSpace(contextMenuText.GameVersion) ||
          string.IsNullOrWhiteSpace(contextMenuText.SourceContentHash))
      {
        return null;
      }

      return context.ContextMenuTexts
          .AsNoTracking()
          .Where(t =>
              t.AddonName == contextMenuText.AddonName &&
              t.OriginalTextsAsText == contextMenuText.OriginalTextsAsText &&
              t.GameVersion == contextMenuText.GameVersion &&
              t.SourceContentHash == contextMenuText.SourceContentHash)
          .AsEnumerable()
          .Where(t =>
              RuntimeLanguageHelper.LanguagesMatch(
                  t.TranslationLang,
                  contextMenuText.TranslationLang) &&
              LegacyWriteSourceLanguagesMatch(
                  t.OriginalLang,
                  contextMenuText.OriginalLang) &&
              (!this.configuration.TranslateAlreadyTranslatedTexts ||
               t.TranslationEngine == contextMenuText.TranslationEngine) &&
              ShouldSaveToDB(t.TranslatedTextsAsText))
          .OrderByDescending(t => t.UpdatedDate ?? t.CreatedDate ?? DateTime.MinValue)
          .ThenByDescending(t => t.Id)
          .FirstOrDefault();
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"FindContextMenuText exception {e}");
      return null;
    }
  }

  /// <summary>
  ///     Finds a dedicated ToDo payload scoped by addon, source hash, game
  ///     version, target language, and translation engine.
  /// </summary>
  /// <param name="toDoText">The ToDo payload to find.</param>
  /// <returns>
  ///     The matching <see cref="ToDoText" />, or <see langword="null" />.
  /// </returns>
  public ToDoText? FindToDoText(ToDoText toDoText)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (string.IsNullOrWhiteSpace(toDoText.AddonName) ||
          string.IsNullOrWhiteSpace(toDoText.OriginalTextsAsText) ||
          string.IsNullOrWhiteSpace(toDoText.GameVersion) ||
          string.IsNullOrWhiteSpace(toDoText.SourceContentHash))
      {
        return null;
      }

      return context.ToDoTexts
          .AsNoTracking()
          .Where(t =>
              t.AddonName == toDoText.AddonName &&
              t.OriginalTextsAsText == toDoText.OriginalTextsAsText &&
              t.GameVersion == toDoText.GameVersion &&
              t.SourceContentHash == toDoText.SourceContentHash)
          .AsEnumerable()
          .Where(t =>
              RuntimeLanguageHelper.LanguagesMatch(
                  t.TranslationLang,
                  toDoText.TranslationLang) &&
              LegacyWriteSourceLanguagesMatch(t.OriginalLang, toDoText.OriginalLang) &&
              (!this.configuration.TranslateAlreadyTranslatedTexts ||
               t.TranslationEngine == toDoText.TranslationEngine) &&
              ShouldSaveToDB(t.TranslatedTextsAsText))
          .OrderByDescending(t => t.UpdatedDate ?? t.CreatedDate ?? DateTime.MinValue)
          .ThenByDescending(t => t.Id)
          .FirstOrDefault();
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"FindToDoText exception {e}");
      return null;
    }
  }

  /// <summary>
  ///     Finds a dedicated Tooltip addon payload scoped by addon, source hash,
  ///     game version, target language, and translation engine.
  /// </summary>
  /// <param name="tooltipText">The Tooltip payload to find.</param>
  /// <returns>
  ///     The matching <see cref="TooltipText" />, or <see langword="null" />.
  /// </returns>
  public TooltipText? FindTooltipText(TooltipText tooltipText)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (string.IsNullOrWhiteSpace(tooltipText.AddonName) ||
          string.IsNullOrWhiteSpace(tooltipText.OriginalTextsAsText) ||
          string.IsNullOrWhiteSpace(tooltipText.GameVersion) ||
          string.IsNullOrWhiteSpace(tooltipText.SourceContentHash))
      {
        return null;
      }

      return context.TooltipTexts
          .AsNoTracking()
          .Where(t =>
              t.AddonName == tooltipText.AddonName &&
              t.OriginalTextsAsText == tooltipText.OriginalTextsAsText &&
              t.GameVersion == tooltipText.GameVersion &&
              t.SourceContentHash == tooltipText.SourceContentHash)
          .AsEnumerable()
          .Where(t =>
              RuntimeLanguageHelper.LanguagesMatch(
                  t.TranslationLang,
                  tooltipText.TranslationLang) &&
              LegacyWriteSourceLanguagesMatch(
                  t.OriginalLang,
                  tooltipText.OriginalLang) &&
              (!this.configuration.TranslateAlreadyTranslatedTexts ||
               t.TranslationEngine == tooltipText.TranslationEngine) &&
              ShouldSaveToDB(t.TranslatedTextsAsText))
          .OrderByDescending(t => t.UpdatedDate ?? t.CreatedDate ?? DateTime.MinValue)
          .ThenByDescending(t => t.Id)
          .FirstOrDefault();
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"FindTooltipText exception {e}");
      return null;
    }
  }

  /// <summary>
  /// Inserts a TalkMessage record into the database.
  /// </summary>
  /// <param name="talkMessage">Formatted TalkMessage to be inserted into the database</param>
  /// <returns></returns>
  public static async Task<string> InsertTalkData(
      TalkMessage talkMessage,
      CancellationToken cancellationToken = default)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    PluginRuntimeLog.Debug($"TalkMessage to be saved in DB: {talkMessage}");

    try
    {
      if (!ShouldSaveToDB(talkMessage.TranslatedTalkMessage))
      {
        return "No data to save.";
      }

      if (!TranslationPersistenceGuard.IsUsableDialogueTranslation(
              talkMessage.OriginalTalkMessage,
              talkMessage.TranslatedTalkMessage,
              talkMessage.OriginalTalkMessageLang,
              talkMessage.TranslationLang))
      {
        return "No data to save.";
      }

      if (ShouldCopyTranslationToClipboard())
      {
        ImGui.SetClipboardText(talkMessage.ToString());
      }

      context.TalkMessage.Add(talkMessage);

      await context.SaveChangesAsync(cancellationToken);

      return "Data inserted to TalkMessages table.";
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Error($"DB Save Failed: {e.Message}\n{e.StackTrace}");
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  ///     Inserts or refreshes a TalkMessage record for the same source line and
  ///     engine, preserving other historical rows while making the refreshed row
  ///     the most recent candidate for future lookups.
  /// </summary>
  /// <param name="talkMessage">Formatted TalkMessage to be inserted or updated in the database.</param>
  /// <returns>A status string describing the persistence outcome.</returns>
  public static async Task<string> UpsertTalkDataAsync(
      TalkMessage talkMessage,
      CancellationToken cancellationToken = default)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (!ShouldSaveToDB(talkMessage.TranslatedTalkMessage))
      {
        return "No data to save.";
      }

      if (!TranslationPersistenceGuard.IsUsableDialogueTranslation(
              talkMessage.OriginalTalkMessage,
              talkMessage.TranslatedTalkMessage,
              talkMessage.OriginalTalkMessageLang,
              talkMessage.TranslationLang))
      {
        return "No data to save.";
      }

      if (ShouldCopyTranslationToClipboard())
      {
        ImGui.SetClipboardText(talkMessage.ToString());
      }

      var matchingRows = context.TalkMessage.Where(t =>
          t.SenderName == talkMessage.SenderName &&
          t.OriginalTalkMessage == talkMessage.OriginalTalkMessage &&
          t.TranslationLang == talkMessage.TranslationLang &&
          t.TranslationEngine == talkMessage.TranslationEngine);
      var matchingRow = OrderTalkMessageLookupQuery(matchingRows)
          .AsEnumerable()
          .FirstOrDefault(t => LegacyWriteSourceLanguagesMatch(
              t.OriginalTalkMessageLang,
              talkMessage.OriginalTalkMessageLang));
      var now = DateTime.Now;
      if (matchingRow != null)
      {
        matchingRow.TranslatedSenderName = talkMessage.TranslatedSenderName;
        matchingRow.TranslatedTalkMessage = talkMessage.TranslatedTalkMessage;
        matchingRow.RTLLangTranslationImageData =
            talkMessage.RTLLangTranslationImageData;
        matchingRow.UpdatedDate = now;
        context.TalkMessage.Update(matchingRow);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return "Data updated in TalkMessages table.";
      }

      talkMessage.CreatedDate ??= now;
      talkMessage.UpdatedDate ??= now;
      context.TalkMessage.Add(talkMessage);
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
      return "Data inserted to TalkMessages table.";
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Error($"DB Upsert Failed: {e.Message}\n{e.StackTrace}");
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  /// Inserts a BattleTalkMessage record into the database.
  /// </summary>
  /// <param name="battleTalkMessage">Formatted BattleTalkMessage to be inserted into the database</param>
  /// <returns></returns>
  public static string InsertBattleTalkData(
      BattleTalkMessage battleTalkMessage)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (!ShouldSaveToDB(battleTalkMessage.TranslatedBattleTalkMessage))
      {
        return "No data to save.";
      }

      if (!TranslationPersistenceGuard.IsUsableDialogueTranslation(
              battleTalkMessage.OriginalBattleTalkMessage,
              battleTalkMessage.TranslatedBattleTalkMessage,
              battleTalkMessage.OriginalBattleTalkMessageLang,
              battleTalkMessage.TranslationLang))
      {
        return "No data to save.";
      }

      context.BattleTalkMessage.Attach(battleTalkMessage);

      if (ShouldCopyTranslationToClipboard())
      {
        ImGui.SetClipboardText(battleTalkMessage.ToString());
      }

      context.SaveChanges();

      return "Data inserted to BattleTalkMessages table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  ///     Inserts or refreshes a BattleTalkMessage record for the same source
  ///     line and engine, preserving other historical rows while making the
  ///     refreshed row the most recent candidate for future lookups.
  /// </summary>
  /// <param name="battleTalkMessage">Formatted BattleTalkMessage to be inserted or updated in the database.</param>
  /// <returns>A status string describing the persistence outcome.</returns>
  public static async Task<string> UpsertBattleTalkDataAsync(
      BattleTalkMessage battleTalkMessage,
      CancellationToken cancellationToken = default)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (!ShouldSaveToDB(battleTalkMessage.TranslatedBattleTalkMessage))
      {
        return "No data to save.";
      }

      if (!TranslationPersistenceGuard.IsUsableDialogueTranslation(
              battleTalkMessage.OriginalBattleTalkMessage,
              battleTalkMessage.TranslatedBattleTalkMessage,
              battleTalkMessage.OriginalBattleTalkMessageLang,
              battleTalkMessage.TranslationLang))
      {
        return "No data to save.";
      }

      if (ShouldCopyTranslationToClipboard())
      {
        ImGui.SetClipboardText(battleTalkMessage.ToString());
      }

      var matchingRows = context.BattleTalkMessage.Where(t =>
          t.SenderName == battleTalkMessage.SenderName &&
          t.OriginalBattleTalkMessage ==
          battleTalkMessage.OriginalBattleTalkMessage &&
          t.TranslationLang == battleTalkMessage.TranslationLang &&
          t.TranslationEngine == battleTalkMessage.TranslationEngine);
      var matchingRow = OrderBattleTalkMessageLookupQuery(matchingRows)
          .AsEnumerable()
          .FirstOrDefault(t => LegacyWriteSourceLanguagesMatch(
              t.OriginalBattleTalkMessageLang,
              battleTalkMessage.OriginalBattleTalkMessageLang));
      var now = DateTime.Now;
      if (matchingRow != null)
      {
        matchingRow.TranslatedSenderName =
            battleTalkMessage.TranslatedSenderName;
        matchingRow.TranslatedBattleTalkMessage =
            battleTalkMessage.TranslatedBattleTalkMessage;
        matchingRow.RTLLangTranslationImageData =
            battleTalkMessage.RTLLangTranslationImageData;
        matchingRow.UpdatedDate = now;
        context.BattleTalkMessage.Update(matchingRow);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return "Data updated in BattleTalkMessages table.";
      }

      battleTalkMessage.CreatedDate ??= now;
      battleTalkMessage.UpdatedDate ??= now;
      context.BattleTalkMessage.Add(battleTalkMessage);
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
      return "Data inserted to BattleTalkMessages table.";
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Error($"DB Upsert Failed: {e.Message}\n{e.StackTrace}");
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  ///     Orders TalkMessage lookup candidates so the most recently refreshed row
  ///     wins when multiple historical rows exist for the same source line.
  /// </summary>
  /// <param name="query">The TalkMessage query to order.</param>
  /// <returns>The ordered TalkMessage query.</returns>
  public static IQueryable<TalkMessage> OrderTalkMessageLookupQuery(
      IQueryable<TalkMessage> query)
  {
    return query.OrderByDescending(t =>
            t.UpdatedDate ?? t.CreatedDate ?? DateTime.MinValue)
        .ThenByDescending(t => t.Id);
  }

  /// <summary>
  ///     Orders BattleTalkMessage lookup candidates so the most recently
  ///     refreshed row wins when multiple historical rows exist for the same
  ///     source line.
  /// </summary>
  /// <param name="query">The BattleTalkMessage query to order.</param>
  /// <returns>The ordered BattleTalkMessage query.</returns>
  public static IQueryable<BattleTalkMessage> OrderBattleTalkMessageLookupQuery(
      IQueryable<BattleTalkMessage> query)
  {
    return query.OrderByDescending(t =>
            t.UpdatedDate ?? t.CreatedDate ?? DateTime.MinValue)
        .ThenByDescending(t => t.Id);
  }

  /// <summary>
  /// Inserts a TalkSubtitleMessage record into the database.
  /// </summary>
  /// <param name="talkSubtitleMessage">Formatted TalkSubtitleMessage to be inserted into the database</param>
  /// <returns></returns>
  public static string InsertTalkSubtitleData(
      TalkSubtitleMessage talkSubtitleMessage)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (!ShouldSaveToDB(
              talkSubtitleMessage.TranslatedTalkSubtitleMessage))
      {
        return "No data to save.";
      }

      if (!TranslationPersistenceGuard.IsUsableDialogueTranslation(
              talkSubtitleMessage.OriginalTalkSubtitleMessage,
              talkSubtitleMessage.TranslatedTalkSubtitleMessage,
              talkSubtitleMessage.OriginalTalkSubtitleMessageLang,
              talkSubtitleMessage.TranslationLang))
      {
        return "No data to save.";
      }

      context.TalkSubtitleMessage.Attach(talkSubtitleMessage);

      if (ShouldCopyTranslationToClipboard())
      {
        ImGui.SetClipboardText(talkSubtitleMessage.ToString());
      }

      context.SaveChanges();

      return "Data inserted to TalkSubtitleMessages table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  /// Inserts a BattleTalkMessage record asynchronously and observes operation
  /// cancellation before the database commit.
  /// </summary>
  /// <param name="battleTalkMessage">Formatted BattleTalkMessage to insert.</param>
  /// <param name="cancellationToken">The owning operation cancellation token.</param>
  /// <returns>A status string describing the persistence outcome.</returns>
  public static async Task<string> InsertBattleTalkDataAsync(
      BattleTalkMessage battleTalkMessage,
      CancellationToken cancellationToken)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (!ShouldSaveToDB(battleTalkMessage.TranslatedBattleTalkMessage) ||
          !TranslationPersistenceGuard.IsUsableDialogueTranslation(
              battleTalkMessage.OriginalBattleTalkMessage,
              battleTalkMessage.TranslatedBattleTalkMessage,
              battleTalkMessage.OriginalBattleTalkMessageLang,
              battleTalkMessage.TranslationLang))
      {
        return "No data to save.";
      }

      context.BattleTalkMessage.Attach(battleTalkMessage);
      if (ShouldCopyTranslationToClipboard())
      {
        ImGui.SetClipboardText(battleTalkMessage.ToString());
      }

      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
      return "Data inserted to BattleTalkMessages table.";
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  /// Inserts a TalkSubtitleMessage record asynchronously and observes operation
  /// cancellation before the database commit.
  /// </summary>
  /// <param name="talkSubtitleMessage">Formatted TalkSubtitleMessage to insert.</param>
  /// <param name="cancellationToken">The owning operation cancellation token.</param>
  /// <returns>A status string describing the persistence outcome.</returns>
  public static async Task<string> InsertTalkSubtitleDataAsync(
      TalkSubtitleMessage talkSubtitleMessage,
      CancellationToken cancellationToken)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (!ShouldSaveToDB(
              talkSubtitleMessage.TranslatedTalkSubtitleMessage) ||
          !TranslationPersistenceGuard.IsUsableDialogueTranslation(
              talkSubtitleMessage.OriginalTalkSubtitleMessage,
              talkSubtitleMessage.TranslatedTalkSubtitleMessage,
              talkSubtitleMessage.OriginalTalkSubtitleMessageLang,
              talkSubtitleMessage.TranslationLang))
      {
        return "No data to save.";
      }

      context.TalkSubtitleMessage.Attach(talkSubtitleMessage);
      if (ShouldCopyTranslationToClipboard())
      {
        ImGui.SetClipboardText(talkSubtitleMessage.ToString());
      }

      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
      return "Data inserted to TalkSubtitleMessages table.";
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  /// Inserts a MiniTalkMessage record into the database.
  /// </summary>
  /// <param name="miniTalkMessage">Formatted MiniTalkMessage to be inserted into the database</param>
  /// <returns></returns>
  public static async Task<string> InsertMiniTalkData(
      MiniTalkMessage miniTalkMessage,
      CancellationToken cancellationToken = default)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (!ShouldSaveToDB(miniTalkMessage.TranslatedMiniTalkMessage))
      {
        return "No data to save.";
      }

      if (!TranslationPersistenceGuard.IsUsableDialogueTranslation(
              miniTalkMessage.OriginalMiniTalkMessage,
              miniTalkMessage.TranslatedMiniTalkMessage,
              miniTalkMessage.OriginalMiniTalkMessageLang,
              miniTalkMessage.TranslationLang))
      {
        return "No data to save.";
      }

      context.MiniTalkMessage.Add(miniTalkMessage);

      if (ShouldCopyTranslationToClipboard())
      {
        ImGui.SetClipboardText(miniTalkMessage.ToString());
      }

      await context.SaveChangesAsync(cancellationToken);

      return "Data inserted to MiniTalkMessages table.";
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  /// Inserts a TextGimmickHintMessage record into the database.
  /// </summary>
  /// <param name="textGimmickHintMessage">Formatted TextGimmickHintMessage to be inserted into the database</param>
  /// <returns></returns>
  public static async Task<string> InsertTextGimmickHintData(
      TextGimmickHintMessage textGimmickHintMessage)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (!ShouldSaveToDB(textGimmickHintMessage.TranslatedText))
      {
        return "No data to save.";
      }

      context.TextGimmickHintMessage.Add(textGimmickHintMessage);

      if (ShouldCopyTranslationToClipboard())
      {
        ImGui.SetClipboardText(textGimmickHintMessage.ToString());
      }

      await context.SaveChangesAsync();

      return "Data inserted to TextGimmickHintMessages table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  /// Inserts a SelectString record into the database.
  /// </summary>
  /// <param name="selectString">Formatted SelectString to be inserted into the database.</param>
  /// <returns></returns>
  public static async Task<string> InsertCutSceneSelectStringData(
      SelectString selectString)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (!ShouldSaveToDB(selectString.TranslatedSelectString) &&
          !ShouldSaveToDB(selectString.TranslatedOptionsAsText))
      {
        return "No data to save.";
      }

      context.SelectString.Add(selectString);

      if (ShouldCopyTranslationToClipboard())
      {
        ImGui.SetClipboardText(selectString.ToString());
      }

      await context.SaveChangesAsync();

      return "Data inserted to SelectStrings table.";
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Error($"DB Save Failed: {e.Message}\n{e.StackTrace}");
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  ///     Inserts or refreshes one generic selection-dialog row in the
  ///     dedicated selection-dialog table.
  /// </summary>
  /// <param name="selectionDialogText">
  ///     The generic selection-dialog row to save.
  /// </param>
  /// <returns>A status message describing the persistence result.</returns>
  public async Task<string> InsertSelectionDialogTextData(
      SelectionDialogText selectionDialogText)
  {
    await using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (!ShouldSaveToDB(selectionDialogText.TranslatedTextsAsText))
      {
        return "No data to save.";
      }

      selectionDialogText.GameVersion ??= GetGameVersion();
      var existingSelectionDialogText = TryFindSelectionDialogTextForSave(
          context,
          selectionDialogText);
      if (existingSelectionDialogText != null)
      {
        MergeSelectionDialogTextValues(
            existingSelectionDialogText,
            selectionDialogText);
        existingSelectionDialogText.UpdatedDate = DateTime.Now;
        await context.SaveChangesAsync().ConfigureAwait(false);
        return "Data merged into SelectionDialogTexts table.";
      }

      context.SelectionDialogTexts.Attach(selectionDialogText);
      await context.SaveChangesAsync().ConfigureAwait(false);
      return "Data inserted to SelectionDialogTexts table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  ///     Inserts or updates a dedicated ContextMenu payload without sharing
  ///     selection-dialog or game-window persistence.
  /// </summary>
  /// <param name="contextMenuText">The ContextMenu payload to persist.</param>
  /// <returns>A status message describing the persistence result.</returns>
  public async Task<string> InsertContextMenuTextData(
      ContextMenuText contextMenuText)
  {
    await using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (!ShouldSaveToDB(contextMenuText.TranslatedTextsAsText))
      {
        return "No data to save.";
      }

      contextMenuText.GameVersion ??= GetGameVersion();
      var existingContextMenuText = context.ContextMenuTexts
          .Where(t =>
              t.AddonName == contextMenuText.AddonName &&
              t.OriginalTextsAsText == contextMenuText.OriginalTextsAsText &&
              t.GameVersion == contextMenuText.GameVersion &&
              t.SourceContentHash == contextMenuText.SourceContentHash)
          .AsEnumerable()
          .Where(t =>
              RuntimeLanguageHelper.LanguagesMatch(
                  t.TranslationLang,
                  contextMenuText.TranslationLang) &&
              LegacyWriteSourceLanguagesMatch(
                  t.OriginalLang,
                  contextMenuText.OriginalLang) &&
              t.TranslationEngine == contextMenuText.TranslationEngine)
          .OrderByDescending(t => t.UpdatedDate ?? t.CreatedDate ?? DateTime.MinValue)
          .ThenByDescending(t => t.Id)
          .FirstOrDefault();
      if (existingContextMenuText != null)
      {
        existingContextMenuText.TranslatedTextsAsText =
            contextMenuText.TranslatedTextsAsText;
        existingContextMenuText.UpdatedDate = DateTime.Now;
        await context.SaveChangesAsync().ConfigureAwait(false);
        return "Data updated in ContextMenuTexts table.";
      }

      context.ContextMenuTexts.Attach(contextMenuText);
      await context.SaveChangesAsync().ConfigureAwait(false);
      return "Data inserted to ContextMenuTexts table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  ///     Inserts or updates a dedicated ToDo payload without sharing
  ///     game-window, quest, or selection-dialog persistence.
  /// </summary>
  /// <param name="toDoText">The ToDo payload to persist.</param>
  /// <returns>A status message describing the persistence result.</returns>
  public async Task<string> InsertToDoTextData(ToDoText toDoText)
  {
    await using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (!ShouldSaveToDB(toDoText.TranslatedTextsAsText))
      {
        return "No data to save.";
      }

      toDoText.GameVersion ??= GetGameVersion();
      var existingToDoText = context.ToDoTexts
          .Where(t =>
              t.AddonName == toDoText.AddonName &&
              t.OriginalTextsAsText == toDoText.OriginalTextsAsText &&
              t.GameVersion == toDoText.GameVersion &&
              t.SourceContentHash == toDoText.SourceContentHash)
          .AsEnumerable()
          .Where(t =>
              RuntimeLanguageHelper.LanguagesMatch(t.TranslationLang, toDoText.TranslationLang) &&
              LegacyWriteSourceLanguagesMatch(t.OriginalLang, toDoText.OriginalLang) &&
              t.TranslationEngine == toDoText.TranslationEngine)
          .OrderByDescending(t => t.UpdatedDate ?? t.CreatedDate ?? DateTime.MinValue)
          .ThenByDescending(t => t.Id)
          .FirstOrDefault();
      if (existingToDoText != null)
      {
        existingToDoText.TranslatedTextsAsText = toDoText.TranslatedTextsAsText;
        existingToDoText.UpdatedDate = DateTime.Now;
        await context.SaveChangesAsync().ConfigureAwait(false);
        return "Data updated in ToDoTexts table.";
      }

      context.ToDoTexts.Attach(toDoText);
      await context.SaveChangesAsync().ConfigureAwait(false);
      return "Data inserted to ToDoTexts table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  ///     Inserts or updates a dedicated Tooltip addon payload without sharing
  ///     game-window or selection-dialog persistence.
  /// </summary>
  /// <param name="tooltipText">The Tooltip payload to persist.</param>
  /// <returns>A status message describing the persistence result.</returns>
  public async Task<string> InsertTooltipTextData(TooltipText tooltipText)
  {
    await using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (!ShouldSaveToDB(tooltipText.TranslatedTextsAsText))
      {
        return "No data to save.";
      }

      tooltipText.GameVersion ??= GetGameVersion();
      var existingTooltipText = context.TooltipTexts
          .Where(t =>
              t.AddonName == tooltipText.AddonName &&
              t.OriginalTextsAsText == tooltipText.OriginalTextsAsText &&
              t.GameVersion == tooltipText.GameVersion &&
              t.SourceContentHash == tooltipText.SourceContentHash)
          .AsEnumerable()
          .Where(t =>
              RuntimeLanguageHelper.LanguagesMatch(
                  t.TranslationLang,
                  tooltipText.TranslationLang) &&
              LegacyWriteSourceLanguagesMatch(
                  t.OriginalLang,
                  tooltipText.OriginalLang) &&
              t.TranslationEngine == tooltipText.TranslationEngine)
          .OrderByDescending(t => t.UpdatedDate ?? t.CreatedDate ?? DateTime.MinValue)
          .ThenByDescending(t => t.Id)
          .FirstOrDefault();
      if (existingTooltipText != null)
      {
        existingTooltipText.TranslatedTextsAsText =
            tooltipText.TranslatedTextsAsText;
        existingTooltipText.UpdatedDate = DateTime.Now;
        await context.SaveChangesAsync().ConfigureAwait(false);
        return "Data updated in TooltipTexts table.";
      }

      context.TooltipTexts.Attach(tooltipText);
      await context.SaveChangesAsync().ConfigureAwait(false);
      return "Data inserted to TooltipTexts table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  ///  Inserts a ToastMessage record into the database.
  /// </summary>
  /// <param name="toastMessage">Formatted ToastMessage to be inserted into the database</param>
  /// <returns></returns>
  public string InsertErrorToastMessageData(ToastMessage toastMessage)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      bool isInThere;
      if (this.ErrorToastsCache != null &&
          this.ErrorToastsCache.Count > 0)
      {

        PluginRuntimeLog.Debug(
            $"Total ErrorToasts in cache: {this.ErrorToastsCache.Count}");

        isInThere = this.ErrorToastsCache.Exists(t =>
            toastMessage.ToastType == t.ToastType &&
            RuntimeLanguageHelper.LanguagesMatch(
                toastMessage.OriginalLang,
                t.OriginalLang) &&
            toastMessage.TranslationLang == t.TranslationLang &&
            toastMessage.OriginalToastMessage ==
            t.OriginalToastMessage && toastMessage.TranslationEngine ==
            t.TranslationEngine);
      }
      else
      {
        isInThere = false;
      }

      if (isInThere)
      {
        return "Data already in the Db.";
      }

      context.ToastMessage.Attach(toastMessage);

      context.SaveChanges();

      if (this.ErrorToastsCache != null)
      {
        this.AppendToastToCache(this.ErrorToastsCache, toastMessage);
      }

      return "Data inserted to ToastMessages table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  ///  Inserts a ToastMessage record into the database.
  /// </summary>
  /// <param name="toastMessage">Formatted ToastMessage to be inserted into the database</param>
  /// <returns></returns>
  public string InsertOtherToastMessageData(ToastMessage toastMessage)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      bool isInThere;
      if (this.OtherToastsCache != null &&
          this.OtherToastsCache.Count > 0)
      {

        PluginRuntimeLog.Debug(
            $"Total ErrorToasts in cache: {this.OtherToastsCache.Count}");

        isInThere = this.OtherToastsCache.Exists(t =>
            toastMessage.ToastType == t.ToastType &&
            RuntimeLanguageHelper.LanguagesMatch(
                toastMessage.OriginalLang,
                t.OriginalLang) &&
            toastMessage.TranslationLang == t.TranslationLang &&
            toastMessage.OriginalToastMessage ==
            t.OriginalToastMessage && toastMessage.TranslationEngine ==
            t.TranslationEngine);
      }
      else
      {
        isInThere = false;
      }

      if (isInThere)
      {
        return "Data already in the Db.";
      }

      context.ToastMessage.Attach(toastMessage);

      context.SaveChanges();

      if (this.OtherToastsCache != null)
      {
        this.AppendToastToCache(this.OtherToastsCache, toastMessage);
      }

      return "Data inserted to ToastMessages table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  ///     Inserts a world-object nameplate translation record into the database
  ///     and updates the in-memory nameplate cache.
  /// </summary>
  /// <param name="namePlateMessage">Formatted nameplate row to persist.</param>
  /// <returns>The persistence result message.</returns>
  public string InsertNamePlateMessageData(NamePlateMessage namePlateMessage)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      if (!ShouldSaveToDB(namePlateMessage.TranslatedNamePlateText))
      {
        return "No data to save.";
      }

      if (namePlateMessage.NamePlateKind != null &&
          !string.IsNullOrWhiteSpace(namePlateMessage.OriginalNamePlateText) &&
          TranslationReuseScope.TryCreate(
              this.configuration,
              namePlateMessage.TranslationEngine,
              out var scope) &&
          NamePlateCacheManager.TryFindMatch(
              (NamePlateKind)namePlateMessage.NamePlateKind.Value,
              namePlateMessage.OriginalNamePlateText,
              scope) != null)
      {
        return "Data already in the Db.";
      }

      context.NamePlateMessages.Add(namePlateMessage);
      context.SaveChanges();
      NamePlateCacheManager.Update(namePlateMessage);

      return "Data inserted to NamePlateMessages table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  /// Inserts a QuestPlate record into the database.
  /// </summary>
  /// <param name="questPlate">Formatted QuestPlate to be inserted into the database</param>
  /// <returns></returns>
  public string InsertQuestPlate(QuestPlate questPlate)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      QuestLuminaResolver.TryPopulateQuestId(questPlate);

      var existingQuestPlate = this.TryFindQuestPlateForSave(context, questPlate);
      if (existingQuestPlate != null)
      {
        this.MergeQuestPlateValues(existingQuestPlate, questPlate);
        existingQuestPlate.UpdatedDate = DateTime.Now;
        existingQuestPlate.UpdateFieldsAsText();

        if (this.configuration.CopyTranslationToClipboard)
        {
          ImGui.SetClipboardText(existingQuestPlate.ToString());
        }

        context.SaveChanges();
        return "Data merged into QuestPlate table.";
      }

      questPlate.UpdateFieldsAsText();
      context.QuestPlate.Attach(questPlate);

      if (this.configuration.CopyTranslationToClipboard)
      {
        ImGui.SetClipboardText(questPlate.ToString());
      }

      context.SaveChanges();

      return "Data inserted to QuestPlate table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  ///     Inserts or merges dedicated quest-popup text without blocking the
  ///     caller's UI lifecycle path.
  /// </summary>
  /// <param name="questPopupText">The popup row to persist.</param>
  /// <returns>The persistence result.</returns>
  public async Task<string> InsertQuestPopupTextData(QuestPopupText questPopupText)
  {
    await using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      var existingQuestPopupText = TryFindQuestPopupTextForSave(
          context,
          questPopupText);
      if (existingQuestPopupText != null)
      {
        MergeQuestPopupTextValues(existingQuestPopupText, questPopupText);
        existingQuestPopupText.UpdatedDate = DateTime.Now;
        await context.SaveChangesAsync().ConfigureAwait(false);
        return "Data merged into QuestPopupTexts table.";
      }

      context.QuestPopupTexts.Attach(questPopupText);
      await context.SaveChangesAsync().ConfigureAwait(false);
      return "Data inserted to QuestPopupTexts table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  ///  Updates an existing QuestPlate record in the database.
  /// </summary>
  /// <param name="questPlate">QuestPlate to be updated</param>
  /// <returns></returns>
  public string UpdateQuestPlate(QuestPlate questPlate)
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      QuestLuminaResolver.TryPopulateQuestId(questPlate);

      var existingQuestPlate = this.TryFindQuestPlateForSave(context, questPlate);
      if (existingQuestPlate != null)
      {
        this.MergeQuestPlateValues(existingQuestPlate, questPlate);
        existingQuestPlate.UpdatedDate = DateTime.Now;
        existingQuestPlate.UpdateFieldsAsText();

        if (this.configuration.CopyTranslationToClipboard)
        {
          ImGui.SetClipboardText(existingQuestPlate.ToString());
        }

        context.SaveChanges();
        return "Data updated on QuestPlate table.";
      }

      questPlate.UpdateFieldsAsText();
      context.QuestPlate.Update(questPlate);

      if (this.configuration.CopyTranslationToClipboard)
      {
        ImGui.SetClipboardText(questPlate.ToString());
      }

      context.SaveChanges();

      return "Data updated on QuestPlate table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  /// <summary>
  ///     Finds a quest plate for save/merge operations using QuestId first
  ///     and falling back to the existing name-based keys.
  /// </summary>
  /// <param name="context">The active DB context.</param>
  /// <param name="questPlate">The quest plate being saved.</param>
  /// <returns>The existing quest plate if one should be merged; otherwise null.</returns>
  private QuestPlate? TryFindQuestPlateForSave(
      EchoglossianDbContext context,
      QuestPlate questPlate)
  {
    questPlate.GameVersion ??= GetGameVersion();
    var hasGameVersion = !string.IsNullOrWhiteSpace(questPlate.GameVersion);
    var hasQuestId = !string.IsNullOrWhiteSpace(questPlate.QuestId);

    if (hasQuestId)
    {
      var questIdMatches = context.QuestPlate.Where(t =>
          t.QuestId == questPlate.QuestId &&
          t.TranslationEngine == questPlate.TranslationEngine &&
          (!hasGameVersion || t.GameVersion == questPlate.GameVersion));
      var questIdMatch = SelectPreferredQuestPlateForSave(
          questIdMatches.AsEnumerable(),
          questPlate);

      if (questIdMatch != null)
      {
        return questIdMatch;
      }
    }

    if (!string.IsNullOrWhiteSpace(questPlate.OriginalQuestMessage))
    {
      var questMessageMatches = context.QuestPlate.Where(t =>
          t.QuestName == questPlate.QuestName &&
          t.OriginalQuestMessage == questPlate.OriginalQuestMessage &&
          t.TranslationEngine == questPlate.TranslationEngine &&
          (!hasGameVersion || t.GameVersion == questPlate.GameVersion));
      if (hasQuestId)
      {
        questMessageMatches = questMessageMatches.Where(t =>
            t.QuestId == questPlate.QuestId ||
            t.QuestId == null ||
            t.QuestId == string.Empty);
      }

      var questMessageMatch = SelectPreferredQuestPlateForSave(
          questMessageMatches.AsEnumerable(),
          questPlate);

      if (questMessageMatch != null)
      {
        return questMessageMatch;
      }
    }

    var questNameMatches = context.QuestPlate.Where(t =>
        t.QuestName == questPlate.QuestName &&
        t.TranslationEngine == questPlate.TranslationEngine &&
        (!hasGameVersion || t.GameVersion == questPlate.GameVersion));
    if (hasQuestId)
    {
      questNameMatches = questNameMatches.Where(t =>
          t.QuestId == questPlate.QuestId ||
          t.QuestId == null ||
          t.QuestId == string.Empty);
    }

    return SelectPreferredQuestPlateForSave(
        questNameMatches.AsEnumerable(),
        questPlate);
  }

  /// <summary>
  ///     Selects the preferred persisted quest plate for runtime reads,
  ///     favoring canonical rows with a matching hash and the most complete
  ///     translated payload when duplicates exist.
  /// </summary>
  /// <param name="candidateQuestPlates">The candidate persisted quest plates.</param>
  /// <param name="requestedQuestPlate">The requested quest plate.</param>
  /// <param name="scope">The resolved translation reuse scope.</param>
  /// <returns>The preferred persisted quest plate, or <see langword="null" />.</returns>
  private static QuestPlate? SelectPreferredQuestPlate(
      IEnumerable<QuestPlate> candidateQuestPlates,
      QuestPlate requestedQuestPlate,
      TranslationReuseScope scope)
  {
    QuestPlate? preferredQuestPlate = null;
    var preferredIdentityScore = int.MinValue;
    var preferredCompletenessScore = int.MinValue;
    var preferredUpdatedDate = DateTime.MinValue;
    var preferredId = int.MinValue;

    foreach (var candidateQuestPlate in candidateQuestPlates)
    {
      if (!scope.Matches(
              candidateQuestPlate.OriginalLang,
              candidateQuestPlate.TranslationLang,
              candidateQuestPlate.TranslationEngine) ||
          !LegacyWriteSourceLanguagesMatch(
              candidateQuestPlate.OriginalLang,
              requestedQuestPlate.OriginalLang) ||
          !IsQuestPlateContentHashCompatible(
              candidateQuestPlate,
              requestedQuestPlate))
      {
        continue;
      }

      candidateQuestPlate.UpdateFieldsFromText();

      var identityScore = ComputeQuestPlateIdentityScore(
          candidateQuestPlate,
          requestedQuestPlate);
      var completenessScore = ComputeQuestPlateCompletenessScore(
          candidateQuestPlate);
      var updatedDate = candidateQuestPlate.UpdatedDate ??
                        candidateQuestPlate.CreatedDate ??
                        DateTime.MinValue;

      if (preferredQuestPlate != null &&
          identityScore < preferredIdentityScore)
      {
        continue;
      }

      if (preferredQuestPlate != null &&
          identityScore == preferredIdentityScore &&
          completenessScore < preferredCompletenessScore)
      {
        continue;
      }

      if (preferredQuestPlate != null &&
          identityScore == preferredIdentityScore &&
          completenessScore == preferredCompletenessScore &&
          updatedDate < preferredUpdatedDate)
      {
        continue;
      }

      if (preferredQuestPlate != null &&
          identityScore == preferredIdentityScore &&
          completenessScore == preferredCompletenessScore &&
          updatedDate == preferredUpdatedDate &&
          candidateQuestPlate.Id <= preferredId)
      {
        continue;
      }

      preferredQuestPlate = candidateQuestPlate;
      preferredIdentityScore = identityScore;
      preferredCompletenessScore = completenessScore;
      preferredUpdatedDate = updatedDate;
      preferredId = candidateQuestPlate.Id;
    }

    return preferredQuestPlate;
  }

  /// <summary>
  ///     Selects the preferred persisted quest plate for save/merge
  ///     operations, favoring canonical rows and the most complete payload when
  ///     duplicate candidates exist.
  /// </summary>
  /// <param name="candidateQuestPlates">The candidate persisted quest plates.</param>
  /// <param name="requestedQuestPlate">The incoming quest plate.</param>
  /// <returns>The preferred persisted quest plate, or <see langword="null" />.</returns>
  private static QuestPlate? SelectPreferredQuestPlateForSave(
      IEnumerable<QuestPlate> candidateQuestPlates,
      QuestPlate requestedQuestPlate)
  {
    QuestPlate? preferredQuestPlate = null;
    var preferredIdentityScore = int.MinValue;
    var preferredCompletenessScore = int.MinValue;
    var preferredUpdatedDate = DateTime.MinValue;
    var preferredId = int.MinValue;

    foreach (var candidateQuestPlate in candidateQuestPlates)
    {
      if (!RuntimeLanguageHelper.LanguagesMatch(
              candidateQuestPlate.TranslationLang,
              requestedQuestPlate.TranslationLang) ||
          !LegacyWriteSourceLanguagesMatch(
              candidateQuestPlate.OriginalLang,
              requestedQuestPlate.OriginalLang))
      {
        continue;
      }

      candidateQuestPlate.UpdateFieldsFromText();

      var identityScore = ComputeQuestPlateIdentityScore(
          candidateQuestPlate,
          requestedQuestPlate);
      var completenessScore = ComputeQuestPlateCompletenessScore(
          candidateQuestPlate);
      var updatedDate = candidateQuestPlate.UpdatedDate ??
                        candidateQuestPlate.CreatedDate ??
                        DateTime.MinValue;

      if (preferredQuestPlate != null &&
          identityScore < preferredIdentityScore)
      {
        continue;
      }

      if (preferredQuestPlate != null &&
          identityScore == preferredIdentityScore &&
          completenessScore < preferredCompletenessScore)
      {
        continue;
      }

      if (preferredQuestPlate != null &&
          identityScore == preferredIdentityScore &&
          completenessScore == preferredCompletenessScore &&
          updatedDate < preferredUpdatedDate)
      {
        continue;
      }

      if (preferredQuestPlate != null &&
          identityScore == preferredIdentityScore &&
          completenessScore == preferredCompletenessScore &&
          updatedDate == preferredUpdatedDate &&
          candidateQuestPlate.Id <= preferredId)
      {
        continue;
      }

      preferredQuestPlate = candidateQuestPlate;
      preferredIdentityScore = identityScore;
      preferredCompletenessScore = completenessScore;
      preferredUpdatedDate = updatedDate;
      preferredId = candidateQuestPlate.Id;
    }

    return preferredQuestPlate;
  }

  /// <summary>
  ///     Gets the identity score for one candidate quest plate relative to the
  ///     requested quest plate.
  /// </summary>
  /// <param name="candidateQuestPlate">The candidate persisted quest plate.</param>
  /// <param name="requestedQuestPlate">The requested quest plate.</param>
  /// <returns>The identity score. Higher values are preferred.</returns>
  private static int ComputeQuestPlateIdentityScore(
      QuestPlate candidateQuestPlate,
      QuestPlate requestedQuestPlate)
  {
    var identityScore = 0;

    if (!string.IsNullOrWhiteSpace(requestedQuestPlate.QuestId) &&
        string.Equals(
            candidateQuestPlate.QuestId,
            requestedQuestPlate.QuestId,
            StringComparison.Ordinal))
    {
      identityScore += 256;
    }

    if (!string.IsNullOrWhiteSpace(requestedQuestPlate.SourceContentHash) &&
        string.Equals(
            candidateQuestPlate.SourceContentHash,
            requestedQuestPlate.SourceContentHash,
            StringComparison.Ordinal))
    {
      identityScore += 128;
    }

    if (!string.IsNullOrWhiteSpace(requestedQuestPlate.QuestTextSheetName) &&
        string.Equals(
            candidateQuestPlate.QuestTextSheetName,
            requestedQuestPlate.QuestTextSheetName,
            StringComparison.Ordinal))
    {
      identityScore += 64;
    }

    if (!string.IsNullOrWhiteSpace(requestedQuestPlate.GameVersion) &&
        string.Equals(
            candidateQuestPlate.GameVersion,
            requestedQuestPlate.GameVersion,
            StringComparison.Ordinal))
    {
      identityScore += 32;
    }

    if (!string.IsNullOrWhiteSpace(requestedQuestPlate.OriginalQuestMessage) &&
        string.Equals(
            candidateQuestPlate.OriginalQuestMessage,
            requestedQuestPlate.OriginalQuestMessage,
            StringComparison.Ordinal))
    {
      identityScore += 16;
    }

    if (!string.IsNullOrWhiteSpace(requestedQuestPlate.QuestName) &&
        string.Equals(
            candidateQuestPlate.QuestName,
            requestedQuestPlate.QuestName,
            StringComparison.Ordinal))
    {
      identityScore += 8;
    }

    if (!string.IsNullOrWhiteSpace(candidateQuestPlate.QuestId))
    {
      identityScore += 4;
    }

    if (!string.IsNullOrWhiteSpace(candidateQuestPlate.SourceContentHash))
    {
      identityScore += 2;
    }

    if (!string.IsNullOrWhiteSpace(candidateQuestPlate.QuestTextSheetName))
    {
      identityScore += 1;
    }

    return identityScore;
  }

  /// <summary>
  ///     Gets the completeness score for one candidate quest plate.
  /// </summary>
  /// <param name="candidateQuestPlate">The candidate persisted quest plate.</param>
  /// <returns>The completeness score. Higher values are preferred.</returns>
  private static int ComputeQuestPlateCompletenessScore(
      QuestPlate candidateQuestPlate)
  {
    var completenessScore = 0;

    if (!string.IsNullOrWhiteSpace(candidateQuestPlate.TranslatedQuestName))
    {
      completenessScore += 64;
    }

    if (!string.IsNullOrWhiteSpace(candidateQuestPlate.TranslatedQuestMessage))
    {
      completenessScore += 64;
    }

    completenessScore += Math.Min(
        candidateQuestPlate.TranslatedObjectiveRowsByKey.Count,
        32) * 4;
    completenessScore += Math.Min(
        candidateQuestPlate.TranslatedSummaryRowsByKey.Count,
        32) * 4;
    completenessScore += Math.Min(
        candidateQuestPlate.TranslatedSystemRowsByKey.Count,
        32) * 4;
    completenessScore += Math.Min(
        candidateQuestPlate.CanonicalRows.Count,
        32) * 2;

    if (!string.IsNullOrWhiteSpace(candidateQuestPlate.QuestTextSheetName))
    {
      completenessScore += 8;
    }

    if (!string.IsNullOrWhiteSpace(candidateQuestPlate.SourceContentHash))
    {
      completenessScore += 8;
    }

    return completenessScore;
  }

  /// <summary>
  ///     Selects the preferred dedicated quest-popup row for runtime reads.
  /// </summary>
  /// <param name="candidateQuestPopupTexts">The candidate popup rows.</param>
  /// <param name="requestedQuestPopupText">The requested popup row.</param>
  /// <param name="scope">The resolved translation reuse scope.</param>
  /// <returns>The preferred popup row, or <see langword="null" />.</returns>
  private static QuestPopupText? SelectPreferredQuestPopupText(
      IEnumerable<QuestPopupText> candidateQuestPopupTexts,
      QuestPopupText requestedQuestPopupText,
      TranslationReuseScope scope)
  {
    QuestPopupText? preferredQuestPopupText = null;
    var preferredIdentityScore = int.MinValue;
    var preferredCompletenessScore = int.MinValue;
    var preferredUpdatedDate = DateTime.MinValue;
    var preferredId = int.MinValue;

    foreach (var candidateQuestPopupText in candidateQuestPopupTexts)
    {
      if (!scope.Matches(
              candidateQuestPopupText.OriginalLang,
              candidateQuestPopupText.TranslationLang,
              candidateQuestPopupText.TranslationEngine) ||
          !LegacyWriteSourceLanguagesMatch(
              candidateQuestPopupText.OriginalLang,
              requestedQuestPopupText.OriginalLang))
      {
        continue;
      }

      var identityScore = 0;
      if (!string.IsNullOrWhiteSpace(requestedQuestPopupText.QuestId) &&
          string.Equals(
              candidateQuestPopupText.QuestId,
              requestedQuestPopupText.QuestId,
              StringComparison.Ordinal))
      {
        identityScore += 16;
      }

      if (!string.IsNullOrWhiteSpace(requestedQuestPopupText.SourceContentHash) &&
          string.Equals(
              candidateQuestPopupText.SourceContentHash,
              requestedQuestPopupText.SourceContentHash,
              StringComparison.Ordinal))
      {
        identityScore += 8;
      }

      if (!string.IsNullOrWhiteSpace(requestedQuestPopupText.OriginalBody) &&
          string.Equals(
              candidateQuestPopupText.OriginalBody,
              requestedQuestPopupText.OriginalBody,
              StringComparison.Ordinal))
      {
        identityScore += 4;
      }

      if (!string.IsNullOrWhiteSpace(requestedQuestPopupText.OriginalTitle) &&
          string.Equals(
              candidateQuestPopupText.OriginalTitle,
              requestedQuestPopupText.OriginalTitle,
              StringComparison.Ordinal))
      {
        identityScore += 2;
      }

      if (!string.IsNullOrWhiteSpace(candidateQuestPopupText.QuestId))
      {
        identityScore += 1;
      }

      var completenessScore = 0;
      if (!string.IsNullOrWhiteSpace(candidateQuestPopupText.TranslatedTitle))
      {
        completenessScore += 8;
      }

      if (!string.IsNullOrWhiteSpace(candidateQuestPopupText.TranslatedBody))
      {
        completenessScore += 8;
      }

      var updatedDate = candidateQuestPopupText.UpdatedDate ??
                        candidateQuestPopupText.CreatedDate ??
                        DateTime.MinValue;

      if (preferredQuestPopupText != null &&
          identityScore < preferredIdentityScore)
      {
        continue;
      }

      if (preferredQuestPopupText != null &&
          identityScore == preferredIdentityScore &&
          completenessScore < preferredCompletenessScore)
      {
        continue;
      }

      if (preferredQuestPopupText != null &&
          identityScore == preferredIdentityScore &&
          completenessScore == preferredCompletenessScore &&
          updatedDate < preferredUpdatedDate)
      {
        continue;
      }

      if (preferredQuestPopupText != null &&
          identityScore == preferredIdentityScore &&
          completenessScore == preferredCompletenessScore &&
          updatedDate == preferredUpdatedDate &&
          candidateQuestPopupText.Id <= preferredId)
      {
        continue;
      }

      preferredQuestPopupText = candidateQuestPopupText;
      preferredIdentityScore = identityScore;
      preferredCompletenessScore = completenessScore;
      preferredUpdatedDate = updatedDate;
      preferredId = candidateQuestPopupText.Id;
    }

    return preferredQuestPopupText;
  }

  /// <summary>
  ///     Gets whether one persisted quest plate is compatible with the
  ///     requested quest plate's content hash semantics.
  /// </summary>
  /// <param name="candidateQuestPlate">The candidate persisted quest plate.</param>
  /// <param name="requestedQuestPlate">The requested quest plate.</param>
  /// <returns>
  ///     <see langword="true" /> when the candidate may be reused; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private static bool IsQuestPlateContentHashCompatible(
      QuestPlate candidateQuestPlate,
      QuestPlate requestedQuestPlate)
  {
    if (string.IsNullOrWhiteSpace(requestedQuestPlate.SourceContentHash))
    {
      return true;
    }

    return string.Equals(
        candidateQuestPlate.SourceContentHash,
        requestedQuestPlate.SourceContentHash,
        StringComparison.Ordinal);
  }

  /// <summary>
  ///     Finds an existing popup row to merge into during save operations.
  /// </summary>
  /// <param name="context">The active DB context.</param>
  /// <param name="questPopupText">The incoming popup row.</param>
  /// <returns>The preferred existing popup row, if one exists.</returns>
  private static QuestPopupText? TryFindQuestPopupTextForSave(
      EchoglossianDbContext context,
      QuestPopupText questPopupText)
  {
    var surfaceMatches = context.QuestPopupTexts.Where(t =>
        t.SurfaceName == questPopupText.SurfaceName &&
        RuntimeLanguageHelper.LanguagesMatch(
            t.TranslationLang,
            questPopupText.TranslationLang));
    var hasQuestId = !string.IsNullOrWhiteSpace(questPopupText.QuestId);

    if (hasQuestId)
    {
      var questIdMatches = surfaceMatches.Where(t =>
          t.QuestId == questPopupText.QuestId);
      var exactQuestIdMatch = SelectPreferredQuestPopupTextForSave(
          questIdMatches.AsEnumerable(),
          questPopupText);
      if (exactQuestIdMatch != null)
      {
        return exactQuestIdMatch;
      }
    }

    var popupTextMatches = surfaceMatches.Where(t =>
        t.OriginalTitle == questPopupText.OriginalTitle &&
        t.OriginalBody == questPopupText.OriginalBody);

    if (hasQuestId)
    {
      popupTextMatches = popupTextMatches.Where(t =>
          t.QuestId == questPopupText.QuestId ||
          t.QuestId == null ||
          t.QuestId == string.Empty);
    }

    return SelectPreferredQuestPopupTextForSave(
        popupTextMatches.AsEnumerable(),
        questPopupText);
  }

  /// <summary>
  ///     Selects the preferred popup row for save/merge operations.
  /// </summary>
  /// <param name="candidateQuestPopupTexts">The candidate popup rows.</param>
  /// <param name="requestedQuestPopupText">The incoming popup row.</param>
  /// <returns>The preferred popup row, or <see langword="null" />.</returns>
  private static QuestPopupText? SelectPreferredQuestPopupTextForSave(
      IEnumerable<QuestPopupText> candidateQuestPopupTexts,
      QuestPopupText requestedQuestPopupText)
  {
    QuestPopupText? preferredQuestPopupText = null;
    var preferredIdentityScore = int.MinValue;
    var preferredCompletenessScore = int.MinValue;
    var preferredUpdatedDate = DateTime.MinValue;
    var preferredId = int.MinValue;

    foreach (var candidateQuestPopupText in candidateQuestPopupTexts)
    {
      if (!RuntimeLanguageHelper.LanguagesMatch(
              candidateQuestPopupText.TranslationLang,
              requestedQuestPopupText.TranslationLang) ||
          !LegacyWriteSourceLanguagesMatch(
              candidateQuestPopupText.OriginalLang,
              requestedQuestPopupText.OriginalLang))
      {
        continue;
      }

      var identityScore = 0;
      if (!string.IsNullOrWhiteSpace(requestedQuestPopupText.QuestId) &&
          string.Equals(
              candidateQuestPopupText.QuestId,
              requestedQuestPopupText.QuestId,
              StringComparison.Ordinal))
      {
        identityScore += 16;
      }

      if (!string.IsNullOrWhiteSpace(requestedQuestPopupText.SourceContentHash) &&
          string.Equals(
              candidateQuestPopupText.SourceContentHash,
              requestedQuestPopupText.SourceContentHash,
              StringComparison.Ordinal))
      {
        identityScore += 8;
      }

      if (!string.IsNullOrWhiteSpace(requestedQuestPopupText.OriginalBody) &&
          string.Equals(
              candidateQuestPopupText.OriginalBody,
              requestedQuestPopupText.OriginalBody,
              StringComparison.Ordinal))
      {
        identityScore += 4;
      }

      if (!string.IsNullOrWhiteSpace(requestedQuestPopupText.OriginalTitle) &&
          string.Equals(
              candidateQuestPopupText.OriginalTitle,
              requestedQuestPopupText.OriginalTitle,
              StringComparison.Ordinal))
      {
        identityScore += 2;
      }

      if (!string.IsNullOrWhiteSpace(candidateQuestPopupText.QuestId))
      {
        identityScore += 1;
      }

      var completenessScore = 0;
      if (!string.IsNullOrWhiteSpace(candidateQuestPopupText.TranslatedTitle))
      {
        completenessScore += 8;
      }

      if (!string.IsNullOrWhiteSpace(candidateQuestPopupText.TranslatedBody))
      {
        completenessScore += 8;
      }

      var updatedDate = candidateQuestPopupText.UpdatedDate ??
                        candidateQuestPopupText.CreatedDate ??
                        DateTime.MinValue;

      if (preferredQuestPopupText != null &&
          identityScore < preferredIdentityScore)
      {
        continue;
      }

      if (preferredQuestPopupText != null &&
          identityScore == preferredIdentityScore &&
          completenessScore < preferredCompletenessScore)
      {
        continue;
      }

      if (preferredQuestPopupText != null &&
          identityScore == preferredIdentityScore &&
          completenessScore == preferredCompletenessScore &&
          updatedDate < preferredUpdatedDate)
      {
        continue;
      }

      if (preferredQuestPopupText != null &&
          identityScore == preferredIdentityScore &&
          completenessScore == preferredCompletenessScore &&
          updatedDate == preferredUpdatedDate &&
          candidateQuestPopupText.Id <= preferredId)
      {
        continue;
      }

      preferredQuestPopupText = candidateQuestPopupText;
      preferredIdentityScore = identityScore;
      preferredCompletenessScore = completenessScore;
      preferredUpdatedDate = updatedDate;
      preferredId = candidateQuestPopupText.Id;
    }

    return preferredQuestPopupText;
  }

  /// <summary>
  ///     Finds an existing generic selection-dialog row that should be merged
  ///     with the incoming save payload instead of creating a duplicate row.
  /// </summary>
  /// <param name="context">The active database context.</param>
  /// <param name="selectionDialogText">The incoming selection-dialog row.</param>
  /// <returns>
  ///     The existing row to merge, or <see langword="null" /> when no
  ///     reusable row exists.
  /// </returns>
  private static SelectionDialogText? TryFindSelectionDialogTextForSave(
      EchoglossianDbContext context,
      SelectionDialogText selectionDialogText)
  {
    return context.SelectionDialogTexts
        .Where(t =>
            t.AddonName == selectionDialogText.AddonName &&
            t.OriginalTextsAsText == selectionDialogText.OriginalTextsAsText)
        .AsEnumerable()
        .Where(t =>
            RuntimeLanguageHelper.LanguagesMatch(
                t.TranslationLang,
                selectionDialogText.TranslationLang) &&
            LegacyWriteSourceLanguagesMatch(
                t.OriginalLang,
                selectionDialogText.OriginalLang))
        .OrderByDescending(t =>
            !string.IsNullOrWhiteSpace(selectionDialogText.SourceContentHash) &&
            string.Equals(
                t.SourceContentHash,
                selectionDialogText.SourceContentHash,
                StringComparison.Ordinal))
        .ThenByDescending(t =>
            !string.IsNullOrWhiteSpace(selectionDialogText.GameVersion) &&
            string.Equals(
                t.GameVersion,
                selectionDialogText.GameVersion,
                StringComparison.Ordinal))
        .ThenByDescending(t => t.UpdatedDate ?? t.CreatedDate ?? DateTime.MinValue)
        .ThenByDescending(t => t.Id)
        .FirstOrDefault();
  }

  /// <summary>
  ///     Selects the preferred generic selection-dialog lookup candidate.
  /// </summary>
  /// <param name="candidateSelectionDialogTexts">
  ///     The candidate selection-dialog rows.
  /// </param>
  /// <param name="requestedSelectionDialogText">
  ///     The requested selection-dialog row.
  /// </param>
  /// <returns>The preferred row, or <see langword="null" />.</returns>
  private static SelectionDialogText? SelectPreferredSelectionDialogText(
      IEnumerable<SelectionDialogText> candidateSelectionDialogTexts,
      SelectionDialogText requestedSelectionDialogText)
  {
    SelectionDialogText? preferredSelectionDialogText = null;
    var preferredIdentityScore = int.MinValue;
    var preferredCompletenessScore = int.MinValue;
    var preferredUpdatedDate = DateTime.MinValue;
    var preferredId = int.MinValue;

    foreach (var candidateSelectionDialogText in candidateSelectionDialogTexts)
    {
      if (!RuntimeLanguageHelper.LanguagesMatch(
              candidateSelectionDialogText.TranslationLang,
              requestedSelectionDialogText.TranslationLang) ||
          !LegacyWriteSourceLanguagesMatch(
              candidateSelectionDialogText.OriginalLang,
              requestedSelectionDialogText.OriginalLang))
      {
        continue;
      }

      var identityScore = 0;
      if (!string.IsNullOrWhiteSpace(requestedSelectionDialogText.SourceContentHash) &&
          string.Equals(
              candidateSelectionDialogText.SourceContentHash,
              requestedSelectionDialogText.SourceContentHash,
              StringComparison.Ordinal))
      {
        identityScore += 8;
      }

      if (!string.IsNullOrWhiteSpace(requestedSelectionDialogText.GameVersion) &&
          string.Equals(
              candidateSelectionDialogText.GameVersion,
              requestedSelectionDialogText.GameVersion,
              StringComparison.Ordinal))
      {
        identityScore += 4;
      }

      var completenessScore = 0;
      if (ShouldSaveToDB(candidateSelectionDialogText.TranslatedTextsAsText))
      {
        completenessScore += 4;
      }

      var updatedDate = candidateSelectionDialogText.UpdatedDate ??
                        candidateSelectionDialogText.CreatedDate ??
                        DateTime.MinValue;
      if (preferredSelectionDialogText != null &&
          identityScore < preferredIdentityScore)
      {
        continue;
      }

      if (preferredSelectionDialogText != null &&
          identityScore == preferredIdentityScore &&
          completenessScore < preferredCompletenessScore)
      {
        continue;
      }

      if (preferredSelectionDialogText != null &&
          identityScore == preferredIdentityScore &&
          completenessScore == preferredCompletenessScore &&
          updatedDate < preferredUpdatedDate)
      {
        continue;
      }

      if (preferredSelectionDialogText != null &&
          identityScore == preferredIdentityScore &&
          completenessScore == preferredCompletenessScore &&
          updatedDate == preferredUpdatedDate &&
          candidateSelectionDialogText.Id <= preferredId)
      {
        continue;
      }

      preferredSelectionDialogText = candidateSelectionDialogText;
      preferredIdentityScore = identityScore;
      preferredCompletenessScore = completenessScore;
      preferredUpdatedDate = updatedDate;
      preferredId = candidateSelectionDialogText.Id;
    }

    return preferredSelectionDialogText;
  }

  /// <summary>
  ///     Compares write-side source identities while recognizing only the four
  ///     historical display names persisted by supported game languages.
  /// </summary>
  /// <param name="left">The persisted source identity.</param>
  /// <param name="right">The incoming source identity.</param>
  /// <returns>True when both values identify the same write-side source.</returns>
  private static bool LegacyWriteSourceLanguagesMatch(
      string? left,
      string? right)
  {
    var normalizedLeft = NormalizeLegacyWriteSourceLanguage(left);
    var normalizedRight = NormalizeLegacyWriteSourceLanguage(right);

    return !string.IsNullOrWhiteSpace(normalizedLeft) &&
           string.Equals(
               normalizedLeft,
               normalizedRight,
               StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>
  ///     Normalizes only approved legacy display names for write identity.
  /// </summary>
  /// <param name="language">The source identity to normalize.</param>
  /// <returns>The approved canonical identity or the trimmed input.</returns>
  private static string NormalizeLegacyWriteSourceLanguage(string? language)
  {
    if (string.IsNullOrWhiteSpace(language))
    {
      return string.Empty;
    }

    var trimmed = language.Trim();
    return trimmed.ToLowerInvariant() switch
    {
      "english" or "en" => "en",
      "deutsch" or "de" => "de",
      "french" or "fr" => "fr",
      "japanese" or "ja" => "ja",
      _ => trimmed,
    };
  }

  /// <summary>
  ///     Merges quest plate data without overwriting already populated fields.
  /// </summary>
  /// <param name="target">The database record to be enriched.</param>
  /// <param name="source">The newer quest plate values.</param>
  private void MergeQuestPlateValues(
        QuestPlate target,
        QuestPlate source)
    {
      if (target == null || source == null)
    {
      return;
    }

    target.QuestId = string.IsNullOrWhiteSpace(source.QuestId)
        ? target.QuestId
        : source.QuestId;
    target.QuestName = string.IsNullOrWhiteSpace(source.QuestName)
        ? target.QuestName
        : source.QuestName;
    target.OriginalQuestMessage =
        string.IsNullOrWhiteSpace(source.OriginalQuestMessage)
            ? target.OriginalQuestMessage
            : source.OriginalQuestMessage;
    target.OriginalLang = string.IsNullOrWhiteSpace(source.OriginalLang)
        ? target.OriginalLang
        : source.OriginalLang;
    target.TranslatedQuestName =
        string.IsNullOrWhiteSpace(source.TranslatedQuestName)
            ? target.TranslatedQuestName
            : source.TranslatedQuestName;
    target.TranslatedQuestMessage =
        string.IsNullOrWhiteSpace(source.TranslatedQuestMessage)
            ? target.TranslatedQuestMessage
            : source.TranslatedQuestMessage;
    target.TranslationLang = string.IsNullOrWhiteSpace(source.TranslationLang)
        ? target.TranslationLang
        : source.TranslationLang;
    target.TranslationEngine = source.TranslationEngine ?? target.TranslationEngine;
    target.GameVersion = string.IsNullOrWhiteSpace(source.GameVersion)
        ? target.GameVersion
        : source.GameVersion;
    target.QuestTextSheetName =
        string.IsNullOrWhiteSpace(source.QuestTextSheetName)
            ? target.QuestTextSheetName
            : source.QuestTextSheetName;
      target.SourceContentHash =
          string.IsNullOrWhiteSpace(source.SourceContentHash)
              ? target.SourceContentHash
              : source.SourceContentHash;
      target.CreatedDate ??= source.CreatedDate;
      target.UpdatedDate = DateTime.Now;

      source.UpdateFieldsFromText();
      target.UpdateFieldsFromText();

      if (source.CanonicalRows.Count != 0)
      {
        target.MergeCanonicalPayloadFrom(source);
      }

      target.TranslatedQuestName =
          string.IsNullOrWhiteSpace(source.TranslatedQuestName)
              ? target.TranslatedQuestName
              : source.TranslatedQuestName;
      target.TranslatedQuestMessage =
          string.IsNullOrWhiteSpace(source.TranslatedQuestMessage)
              ? target.TranslatedQuestMessage
              : source.TranslatedQuestMessage;

      if (source.CanonicalRows.Count == 0)
      {
        target.SynchronizeLegacyTextProjections();
      }

      target.PruneTranslatedRowsToCanonicalPayload();
    }

  /// <summary>
  ///     Merges generic selection-dialog values without overwriting populated
  ///     translated fields with empties.
  /// </summary>
  /// <param name="target">The existing selection-dialog row.</param>
  /// <param name="source">The incoming selection-dialog row.</param>
  private static void MergeSelectionDialogTextValues(
      SelectionDialogText target,
      SelectionDialogText source)
  {
    if (target == null || source == null)
    {
      return;
    }

    target.AddonName = string.IsNullOrWhiteSpace(source.AddonName)
        ? target.AddonName
        : source.AddonName;
    target.OriginalTextsAsText =
        string.IsNullOrWhiteSpace(source.OriginalTextsAsText)
            ? target.OriginalTextsAsText
            : source.OriginalTextsAsText;
    target.OriginalLang = string.IsNullOrWhiteSpace(source.OriginalLang)
        ? target.OriginalLang
        : source.OriginalLang;
    target.TranslatedTextsAsText =
        string.IsNullOrWhiteSpace(source.TranslatedTextsAsText)
            ? target.TranslatedTextsAsText
            : source.TranslatedTextsAsText;
    target.TranslationLang = string.IsNullOrWhiteSpace(source.TranslationLang)
        ? target.TranslationLang
        : source.TranslationLang;
    target.TranslationEngine = source.TranslationEngine ??
                               target.TranslationEngine;
    target.GameVersion = string.IsNullOrWhiteSpace(source.GameVersion)
        ? target.GameVersion
        : source.GameVersion;
    target.SourceContentHash = string.IsNullOrWhiteSpace(source.SourceContentHash)
        ? target.SourceContentHash
        : source.SourceContentHash;
    target.CreatedDate ??= source.CreatedDate;
    target.UpdatedDate = DateTime.Now;
  }

  /// <summary>
  ///     Merges popup-table values without overwriting already populated
  ///     translated fields with empties.
  /// </summary>
  /// <param name="target">The existing popup row.</param>
  /// <param name="source">The incoming popup row.</param>
  private static void MergeQuestPopupTextValues(
      QuestPopupText target,
      QuestPopupText source)
  {
    if (target == null || source == null)
    {
      return;
    }

    target.SurfaceName = string.IsNullOrWhiteSpace(source.SurfaceName)
        ? target.SurfaceName
        : source.SurfaceName;
    target.QuestId = string.IsNullOrWhiteSpace(source.QuestId)
        ? target.QuestId
        : source.QuestId;
    target.OriginalTitle = string.IsNullOrWhiteSpace(source.OriginalTitle)
        ? target.OriginalTitle
        : source.OriginalTitle;
    target.OriginalBody = string.IsNullOrWhiteSpace(source.OriginalBody)
        ? target.OriginalBody
        : source.OriginalBody;
    target.OriginalLang = string.IsNullOrWhiteSpace(source.OriginalLang)
        ? target.OriginalLang
        : source.OriginalLang;
    target.TranslatedTitle = string.IsNullOrWhiteSpace(source.TranslatedTitle)
        ? target.TranslatedTitle
        : source.TranslatedTitle;
    target.TranslatedBody = string.IsNullOrWhiteSpace(source.TranslatedBody)
        ? target.TranslatedBody
        : source.TranslatedBody;
    target.TranslationLang = string.IsNullOrWhiteSpace(source.TranslationLang)
        ? target.TranslationLang
        : source.TranslationLang;
    target.TranslationEngine = source.TranslationEngine ??
                               target.TranslationEngine;
    target.GameVersion = string.IsNullOrWhiteSpace(source.GameVersion)
        ? target.GameVersion
        : source.GameVersion;
    target.SourceContentHash = string.IsNullOrWhiteSpace(source.SourceContentHash)
        ? target.SourceContentHash
        : source.SourceContentHash;
    target.CreatedDate ??= source.CreatedDate;
    target.UpdatedDate = DateTime.Now;
  }

  /// <summary>
  /// Inserts or updates a GameWindow record in the database, ensuring uniqueness
  /// per AddonName + Lang + Engine + Version + OriginalWindowStrings.
  /// Updates the in-memory cache accordingly.
  /// </summary>
  /// <param name="gameWindow">The GameWindow entity to insert or update.</param>
  /// <returns>Status message indicating result.</returns>
  public static string InsertGameWindow(GameWindow gameWindow)
  {
    return GameWindowPersistenceHelper.InsertGameWindow(
        ConfigDirectory,
        gameWindow,
        GameWindowCacheManager.Update);
  }

  /// <summary>
  /// Loads all error toast messages from the database.
  /// </summary>
  public void LoadAllErrorToasts()
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      this.ErrorToastsCache = context.ToastMessage
          .AsNoTracking()
          .Where(t => t.ToastType == "Error")
          .ToList();
    }
    catch (Exception e)
    {
      this.ErrorToastsCache = new List<ToastMessage>();
      PluginRuntimeLog.Debug("Could not find any Error Toasts in Database", e.Message);
    }
  }

  /// <summary>
  /// Loads all other toast messages from the database.
  /// </summary>
  public void LoadAllOtherToasts()
  {
    using var context = new EchoglossianDbContext(ConfigDirectory);

    try
    {
      this.OtherToastsCache = context.ToastMessage
          .AsNoTracking()
          .Where(t => t.ToastType == "NonError")
          .ToList();
    }
    catch (Exception e)
    {
      this.OtherToastsCache = new List<ToastMessage>();
      PluginRuntimeLog.Debug("Could not find any Other Toasts in Database", e.Message);
    }
  }

  private void AppendToastToCache(
      List<ToastMessage> cache,
      ToastMessage toastMessage)
  {
    if (cache.Exists(t =>
            t.ToastType == toastMessage.ToastType &&
            RuntimeLanguageHelper.LanguagesMatch(
                t.OriginalLang,
                toastMessage.OriginalLang) &&
            t.TranslationLang == toastMessage.TranslationLang &&
            t.OriginalToastMessage == toastMessage.OriginalToastMessage &&
            t.TranslationEngine == toastMessage.TranslationEngine))
    {
      return;
    }

    cache.Add(toastMessage);
  }

  /// <summary>
  /// Checks if the text should be saved to the database.
  /// </summary>
  /// <param name="text"></param>
  /// <returns></returns>
  public static bool ShouldSaveToDB(string? text)
    {
      return TranslationResultGuard.IsPersistableTranslation(text);
    }

  /// <summary>
  ///     Finds an entity in the database matching the given filter.
  /// </summary>
  /// <typeparam name="T">Type of entity.</typeparam>
  /// <param name="predicate">Predicate to match.</param>
  /// <returns>Matching entity or null.</returns>
  public static T? FindEntity<T>(Func<T, bool> predicate)
      where T : class, IGenericEntity
  {
    using var context = new EchoglossianDbContext(
        PluginInterface.GetPluginConfigDirectory() +
        Path.DirectorySeparatorChar);
    PluginRuntimeLog.Debug(
        $"FindEntity<{typeof(T).Name}> called with predicate: {predicate}");
    try
    {
      return context.Set<T>().AsNoTracking().AsEnumerable().FirstOrDefault(predicate);
    }
    catch (Exception ex)
    {
      PluginRuntimeLog.Error(
          $"FindEntity<{typeof(T).Name}> failed: {ex.Message}");
      return null;
    }
  }

  /// <summary>
  ///     Inserts an entity into the database.
  /// </summary>
  /// <typeparam name="T">Entity type.</typeparam>
  /// <param name="entity">Entity to insert.</param>
  /// <returns>Result message.</returns>
  public static async Task<string> InsertEntity<T>(T entity)
     where T : class
  {
    if (entity is GameWindow gameWindow)
    {
      return InsertGameWindow(gameWindow) ?? "Plugin not available";
    }

    using var context = new EchoglossianDbContext(
        PluginInterface.GetPluginConfigDirectory() +
        Path.DirectorySeparatorChar);

    PluginRuntimeLog.Debug($"InsertEntity<{typeof(T).Name}> called with entity: {entity}");

    try
    {
      context.Set<T>().Add(entity);
      await context.SaveChangesAsync();
      return "Entity inserted.";
    }
    catch (Exception ex)
    {
      PluginRuntimeLog.Error($"InsertEntity<{typeof(T).Name}> failed: {ex.Message}");
      return $"Insert failed: {ex.Message}";
    }
  }

}



