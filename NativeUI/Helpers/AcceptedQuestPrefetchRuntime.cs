// <copyright file="AcceptedQuestPrefetchRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;

using QuestManager = FFXIVClientStructs.FFXIV.Client.Game.QuestManager;

namespace Echoglossian;

public partial class Echoglossian
{
  private const int AcceptedQuestPrefetchQuestsPerTick = 2;

  private static readonly TimeSpan AcceptedQuestPrefetchTickInterval =
      TimeSpan.FromSeconds(2);

  private readonly List<uint> acceptedQuestPrefetchQueue = [];

  private string acceptedQuestPrefetchSignature = string.Empty;

  private DateTime acceptedQuestPrefetchLastTickUtc = DateTime.MinValue;

  private int acceptedQuestPrefetchQueueIndex;

  /// <summary>
  ///     Ticks the accepted-quest prefetch runtime so active quests can be
  ///     translated into the canonical quest table before quest addons need to
  ///     render them.
  /// </summary>
  private void TickAcceptedQuestPrefetch()
  {
    if (!this.ShouldPrefetchAcceptedQuests() ||
        DateTime.UtcNow - this.acceptedQuestPrefetchLastTickUtc <
        AcceptedQuestPrefetchTickInterval)
    {
      return;
    }

    this.acceptedQuestPrefetchLastTickUtc = DateTime.UtcNow;

    if (!TryCollectAcceptedQuestIds(out var acceptedQuestIds))
    {
      return;
    }

    var acceptedQuestSignature = BuildAcceptedQuestSignature(acceptedQuestIds);
    if (!string.Equals(
            this.acceptedQuestPrefetchSignature,
            acceptedQuestSignature,
            StringComparison.Ordinal))
    {
      this.acceptedQuestPrefetchSignature = acceptedQuestSignature;
      this.acceptedQuestPrefetchQueue.Clear();
      this.acceptedQuestPrefetchQueue.AddRange(acceptedQuestIds);
      this.acceptedQuestPrefetchQueueIndex = 0;
    }

    if (this.acceptedQuestPrefetchQueueIndex >=
        this.acceptedQuestPrefetchQueue.Count)
    {
      return;
    }

    var processedQuestCount = 0;
    while (processedQuestCount < AcceptedQuestPrefetchQuestsPerTick &&
           this.acceptedQuestPrefetchQueueIndex <
           this.acceptedQuestPrefetchQueue.Count)
    {
      var questId =
          this.acceptedQuestPrefetchQueue[this.acceptedQuestPrefetchQueueIndex++];
      this.PrefetchAcceptedQuest(questId);
      processedQuestCount++;
    }
  }

  /// <summary>
  ///     Clears the accepted-quest prefetch runtime state.
  /// </summary>
  private void ClearAcceptedQuestPrefetchState()
  {
    this.acceptedQuestPrefetchQueue.Clear();
    this.acceptedQuestPrefetchQueueIndex = 0;
    this.acceptedQuestPrefetchSignature = string.Empty;
    this.acceptedQuestPrefetchLastTickUtc = DateTime.MinValue;
  }

  /// <summary>
  ///     Gets whether accepted quests should be prefetched in the current
  ///     runtime state.
  /// </summary>
  /// <returns>True when the background prefetch should run.</returns>
  private bool ShouldPrefetchAcceptedQuests()
  {
    return this.configuration.Translate &&
           ClientStateInterface.IsLoggedIn &&
           (this.configuration.TranslateJournal ||
            this.configuration.TranslateJournalAccept ||
            this.configuration.TranslateJournalResult ||
            this.configuration.TranslateToDoList ||
            this.configuration.TranslateScenarioTree ||
            this.configuration.TranslateRecommendList ||
            this.configuration.TranslateAreaMap);
  }

