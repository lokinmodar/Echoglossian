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

  private const bool AcceptedQuestPrefetchEmitDalamudLog = false;

  private static readonly TimeSpan AcceptedQuestPrefetchTickInterval =
      TimeSpan.FromSeconds(2);

  private readonly List<uint> acceptedQuestPrefetchQueue = [];

  private readonly AcceptedQuestPrefetchRequestQueue
      acceptedQuestPrefetchRequestedQuestQueue = new();

  private string acceptedQuestPrefetchSignature = string.Empty;

  private DateTime acceptedQuestPrefetchLastTickUtc = DateTime.MinValue;

  private int acceptedQuestPrefetchQueueIndex;

  private bool acceptedQuestPrefetchNotificationActive;

  private int acceptedQuestPrefetchNotificationQuestCount;

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

    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out _))
    {
      return;
    }

    this.acceptedQuestPrefetchLastTickUtc = DateTime.UtcNow;

    var hasAcceptedQuestIds = TryCollectAcceptedQuestIds(
        out var acceptedQuestIds);
    if (!hasAcceptedQuestIds)
    {
      this.acceptedQuestPrefetchQueue.Clear();
      this.acceptedQuestPrefetchQueueIndex = 0;
      this.acceptedQuestPrefetchSignature = string.Empty;
    }
    else
    {
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
        this.NotifyAcceptedQuestPrefetchStarted(
            this.acceptedQuestPrefetchQueue.Count);
      }
    }

    var processedQuestCount = 0;
    HashSet<uint> processedQuestIds = [];
    while (processedQuestCount < AcceptedQuestPrefetchQuestsPerTick &&
           this.TryDequeueAcceptedQuestPrefetchRequest(out var requestedQuestId))
    {
      this.PrefetchAcceptedQuest(requestedQuestId);
      processedQuestIds.Add(requestedQuestId);
      processedQuestCount++;
    }

    while (processedQuestCount < AcceptedQuestPrefetchQuestsPerTick &&
           this.acceptedQuestPrefetchQueueIndex <
           this.acceptedQuestPrefetchQueue.Count)
    {
      var questId =
          this.acceptedQuestPrefetchQueue[this.acceptedQuestPrefetchQueueIndex++];
      if (!processedQuestIds.Add(questId))
      {
        continue;
      }

      this.PrefetchAcceptedQuest(questId);
      processedQuestCount++;
    }

    if (this.acceptedQuestPrefetchRequestedQuestQueue.Count == 0 &&
        (!hasAcceptedQuestIds ||
         this.acceptedQuestPrefetchQueueIndex >=
         this.acceptedQuestPrefetchQueue.Count))
    {
      this.TryCompleteAcceptedQuestPrefetchNotification();
    }
  }

  /// <summary>
  ///     Requests a prioritized background prefetch for an accepted quest.
  ///     This only schedules work; translation and persistence remain owned by
  ///     the framework-update prefetch runtime.
  /// </summary>
  /// <param name="questId">The accepted quest identifier to prefetch.</param>
  private void RequestAcceptedQuestPrefetch(uint questId)
  {
    if (!this.acceptedQuestPrefetchRequestedQuestQueue.Request(questId))
    {
      return;
    }
  }

  /// <summary>
  ///     Dequeues one deduplicated accepted-quest prefetch request.
  /// </summary>
  /// <param name="questId">The requested accepted quest identifier.</param>
  /// <returns>True when one request was available.</returns>
  private bool TryDequeueAcceptedQuestPrefetchRequest(out uint questId)
  {
    questId = 0;
    return this.acceptedQuestPrefetchRequestedQuestQueue.TryDequeue(
        out questId);
  }

  /// <summary>
  ///     Clears the accepted-quest prefetch runtime state.
  /// </summary>
  private void ClearAcceptedQuestPrefetchState()
  {
    this.acceptedQuestPrefetchQueue.Clear();
    this.acceptedQuestPrefetchRequestedQuestQueue.Clear();
    this.acceptedQuestPrefetchQueueIndex = 0;
    this.acceptedQuestPrefetchSignature = string.Empty;
    this.acceptedQuestPrefetchLastTickUtc = DateTime.MinValue;
    this.acceptedQuestPrefetchNotificationActive = false;
    this.acceptedQuestPrefetchNotificationQuestCount = 0;
  }

  /// <summary>
  ///     Shows the accepted-quest prefetch start notification when a new
  ///     accepted-quest queue begins.
  /// </summary>
  /// <param name="questCount">The number of accepted quests being prefetched.</param>
  private void NotifyAcceptedQuestPrefetchStarted(int questCount)
  {
    if (questCount <= 0 || !this.configuration.ShowQuestProgressNotifications)
    {
      return;
    }

    this.acceptedQuestPrefetchNotificationActive = true;
    this.acceptedQuestPrefetchNotificationQuestCount = questCount;

    NotificationManager.AddNotification(new Notification
    {
      Title = Resources.Name,
      Content = string.Format(
          CultureInfo.CurrentCulture,
          Resources.AcceptedQuestPrefetchStartedNotification,
          questCount),
      Icon = FontAwesomeIcon.Book.ToNotificationIcon(),
      Type = NotificationType.Info,
    });
  }

  /// <summary>
  ///     Shows the accepted-quest prefetch completion notification once the
  ///     current queue has fully drained.
  /// </summary>
  private void TryCompleteAcceptedQuestPrefetchNotification()
  {
    if (!this.acceptedQuestPrefetchNotificationActive)
    {
      return;
    }

    if (!this.configuration.ShowQuestProgressNotifications)
    {
      this.acceptedQuestPrefetchNotificationActive = false;
      this.acceptedQuestPrefetchNotificationQuestCount = 0;
      return;
    }

    NotificationManager.AddNotification(new Notification
    {
      Title = Resources.Name,
      Content = string.Format(
          CultureInfo.CurrentCulture,
          Resources.AcceptedQuestPrefetchCompletedNotification,
          this.acceptedQuestPrefetchNotificationQuestCount),
      Icon = FontAwesomeIcon.Check.ToNotificationIcon(),
      Type = NotificationType.Success,
    });

    this.acceptedQuestPrefetchNotificationActive = false;
    this.acceptedQuestPrefetchNotificationQuestCount = 0;
  }

  /// <summary>
  ///     Gets whether accepted quests should be prefetched in the current
  ///     runtime state.
  /// </summary>
  /// <returns>True when the background prefetch should run.</returns>
  private bool ShouldPrefetchAcceptedQuests()
  {
    return this.configuration.Translate &&
           FrameworkAccessGuard.IsClientReadyForPlayerScopedFrameworkAccess() &&
           (this.configuration.TranslateJournal ||
            this.configuration.TranslateJournalDetail ||
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
    this.LogAcceptedQuestPrefetchEvent(
        "quest-start",
        questId,
        detail: "Accepted-quest prefetch tick picked this quest for processing.");

    if (!QuestProgressResolver.TryResolveQuestProgress(
            questId.ToString(CultureInfo.InvariantCulture),
            out var questProgressSnapshot))
    {
      this.LogAcceptedQuestPrefetchEvent(
          "resolve-failed",
          questId,
          detail: "QuestProgressResolver could not resolve live progress for this accepted quest.");
      return;
    }

    var questCanonicalData = QuestCanonicalData.Create(
        questProgressSnapshot,
        GetGameVersion());
    var currentQuestSequenceText = questCanonicalData.CurrentSequenceText;
    var translationService = TranslationService;
    var nameDispatchResult = RunAcceptedQuestPrefetchOperationEntry(
        questCanonicalData,
        ResolveCurrentPrefetchSourceLanguage,
        this.configuration,
        this.TryGetQueuedTranslation,
        this.QueueTranslation,
        (sourceText, capturedSource, targetLanguage, originContext) =>
            translationService.Translate(
                sourceText,
                capturedSource,
                targetLanguage,
                originContext: originContext),
        this.FindQuestPlate,
        this.FindQuestPlateByName,
        questPlate =>
        {
          this.EmitAcceptedQuestPrefetchDiagnostic(
              questCanonicalData,
              questPlate);
          this.InsertQuestPlate(questPlate);
        },
        (questPlate, fromCache) =>
        {
          this.LogAcceptedQuestPrefetchTranslationEvent(
              questProgressSnapshot,
              "Name",
              fromCache ? "cache-hit" : "resolved",
              sourceText: questProgressSnapshot.QuestName,
              translatedText: questPlate.TranslatedQuestName);
          this.UpdateQuestPlate(questPlate);
        },
        out var sourceLanguage,
        out var scope,
        out var existingQuestPlate);
    if (existingQuestPlate == null)
    {
      return;
    }

    this.LogAcceptedQuestPrefetchEvent(
        "resolved",
        questProgressSnapshot.QuestId,
        questProgressSnapshot.QuestName,
        $"sequence={questProgressSnapshot.QuestSequence}, sheet={questProgressSnapshot.QuestSheetName}, currentSeqLen={currentQuestSequenceText.Length}, summaries={questProgressSnapshot.QuestSeqTexts.Count}, objectives={questProgressSnapshot.QuestSteps.Count}, system={questProgressSnapshot.QuestSystemTexts.Count}");
    this.LogAcceptedQuestPrefetchEvent(
        "existing-row",
        questProgressSnapshot.QuestId,
        questProgressSnapshot.QuestName,
        $"translatedName={(!string.IsNullOrWhiteSpace(existingQuestPlate.TranslatedQuestName)).ToString()}, translatedMessage={(!string.IsNullOrWhiteSpace(existingQuestPlate.TranslatedQuestMessage)).ToString()}, translatedObjectives={existingQuestPlate.TranslatedObjectives.Count}, translatedSummaries={existingQuestPlate.TranslatedSummaries.Count}, translatedSystem={existingQuestPlate.TranslatedSystemRows.Count}");

    if (!string.IsNullOrWhiteSpace(existingQuestPlate.TranslatedQuestName))
    {
      this.LogAcceptedQuestPrefetchTranslationEvent(
          questProgressSnapshot,
          "Name",
          "skip-existing",
          sourceText: questProgressSnapshot.QuestName);
    }
    else if (nameDispatchResult is PrefetchTranslationDispatchResult.Queued or
             PrefetchTranslationDispatchResult.AlreadyPending)
    {
      this.LogAcceptedQuestPrefetchTranslationEvent(
          questProgressSnapshot,
          "Name",
          nameDispatchResult == PrefetchTranslationDispatchResult.Queued
              ? "queued"
              : "already-in-flight",
          sourceText: questProgressSnapshot.QuestName);
    }

    this.PrefetchAcceptedQuestCurrentMessage(
        questProgressSnapshot,
        currentQuestSequenceText,
        existingQuestPlate,
        sourceLanguage,
        scope);
    this.PrefetchAcceptedQuestSummaries(
        questProgressSnapshot,
        currentQuestSequenceText,
        existingQuestPlate,
        sourceLanguage,
        scope);
    this.PrefetchAcceptedQuestObjectives(
        questProgressSnapshot,
        currentQuestSequenceText,
        existingQuestPlate,
        sourceLanguage,
        scope);
    this.PrefetchAcceptedQuestSystemRows(
        questProgressSnapshot,
        currentQuestSequenceText,
        existingQuestPlate,
        sourceLanguage,
        scope);
  }

  /// <summary>
  ///     Prefetches the current quest-body message for an accepted quest when it
  ///     is not yet persisted.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <param name="existingQuestPlate">The existing persisted quest plate, if any.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <param name="scope">The immutable operation reuse scope.</param>
  private void PrefetchAcceptedQuestCurrentMessage(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      QuestPlate existingQuestPlate,
      SourceClientLanguage sourceLanguage,
      TranslationReuseScope scope)
  {
    if (string.IsNullOrWhiteSpace(currentQuestSequenceText) ||
        !string.IsNullOrWhiteSpace(existingQuestPlate.TranslatedQuestMessage))
    {
      this.LogAcceptedQuestPrefetchTranslationEvent(
          questProgressSnapshot,
          "Message",
          string.IsNullOrWhiteSpace(currentQuestSequenceText)
              ? "skip-empty"
              : "skip-existing",
          sourceText: currentQuestSequenceText);
      return;
    }

    var translationService = TranslationService;
    var dispatchResult = DispatchAcceptedQuestPrefetchTranslation(
        $"AcceptedQuestPrefetch|{questProgressSnapshot.CacheKey}|Message|{currentQuestSequenceText}",
        sourceLanguage,
        scope,
        this.TryGetQueuedTranslation,
        this.QueueTranslation,
        () => translationService.Translate(
            currentQuestSequenceText,
            sourceLanguage,
            scope.TargetLanguageCode,
            originContext: BuildAcceptedQuestOriginContext(
                questProgressSnapshot,
                "Message")),
        (translatedQuestMessage, capturedScope, fromCache) =>
        {
          this.LogAcceptedQuestPrefetchTranslationEvent(
              questProgressSnapshot,
              "Message",
              fromCache ? "cache-hit" : "resolved",
              sourceText: currentQuestSequenceText,
              translatedText: translatedQuestMessage);
          this.ApplyAcceptedQuestMessageTranslation(
              questProgressSnapshot,
              currentQuestSequenceText,
              translatedQuestMessage,
              capturedScope);
        });
    if (dispatchResult is PrefetchTranslationDispatchResult.Queued or
        PrefetchTranslationDispatchResult.AlreadyPending)
    {
      this.LogAcceptedQuestPrefetchTranslationEvent(
          questProgressSnapshot,
          "Message",
          dispatchResult == PrefetchTranslationDispatchResult.Queued
              ? "queued"
              : "already-in-flight",
          sourceText: currentQuestSequenceText);
    }
  }

  /// <summary>
  ///     Prefetches all SEQ summary rows for an accepted quest.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <param name="existingQuestPlate">The existing persisted quest plate, if any.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <param name="scope">The immutable operation reuse scope.</param>
  private void PrefetchAcceptedQuestSummaries(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      QuestPlate existingQuestPlate,
      SourceClientLanguage sourceLanguage,
      TranslationReuseScope scope)
  {
    foreach (var questSequenceEntry in questProgressSnapshot.QuestSeqTexts)
    {
      if (string.IsNullOrWhiteSpace(questSequenceEntry.Text) ||
          TryGetTranslatedQuestPlateValue(
              existingQuestPlate.TranslatedSummaryRowsByKey,
              existingQuestPlate.TranslatedSummaries,
              questSequenceEntry.KeyText,
              questSequenceEntry.Text,
              out var translatedSummaryText) &&
          !string.IsNullOrWhiteSpace(translatedSummaryText))
      {
        this.LogAcceptedQuestPrefetchTranslationEvent(
            questProgressSnapshot,
            "Summary",
            string.IsNullOrWhiteSpace(questSequenceEntry.Text)
                ? "skip-empty"
                : "skip-existing",
            questSequenceEntry.KeyText,
            questSequenceEntry.Text);
        continue;
      }

      var translationService = TranslationService;
      var dispatchResult = DispatchAcceptedQuestPrefetchTranslation(
          $"AcceptedQuestPrefetch|{questProgressSnapshot.CacheKey}|Summary|{questSequenceEntry.KeyText}|{questSequenceEntry.Text}",
          sourceLanguage,
          scope,
          this.TryGetQueuedTranslation,
          this.QueueTranslation,
          () => translationService.Translate(
              questSequenceEntry.Text,
              sourceLanguage,
              scope.TargetLanguageCode,
              originContext: BuildAcceptedQuestOriginContext(
                  questProgressSnapshot,
                  "Summary")),
          (translatedSummaryTextValue, capturedScope, fromCache) =>
          {
              this.LogAcceptedQuestPrefetchTranslationEvent(
                  questProgressSnapshot,
                  "Summary",
                  fromCache ? "cache-hit" : "resolved",
                  questSequenceEntry.KeyText,
                  questSequenceEntry.Text,
                  translatedSummaryTextValue);
              this.ApplyAcceptedQuestSummaryTranslation(
                  questProgressSnapshot,
                  currentQuestSequenceText,
                  questSequenceEntry.KeyText,
                  questSequenceEntry.Text,
                  translatedSummaryTextValue,
                  capturedScope);
          });
      if (dispatchResult is PrefetchTranslationDispatchResult.Queued or
          PrefetchTranslationDispatchResult.AlreadyPending)
      {
        this.LogAcceptedQuestPrefetchTranslationEvent(
            questProgressSnapshot,
            "Summary",
            dispatchResult == PrefetchTranslationDispatchResult.Queued
                ? "queued"
                : "already-in-flight",
            questSequenceEntry.KeyText,
            questSequenceEntry.Text);
      }
    }
  }

  /// <summary>
  ///     Prefetches all TODO rows for an accepted quest.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <param name="existingQuestPlate">The existing persisted quest plate, if any.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <param name="scope">The immutable operation reuse scope.</param>
  private void PrefetchAcceptedQuestObjectives(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      QuestPlate existingQuestPlate,
      SourceClientLanguage sourceLanguage,
      TranslationReuseScope scope)
  {
    foreach (var questStep in questProgressSnapshot.QuestSteps)
    {
      if (string.IsNullOrWhiteSpace(questStep.Text) ||
          TryGetTranslatedQuestPlateValue(
              existingQuestPlate.TranslatedObjectiveRowsByKey,
              existingQuestPlate.TranslatedObjectives,
              questStep.KeyText,
              questStep.Text,
              out var translatedObjectiveText) &&
          !string.IsNullOrWhiteSpace(translatedObjectiveText))
      {
        this.LogAcceptedQuestPrefetchTranslationEvent(
            questProgressSnapshot,
            "Objective",
            string.IsNullOrWhiteSpace(questStep.Text)
                ? "skip-empty"
                : "skip-existing",
            questStep.KeyText,
            questStep.Text);
        continue;
      }

      var translationService = TranslationService;
      var dispatchResult = DispatchAcceptedQuestPrefetchTranslation(
          $"AcceptedQuestPrefetch|{questProgressSnapshot.CacheKey}|Objective|{questStep.KeyText}|{questStep.Text}",
          sourceLanguage,
          scope,
          this.TryGetQueuedTranslation,
          this.QueueTranslation,
          () => translationService.Translate(
              questStep.Text,
              sourceLanguage,
              scope.TargetLanguageCode,
              originContext: BuildAcceptedQuestOriginContext(
                  questProgressSnapshot,
                  "Objective")),
          (translatedObjectiveTextValue, capturedScope, fromCache) =>
          {
              this.LogAcceptedQuestPrefetchTranslationEvent(
                  questProgressSnapshot,
                  "Objective",
                  fromCache ? "cache-hit" : "resolved",
                  questStep.KeyText,
                  questStep.Text,
                  translatedObjectiveTextValue);
              this.ApplyAcceptedQuestObjectiveTranslation(
                  questProgressSnapshot,
                  currentQuestSequenceText,
                  questStep.KeyText,
                  questStep.Text,
                  translatedObjectiveTextValue,
                  capturedScope);
          });
      if (dispatchResult is PrefetchTranslationDispatchResult.Queued or
          PrefetchTranslationDispatchResult.AlreadyPending)
      {
        this.LogAcceptedQuestPrefetchTranslationEvent(
            questProgressSnapshot,
            "Objective",
            dispatchResult == PrefetchTranslationDispatchResult.Queued
                ? "queued"
                : "already-in-flight",
            questStep.KeyText,
            questStep.Text);
      }
    }
  }

  /// <summary>
  ///     Prefetches all SYSTEM rows for an accepted quest.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <param name="existingQuestPlate">The existing persisted quest plate, if any.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <param name="scope">The immutable operation reuse scope.</param>
  private void PrefetchAcceptedQuestSystemRows(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      QuestPlate existingQuestPlate,
      SourceClientLanguage sourceLanguage,
      TranslationReuseScope scope)
  {
    foreach (var questSystemText in questProgressSnapshot.QuestSystemTexts)
    {
      if (string.IsNullOrWhiteSpace(questSystemText.Text) ||
          TryGetTranslatedQuestPlateValue(
              existingQuestPlate.TranslatedSystemRowsByKey,
              existingQuestPlate.TranslatedSystemRows,
              questSystemText.KeyText,
              questSystemText.Text,
              out var translatedSystemRowText) &&
          !string.IsNullOrWhiteSpace(translatedSystemRowText))
      {
        this.LogAcceptedQuestPrefetchTranslationEvent(
            questProgressSnapshot,
            "System",
            string.IsNullOrWhiteSpace(questSystemText.Text)
                ? "skip-empty"
                : "skip-existing",
            questSystemText.KeyText,
            questSystemText.Text);
        continue;
      }

      var translationService = TranslationService;
      var dispatchResult = DispatchAcceptedQuestPrefetchTranslation(
          $"AcceptedQuestPrefetch|{questProgressSnapshot.CacheKey}|System|{questSystemText.KeyText}|{questSystemText.Text}",
          sourceLanguage,
          scope,
          this.TryGetQueuedTranslation,
          this.QueueTranslation,
          () => translationService.Translate(
              questSystemText.Text,
              sourceLanguage,
              scope.TargetLanguageCode,
              originContext: BuildAcceptedQuestOriginContext(
                  questProgressSnapshot,
                  "System")),
          (translatedSystemTextValue, capturedScope, fromCache) =>
          {
              this.LogAcceptedQuestPrefetchTranslationEvent(
                  questProgressSnapshot,
                  "System",
                  fromCache ? "cache-hit" : "resolved",
                  questSystemText.KeyText,
                  questSystemText.Text,
                  translatedSystemTextValue);
              this.ApplyAcceptedQuestSystemTranslation(
                  questProgressSnapshot,
                  currentQuestSequenceText,
                  questSystemText.KeyText,
                  questSystemText.Text,
                  translatedSystemTextValue,
                  capturedScope);
          });
      if (dispatchResult is PrefetchTranslationDispatchResult.Queued or
          PrefetchTranslationDispatchResult.AlreadyPending)
      {
        this.LogAcceptedQuestPrefetchTranslationEvent(
            questProgressSnapshot,
            "System",
            dispatchResult == PrefetchTranslationDispatchResult.Queued
                ? "queued"
                : "already-in-flight",
            questSystemText.KeyText,
            questSystemText.Text);
      }
    }
  }

  /// <summary>
  ///     Applies a prefetched current-message translation into the canonical
  ///     quest plate row.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <param name="translatedQuestMessage">The translated quest message.</param>
  /// <param name="scope">The immutable operation reuse scope.</param>
  private void ApplyAcceptedQuestMessageTranslation(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      string translatedQuestMessage,
      TranslationReuseScope scope)
  {
    if (string.IsNullOrWhiteSpace(currentQuestSequenceText) ||
        string.IsNullOrWhiteSpace(translatedQuestMessage))
    {
      return;
    }

    var questCanonicalData = QuestCanonicalData.Create(
        questProgressSnapshot,
        GetGameVersion());
    var questPlate = this.CreateAcceptedQuestPrefetchPlate(
        questCanonicalData,
        scope);
    questPlate.TranslatedQuestMessage = translatedQuestMessage;

    if (questCanonicalData.TryGetCurrentSequenceEntry(out var currentSequenceEntry))
    {
      questPlate.SetTranslatedSummaryText(
          currentSequenceEntry.KeyText,
          currentSequenceEntry.Text,
          translatedQuestMessage);
    }
    else
    {
      questPlate.SetTranslatedSummaryText(
          rowKey: null,
          sourceText: currentQuestSequenceText,
          translatedQuestMessage);
    }

    this.UpdateQuestPlate(questPlate);
  }

  /// <summary>
  ///     Applies a prefetched SEQ summary translation into the canonical quest
  ///     plate row.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <param name="summaryRowKey">The canonical summary-row key.</param>
  /// <param name="originalSummaryText">The original summary text.</param>
  /// <param name="translatedSummaryText">The translated summary text.</param>
  /// <param name="scope">The immutable operation reuse scope.</param>
  private void ApplyAcceptedQuestSummaryTranslation(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      string summaryRowKey,
      string originalSummaryText,
      string translatedSummaryText,
      TranslationReuseScope scope)
  {
    if (string.IsNullOrWhiteSpace(originalSummaryText) ||
        string.IsNullOrWhiteSpace(translatedSummaryText))
    {
      return;
    }

    var questCanonicalData = QuestCanonicalData.Create(
        questProgressSnapshot,
        GetGameVersion());
    var questPlate = this.CreateAcceptedQuestPrefetchPlate(
        questCanonicalData,
        scope);
    questPlate.SetTranslatedSummaryText(
        summaryRowKey,
        originalSummaryText,
        translatedSummaryText);

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
  /// <param name="objectiveRowKey">The canonical objective-row key.</param>
  /// <param name="originalObjectiveText">The original objective text.</param>
  /// <param name="translatedObjectiveText">The translated objective text.</param>
  /// <param name="scope">The immutable operation reuse scope.</param>
  private void ApplyAcceptedQuestObjectiveTranslation(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      string objectiveRowKey,
      string originalObjectiveText,
      string translatedObjectiveText,
      TranslationReuseScope scope)
  {
    if (string.IsNullOrWhiteSpace(originalObjectiveText) ||
        string.IsNullOrWhiteSpace(translatedObjectiveText))
    {
      return;
    }

    var questCanonicalData = QuestCanonicalData.Create(
        questProgressSnapshot,
        GetGameVersion());
    var questPlate = this.CreateAcceptedQuestPrefetchPlate(
        questCanonicalData,
        scope);
    questPlate.SetTranslatedObjectiveText(
        objectiveRowKey,
        originalObjectiveText,
        translatedObjectiveText);
    this.UpdateQuestPlate(questPlate);
  }

  /// <summary>
  ///     Applies a prefetched SYSTEM-row translation into the canonical quest
  ///     plate row.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentQuestSequenceText">The current SEQ row text.</param>
  /// <param name="systemRowKey">The canonical SYSTEM-row key.</param>
  /// <param name="originalSystemText">The original SYSTEM-row text.</param>
  /// <param name="translatedSystemText">The translated SYSTEM-row text.</param>
  /// <param name="scope">The immutable operation reuse scope.</param>
  private void ApplyAcceptedQuestSystemTranslation(
      QuestProgressSnapshot questProgressSnapshot,
      string currentQuestSequenceText,
      string systemRowKey,
      string originalSystemText,
      string translatedSystemText,
      TranslationReuseScope scope)
  {
    if (string.IsNullOrWhiteSpace(originalSystemText) ||
        string.IsNullOrWhiteSpace(translatedSystemText))
    {
      return;
    }

    var questCanonicalData = QuestCanonicalData.Create(
        questProgressSnapshot,
        GetGameVersion());
    var questPlate = this.CreateAcceptedQuestPrefetchPlate(
        questCanonicalData,
        scope);
    questPlate.SetTranslatedSystemText(
        systemRowKey,
        originalSystemText,
        translatedSystemText);
    this.UpdateQuestPlate(questPlate);
  }

  /// <summary>
  ///     Creates the canonical accepted-quest quest plate snapshot used by the
  ///     background prefetch runtime.
  /// </summary>
  /// <param name="questCanonicalData">The canonical quest payload.</param>
  /// <param name="scope">The immutable operation reuse scope.</param>
  /// <returns>The canonical quest plate snapshot.</returns>
  private QuestPlate CreateAcceptedQuestPrefetchPlate(
      QuestCanonicalData questCanonicalData,
      TranslationReuseScope scope)
  {
    return questCanonicalData.ToQuestPlate(
        scope.SourceLanguageCode,
        scope.TargetLanguageCode,
        scope.TranslationEngine!.Value,
        DateTime.Now);
  }

  /// <summary>
  ///     Adds the complete operation scope to one accepted-quest payload key.
  /// </summary>
  /// <param name="payloadIdentity">The existing payload identity.</param>
  /// <param name="scope">The immutable operation reuse scope.</param>
  /// <returns>The source-scoped broker key.</returns>
  private static string BuildAcceptedQuestScopedTranslationKey(
      string payloadIdentity,
      TranslationReuseScope scope)
  {
    return BuildTranslationReuseScopedKey(payloadIdentity, scope);
  }

  /// <summary>
  ///     Builds the diagnostic surface identity for one accepted-quest field.
  /// </summary>
  /// <param name="questProgressSnapshot">The accepted-quest progress snapshot.</param>
  /// <param name="fieldName">The translated field name.</param>
  /// <returns>The diagnostic surface identity.</returns>
  private static string BuildAcceptedQuestOriginContext(
      QuestProgressSnapshot questProgressSnapshot,
      string fieldName)
  {
    return $"AcceptedQuest/{questProgressSnapshot.QuestId}/{fieldName}";
  }

  /// <summary>
  ///     Dispatches accepted-quest work through the production scoped prefetch
  ///     orchestrator.
  /// </summary>
  /// <param name="payloadIdentity">The accepted-quest payload identity.</param>
  /// <param name="sourceLanguage">The operation-captured source contract.</param>
  /// <param name="scope">The operation-captured reuse scope.</param>
  /// <param name="tryGetTranslation">The shared broker cache lookup.</param>
  /// <param name="queueTranslation">The shared broker queue operation.</param>
  /// <param name="resolver">The translation resolver.</param>
  /// <param name="onCompleted">The persistence callback.</param>
  /// <returns>The production dispatch result.</returns>
  internal static PrefetchTranslationDispatchResult
      DispatchAcceptedQuestPrefetchTranslation(
          string payloadIdentity,
          SourceClientLanguage sourceLanguage,
          TranslationReuseScope scope,
          TryGetPrefetchTranslationDelegate tryGetTranslation,
          QueuePrefetchTranslationDelegate queueTranslation,
          Func<string> resolver,
          Action<string, TranslationReuseScope, bool> onCompleted)
  {
    return DispatchScopedPrefetchTranslation(
        BuildAcceptedQuestScopedTranslationKey(payloadIdentity, scope),
        sourceLanguage,
        scope,
        tryGetTranslation,
        queueTranslation,
        resolver,
        onCompleted);
  }

  /// <summary>
  ///     Captures live scope and runs one production accepted-quest operation
  ///     from canonical persistence through name broker completion.
  /// </summary>
  /// <param name="questCanonicalData">The canonical quest payload.</param>
  /// <param name="sourceLanguageResolver">Resolves the live client source.</param>
  /// <param name="configuration">The live translation configuration.</param>
  /// <param name="tryGetTranslation">The shared broker cache lookup.</param>
  /// <param name="queueTranslation">The shared broker queue operation.</param>
  /// <param name="translate">The production translation operation.</param>
  /// <param name="findRow">The production quest-row lookup.</param>
  /// <param name="findRowByName">The production quest-row name lookup.</param>
  /// <param name="persistCanonicalRow">The canonical-row persistence operation.</param>
  /// <param name="persistNameRow">The translated-name persistence operation.</param>
  /// <param name="sourceLanguage">The captured source contract.</param>
  /// <param name="scope">The captured reuse scope.</param>
  /// <param name="existingRow">The canonical row used by sibling work units.</param>
  /// <returns>The production dispatch result.</returns>
  internal static PrefetchTranslationDispatchResult
      RunAcceptedQuestPrefetchOperationEntry(
          QuestCanonicalData questCanonicalData,
          Func<SourceClientLanguage?> sourceLanguageResolver,
          Config configuration,
          TryGetPrefetchTranslationDelegate tryGetTranslation,
          QueuePrefetchTranslationDelegate queueTranslation,
          ResolvePrefetchTranslationDelegate translate,
          Func<QuestPlate, QuestPlate?> findRow,
          Func<QuestPlate, QuestPlate?> findRowByName,
          Action<QuestPlate> persistCanonicalRow,
          Action<QuestPlate, bool> persistNameRow,
          out SourceClientLanguage sourceLanguage,
          out TranslationReuseScope scope,
          out QuestPlate existingRow)
  {
    if (!TryCapturePrefetchOperationScope(
            sourceLanguageResolver,
            configuration,
            out sourceLanguage,
            out scope))
    {
      existingRow = null!;
      return PrefetchTranslationDispatchResult.Rejected;
    }

    return RunAcceptedQuestPrefetchOperationEntry(
        questCanonicalData,
        sourceLanguage,
        scope,
        tryGetTranslation,
        queueTranslation,
        translate,
        findRow,
        findRowByName,
        persistCanonicalRow,
        persistNameRow,
        out existingRow);
  }

  /// <summary>
  ///     Runs one captured accepted-quest operation from canonical persistence
  ///     through name broker completion.
  /// </summary>
  /// <param name="questCanonicalData">The canonical quest payload.</param>
  /// <param name="sourceLanguage">The operation-captured source contract.</param>
  /// <param name="scope">The operation-captured reuse scope.</param>
  /// <param name="tryGetTranslation">The shared broker cache lookup.</param>
  /// <param name="queueTranslation">The shared broker queue operation.</param>
  /// <param name="translate">The production translation operation.</param>
  /// <param name="findRow">The production quest-row lookup.</param>
  /// <param name="findRowByName">The production quest-row name lookup.</param>
  /// <param name="persistCanonicalRow">The canonical-row persistence operation.</param>
  /// <param name="persistNameRow">The translated-name persistence operation.</param>
  /// <param name="existingRow">The canonical row used by sibling work units.</param>
  /// <returns>The production dispatch result.</returns>
  private static PrefetchTranslationDispatchResult
      RunAcceptedQuestPrefetchOperationEntry(
          QuestCanonicalData questCanonicalData,
          SourceClientLanguage sourceLanguage,
          TranslationReuseScope scope,
          TryGetPrefetchTranslationDelegate tryGetTranslation,
          QueuePrefetchTranslationDelegate queueTranslation,
          ResolvePrefetchTranslationDelegate translate,
          Func<QuestPlate, QuestPlate?> findRow,
          Func<QuestPlate, QuestPlate?> findRowByName,
          Action<QuestPlate> persistCanonicalRow,
          Action<QuestPlate, bool> persistNameRow,
          out QuestPlate existingRow)
  {
    if (!IsValidCapturedPrefetchScope(sourceLanguage, scope))
    {
      existingRow = null!;
      return PrefetchTranslationDispatchResult.Rejected;
    }

    var canonicalQuestPlate = questCanonicalData.ToQuestPlate(
        scope.SourceLanguageCode,
        scope.TargetLanguageCode,
        scope.TranslationEngine!.Value,
        DateTime.Now);
    persistCanonicalRow(canonicalQuestPlate);
    existingRow = findRow(canonicalQuestPlate) ??
                  findRowByName(canonicalQuestPlate) ??
                  canonicalQuestPlate;

    if (!string.IsNullOrWhiteSpace(existingRow.TranslatedQuestName))
    {
      return PrefetchTranslationDispatchResult.Rejected;
    }

    var questProgressSnapshot = questCanonicalData.QuestProgressSnapshot;
    return DispatchAcceptedQuestPrefetchTranslation(
        $"AcceptedQuestPrefetch|{questProgressSnapshot.CacheKey}|Name|{questProgressSnapshot.QuestName}",
        sourceLanguage,
        scope,
        tryGetTranslation,
        queueTranslation,
        () => translate(
            questProgressSnapshot.QuestName,
            sourceLanguage,
            scope.TargetLanguageCode,
            BuildAcceptedQuestOriginContext(
                questProgressSnapshot,
                "Name")),
        (translatedQuestName, capturedScope, fromCache) =>
        {
          if (string.IsNullOrWhiteSpace(translatedQuestName))
          {
            return;
          }

          var questPlate = questCanonicalData.ToQuestPlate(
              capturedScope.SourceLanguageCode,
              capturedScope.TargetLanguageCode,
              capturedScope.TranslationEngine!.Value,
              DateTime.Now);
          questPlate.TranslatedQuestName = translatedQuestName;
          persistNameRow(questPlate, fromCache);
        });
  }

  /// <summary>
  ///     Emits the current canonical accepted-quest payload and its projected
  ///     quest-plate shape into a purpose-named diagnostic file next to the DB.
  /// </summary>
  /// <param name="questCanonicalData">The canonical quest payload.</param>
  /// <param name="questPlate">The projected quest plate.</param>
  private void EmitAcceptedQuestPrefetchDiagnostic(
      QuestCanonicalData questCanonicalData,
      QuestPlate questPlate)
  {
    if (questCanonicalData == null || questPlate == null)
    {
      return;
    }

    var builder = new StringBuilder();
    questPlate.UpdateFieldsAsText(prettyPrint: true);
    builder.AppendLine(questCanonicalData.ToString());
    builder.AppendLine();
    builder.AppendLine("ProjectedQuestPlate");
    builder.AppendLine(questPlate.ToString());

    DiagnosticFileEmitter.Emit(
        "accepted-quest-prefetch-canonical",
        $"{questCanonicalData.QuestProgressSnapshot.QuestId}:{questCanonicalData.QuestProgressSnapshot.QuestName}",
        builder.ToString());
  }

  /// <summary>
  ///     Tries to resolve the current SEQ entry for the supplied quest snapshot.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="currentSequenceEntry">The current SEQ entry, when found.</param>
  /// <returns>True when a current SEQ entry could be resolved.</returns>
  /// <summary>
  ///     Tries to resolve an existing translated row using canonical row-keyed
  ///     storage first and legacy text-keyed storage second.
  /// </summary>
  /// <param name="canonicalRows">The canonical translated dictionary.</param>
  /// <param name="legacyRows">The legacy translated dictionary.</param>
  /// <param name="rowKey">The canonical row key.</param>
  /// <param name="sourceText">The legacy source text key.</param>
  /// <param name="translatedText">The resolved translated text.</param>
  /// <returns>True when a translated value was found.</returns>
  private static bool TryGetTranslatedQuestPlateValue(
      IReadOnlyDictionary<string, string> canonicalRows,
      IReadOnlyDictionary<string, string> legacyRows,
      string rowKey,
      string sourceText,
      out string translatedText)
  {
    translatedText = string.Empty;

    if (!string.IsNullOrWhiteSpace(rowKey) &&
        canonicalRows.TryGetValue(rowKey, out translatedText) &&
        !string.IsNullOrWhiteSpace(translatedText))
    {
      return true;
    }

    if (!string.IsNullOrWhiteSpace(sourceText) &&
        legacyRows.TryGetValue(sourceText, out translatedText) &&
        !string.IsNullOrWhiteSpace(translatedText))
    {
      return true;
    }

    translatedText = string.Empty;
    return false;
  }

  /// <summary>
  /// Emits a concise accepted-quest prefetch activity event to the accepted
  /// quest diagnostic file and, when enabled, to the Dalamud log.
  /// </summary>
  /// <param name="phase">The lifecycle phase being logged.</param>
  /// <param name="questId">The quest id.</param>
  /// <param name="questName">The quest name when available.</param>
  /// <param name="detail">Additional structured detail.</param>
  private void LogAcceptedQuestPrefetchEvent(
      string phase,
      uint questId,
      string? questName = null,
      string? detail = null)
  {
    var questLabel = string.IsNullOrWhiteSpace(questName)
        ? questId.ToString(CultureInfo.InvariantCulture)
        : $"{questId}:{questName}";
    var content =
        $"phase={phase}{Environment.NewLine}quest={questLabel}{Environment.NewLine}detail={detail ?? string.Empty}";
    var logLine =
        $"[AcceptedQuestPrefetch] phase={phase} quest='{questLabel}' detail='{detail ?? string.Empty}'";
    if (AcceptedQuestPrefetchEmitDalamudLog &&
        string.Equals(phase, "resolve-failed", StringComparison.Ordinal))
    {
      PluginRuntimeLog.Warning(logLine);
    }
    else if (AcceptedQuestPrefetchEmitDalamudLog)
    {
      PluginRuntimeLog.Debug(logLine);
    }

    DiagnosticFileEmitter.Emit(
        "accepted-quest-prefetch-activity",
        questLabel,
        content);
  }

  /// <summary>
  /// Emits a translation-stage accepted-quest prefetch event for one canonical
  /// quest field or row.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest snapshot.</param>
  /// <param name="category">The translation category.</param>
  /// <param name="phase">The translation phase.</param>
  /// <param name="rowKey">The optional row key.</param>
  /// <param name="sourceText">The source text.</param>
  /// <param name="translatedText">The translated text, when resolved.</param>
  private void LogAcceptedQuestPrefetchTranslationEvent(
      QuestProgressSnapshot questProgressSnapshot,
      string category,
      string phase,
      string? rowKey = null,
      string? sourceText = null,
      string? translatedText = null)
  {
    var detail =
        $"category={category}, rowKey={rowKey ?? string.Empty}, sourceLen={sourceText?.Length ?? 0}, translatedLen={translatedText?.Length ?? 0}, cacheKey={questProgressSnapshot.CacheKey}";
    this.LogAcceptedQuestPrefetchEvent(
        $"translation-{phase}",
        questProgressSnapshot.QuestId,
        questProgressSnapshot.QuestName,
        detail);
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
    return BuildAcceptedQuestSignature(
        acceptedQuestIds,
        QuestManager.GetQuestSequence,
        TryResolveAcceptedQuestTodoSignature);
  }

  /// <summary>
  ///     Builds a stable accepted-quest signature from supplied live state
  ///     resolvers.
  /// </summary>
  /// <param name="acceptedQuestIds">The accepted quest ids.</param>
  /// <param name="questSequenceResolver">Resolves the live quest sequence.</param>
  /// <param name="todoSignatureResolver">Resolves live TODO progress state.</param>
  /// <returns>The stable accepted-quest signature.</returns>
  internal static string BuildAcceptedQuestSignature(
      IReadOnlyCollection<uint> acceptedQuestIds,
      Func<uint, byte> questSequenceResolver,
      Func<uint, string?> todoSignatureResolver)
  {
    if (acceptedQuestIds.Count == 0)
    {
      return string.Empty;
    }

    return string.Join(
        "|",
        acceptedQuestIds.Select(questId =>
        {
          var questSequence = questSequenceResolver(questId);
          var todoSignature = todoSignatureResolver(questId);
          return string.IsNullOrWhiteSpace(todoSignature)
              ? $"{questId}:{questSequence}"
              : $"{questId}:{questSequence}:{todoSignature}";
        }));
  }

  /// <summary>
  ///     Attempts to resolve live TODO state for an accepted quest.
  /// </summary>
  /// <param name="questId">The accepted quest id.</param>
  /// <returns>The live TODO signature, or null when unavailable.</returns>
  private static string? TryResolveAcceptedQuestTodoSignature(uint questId)
  {
    try
    {
      unsafe
      {
        return QuestTodoProgressResolver.TryResolveQuestTodoProgress(
            questId,
            out var snapshot)
            ? snapshot.CacheKey
            : null;
      }
    }
    catch (Exception exception)
    {
      PluginRuntimeLog.Debug(
          "[AcceptedQuestPrefetch] Could not resolve live TODO signature " +
          $"for quest '{questId}': {exception.Message}");
      return null;
    }
  }
}


