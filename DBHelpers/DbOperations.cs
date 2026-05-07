// <copyright file="DbOperations.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian;

/// <summary>
///  Defines operations for managing and retrieving translation data
/// </summary>
public partial class Echoglossian
{
  public static TalkMessage? FoundTalkMessage { get; set; }

  public ToastMessage? FoundToastMessage { get; set; }

  public static BattleTalkMessage? FoundBattleTalkMessage { get; set; }

  public static TalkSubtitleMessage? FoundTalkSubtitleMessage { get; set; }

  public static MiniTalkMessage? FoundMiniTalkMessage { get; set; }

  public static TextGimmickHintMessage? FoundTextGimmickHintMessage { get; set; }

  public static SelectString? FoundSelectStringMessage { get; set; }

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
      var existingTalkMessage = context.TalkMessage.AsNoTracking().Where(t =>
          t.SenderName == talkMessage.SenderName &&
          t.OriginalTalkMessage == talkMessage.OriginalTalkMessage &&
          t.TranslationLang == talkMessage.TranslationLang);
      if (ShouldFilterStoredTranslationsByEngine())
      {
        existingTalkMessage = existingTalkMessage.Where(t =>
            t.TranslationEngine == talkMessage.TranslationEngine);
      }

      var localFoundTalkMessage = existingTalkMessage.FirstOrDefault();
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
          t.ToastType == toastMessage.ToastType);

      if (this.configuration.TranslateAlreadyTranslatedTexts)
      {
        existingToastMessage = existingToastMessage.Where(t =>
            t.TranslationEngine == toastMessage.TranslationEngine);
      }

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
          !string.IsNullOrWhiteSpace(t.TranslatedToastMessage));

      if (this.configuration.TranslateAlreadyTranslatedTexts)
      {
        existingToastMessage = existingToastMessage.Where(t =>
            t.TranslationEngine == toastMessage.TranslationEngine);
      }

      return existingToastMessage.FirstOrDefault();
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"FindAndReturnToastMessage exception {e}");
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
          t.ToastType == toastMessage.ToastType);

      if (this.configuration.TranslateAlreadyTranslatedTexts)
      {
        existingToastMessage = existingToastMessage.Where(t =>
            t.TranslationEngine == toastMessage.TranslationEngine);
      }

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
      var existingBattleTalkMessage = context.BattleTalkMessage.AsNoTracking().Where(t =>
          t.SenderName == battleTalkMessage.SenderName &&
          t.OriginalBattleTalkMessage ==
          battleTalkMessage.OriginalBattleTalkMessage &&
          t.TranslationLang == battleTalkMessage.TranslationLang &&
          t.TranslatedBattleTalkMessage != null &&
          t.TranslatedBattleTalkMessage != string.Empty);

      if (ShouldFilterStoredTranslationsByEngine())
      {
        existingBattleTalkMessage = existingBattleTalkMessage.Where(t =>
            t.TranslationEngine == battleTalkMessage.TranslationEngine);
      }

      var localFoundBattleTalkMessage =
          existingBattleTalkMessage.FirstOrDefault();
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
      questPlate.GameVersion ??= GetGameVersion();
      QuestLuminaResolver.TryPopulateQuestId(questPlate);
      var filterByEngine = this.configuration?.TranslateAlreadyTranslatedTexts ==
                           true;

      QuestPlate? localFoundQuestPlate = null;
      var matchedByQuestId = false;

      // Look up without GameVersion so that cross-patch reuse is possible.
      if (!string.IsNullOrWhiteSpace(questPlate.QuestId))
      {
        var questIdMatch = context.QuestPlate.AsNoTracking().Where(t =>
            t.QuestId == questPlate.QuestId &&
            t.TranslationLang == questPlate.TranslationLang &&
            (!filterByEngine ||
             t.TranslationEngine == questPlate.TranslationEngine));

        localFoundQuestPlate = questIdMatch.FirstOrDefault();
        matchedByQuestId = localFoundQuestPlate != null;

        if (localFoundQuestPlate == null)
        {
          return null;
        }
      }

      if (localFoundQuestPlate == null &&
          string.IsNullOrWhiteSpace(questPlate.QuestId) &&
          !string.IsNullOrWhiteSpace(questPlate.OriginalQuestMessage))
      {
        var questMessageMatch = context.QuestPlate.AsNoTracking().Where(t =>
            t.QuestName == questPlate.QuestName &&
            t.OriginalQuestMessage == questPlate.OriginalQuestMessage &&
            t.TranslationLang == questPlate.TranslationLang &&
            (!filterByEngine ||
             t.TranslationEngine == questPlate.TranslationEngine));

        localFoundQuestPlate = questMessageMatch.FirstOrDefault();
      }

      if (localFoundQuestPlate == null &&
          string.IsNullOrWhiteSpace(questPlate.QuestId) &&
          !string.IsNullOrWhiteSpace(questPlate.QuestName))
      {
        var questNameMatch = context.QuestPlate.AsNoTracking().Where(t =>
            t.QuestName == questPlate.QuestName &&
            t.TranslationLang == questPlate.TranslationLang &&
            (!filterByEngine ||
             t.TranslationEngine == questPlate.TranslationEngine));

        localFoundQuestPlate = questNameMatch.FirstOrDefault();
      }

      if (localFoundQuestPlate == null ||
          (!matchedByQuestId &&
           localFoundQuestPlate.OriginalQuestMessage !=
           questPlate.OriginalQuestMessage))
      {
        return null;
      }

      // Content-hash check: when the incoming plate carries a hash (meaning the
      // snapshot was resolved from the live sheet) compare it with what is stored.
      // A mismatch means quest content changed in a patch → need retranslation.
      // An empty stored hash means a legacy row → retranslate once to populate it.
      var incomingHash = questPlate.SourceContentHash;
      var storedHash = localFoundQuestPlate.SourceContentHash;
      if (!string.IsNullOrEmpty(incomingHash) &&
          !string.Equals(incomingHash, storedHash, StringComparison.Ordinal))
      {
        // Content changed or missing — signal the caller to retranslate.
        return null;
      }

      localFoundQuestPlate.UpdateFieldsFromText();
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
      questPlate.GameVersion ??= GetGameVersion();
      QuestLuminaResolver.TryPopulateQuestId(questPlate);
      var filterByEngine = this.configuration?.TranslateAlreadyTranslatedTexts ==
                           true;

      // Prefer QuestId lookup (stable primary key) when available so that two
      // quests sharing a display name are never confused. Fall back to name-only
      // match for legacy rows that were stored before QuestId was populated.
      QuestPlate? localFoundQuestPlate = null;
      var matchedByQuestId = false;

      if (!string.IsNullOrWhiteSpace(questPlate.QuestId))
      {
        var questIdMatch = context.QuestPlate.AsNoTracking().Where(t =>
            t.QuestId == questPlate.QuestId &&
            t.TranslationLang == questPlate.TranslationLang &&
            (!filterByEngine ||
             t.TranslationEngine == questPlate.TranslationEngine));

        localFoundQuestPlate = questIdMatch.FirstOrDefault();
        matchedByQuestId = localFoundQuestPlate != null;

        if (localFoundQuestPlate == null)
        {
          return null;
        }
      }

      if (localFoundQuestPlate == null &&
          string.IsNullOrWhiteSpace(questPlate.QuestId) &&
          !string.IsNullOrWhiteSpace(questPlate.QuestName))
      {
        var questNameMatch = context.QuestPlate.AsNoTracking().Where(t =>
            t.QuestName == questPlate.QuestName &&
            t.TranslationLang == questPlate.TranslationLang &&
            (!filterByEngine ||
             t.TranslationEngine == questPlate.TranslationEngine));

        localFoundQuestPlate = questNameMatch.FirstOrDefault();
      }

      if (localFoundQuestPlate == null ||
          (!matchedByQuestId &&
           localFoundQuestPlate.QuestName != questPlate.QuestName))
      {
        return null;
      }

      // Content-hash check: same semantics as FindQuestPlate.
      // When the caller sets SourceContentHash on the incoming plate, a mismatch
      // means quest content changed → retranslate. Empty incoming hash is a
      // no-op so callers that don't resolve a snapshot still get the old behavior.
      var incomingHash = questPlate.SourceContentHash;
      var storedHash = localFoundQuestPlate.SourceContentHash;
      if (!string.IsNullOrEmpty(incomingHash) &&
          !string.Equals(incomingHash, storedHash, StringComparison.Ordinal))
      {
        return null;
      }

      localFoundQuestPlate.UpdateFieldsFromText();
      return localFoundQuestPlate;
    }
    catch (Exception e)
    {
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
      var existingTalkSubtitleMessage =
          context.TalkSubtitleMessage.AsNoTracking().Where(t =>
              t.OriginalTalkSubtitleMessage == talkSubtitleMessage
                  .OriginalTalkSubtitleMessage && t.TranslationLang ==
              talkSubtitleMessage.TranslationLang);

      if (this.configuration?.TranslateAlreadyTranslatedTexts == true)
      {
        existingTalkSubtitleMessage =
            existingTalkSubtitleMessage.Where(t =>
                t.TranslationEngine ==
                talkSubtitleMessage.TranslationEngine);
      }

      var localFoundTalkSubtitleMessage =
          existingTalkSubtitleMessage.FirstOrDefault();
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
      var existingMiniTalkMessage =
          context.MiniTalkMessage.AsNoTracking().Where(t =>
              t.OriginalMiniTalkMessage == miniTalkMessage
                  .OriginalMiniTalkMessage && t.TranslationLang ==
              miniTalkMessage.TranslationLang);

      if (this.configuration?.TranslateAlreadyTranslatedTexts == true)
      {
        existingMiniTalkMessage =
            existingMiniTalkMessage.Where(t =>
                t.TranslationEngine ==
                miniTalkMessage.TranslationEngine);
      }

      var localFoundMiniTalkMessage =
          existingMiniTalkMessage.FirstOrDefault();
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
      var existingTextGimmickHintMessage =
          context.TextGimmickHintMessage.AsNoTracking().Where(t =>
              t.OriginalText == textGimmickHintMessage.OriginalText &&
              t.TranslationLang == textGimmickHintMessage.TranslationLang);

      if (this.configuration?.TranslateAlreadyTranslatedTexts == true)
      {
        existingTextGimmickHintMessage =
            existingTextGimmickHintMessage.Where(t =>
                t.TranslationEngine ==
                textGimmickHintMessage.TranslationEngine);
      }

      var localFoundTextGimmickHintMessage =
          existingTextGimmickHintMessage.FirstOrDefault();
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
      var existingSelectString =
          context.SelectString.AsNoTracking().Where(t =>
              t.OriginalSelectString == selectString.OriginalSelectString &&
              t.OriginalOptionsAsText == selectString.OriginalOptionsAsText &&
              t.TranslationLang == selectString.TranslationLang);

      if (this.configuration?.TranslateAlreadyTranslatedTexts == true)
      {
        existingSelectString = existingSelectString.Where(t =>
            t.TranslationEngine == selectString.TranslationEngine);
      }

      var localFoundSelectString =
          existingSelectString.FirstOrDefault();
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
  /// Finds and returns a GameWindow from the database.
  /// </summary>
  /// <param name="gameWindow">Formatted GameWindow to be found in the database</param>
  /// <returns></returns>
  public static GameWindow? FindAndReturnGameWindow(GameWindow gameWindow)
  {
    using var context = new EchoglossianDbContext(
        PluginInterface.GetPluginConfigDirectory() +
        Path.DirectorySeparatorChar);
    try
    {
      var existingGameWindow = context.GameWindow.AsNoTracking().Where(t =>
          t.WindowAddonName == gameWindow.WindowAddonName &&
          t.TranslationLang == gameWindow.TranslationLang);
      var localFoundGameWindow = existingGameWindow.FirstOrDefault();
      if (localFoundGameWindow == null)
      {
        return null;
      }
      if (localFoundGameWindow?.WindowAddonName !=
          gameWindow.WindowAddonName)
      {
        return null;
      }

      return localFoundGameWindow;
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Debug($"FindAndReturnGameWindow exception {e}");
      return null;
    }
  }

  /// <summary>
  /// Inserts a TalkMessage record into the database.
  /// </summary>
  /// <param name="talkMessage">Formatted TalkMessage to be inserted into the database</param>
  /// <returns></returns>
  public static async Task<string> InsertTalkData(TalkMessage talkMessage)
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

      await context.SaveChangesAsync();

      return "Data inserted to TalkMessages table.";
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Error($"DB Save Failed: {e.Message}\n{e.StackTrace}");
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
  /// Inserts a MiniTalkMessage record into the database.
  /// </summary>
  /// <param name="miniTalkMessage">Formatted MiniTalkMessage to be inserted into the database</param>
  /// <returns></returns>
  public static async Task<string> InsertMiniTalkData(
      MiniTalkMessage miniTalkMessage)
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

      await context.SaveChangesAsync();

      return "Data inserted to MiniTalkMessages table.";
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

    if (!string.IsNullOrWhiteSpace(questPlate.QuestId))
    {
      var questIdMatch = context.QuestPlate.FirstOrDefault(t =>
          t.QuestId == questPlate.QuestId &&
          t.TranslationLang == questPlate.TranslationLang &&
          (!this.configuration.TranslateAlreadyTranslatedTexts ||
           t.TranslationEngine == questPlate.TranslationEngine) &&
          (!hasGameVersion || t.GameVersion == questPlate.GameVersion));

      if (questIdMatch != null)
      {
        questIdMatch.UpdateFieldsFromText();
        return questIdMatch;
      }

      return null;
    }

    if (!string.IsNullOrWhiteSpace(questPlate.OriginalQuestMessage))
    {
      var questMessageMatch = context.QuestPlate.FirstOrDefault(t =>
          t.QuestName == questPlate.QuestName &&
          t.OriginalQuestMessage == questPlate.OriginalQuestMessage &&
          t.TranslationLang == questPlate.TranslationLang &&
          (!this.configuration.TranslateAlreadyTranslatedTexts ||
           t.TranslationEngine == questPlate.TranslationEngine) &&
          (!hasGameVersion || t.GameVersion == questPlate.GameVersion));

      if (questMessageMatch != null)
      {
        questMessageMatch.UpdateFieldsFromText();
        return questMessageMatch;
      }
    }

    var questNameMatch = context.QuestPlate.FirstOrDefault(t =>
        t.QuestName == questPlate.QuestName &&
        t.TranslationLang == questPlate.TranslationLang &&
        (!this.configuration.TranslateAlreadyTranslatedTexts ||
         t.TranslationEngine == questPlate.TranslationEngine) &&
        (!hasGameVersion || t.GameVersion == questPlate.GameVersion));

    if (questNameMatch != null)
    {
      questNameMatch.UpdateFieldsFromText();
    }

    return questNameMatch;
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