  /// <summary>
  ///     Prefetches the canonical text for one accepted quest and schedules any
  ///     missing translations through the shared paced broker.
  /// </summary>
  /// <param name="questId">The accepted quest identifier.</param>
  private void PrefetchAcceptedQuest(uint questId)
  {
    if (!QuestProgressResolver.TryResolveQuestProgress(
            questId.ToString(CultureInfo.InvariantCulture),
            out var questProgressSnapshot))
    {
      return;
    }

    var currentQuestSequenceText =
        GetAcceptedQuestCurrentSequenceText(questProgressSnapshot);
    var questPlate = this.CreateAcceptedQuestPrefetchPlate(
        questProgressSnapshot,
        currentQuestSequenceText);
    this.InsertQuestPlate(questPlate);

    var existingQuestPlate = this.FindQuestPlate(questPlate) ??
                             this.FindQuestPlateByName(questPlate) ??
                             questPlate;

    this.PrefetchAcceptedQuestName(
        questProgressSnapshot,
        currentQuestSequenceText,
        existingQuestPlate);
    this.PrefetchAcceptedQuestCurrentMessage(
        questProgressSnapshot,
        currentQuestSequenceText,
        existingQuestPlate);
    this.PrefetchAcceptedQuestSummaries(
        questProgressSnapshot,
        currentQuestSequenceText,
        existingQuestPlate);
    this.PrefetchAcceptedQuestObjectives(
        questProgressSnapshot,
        currentQuestSequenceText,
        existingQuestPlate);
    this.PrefetchAcceptedQuestSystemRows(
        questProgressSnapshot,
        currentQuestSequenceText,
        existingQuestPlate);
  }

  /// <summary>
  ///     Prefetches the translated quest name for an accepted quest when it is
  ///     not yet persisted.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <param name="existingQuestPlate">The existing persisted quest plate, if any.</param>
  private void PrefetchAcceptedQuestName(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      QuestPlate existingQuestPlate)
  {
    if (!string.IsNullOrWhiteSpace(existingQuestPlate.TranslatedQuestName))
    {
      return;
    }

    var translationKey =
        $"AcceptedQuestPrefetch|{questProgressSnapshot.CacheKey}|Name|{questProgressSnapshot.QuestName}";
    if (this.TryGetQueuedTranslation(
            translationKey,
            out var cachedTranslatedQuestName))
    {
      this.ApplyAcceptedQuestNameTranslation(
          questProgressSnapshot,
          currentQuestSequenceText,
          cachedTranslatedQuestName);
      return;
    }

    this.QueueTranslation(
        translationKey,
        () => TranslationService.Translate(
            questProgressSnapshot.QuestName,
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code),
        translatedQuestName => this.ApplyAcceptedQuestNameTranslation(
            questProgressSnapshot,
            currentQuestSequenceText,
            translatedQuestName));
  }

  /// <summary>
  ///     Prefetches the current quest-body message for an accepted quest when it
  ///     is not yet persisted.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <param name="existingQuestPlate">The existing persisted quest plate, if any.</param>
  private void PrefetchAcceptedQuestCurrentMessage(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      QuestPlate existingQuestPlate)
  {
    if (string.IsNullOrWhiteSpace(currentQuestSequenceText) ||
        !string.IsNullOrWhiteSpace(existingQuestPlate.TranslatedQuestMessage))
    {
      return;
    }

    var translationKey =
        $"AcceptedQuestPrefetch|{questProgressSnapshot.CacheKey}|Message|{currentQuestSequenceText}";
    if (this.TryGetQueuedTranslation(
            translationKey,
            out var cachedTranslatedQuestMessage))
    {
      this.ApplyAcceptedQuestMessageTranslation(
          questProgressSnapshot,
          currentQuestSequenceText,
          cachedTranslatedQuestMessage);
      return;
    }

    this.QueueTranslation(
        translationKey,
        () => TranslationService.Translate(
            currentQuestSequenceText,
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code),
        translatedQuestMessage => this.ApplyAcceptedQuestMessageTranslation(
            questProgressSnapshot,
            currentQuestSequenceText,
            translatedQuestMessage));
  }

  /// <summary>
  ///     Prefetches all SEQ summary rows for an accepted quest.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <param name="existingQuestPlate">The existing persisted quest plate, if any.</param>
  private void PrefetchAcceptedQuestSummaries(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      QuestPlate existingQuestPlate)
  {
    foreach (var questSequenceEntry in questProgressSnapshot.QuestSeqTexts)
    {
      if (string.IsNullOrWhiteSpace(questSequenceEntry.Text) ||
          existingQuestPlate.TranslatedSummaries.TryGetValue(
              questSequenceEntry.Text,
              out var translatedSummaryText) &&
          !string.IsNullOrWhiteSpace(translatedSummaryText))
      {
        continue;
      }

      var translationKey =
          $"AcceptedQuestPrefetch|{questProgressSnapshot.CacheKey}|Summary|{questSequenceEntry.KeyText}|{questSequenceEntry.Text}";
      if (this.TryGetQueuedTranslation(
              translationKey,
              out var cachedTranslatedSummaryText))
      {
        this.ApplyAcceptedQuestSummaryTranslation(
            questProgressSnapshot,
            currentQuestSequenceText,
            questSequenceEntry.Text,
            cachedTranslatedSummaryText);
        continue;
      }

      this.QueueTranslation(
          translationKey,
          () => TranslationService.Translate(
              questSequenceEntry.Text,
              ClientStateInterface.ClientLanguage.Humanize(),
              LangDict[LanguageInt].Code),
          translatedSummaryTextValue =>
              this.ApplyAcceptedQuestSummaryTranslation(
                  questProgressSnapshot,
                  currentQuestSequenceText,
                  questSequenceEntry.Text,
                  translatedSummaryTextValue));
    }
  }

  /// <summary>
  ///     Prefetches all TODO rows for an accepted quest.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <param name="existingQuestPlate">The existing persisted quest plate, if any.</param>
  private void PrefetchAcceptedQuestObjectives(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      QuestPlate existingQuestPlate)
  {
    foreach (var questStep in questProgressSnapshot.QuestSteps)
    {
      if (string.IsNullOrWhiteSpace(questStep.Text) ||
          existingQuestPlate.TranslatedObjectives.TryGetValue(
              questStep.Text,
              out var translatedObjectiveText) &&
          !string.IsNullOrWhiteSpace(translatedObjectiveText))
      {
        continue;
      }

      var translationKey =
          $"AcceptedQuestPrefetch|{questProgressSnapshot.CacheKey}|Objective|{questStep.KeyText}|{questStep.Text}";
      if (this.TryGetQueuedTranslation(
              translationKey,
              out var cachedTranslatedObjectiveText))
      {
        this.ApplyAcceptedQuestObjectiveTranslation(
            questProgressSnapshot,
            currentQuestSequenceText,
            questStep.Text,
            cachedTranslatedObjectiveText);
        continue;
      }

      this.QueueTranslation(
          translationKey,
          () => TranslationService.Translate(
              questStep.Text,
              ClientStateInterface.ClientLanguage.Humanize(),
              LangDict[LanguageInt].Code),
          translatedObjectiveTextValue =>
              this.ApplyAcceptedQuestObjectiveTranslation(
                  questProgressSnapshot,
                  currentQuestSequenceText,
                  questStep.Text,
                  translatedObjectiveTextValue));
    }
  }

  /// <summary>
  ///     Prefetches all SYSTEM rows for an accepted quest.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <param name="existingQuestPlate">The existing persisted quest plate, if any.</param>
  private void PrefetchAcceptedQuestSystemRows(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      QuestPlate existingQuestPlate)
  {
    foreach (var questSystemText in questProgressSnapshot.QuestSystemTexts)
    {
      if (string.IsNullOrWhiteSpace(questSystemText.Text) ||
          existingQuestPlate.TranslatedSystemRows.TryGetValue(
              questSystemText.Text,
              out var translatedSystemRowText) &&
          !string.IsNullOrWhiteSpace(translatedSystemRowText))
      {
        continue;
      }

      var translationKey =
          $"AcceptedQuestPrefetch|{questProgressSnapshot.CacheKey}|System|{questSystemText.KeyText}|{questSystemText.Text}";
      if (this.TryGetQueuedTranslation(
              translationKey,
              out var cachedTranslatedSystemRowText))
      {
        this.ApplyAcceptedQuestSystemTranslation(
            questProgressSnapshot,
            currentQuestSequenceText,
            questSystemText.Text,
            cachedTranslatedSystemRowText);
        continue;
      }

      this.QueueTranslation(
          translationKey,
          () => TranslationService.Translate(
              questSystemText.Text,
              ClientStateInterface.ClientLanguage.Humanize(),
              LangDict[LanguageInt].Code),
          translatedSystemTextValue =>
              this.ApplyAcceptedQuestSystemTranslation(
                  questProgressSnapshot,
                  currentQuestSequenceText,
                  questSystemText.Text,
                  translatedSystemTextValue));
    }
  }

  /// <summary>
  ///     Applies a prefetched quest-name translation into the canonical quest
  ///     plate row.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <param name="translatedQuestName">The translated quest name.</param>
  private void ApplyAcceptedQuestNameTranslation(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      string translatedQuestName)
  {
    if (string.IsNullOrWhiteSpace(translatedQuestName))
    {
      return;
    }

    var questPlate = this.CreateAcceptedQuestPrefetchPlate(
        questProgressSnapshot,
        currentQuestSequenceText);
    questPlate.TranslatedQuestName = translatedQuestName;
    this.UpdateQuestPlate(questPlate);
  }

  /// <summary>
  ///     Applies a prefetched current-message translation into the canonical
  ///     quest plate row.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <param name="translatedQuestMessage">The translated quest message.</param>
  private void ApplyAcceptedQuestMessageTranslation(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      string translatedQuestMessage)
  {
    if (string.IsNullOrWhiteSpace(currentQuestSequenceText) ||
        string.IsNullOrWhiteSpace(translatedQuestMessage))
    {
      return;
    }

    var questPlate = this.CreateAcceptedQuestPrefetchPlate(
        questProgressSnapshot,
        currentQuestSequenceText);
    questPlate.TranslatedQuestMessage = translatedQuestMessage;
    questPlate.Summaries[currentQuestSequenceText] = translatedQuestMessage;
    questPlate.TranslatedSummaries[currentQuestSequenceText] =
        translatedQuestMessage;
    this.UpdateQuestPlate(questPlate);
  }

  /// <summary>
  ///     Applies a prefetched SEQ summary translation into the canonical quest
  ///     plate row.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <param name="originalSummaryText">The original summary text.</param>
  /// <param name="translatedSummaryText">The translated summary text.</param>
  private void ApplyAcceptedQuestSummaryTranslation(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      string originalSummaryText,
      string translatedSummaryText)
  {
    if (string.IsNullOrWhiteSpace(originalSummaryText) ||
        string.IsNullOrWhiteSpace(translatedSummaryText))
    {
      return;
    }

    var questPlate = this.CreateAcceptedQuestPrefetchPlate(
        questProgressSnapshot,
        currentQuestSequenceText);
    questPlate.Summaries[originalSummaryText] = translatedSummaryText;
    questPlate.TranslatedSummaries[originalSummaryText] =
        translatedSummaryText;

    if (string.Equals(
            originalSummaryText,
            currentQuestSequenceText,
            StringComparison.Ordinal))
    {
      questPlate.TranslatedQuestMessage = translatedSummaryText;
    }

    this.UpdateQuestPlate(questPlate);
  }

  /// <summary>
  ///     Applies a prefetched TODO translation into the canonical quest plate
  ///     row.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <param name="originalObjectiveText">The original objective text.</param>
  /// <param name="translatedObjectiveText">The translated objective text.</param>
  private void ApplyAcceptedQuestObjectiveTranslation(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      string originalObjectiveText,
      string translatedObjectiveText)
  {
    if (string.IsNullOrWhiteSpace(originalObjectiveText) ||
        string.IsNullOrWhiteSpace(translatedObjectiveText))
    {
      return;
    }

    var questPlate = this.CreateAcceptedQuestPrefetchPlate(
        questProgressSnapshot,
        currentQuestSequenceText);
    questPlate.Objectives[originalObjectiveText] = translatedObjectiveText;
    questPlate.TranslatedObjectives[originalObjectiveText] =
        translatedObjectiveText;
    this.UpdateQuestPlate(questPlate);
  }

  /// <summary>
  ///     Applies a prefetched SYSTEM-row translation into the canonical quest
  ///     plate row.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <param name="originalSystemText">The original SYSTEM-row text.</param>
  /// <param name="translatedSystemText">The translated SYSTEM-row text.</param>
  private void ApplyAcceptedQuestSystemTranslation(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      string originalSystemText,
      string translatedSystemText)
  {
    if (string.IsNullOrWhiteSpace(originalSystemText) ||
        string.IsNullOrWhiteSpace(translatedSystemText))
    {
      return;
    }

    var questPlate = this.CreateAcceptedQuestPrefetchPlate(
        questProgressSnapshot,
        currentQuestSequenceText);
    questPlate.SystemRows[originalSystemText] = translatedSystemText;
    questPlate.TranslatedSystemRows[originalSystemText] =
        translatedSystemText;
    this.UpdateQuestPlate(questPlate);
  }

  /// <summary>
  ///     Creates the canonical accepted-quest quest plate snapshot used by the
  ///     background prefetch runtime.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <returns>The canonical quest plate snapshot.</returns>
  private QuestPlate CreateAcceptedQuestPrefetchPlate(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText)
  {
    var questPlate = new QuestPlate(
        questProgressSnapshot.QuestName,
        currentQuestSequenceText,
        ClientStateInterface.ClientLanguage.Humanize(),
        string.Empty,
        string.Empty,
        questProgressSnapshot.QuestId.ToString(CultureInfo.InvariantCulture),
        LangDict[LanguageInt].Code,
        this.configuration.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now,
        GetGameVersion());
    questPlate.QuestTextSheetName = questProgressSnapshot.QuestSheetName;
    questPlate.SourceContentHash = questProgressSnapshot.ContentHash;

    foreach (var questStep in questProgressSnapshot.QuestSteps)
    {
      if (!string.IsNullOrWhiteSpace(questStep.Text))
      {
        questPlate.Objectives[questStep.Text] = questStep.Text;
      }
    }

    foreach (var questSequenceText in questProgressSnapshot.QuestSeqTexts)
    {
      if (!string.IsNullOrWhiteSpace(questSequenceText.Text))
      {
        questPlate.Summaries[questSequenceText.Text] = questSequenceText.Text;
      }
    }

    foreach (var questSystemText in questProgressSnapshot.QuestSystemTexts)
    {
      if (!string.IsNullOrWhiteSpace(questSystemText.Text))
      {
        questPlate.SystemRows[questSystemText.Text] = questSystemText.Text;
      }
    }

    return questPlate;
  }

  /// <summary>
  ///     Collects the currently accepted quest ids from the live quest manager.
  /// </summary>
  /// <param name="acceptedQuestIds">The accepted quest ids.</param>
  /// <returns>True when at least one accepted quest was collected.</returns>
  private static unsafe bool TryCollectAcceptedQuestIds(
      out List<uint> acceptedQuestIds)
  {
    acceptedQuestIds = [];
    var questManager = QuestManager.Instance();
    if (questManager == null)
    {
      return false;
    }

    HashSet<uint> seenQuestIds = [];
    foreach (ref QuestWork questWork in questManager->NormalQuests)
    {
      if (questWork.QuestId != 0 &&
          seenQuestIds.Add(questWork.QuestId))
      {
        acceptedQuestIds.Add(questWork.QuestId);
      }
    }

    foreach (ref DailyQuestWork dailyQuestWork in questManager->DailyQuests)
    {
      if (dailyQuestWork.QuestId != 0 &&
          seenQuestIds.Add(dailyQuestWork.QuestId))
      {
        acceptedQuestIds.Add(dailyQuestWork.QuestId);
      }
    }

    acceptedQuestIds.Sort();
    return acceptedQuestIds.Count > 0;
  }

  /// <summary>
  ///     Builds a stable signature for the currently accepted quests, including
  ///     their live sequence.
  /// </summary>
  /// <param name="acceptedQuestIds">The accepted quest ids.</param>
  /// <returns>The stable accepted-quest signature.</returns>
  private static string BuildAcceptedQuestSignature(
      IReadOnlyCollection<uint> acceptedQuestIds)
  {
    if (acceptedQuestIds.Count == 0)
    {
      return string.Empty;
    }

    return string.Join(
        "|",
        acceptedQuestIds.Select(questId =>
            $"{questId}:{QuestManager.GetQuestSequence(questId)}"));
  }

  /// <summary>
  ///     Gets the current SEQ row text for an accepted quest snapshot.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <returns>The current SEQ row text, or an empty string when unavailable.</returns>
  private static string GetAcceptedQuestCurrentSequenceText(
      QuestProgressSnapshot questProgressSnapshot)
  {
    if (questProgressSnapshot.QuestSeqTexts.Count == 0)
    {
      return string.Empty;
    }

    var questSequenceIndex = Math.Min(
        (int)questProgressSnapshot.QuestSequence,
        questProgressSnapshot.QuestSeqTexts.Count - 1);
    if (questSequenceIndex >= 0 &&
        questSequenceIndex < questProgressSnapshot.QuestSeqTexts.Count)
    {
      var currentQuestSequenceText =
          questProgressSnapshot.QuestSeqTexts[questSequenceIndex].Text;
      if (!string.IsNullOrWhiteSpace(currentQuestSequenceText))
      {
        return currentQuestSequenceText;
      }
    }

    foreach (var questSequenceText in questProgressSnapshot.QuestSeqTexts)
    {
      if (!string.IsNullOrWhiteSpace(questSequenceText.Text))
      {
        return questSequenceText.Text;
      }
    }

    return string.Empty;
  }
}
