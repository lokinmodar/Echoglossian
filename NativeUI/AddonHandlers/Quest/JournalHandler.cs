// <copyright file="JournalHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Handles the Journal and JournalDetail quest addon runtime inside the
///     standalone quest-handler model.
/// </summary>
internal sealed class JournalHandler : QuestAddonHandlerBase
{
  private const string JournalAddonName = "Journal";

  private const string JournalDetailAddonName = "JournalDetail";

  private const string JournalListHoverPrefix = "JournalList-";

  private const string JournalDetailHoverPrefix = "JournalDetail-";

  private readonly Dictionary<string, string> journalListTextCache =
      new(StringComparer.Ordinal);

  private readonly Dictionary<nint, QuestHoverTranslationSnapshot> journalListHoverCache =
      [];

  private readonly Dictionary<string, string> journalDetailTextCache =
      new(StringComparer.Ordinal);

  private string currentJournalDetailScopeKey = string.Empty;

  /// <summary>
  ///     Initializes a new instance of the <see cref="JournalHandler" /> class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public JournalHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PreUpdate, this.OnJournalQuestEvent);
    this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.OnJournalQuestEvent);
    this.RegisterHandler(AddonEvent.PostRequestedUpdate, this.OnJournalDetailEvent);
    this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.OnJournalDetailEvent);
    this.RegisterHandler(AddonEvent.PreHide, this.OnJournalCleanupEvent);
    this.RegisterHandler(AddonEvent.PreFinalize, this.OnJournalCleanupEvent);
  }

  /// <summary>
  ///     Gets the active plugin configuration through the legacy Journal
  ///     member name used by the ported code.
  /// </summary>
  private Config configuration => this.Config;

  /// <summary>
  ///     Gets whether the Journal family should use hover tooltips.
  /// </summary>
  private bool JournalUsesHoverTooltips =>
      QuestAddonModeHelpers.UsesHoverTooltips(
          this.Config.JournalTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the Journal family should write translated text into the
  ///     native addon.
  /// </summary>
  private bool JournalWritesNativeTranslation =>
      QuestAddonModeHelpers.WritesNativeTranslation(
          this.Config.JournalTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the Journal family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool JournalHoverShowsOriginal =>
      QuestAddonModeHelpers.ShowsOriginalTooltips(
          this.Config.JournalTranslationDisplayMode);

  /// <summary>
  ///     Gets whether translated Journal text should be normalized before
  ///     being written into the native UI.
  /// </summary>
  private bool JournalShouldRemoveDiacritics =>
      QuestAddonModeHelpers.ShouldRemoveDiacritics(
          this.Config.JournalTranslationDisplayMode,
          this.Config.RemoveDiacriticsWhenUsingReplacementQuest);

  /// <summary>
  ///     Collects and resolves Journal summary nodes.
  /// </summary>
  /// <param name="journalBox">The Journal detail component.</param>
  /// <param name="foundQuestPlate">The quest plate currently resolved from the DB.</param>
  /// <param name="summaryText">The current summary text.</param>
  /// <returns>The summary entries to render and cache.</returns>
  private unsafe List<SummaryQuest> TranslateSummaries(
      AtkComponentBase* journalBox,
      QuestPlate foundQuestPlate,
      string summaryText)
  {
    List<SummaryQuest> summaries = [];
    if (summaryText == string.Empty)
    {
      return summaries;
    }

    for (var i = 0; i < journalBox->UldManager.NodeListCount; i++)
    {
      if (journalBox->UldManager.NodeList[i]->NodeId < 480700 ||
          journalBox->UldManager.NodeList[i]->NodeId > 480750)
      {
        continue;
      }

      if (!journalBox->UldManager.NodeList[i]->IsVisible())
      {
        continue;
      }

      var summaryItemNode =
          journalBox->UldManager.NodeList[i]->GetAsAtkComponentNode();
      var summaryNode =
          summaryItemNode->Component->UldManager.SearchNodeById(2);
      if (summaryNode == null || summaryNode->Type != NodeType.Text ||
          !summaryNode->IsVisible())
      {
        continue;
      }

      var summaryTextNode = summaryNode->GetAsAtkTextNode();
      if (summaryTextNode->NodeText.IsEmpty)
      {
        continue;
      }

      var originalText = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)summaryTextNode->NodeText.StringPtr.Value);
      if (QuestUiTranslationCache.TryGetAppliedSnapshot(
              originalText,
              out var appliedSummarySnapshot))
      {
        summaries.Add(
            new SummaryQuest(
                appliedSummarySnapshot.OriginalText,
                appliedSummarySnapshot.AppliedText,
                summaryTextNode,
                true));
        continue;
      }

      if (foundQuestPlate != null &&
          (foundQuestPlate.TranslatedSummaries.TryGetValue(
               originalText,
               out var storedSummaryText) ||
           foundQuestPlate.Summaries.TryGetValue(
               originalText,
               out storedSummaryText)))
      {
        summaries.Add(
            new SummaryQuest(
                originalText,
                storedSummaryText,
                summaryTextNode,
                false));
        QuestUiTranslationCache.Remember(
            originalText,
            storedSummaryText);
        continue;
      }

      if (foundQuestPlate != null)
      {
        var summaryCacheKey = $"JournalDetailSummary|{originalText}";
        if (this.TryGetQueuedTranslation(
                summaryCacheKey,
                out var cachedTranslatedText))
        {
          summaries.Add(
              new SummaryQuest(
                  originalText,
                  cachedTranslatedText,
                  summaryTextNode,
                  true));
          QuestUiTranslationCache.Remember(
              originalText,
              cachedTranslatedText);
          continue;
        }

        this.QueueTranslation(
            summaryCacheKey,
            () => this.Translate(originalText),
            translatedText =>
            {
              var questPlateToUpdate = foundQuestPlate.Clone();
              questPlateToUpdate.Summaries[originalText] = translatedText;
              questPlateToUpdate.TranslatedSummaries[originalText] =
                  translatedText;
              questPlateToUpdate.UpdatedDate = DateTime.Now;
              this.UpdateQuestPlate(questPlateToUpdate);
            });
      }

      summaries.Add(
          new SummaryQuest(
              originalText,
              originalText,
              summaryTextNode,
              false));
    }

    return summaries;
  }

  /// <summary>
  ///     Resolves quest progress step texts from the quest sheet so the
  ///     Journal body tooltip can follow the quest progression source instead
  ///     of depending only on the live UI text.
  /// </summary>
  /// <param name="foundQuestPlate">The quest plate currently resolved from the DB.</param>
  /// <param name="questProgressSnapshot">The Lumina-backed quest progress snapshot.</param>
  /// <returns>The translated quest progress step texts in display order.</returns>
  private List<string> TranslateQuestProgressSections(
      QuestPlate? foundQuestPlate,
      QuestProgressSnapshot questProgressSnapshot)
  {
    List<string> translatedQuestProgressSections = [];
    HashSet<string> seenQuestSteps = new(StringComparer.Ordinal);
    if (questProgressSnapshot.QuestSteps.Count == 0)
    {
      return translatedQuestProgressSections;
    }

    foreach (var questStep in questProgressSnapshot.QuestSteps)
    {
      if (string.IsNullOrWhiteSpace(questStep.Text))
      {
        continue;
      }

      if (!seenQuestSteps.Add(questStep.Text))
      {
        continue;
      }

      if (QuestUiTranslationCache.TryGetAppliedSnapshot(
              questStep.Text,
              out var appliedQuestStepSnapshot))
      {
        translatedQuestProgressSections.Add(
            appliedQuestStepSnapshot.AppliedText);
        continue;
      }

      if (foundQuestPlate != null &&
          foundQuestPlate.Objectives.TryGetValue(
              questStep.Text,
              out var storedQuestStepText))
      {
        translatedQuestProgressSections.Add(storedQuestStepText);
        QuestUiTranslationCache.Remember(
            questStep.Text,
            storedQuestStepText);
        continue;
      }

      var questProgressCacheKey =
          $"JournalDetailProgress|{questProgressSnapshot.CacheKey}|{questStep.KeyText}|{questStep.Text}";
      if (foundQuestPlate != null &&
          this.TryGetQueuedTranslation(
              questProgressCacheKey,
              out var cachedTranslatedQuestStepText))
      {
        translatedQuestProgressSections.Add(
            cachedTranslatedQuestStepText);
        QuestUiTranslationCache.Remember(
            questStep.Text,
            cachedTranslatedQuestStepText);
        continue;
      }

      if (foundQuestPlate != null)
      {
        this.QueueTranslation(
            questProgressCacheKey,
            () => this.Translate(questStep.Text),
            translatedQuestStepText =>
            {
              var questPlateToUpdate = foundQuestPlate.Clone();
              questPlateToUpdate.Objectives[questStep.Text] =
                  translatedQuestStepText;
              questPlateToUpdate.UpdatedDate = DateTime.Now;
              this.UpdateQuestPlate(questPlateToUpdate);
            });
      }

      translatedQuestProgressSections.Add(questStep.Text);
    }

    return translatedQuestProgressSections;
  }

  /// <summary>
  ///     Gets the quest sequence row text for the current quest phase so the
  ///     JournalDetail hover body can stay anchored to one sheet row instead of
  ///     aggregating multiple quest steps.
  /// </summary>
  /// <param name="questProgressSnapshot">The Lumina-backed quest progress snapshot.</param>
  /// <returns>The current quest sequence row text, or an empty string when unavailable.</returns>
  private static string GetCurrentQuestSequenceText(
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
      var questSequenceText =
          questProgressSnapshot.QuestSeqTexts[questSequenceIndex].Text;
      if (!string.IsNullOrWhiteSpace(questSequenceText))
      {
        return questSequenceText;
      }
    }

    foreach (var questSequenceEntry in questProgressSnapshot.QuestSeqTexts)
    {
      if (!string.IsNullOrWhiteSpace(questSequenceEntry.Text))
      {
        return questSequenceEntry.Text;
      }
    }

    return string.Empty;
  }

  /// <summary>
  ///     Gets the translated quest sequence text for the current quest phase.
  ///     The result is cached back into the quest plate so the same sequence row
  ///     does not keep re-translating while the addon repaints.
  /// </summary>
  /// <param name="foundQuestPlate">The quest plate currently resolved from the DB.</param>
  /// <param name="questProgressSnapshot">The Lumina-backed quest progress snapshot.</param>
  /// <returns>The translated quest sequence row text, or the source text if translation is not ready yet.</returns>
  private string TranslateCurrentQuestSequenceText(
      QuestPlate? foundQuestPlate,
      QuestProgressSnapshot questProgressSnapshot,
      string journalDetailScopeKey)
  {
    var currentQuestSequenceText = GetCurrentQuestSequenceText(questProgressSnapshot);
    if (string.IsNullOrWhiteSpace(currentQuestSequenceText))
    {
      return string.Empty;
    }

    if (this.TryGetJournalDetailCachedText(
            journalDetailScopeKey,
            currentQuestSequenceText,
            out var cachedQuestSequenceText))
    {
      return cachedQuestSequenceText;
    }

    if (foundQuestPlate != null &&
        (foundQuestPlate.TranslatedSummaries.TryGetValue(
             currentQuestSequenceText,
             out var storedQuestSequenceText) ||
         foundQuestPlate.Summaries.TryGetValue(
             currentQuestSequenceText,
             out storedQuestSequenceText)))
    {
      this.RememberJournalDetailCachedText(
          journalDetailScopeKey,
          currentQuestSequenceText,
          storedQuestSequenceText);
      return storedQuestSequenceText;
    }

    var questSequenceCacheKey =
        $"JournalDetailSequence|{questProgressSnapshot.CacheKey}|{currentQuestSequenceText}";
    if (foundQuestPlate != null &&
        this.TryGetQueuedTranslation(
            questSequenceCacheKey,
            out var cachedTranslatedQuestSequenceText))
    {
      this.RememberJournalDetailCachedText(
          journalDetailScopeKey,
          currentQuestSequenceText,
          cachedTranslatedQuestSequenceText);
      return cachedTranslatedQuestSequenceText;
    }

    if (foundQuestPlate != null)
    {
      this.QueueTranslation(
          questSequenceCacheKey,
          () => this.Translate(currentQuestSequenceText),
            translatedQuestSequenceText =>
            {
              var questPlateToUpdate = foundQuestPlate.Clone();
              questPlateToUpdate.Summaries[currentQuestSequenceText] =
                  translatedQuestSequenceText;
              questPlateToUpdate.TranslatedSummaries[currentQuestSequenceText] =
                  translatedQuestSequenceText;
              questPlateToUpdate.UpdatedDate = DateTime.Now;
              this.UpdateQuestPlate(questPlateToUpdate);
            });
    }

    return currentQuestSequenceText;
  }

  /// <summary>
  ///     Applies translations to the active Journal detail box.
  /// </summary>
  /// <param name="journalBox">The journal detail component.</param>
  /// <param name="foundQuestPlate">The quest plate currently resolved from the DB.</param>
  /// <param name="questProgressSnapshot">The Lumina-backed quest progress snapshot.</param>
  /// <param name="questName">The quest name.</param>
  /// <param name="questMessage">The quest message.</param>
  /// <param name="objectiveText">The objective text.</param>
  /// <param name="summaryText">The summary text.</param>
  /// <param name="questNameNode">The quest-name text node.</param>
  /// <param name="descriptionNode">The description text node.</param>
  /// <param name="objectiveNode">The objective text node.</param>
  /// <param name="summaryNode">The optional summary text node.</param>
  private unsafe void TranslateQuestOnJournalBox(
      AtkComponentBase* journalBox,
      QuestPlate foundQuestPlate,
      QuestProgressSnapshot? questProgressSnapshot,
      string questName,
      string questMessage,
      string objectiveText,
      string summaryText,
      AtkTextNode* questNameNode,
      AtkTextNode* descriptionNode,
      AtkTextNode* objectiveNode,
      AtkTextNode* summaryNode)
  {
    string translatedQuestName = questName;
    string translatedQuestMessage = questMessage;
    string translatedQuestObjective = objectiveText;
    var translatedQuestSummary = summaryText;
    var journalDetailScopeKey = BuildJournalDetailScopeKey(
        questProgressSnapshot,
        questName,
        questMessage);
    this.EnsureJournalDetailScope(journalDetailScopeKey);

    if (!this.JournalUsesHoverTooltips &&
        this.TryGetJournalDetailCachedText(
            journalDetailScopeKey,
            questName,
            out _) &&
        this.TryGetJournalDetailCachedText(
            journalDetailScopeKey,
            questMessage,
            out _) &&
        this.TryGetJournalDetailCachedText(
            journalDetailScopeKey,
            objectiveText,
            out _) &&
        (summaryText == string.Empty ||
         this.TryGetJournalDetailCachedText(
             journalDetailScopeKey,
             summaryText,
             out _)))
    {
      return;
    }

    // Do not aggregate the extra JournalDetail summary nodes into the
    // canonical quest body. Those nodes can retain stale visible text across
    // quest switches and contaminate the persisted row with summaries from a
    // different quest. For the stable body, use only the current description,
    // current objective, live summary node, and current SEQ row.
    List<SummaryQuest> summaries = [];

    if (foundQuestPlate != null)
    {
      if (!this.TryGetJournalDetailCachedText(
              journalDetailScopeKey,
              questName,
              out translatedQuestName))
      {
        translatedQuestName = foundQuestPlate.TranslatedQuestName;
      }

      if (this.TryGetJournalDetailCachedText(
              journalDetailScopeKey,
              questMessage,
              out translatedQuestMessage))
      {
      }
      else if (!string.IsNullOrWhiteSpace(foundQuestPlate.TranslatedQuestMessage))
      {
        translatedQuestMessage = foundQuestPlate.TranslatedQuestMessage;
      }
      else
      {
        var messageCacheKey = questProgressSnapshot.HasValue
            ? $"JournalDetailMessage|{questProgressSnapshot.Value.CacheKey}|{questMessage}"
            : $"JournalDetailMessage|{questMessage}";
        if (this.TryGetQueuedTranslation(
                messageCacheKey,
                out var cachedTranslatedQuestMessage))
        {
          translatedQuestMessage = cachedTranslatedQuestMessage;
        }
        else
        {
          this.QueueTranslation(
              messageCacheKey,
              () => this.Translate(questMessage),
              translatedMessage =>
              {
                var questPlateToUpdate = foundQuestPlate.Clone();
                questPlateToUpdate.TranslatedQuestMessage =
                    translatedMessage;
                questPlateToUpdate.UpdatedDate = DateTime.Now;
                this.UpdateQuestPlate(questPlateToUpdate);
              });
        }
      }

      var objectiveCacheKey = questProgressSnapshot.HasValue
          ? $"JournalDetailObjective|{questProgressSnapshot.Value.CacheKey}|{objectiveText}"
          : $"JournalDetailObjective|{objectiveText}";
      if (this.TryGetJournalDetailCachedText(
              journalDetailScopeKey,
              objectiveText,
              out translatedQuestObjective))
      {
      }
      else if (foundQuestPlate.TranslatedObjectives.TryGetValue(
                   objectiveText,
                   out var storedObjectiveText) ||
               foundQuestPlate.Objectives.TryGetValue(
                   objectiveText,
                   out storedObjectiveText))
      {
        translatedQuestObjective = storedObjectiveText;
      }
      else if (this.TryGetQueuedTranslation(
                   objectiveCacheKey,
                   out var cachedTranslatedObjective))
      {
        translatedQuestObjective = cachedTranslatedObjective;
      }
      else
      {
        this.QueueTranslation(
            objectiveCacheKey,
            () => this.Translate(objectiveText),
            translatedObjective =>
            {
              var questPlateToUpdate = foundQuestPlate.Clone();
              questPlateToUpdate.Objectives[objectiveText] =
                  translatedObjective;
              questPlateToUpdate.TranslatedObjectives[objectiveText] =
                  translatedObjective;
              questPlateToUpdate.UpdatedDate = DateTime.Now;
              this.UpdateQuestPlate(questPlateToUpdate);
            });
        translatedQuestObjective = objectiveText;
      }

      if (summaryText != string.Empty)
      {
        var summaryCacheKey = questProgressSnapshot.HasValue
            ? $"JournalDetailSummaryText|{questProgressSnapshot.Value.CacheKey}|{summaryText}"
            : $"JournalDetailSummaryText|{summaryText}";
        if (this.TryGetJournalDetailCachedText(
                journalDetailScopeKey,
                summaryText,
                out translatedQuestSummary))
        {
        }
        else if (foundQuestPlate.TranslatedSummaries.TryGetValue(
                     summaryText,
                     out var storedSummaryText) ||
                 foundQuestPlate.Summaries.TryGetValue(
                     summaryText,
                     out storedSummaryText))
        {
          translatedQuestSummary = storedSummaryText;
        }
        else if (this.TryGetQueuedTranslation(
                     summaryCacheKey,
                     out var cachedTranslatedSummary))
        {
          translatedQuestSummary = cachedTranslatedSummary;
        }
        else
        {
          this.QueueTranslation(
              summaryCacheKey,
              () => this.Translate(summaryText),
              translatedSummary =>
              {
                var questPlateToUpdate = foundQuestPlate.Clone();
                questPlateToUpdate.Summaries[summaryText] = translatedSummary;
                questPlateToUpdate.TranslatedSummaries[summaryText] =
                    translatedSummary;
                questPlateToUpdate.UpdatedDate = DateTime.Now;
                this.UpdateQuestPlate(questPlateToUpdate);
              });
          translatedQuestSummary = summaryText;
        }
      }
    }
    else
    {
      List<string> journalDetailBatchSources =
      [
        questName,
        questMessage,
        objectiveText,
      ];
      var currentQuestSequenceText = questProgressSnapshot.HasValue
          ? GetCurrentQuestSequenceText(questProgressSnapshot.Value)
          : string.Empty;
      var includesCurrentQuestSequenceText =
          !string.IsNullOrWhiteSpace(currentQuestSequenceText) &&
          !journalDetailBatchSources.Contains(
              currentQuestSequenceText,
              StringComparer.Ordinal);

      if (summaryText != string.Empty)
      {
        journalDetailBatchSources.Add(summaryText);
      }

      if (includesCurrentQuestSequenceText)
      {
        journalDetailBatchSources.Add(currentQuestSequenceText);
      }

      var cacheKey = questProgressSnapshot.HasValue
          ? $"JournalDetail|{questProgressSnapshot.Value.CacheKey}|{SerializeTranslationBatch(journalDetailBatchSources)}"
          : $"JournalDetail|{SerializeTranslationBatch(journalDetailBatchSources)}";

      if (this.TryGetQueuedTranslation(
              cacheKey,
              out var cachedTranslatedPayload) &&
          TryDeserializeTranslationBatch(
              cachedTranslatedPayload,
              out var cachedTranslatedTexts) &&
          cachedTranslatedTexts.Length == journalDetailBatchSources.Count)
      {
        translatedQuestName = cachedTranslatedTexts[0];
        translatedQuestMessage = cachedTranslatedTexts[1];
        translatedQuestObjective = cachedTranslatedTexts[2];
        var textIndex = 3;
        if (summaryText != string.Empty)
        {
          translatedQuestSummary = cachedTranslatedTexts[textIndex++];
        }

        QuestPlate translatedQuestPlate = new(
            questName,
            questMessage,
            ClientStateInterface.ClientLanguage.Humanize(),
            translatedQuestName,
            translatedQuestMessage,
            string.Empty,
            LangDict[LanguageInt].Code,
            this.configuration.ChosenTransEngine,
            DateTime.Now,
            DateTime.Now);
        this.ApplyQuestProgressMetadata(
            translatedQuestPlate,
            questProgressSnapshot);

        if (summaryText != string.Empty)
        {
          translatedQuestPlate.Summaries.Add(
              summaryText,
              translatedQuestSummary);
          translatedQuestPlate.TranslatedSummaries.Add(
              summaryText,
              translatedQuestSummary);
        }

        if (includesCurrentQuestSequenceText)
        {
          var translatedCurrentQuestSequenceText =
              cachedTranslatedTexts[textIndex++];
          translatedQuestPlate.Summaries[currentQuestSequenceText] =
              translatedCurrentQuestSequenceText;
          translatedQuestPlate.TranslatedSummaries[currentQuestSequenceText] =
              translatedCurrentQuestSequenceText;
        }

        translatedQuestPlate.Objectives.Add(
            objectiveText,
            translatedQuestObjective);
        translatedQuestPlate.TranslatedObjectives[objectiveText] =
            translatedQuestObjective;
        this.InsertQuestPlate(translatedQuestPlate);
      }
      else
      {
        this.QueueTranslationBatch(
            cacheKey,
            journalDetailBatchSources,
            translatedTexts =>
            {
              if (translatedTexts.Length != journalDetailBatchSources.Count)
              {
                return;
              }

              var translatedIndex = 0;
              var batchTranslatedQuestName = translatedTexts[translatedIndex++];
              var batchTranslatedQuestMessage =
                  translatedTexts[translatedIndex++];
              var batchTranslatedQuestObjective =
                  translatedTexts[translatedIndex++];
              var batchTranslatedQuestSummary = string.Empty;

              if (summaryText != string.Empty)
              {
                batchTranslatedQuestSummary = translatedTexts[translatedIndex++];
              }

              QuestPlate translatedQuestPlate = new(
                  questName,
                  questMessage,
                  ClientStateInterface.ClientLanguage.Humanize(),
                  batchTranslatedQuestName,
                  batchTranslatedQuestMessage,
                  string.Empty,
                  LangDict[LanguageInt].Code,
                  this.configuration.ChosenTransEngine,
                  DateTime.Now,
                  DateTime.Now);
              this.ApplyQuestProgressMetadata(
                  translatedQuestPlate,
                  questProgressSnapshot);

              if (summaryText != string.Empty)
              {
                translatedQuestPlate.Summaries.Add(
                    summaryText,
                    batchTranslatedQuestSummary);
                translatedQuestPlate.TranslatedSummaries.Add(
                    summaryText,
                    batchTranslatedQuestSummary);
              }

              translatedQuestPlate.Objectives.Add(
                  objectiveText,
                  batchTranslatedQuestObjective);
              translatedQuestPlate.TranslatedObjectives[objectiveText] =
                  batchTranslatedQuestObjective;

              if (includesCurrentQuestSequenceText)
              {
                var batchTranslatedCurrentQuestSequenceText =
                    translatedTexts[translatedIndex++];
                translatedQuestPlate.Summaries[currentQuestSequenceText] =
                    batchTranslatedCurrentQuestSequenceText;
                translatedQuestPlate.TranslatedSummaries[
                    currentQuestSequenceText] =
                    batchTranslatedCurrentQuestSequenceText;
              }

              this.InsertQuestPlate(translatedQuestPlate);
            });
      }
    }

    if (this.JournalShouldRemoveDiacritics)
    {
        translatedQuestName = this.NormalizeQuestText(
          translatedQuestName ?? string.Empty);
        translatedQuestMessage = this.NormalizeQuestText(
          translatedQuestMessage ?? string.Empty);
        translatedQuestObjective = this.NormalizeQuestText(
          translatedQuestObjective ?? string.Empty);
        translatedQuestSummary = this.NormalizeQuestText(
          translatedQuestSummary ?? string.Empty);

      foreach (var summary in summaries)
      {
        summary.TranslatedText = this.NormalizeQuestText(
          summary.TranslatedText ?? string.Empty);
      }
    }

    if (this.JournalWritesNativeTranslation)
    {
      questNameNode->SetText(translatedQuestName);
      descriptionNode->SetText(translatedQuestMessage);
      objectiveNode->SetText(translatedQuestObjective);
      if (summaryText != string.Empty && summaryNode != null)
      {
        summaryNode->SetText(translatedQuestSummary);
      }
    }

    this.RememberJournalDetailCachedText(
        journalDetailScopeKey,
        questName,
        translatedQuestName);
    this.RememberJournalDetailCachedText(
        journalDetailScopeKey,
        questMessage,
        translatedQuestMessage);
    this.RememberJournalDetailCachedText(
        journalDetailScopeKey,
        objectiveText,
        translatedQuestObjective);
    if (summaryText != string.Empty)
    {
      this.RememberJournalDetailCachedText(
          journalDetailScopeKey,
          summaryText,
          translatedQuestSummary);
    }

    if (this.JournalUsesHoverTooltips)
    {
      this.RegisterTranslatedHoverTooltip(
          $"JournalDetail-QuestName-{(nint)questNameNode:X}",
          questNameNode,
          questName,
          translatedQuestName,
          swapEnabled: this.JournalHoverShowsOriginal,
          forceEnabled: true,
          denseHitbox: true);

      var currentQuestSequenceText = questProgressSnapshot.HasValue
          ? GetCurrentQuestSequenceText(questProgressSnapshot.Value)
          : string.Empty;
      var originalQuestDescription = questMessage;
      var translatedQuestDescription = translatedQuestMessage;
      var translatedCurrentQuestSequenceText =
          !string.IsNullOrWhiteSpace(currentQuestSequenceText) &&
          questProgressSnapshot.HasValue
              ? this.TranslateCurrentQuestSequenceText(
                  foundQuestPlate,
                  questProgressSnapshot.Value,
                  journalDetailScopeKey)
              : string.Empty;
      var originalQuestSummaryBody = BuildQuestPlateSummarySection(
          currentQuestSequenceText,
          summaryText,
          summaries,
          useTranslatedText: false);
      var translatedQuestSummaryBody = BuildQuestPlateSummarySection(
          translatedCurrentQuestSequenceText,
          translatedQuestSummary,
          summaries,
          useTranslatedText: true);
      var originalQuestBody = BuildQuestPlateHoverBody(
          originalQuestDescription,
          objectiveText,
          originalQuestSummaryBody);
      var translatedQuestBody = BuildQuestPlateHoverBody(
          translatedQuestDescription,
          translatedQuestObjective,
          translatedQuestSummaryBody);

      if (!string.IsNullOrWhiteSpace(originalQuestBody) ||
          !string.IsNullOrWhiteSpace(translatedQuestBody))
      {
        var questCanvasNode = journalBox->UldManager.SearchNodeById(14);
        var questBodyHoverKey = questCanvasNode != null
            ? $"JournalDetail-QuestBody-{(nint)questCanvasNode:X}"
            : $"JournalDetail-QuestBody-{(nint)descriptionNode:X}";
        if (questCanvasNode != null &&
            this.TryGetQuestPlateHoverBounds(
                questCanvasNode,
                out var bodyTopLeft,
                out var bodyBottomRight))
        {
          ExpandQuestPlateHoverBoundsForTextNode(
              ref bodyTopLeft,
              ref bodyBottomRight,
              descriptionNode);
          ExpandQuestPlateHoverBoundsForTextNode(
              ref bodyTopLeft,
              ref bodyBottomRight,
              objectiveNode);
          ExpandQuestPlateHoverBoundsForTextNode(
              ref bodyTopLeft,
              ref bodyBottomRight,
              summaryNode);

          this.RegisterTranslatedHoverTooltip(
              questBodyHoverKey,
              bodyTopLeft,
              bodyBottomRight,
              originalQuestBody,
              translatedQuestBody,
              swapEnabled: this.JournalHoverShowsOriginal,
              forceEnabled: true);
        }
        else
        {
          var bodyLeft = descriptionNode->ScreenX;
          var bodyTop = descriptionNode->ScreenY;
          var bodyRight =
              bodyLeft + Math.Max(1f, descriptionNode->GetWidth());
          var bodyBottom =
              bodyTop + Math.Max(1f, descriptionNode->GetHeight());

          void ExpandBodyBounds(AtkTextNode* node)
          {
            if (node == null || !node->IsVisible())
            {
              return;
            }

            bodyLeft = Math.Min(bodyLeft, node->ScreenX);
            bodyTop = Math.Min(bodyTop, node->ScreenY);
            bodyRight = Math.Max(
                bodyRight,
                node->ScreenX + Math.Max(1f, node->GetWidth()));
            bodyBottom = Math.Max(
                bodyBottom,
                node->ScreenY + Math.Max(1f, node->GetHeight()));
          }

          ExpandBodyBounds(objectiveNode);
          if (summaryNode != null)
          {
            ExpandBodyBounds(summaryNode);
          }

          bodyLeft -= 28f;
          bodyTop -= 18f;
          bodyRight += 28f;
          bodyBottom += 22f;

          this.RegisterTranslatedHoverTooltip(
              questBodyHoverKey,
              new Vector2(bodyLeft, bodyTop),
              new Vector2(bodyRight, bodyBottom),
              originalQuestBody,
              translatedQuestBody,
              swapEnabled: this.JournalHoverShowsOriginal,
              forceEnabled: true);
        }
      }
    }
  }

  /// <summary>
  ///     Translates the active Journal detail addon.
  /// </summary>
  private unsafe void TranslateJournalDetail()
  {
    if (!this.configuration.TranslateJournal)
    {
      return;
    }

    var atkStage = AtkStage.Instance();
    var journalDetail =
        atkStage->RaptureAtkUnitManager->GetAddonByName("JournalDetail");
    if (journalDetail == null || !journalDetail->IsVisible)
    {
      return;
    }

    if (!this.TranslateJournalBox(journalDetail))
    {
      this.TranslateCompletedQuest(journalDetail);
    }
  }

  /// <summary>
  ///     Translates the active Journal quest list addon.
  /// </summary>
  private unsafe void TranslateJournalQuests()
  {
    if (!this.configuration.TranslateJournal)
    {
      return;
    }

    var atkStage = AtkStage.Instance();
    var journal =
        atkStage->RaptureAtkUnitManager->GetAddonByName("Journal");
    if (journal == null || !journal->IsVisible)
    {
      return;
    }

    try
    {
      var questListRoot = journal->GetNodeById(25);
      if (questListRoot == null || !questListRoot->IsVisible())
      {
        return;
      }

      var questListNode = questListRoot->GetAsAtkComponentNode()->Component;
      if (questListNode == null)
      {
        return;
      }

      HashSet<string> visibleJournalQuestNames = new(StringComparer.Ordinal);
      HashSet<nint> visibleJournalQuestNodeKeys = [];

      for (var i = 0; i < questListNode->UldManager.NodeListCount; i++)
      {
        if (!questListNode->UldManager.NodeList[i]->IsVisible() ||
            questListNode->UldManager.NodeList[i]->NodeId == 5)
        {
          continue;
        }

        if (questListNode->UldManager.NodeList[i]->Type ==
            NodeType.Collision ||
            questListNode->UldManager.NodeList[i]->Type == NodeType.Res)
        {
          continue;
        }

        var questItemNode =
            questListNode->UldManager.NodeList[i]->GetAsAtkComponentNode();
        var questNameNode =
            questItemNode->Component->UldManager.SearchNodeById(3);
        if (questNameNode == null || !questNameNode->IsVisible() ||
            questNameNode->Type != NodeType.Text)
        {
          continue;
        }

        var questName = questNameNode->GetAsAtkTextNode();
        if (questName->NodeText.IsEmpty)
        {
          continue;
        }

        var questNameText = MemoryHelper.ReadSeStringAsString(
            out _,
            (nint)questName->NodeText.StringPtr.Value);
        var questNameNodeKey = (nint)questNameNode;
        visibleJournalQuestNames.Add(questNameText);
        visibleJournalQuestNodeKeys.Add(questNameNodeKey);

        if (this.TryGetJournalListCachedText(
                questNameText,
                out var cachedTranslatedQuestName))
        {
          if (this.JournalWritesNativeTranslation)
          {
            questName->SetText(cachedTranslatedQuestName);
          }

          this.RememberJournalListHover(
              questNameNodeKey,
              questNameText,
              cachedTranslatedQuestName);
          if (this.JournalUsesHoverTooltips &&
              this.TryGetJournalListHover(
                  questNameNodeKey,
                  out var cachedHoverTranslation))
          {
            this.RegisterTranslatedHoverTooltip(
                $"JournalList-{questNameNodeKey:X}",
                questName,
                cachedHoverTranslation.OriginalText,
                cachedHoverTranslation.TranslatedText,
                swapEnabled: this.JournalHoverShowsOriginal,
                forceEnabled: true,
                denseHitbox: true);
          }

          continue;
        }

        var questPlate = this.CreateQuestPlate(
          questNameText,
          string.Empty,
          string.Empty);
        var foundQuestPlate = this.FindQuestPlateByName(questPlate);
        if (foundQuestPlate != null)
        {
          var translQuestName = foundQuestPlate.TranslatedQuestName;
          if (this.JournalShouldRemoveDiacritics)
          {
            translQuestName = this.NormalizeQuestText(
                foundQuestPlate.TranslatedQuestName ?? string.Empty);
          }

          if (this.JournalWritesNativeTranslation)
          {
            questName->SetText(translQuestName);
          }

          this.RememberJournalListHover(
              questNameNodeKey,
              questNameText,
              translQuestName);

          this.RememberJournalListCachedText(
              questNameText,
              translQuestName);

          if (this.JournalUsesHoverTooltips)
          {
            this.RegisterTranslatedHoverTooltip(
                $"JournalList-{questNameNodeKey:X}",
                questName,
                questNameText,
                translQuestName,
                swapEnabled: this.JournalHoverShowsOriginal,
                forceEnabled: true,
                denseHitbox: true);
          }
          continue;
        }

        var journalQuestCacheKey = $"Journal|{questNameText}";
        if (!this.TryGetQueuedTranslation(
                journalQuestCacheKey,
                out var translatedNameText))
        {
          if (this.JournalUsesHoverTooltips)
          {
            this.RegisterTranslatedHoverTooltip(
                $"JournalList-{questNameNodeKey:X}",
                questName,
                questNameText,
                questNameText,
                swapEnabled: this.JournalHoverShowsOriginal,
                forceEnabled: true,
                denseHitbox: true);
          }

          this.QueueTranslation(
              journalQuestCacheKey,
              () => this.Translate(questNameText),
              resolvedTranslatedNameText =>
              {
                var translatedQuestPlate = this.CreateTranslatedQuestPlate(
                    questNameText,
                    string.Empty,
                    resolvedTranslatedNameText,
                    string.Empty);

                this.InsertQuestPlate(translatedQuestPlate);
              });
          continue;
        }

        if (this.JournalShouldRemoveDiacritics)
        {
          translatedNameText = this.NormalizeQuestText(
              translatedNameText ?? string.Empty);
        }

        if (this.JournalWritesNativeTranslation)
        {
          questName->SetText(translatedNameText);
        }

        this.RememberJournalListHover(
            questNameNodeKey,
            questNameText,
            translatedNameText);
        this.RememberJournalListCachedText(
            questNameText,
            translatedNameText);
        if (this.JournalUsesHoverTooltips)
        {
          this.RegisterTranslatedHoverTooltip(
              $"JournalList-{questNameNodeKey:X}",
              questName,
              questNameText,
              translatedNameText,
              swapEnabled: this.JournalHoverShowsOriginal,
              forceEnabled: true,
          denseHitbox: true);
        }
      }

      this.TrimJournalListRuntimeState(
          visibleJournalQuestNames,
          visibleJournalQuestNodeKeys);
    }
    catch (Exception e)
    {
      PluginLog.Error($"Error: {e}");
    }
  }

  /// <summary>
  ///     Translates a completed Journal quest view.
  /// </summary>
  /// <param name="journalDetail">The journal detail addon.</param>
  private unsafe void TranslateCompletedQuest(AtkUnitBase* journalDetail)
  {
    try
    {
      var questNameNode = journalDetail->GetTextNodeById(38);
      if (questNameNode == null || questNameNode->NodeText.IsEmpty)
      {
        return;
      }

      if (!journalDetail->GetNodeById(46)->IsVisible())
      {
        return;
      }

      var description = journalDetail->GetNodeById(46);
      if (description == null || description->Type != NodeType.Text)
      {
        return;
      }

      var questName = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)questNameNode->NodeText.StringPtr.Value);
      var descriptionNode = description->GetAsAtkTextNode();
      var questMessage = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)descriptionNode->NodeText.StringPtr.Value);
      var questPlate = this.CreateQuestPlate(questName, questMessage, string.Empty);
      if (QuestProgressResolver.TryResolveQuestProgress(
              questPlate,
              out var resolvedCompletedSnapshot))
      {
        questPlate.SourceContentHash = resolvedCompletedSnapshot.ContentHash;
      }

      var foundQuestPlate = this.FindQuestPlate(questPlate);
      if (foundQuestPlate != null &&
          !string.Equals(
              foundQuestPlate.GameVersion,
              GetGameVersion(),
              StringComparison.Ordinal))
      {
        this.UpdateQuestPlateGameVersion(
            foundQuestPlate.Id,
            GetGameVersion());
      }

      string translatedQuestName = questName;
      string translatedQuestMessage = questMessage;
      var journalDetailScopeKey = BuildJournalDetailScopeKey(
          resolvedCompletedSnapshot,
          questName,
          questMessage);
      this.EnsureJournalDetailScope(journalDetailScopeKey);

      if (this.TryGetJournalDetailCachedText(
              journalDetailScopeKey,
              questName,
              out translatedQuestName) &&
          this.TryGetJournalDetailCachedText(
              journalDetailScopeKey,
              questMessage,
              out translatedQuestMessage))
      {
      }
      else if (foundQuestPlate != null)
      {
        translatedQuestName = foundQuestPlate.TranslatedQuestName;
        translatedQuestMessage = foundQuestPlate.TranslatedQuestMessage;
      }
      else
      {
        var questDetailCacheKey =
            $"JournalDetailCompleted|{questName}|{questMessage}";
        if (this.TryGetQueuedTranslation(
                questDetailCacheKey,
                out var cachedTranslatedPayload) &&
            TryDeserializeTranslationPair(
                cachedTranslatedPayload,
                out translatedQuestName,
                out translatedQuestMessage))
        {
        }
        else
        {
          this.QueueTranslation(
              questDetailCacheKey,
              () => SerializeTranslationPair(
                  this.Translate(questName),
                  this.Translate(questMessage)),
              translatedPayload =>
              {
                if (!TryDeserializeTranslationPair(
                        translatedPayload,
                        out var resolvedQuestName,
                        out var resolvedQuestMessage))
                {
                  return;
                }

                var translatedQuestPlate = this.CreateTranslatedQuestPlate(
                    questName,
                    questMessage,
                    resolvedQuestName,
                    resolvedQuestMessage);
                this.ApplyQuestProgressMetadata(
                    translatedQuestPlate,
                    resolvedCompletedSnapshot);
                this.InsertQuestPlate(translatedQuestPlate);
              });
      }
      }

      if (this.JournalShouldRemoveDiacritics)
      {
        translatedQuestName = this.NormalizeQuestText(
          translatedQuestName ?? string.Empty);
        translatedQuestMessage = this.NormalizeQuestText(
          translatedQuestMessage ?? string.Empty);
      }

      if (this.JournalWritesNativeTranslation)
      {
        questNameNode->SetText(translatedQuestName);
        descriptionNode->SetText(translatedQuestMessage);
      }

      this.RememberJournalDetailCachedText(
          journalDetailScopeKey,
          questName,
          translatedQuestName);
      this.RememberJournalDetailCachedText(
          journalDetailScopeKey,
          questMessage,
          translatedQuestMessage);

      if (this.JournalUsesHoverTooltips)
      {
        this.RegisterTranslatedHoverTooltip(
            $"JournalDetail-CompletedQuestName-{(nint)questNameNode:X}",
            questNameNode,
            questName,
            translatedQuestName,
            swapEnabled: this.JournalHoverShowsOriginal,
            forceEnabled: true,
            denseHitbox: true);
        this.RegisterTranslatedHoverTooltip(
            $"JournalDetail-CompletedQuestMessage-{(nint)descriptionNode:X}",
            descriptionNode,
            questMessage,
            translatedQuestMessage,
            swapEnabled: this.JournalHoverShowsOriginal,
            forceEnabled: true,
            denseHitbox: true);

        var completedQuestBodyHoverKey =
            $"JournalDetail-CompletedQuestBody-{(nint)descriptionNode:X}";
        var questCanvasNode = journalDetail->GetNodeById(14);
        PluginLog.Debug(
            $"[JournalDetail] completed body candidate key='{completedQuestBodyHoverKey}' mode={this.configuration.JournalTranslationDisplayMode} " +
            $"hover={this.JournalUsesHoverTooltips} native={this.JournalWritesNativeTranslation} swap={this.JournalHoverShowsOriginal} " +
            $"trigger={(questCanvasNode != null ? "JournalCanvasComponentNode14" : "descriptionFallback")}");
        if (this.TryGetQuestPlateHoverBounds(
                questCanvasNode,
                out var bodyTopLeft,
                out var bodyBottomRight))
        {
          this.RegisterTranslatedHoverTooltip(
              completedQuestBodyHoverKey,
              bodyTopLeft,
              bodyBottomRight,
              questMessage,
              translatedQuestMessage,
              swapEnabled: this.JournalHoverShowsOriginal,
              forceEnabled: true);
        }
        else
        {
          var bodyLeft = descriptionNode->ScreenX;
          var bodyTop = descriptionNode->ScreenY;
          var bodyRight =
              bodyLeft + Math.Max(1f, descriptionNode->GetWidth());
          var bodyBottom =
              bodyTop + Math.Max(1f, descriptionNode->GetHeight());
          bodyLeft -= 28f;
          bodyTop -= 18f;
          bodyRight += 28f;
          bodyBottom += 22f;

          this.RegisterTranslatedHoverTooltip(
              completedQuestBodyHoverKey,
              new Vector2(bodyLeft, bodyTop),
              new Vector2(bodyRight, bodyBottom),
              questMessage,
              translatedQuestMessage,
              swapEnabled: this.JournalHoverShowsOriginal,
              forceEnabled: true);
        }
      }
    }
    catch (Exception e)
    {
      PluginLog.Error($"Error in UiJournalHandler: {e}");
    }
  }

  /// <summary>
  ///     Translates the active Journal detail view.
  /// </summary>
  private unsafe bool TranslateJournalBox(AtkUnitBase* journalDetail)
  {
    try
    {
      var questNameNode = journalDetail->GetTextNodeById(38);
      if (questNameNode == null || questNameNode->NodeText.IsEmpty)
      {
        return false;
      }

      if (!journalDetail->GetNodeById(43)->IsVisible())
      {
        return false;
      }

      var journalBox = journalDetail->GetNodeById(43)->GetComponent();
      var description = journalBox->UldManager.SearchNodeById(8);
      if (description == null || description->Type != NodeType.Text)
      {
        return false;
      }

      var objectiveResNode =
          journalBox->UldManager.SearchNodeById(12)->GetComponent()->
              UldManager.SearchNodeById(3);
      if (objectiveResNode == null ||
          objectiveResNode->Type != NodeType.Text)
      {
        return true;
      }

      var summaryText = string.Empty;
      AtkTextNode* summaryNode = null;
      var summaryBox = journalBox->UldManager.SearchNodeById(52);
      if (summaryBox != null && summaryBox->IsVisible())
      {
        var summaryResNode =
            summaryBox->GetComponent()->UldManager.SearchNodeById(2);
        if (summaryResNode != null &&
            summaryResNode->Type == NodeType.Text)
        {
          summaryNode = summaryResNode->GetAsAtkTextNode();
          summaryText = MemoryHelper.ReadSeStringAsString(
              out _,
              (nint)summaryNode->NodeText.StringPtr.Value);
        }
      }

      var questName = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)questNameNode->NodeText.StringPtr.Value);
      var descriptionNode = description->GetAsAtkTextNode();
      var questMessage = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)descriptionNode->NodeText.StringPtr.Value);
      var objectiveNode = objectiveResNode->GetAsAtkTextNode();
      var objectiveText = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)objectiveNode->NodeText.StringPtr.Value);
      var questPlate = this.CreateQuestPlate(questName, questMessage, string.Empty);

      QuestProgressSnapshot? questProgressSnapshot = null;
      if (QuestProgressResolver.TryResolveQuestProgress(
              questPlate,
              out var resolvedQuestProgressSnapshot))
      {
        questProgressSnapshot = resolvedQuestProgressSnapshot;
        questPlate.SourceContentHash = resolvedQuestProgressSnapshot.ContentHash;
      }

      var foundQuestPlate = this.FindQuestPlate(questPlate);
      if (foundQuestPlate != null &&
          !string.Equals(
              foundQuestPlate.GameVersion,
              GetGameVersion(),
              StringComparison.Ordinal))
      {
        this.UpdateQuestPlateGameVersion(
            foundQuestPlate.Id,
            GetGameVersion());
      }

      this.EnsureQuestPlateMetadataPersisted(
          foundQuestPlate,
          questProgressSnapshot);

      this.TranslateQuestOnJournalBox(
          journalBox,
          foundQuestPlate,
          questProgressSnapshot,
          questName,
          questMessage,
          objectiveText,
          summaryText,
          questNameNode,
          descriptionNode,
          objectiveNode,
          summaryNode);
    }
    catch (Exception e)
    {
      PluginLog.Error($"Error in UIJournalHandler: {e}");
    }

    return true;
  }

  /// <summary>
  ///     Handles Journal detail refresh events.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnJournalDetailEvent(AddonEvent type, AddonArgs args)
  {
    if (type == AddonEvent.PreRequestedUpdate &&
        !string.Equals(args.AddonName, JournalDetailAddonName, StringComparison.Ordinal))
    {
      return;
    }

    if (type == AddonEvent.PostRequestedUpdate &&
        !string.Equals(args.AddonName, JournalAddonName, StringComparison.Ordinal))
    {
      return;
    }

    this.TranslateJournalDetail();
  }

  /// <summary>
  ///     Handles Journal quest-list refresh events.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnJournalQuestEvent(AddonEvent type, AddonArgs args)
  {
    if (!string.Equals(args.AddonName, JournalAddonName, StringComparison.Ordinal))
    {
      return;
    }

    this.TranslateJournalQuests();
  }

  /// <summary>
  ///     Clears quest hover registrations when Journal views close.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private void OnJournalCleanupEvent(AddonEvent type, AddonArgs args)
  {
    if (string.Equals(args.AddonName, JournalAddonName, StringComparison.Ordinal))
    {
      this.journalListTextCache.Clear();
      this.journalListHoverCache.Clear();
      this.journalDetailTextCache.Clear();
      this.currentJournalDetailScopeKey = string.Empty;
      this.RemoveHoverTooltipsByPrefix(JournalListHoverPrefix);
      this.RemoveHoverTooltipsByPrefix(JournalDetailHoverPrefix);
      return;
    }

    if (string.Equals(args.AddonName, JournalDetailAddonName, StringComparison.Ordinal))
    {
      this.journalDetailTextCache.Clear();
      this.currentJournalDetailScopeKey = string.Empty;
      this.RemoveHoverTooltipsByPrefix(JournalDetailHoverPrefix);
    }
  }

  /// <summary>
  ///     Builds the current JournalDetail cache scope key so each quest detail
  ///     view can keep its own local runtime state.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest progress snapshot, if any.</param>
  /// <param name="questName">The current quest name.</param>
  /// <param name="questMessage">The current quest message.</param>
  /// <returns>A stable scope key for the current quest detail body.</returns>
  private static string BuildJournalDetailScopeKey(
      QuestProgressSnapshot? questProgressSnapshot,
      string questName,
      string questMessage)
  {
    return questProgressSnapshot?.CacheKey ??
           $"{questName}|{questMessage}";
  }

  /// <summary>
  ///     Ensures the JournalDetail runtime cache is scoped to the currently
  ///     visible quest only.
  /// </summary>
  /// <param name="scopeKey">The quest-detail scope key.</param>
  private void EnsureJournalDetailScope(string scopeKey)
  {
    if (string.Equals(
            this.currentJournalDetailScopeKey,
            scopeKey,
            StringComparison.Ordinal))
    {
      return;
    }

    this.currentJournalDetailScopeKey = scopeKey;
    this.journalDetailTextCache.Clear();
    this.RemoveHoverTooltipsByPrefix(JournalDetailHoverPrefix);
  }

  /// <summary>
  ///     Attempts to get translated JournalDetail text from the local
  ///     quest-scoped runtime cache.
  /// </summary>
  /// <param name="scopeKey">The current quest-detail scope key.</param>
  /// <param name="originalText">The source text visible for that scope.</param>
  /// <param name="translatedText">The cached translated text.</param>
  /// <returns>True when the scoped runtime cache already has the text.</returns>
  private bool TryGetJournalDetailCachedText(
      string scopeKey,
      string originalText,
      out string translatedText)
  {
    translatedText = string.Empty;
    if (string.IsNullOrWhiteSpace(scopeKey) ||
        string.IsNullOrWhiteSpace(originalText))
    {
      return false;
    }

    return this.journalDetailTextCache.TryGetValue(
        $"{scopeKey}|{originalText}",
        out translatedText);
  }

  /// <summary>
  ///     Remembers translated JournalDetail text inside the local quest-scoped
  ///     runtime cache.
  /// </summary>
  /// <param name="scopeKey">The current quest-detail scope key.</param>
  /// <param name="originalText">The source text visible for that scope.</param>
  /// <param name="translatedText">The translated text resolved for that scope.</param>
  private void RememberJournalDetailCachedText(
      string scopeKey,
      string originalText,
      string translatedText)
  {
    if (string.IsNullOrWhiteSpace(scopeKey) ||
        string.IsNullOrWhiteSpace(originalText) ||
        string.IsNullOrWhiteSpace(translatedText))
    {
      return;
    }

    this.journalDetailTextCache[$"{scopeKey}|{originalText}"] =
        translatedText;
  }

  /// <summary>
  ///     Attempts to get translated text for a visible Journal quest-list
  ///     entry from the current list runtime cache.
  /// </summary>
  /// <param name="originalText">The original quest name.</param>
  /// <param name="translatedText">The cached translated quest name.</param>
  /// <returns>True when the current visible Journal list already cached the quest.</returns>
  private bool TryGetJournalListCachedText(
      string originalText,
      out string translatedText)
  {
    translatedText = string.Empty;
    return !string.IsNullOrWhiteSpace(originalText) &&
           this.journalListTextCache.TryGetValue(
               originalText,
               out translatedText);
  }

  /// <summary>
  ///     Remembers a translated quest name inside the current Journal visible
  ///     list runtime cache.
  /// </summary>
  /// <param name="originalText">The original quest name.</param>
  /// <param name="translatedText">The translated quest name.</param>
  private void RememberJournalListCachedText(
      string originalText,
      string translatedText)
  {
    if (string.IsNullOrWhiteSpace(originalText) ||
        string.IsNullOrWhiteSpace(translatedText))
    {
      return;
    }

    this.journalListTextCache[originalText] = translatedText;
  }

  /// <summary>
  ///     Attempts to get a local hover snapshot for a visible Journal quest
  ///     list node.
  /// </summary>
  /// <param name="nodeKey">The live quest-name node key.</param>
  /// <param name="snapshot">The cached local hover snapshot.</param>
  /// <returns>True when a local hover snapshot exists for that node.</returns>
  private bool TryGetJournalListHover(
      nint nodeKey,
      out QuestHoverTranslationSnapshot snapshot)
  {
    return this.journalListHoverCache.TryGetValue(
        nodeKey,
        out snapshot!);
  }

  /// <summary>
  ///     Remembers the hover translation pair for a visible Journal quest list
  ///     node.
  /// </summary>
  /// <param name="nodeKey">The live quest-name node key.</param>
  /// <param name="originalText">The original visible quest name.</param>
  /// <param name="translatedText">The translated visible quest name.</param>
  private void RememberJournalListHover(
      nint nodeKey,
      string originalText,
      string translatedText)
  {
    if (nodeKey == nint.Zero ||
        string.IsNullOrWhiteSpace(originalText) ||
        string.IsNullOrWhiteSpace(translatedText))
    {
      return;
    }

    this.journalListHoverCache[nodeKey] =
        new QuestHoverTranslationSnapshot(
            originalText,
            translatedText);
  }

  /// <summary>
  ///     Trims Journal quest-list runtime caches so they only keep the quest
  ///     names and node anchors visible in the current list snapshot.
  /// </summary>
  /// <param name="visibleQuestNames">The currently visible quest names.</param>
  /// <param name="visibleQuestNodeKeys">The currently visible quest node keys.</param>
  private void TrimJournalListRuntimeState(
      HashSet<string> visibleQuestNames,
      HashSet<nint> visibleQuestNodeKeys)
  {
    List<string> hiddenQuestNames = [];
    foreach (var cachedQuestName in this.journalListTextCache.Keys)
    {
      if (!visibleQuestNames.Contains(cachedQuestName))
      {
        hiddenQuestNames.Add(cachedQuestName);
      }
    }

    foreach (var hiddenQuestName in hiddenQuestNames)
    {
      this.journalListTextCache.Remove(hiddenQuestName);
    }

    List<nint> hiddenQuestNodeKeys = [];
    foreach (var cachedQuestNodeKey in this.journalListHoverCache.Keys)
    {
      if (!visibleQuestNodeKeys.Contains(cachedQuestNodeKey))
      {
        hiddenQuestNodeKeys.Add(cachedQuestNodeKey);
      }
    }

    foreach (var hiddenQuestNodeKey in hiddenQuestNodeKeys)
    {
      this.journalListHoverCache.Remove(hiddenQuestNodeKey);
    }
  }

  /// <summary>
  ///     Applies canonical quest metadata resolved from the current quest
  ///     progress snapshot so persisted JournalDetail rows stay aligned with
  ///     the sheet-first quest model.
  /// </summary>
  /// <param name="questPlate">The quest plate being materialized.</param>
  /// <param name="questProgressSnapshot">The resolved quest progress snapshot, if any.</param>
  private void ApplyQuestProgressMetadata(
      QuestPlate questPlate,
      QuestProgressSnapshot? questProgressSnapshot)
  {
    if (questPlate == null)
    {
      return;
    }

    questPlate.GameVersion ??= GetGameVersion();
    if (!questProgressSnapshot.HasValue)
    {
      return;
    }

    questPlate.QuestId = questProgressSnapshot.Value.QuestId.ToString();
    questPlate.QuestTextSheetName = questProgressSnapshot.Value.QuestSheetName;
    questPlate.SourceContentHash = questProgressSnapshot.Value.ContentHash;
  }

  /// <summary>
  ///     Persists canonical quest metadata into an existing JournalDetail row
  ///     when the row was created before the sheet-first fields were populated.
  /// </summary>
  /// <param name="questPlate">The quest plate currently loaded from the DB.</param>
  /// <param name="questProgressSnapshot">The resolved quest progress snapshot, if any.</param>
  private void EnsureQuestPlateMetadataPersisted(
      QuestPlate? questPlate,
      QuestProgressSnapshot? questProgressSnapshot)
  {
    if (questPlate == null || !questProgressSnapshot.HasValue)
    {
      return;
    }

    var expectedQuestId = questProgressSnapshot.Value.QuestId.ToString();
    var expectedSheetName = questProgressSnapshot.Value.QuestSheetName;
    var expectedContentHash = questProgressSnapshot.Value.ContentHash;
    if (string.Equals(
            questPlate.QuestId,
            expectedQuestId,
            StringComparison.Ordinal) &&
        string.Equals(
            questPlate.QuestTextSheetName,
            expectedSheetName,
            StringComparison.Ordinal) &&
        string.Equals(
            questPlate.SourceContentHash,
            expectedContentHash,
            StringComparison.Ordinal))
    {
      return;
    }

    var questPlateToUpdate = questPlate.Clone();
    this.ApplyQuestProgressMetadata(
        questPlateToUpdate,
        questProgressSnapshot);
    questPlateToUpdate.UpdatedDate = DateTime.Now;
    this.UpdateQuestPlate(questPlateToUpdate);

    this.ApplyQuestProgressMetadata(
        questPlate,
        questProgressSnapshot);
  }

  /// <summary>
  ///     Builds a single multi-paragraph tooltip body from the quest plate text
  ///     sections that are currently visible.
  /// </summary>
  /// <param name="sections">Quest plate text sections to join.</param>
  /// <returns>A multi-paragraph tooltip body.</returns>
  private static string BuildQuestPlateHoverBody(params string?[] sections)
  {
    List<string> lines = [];
    HashSet<string> seenSections = new(StringComparer.Ordinal);
    foreach (var section in sections)
    {
      if (string.IsNullOrWhiteSpace(section))
      {
        continue;
      }

      var normalizedSection = section.Trim();
      if (!seenSections.Add(normalizedSection))
      {
        continue;
      }

      lines.Add(normalizedSection);
    }

    return string.Join(Environment.NewLine + Environment.NewLine, lines);
  }

  /// <summary>
  ///     Builds the visible JournalDetail summary block from the currently
  ///     visible summary text and summary nodes.
  /// </summary>
  /// <param name="primarySummaryText">The main summary text currently shown in the detail pane.</param>
  /// <param name="summaries">The visible summary nodes collected for the detail pane.</param>
  /// <param name="useTranslatedText">Whether to use translated or original summary entries.</param>
  /// <returns>A deduplicated summary block for the tooltip body.</returns>
  private static string BuildQuestPlateSummarySection(
      string? currentQuestSequenceText,
      string? primarySummaryText,
      IReadOnlyCollection<SummaryQuest> summaries,
      bool useTranslatedText)
  {
    List<string> sections = [];
    HashSet<string> seenSections = new(StringComparer.Ordinal);

    void AddSection(string? text)
    {
      if (string.IsNullOrWhiteSpace(text))
      {
        return;
      }

      var normalizedSection = text.Trim();
      if (!seenSections.Add(normalizedSection))
      {
        return;
      }

      sections.Add(normalizedSection);
    }

    AddSection(currentQuestSequenceText);
    AddSection(primarySummaryText);
    foreach (var summary in summaries)
    {
      AddSection(useTranslatedText ? summary.TranslatedText : summary.OriginalText);
    }

    return string.Join(Environment.NewLine, sections);
  }

  /// <summary>
  ///     Expands an existing quest-body hover rectangle to include a visible
  ///     text node and some practical padding around it.
  /// </summary>
  /// <param name="topLeft">The current top-left coordinate.</param>
  /// <param name="bottomRight">The current bottom-right coordinate.</param>
  /// <param name="textNode">The text node to include.</param>
  private static unsafe void ExpandQuestPlateHoverBoundsForTextNode(
      ref Vector2 topLeft,
      ref Vector2 bottomRight,
      AtkTextNode* textNode)
  {
    if (textNode == null || !textNode->IsVisible())
    {
      return;
    }

    topLeft = new Vector2(
        Math.Max(0f, Math.Min(topLeft.X, textNode->ScreenX - 20f)),
        Math.Max(0f, Math.Min(topLeft.Y, textNode->ScreenY - 12f)));
    bottomRight = new Vector2(
        Math.Max(
            bottomRight.X,
            textNode->ScreenX + Math.Max(1f, textNode->GetWidth()) + 20f),
        Math.Max(
            bottomRight.Y,
            textNode->ScreenY + Math.Max(1f, textNode->GetHeight()) + 16f));
  }

  /// <summary>
  ///     Gets the bounds of the JournalCanvasComponentNode used as the quest
  ///     plate hover trigger.
  /// </summary>
  /// <param name="questCanvasNode">The journal detail component.</param>
  /// <param name="topLeft">The top-left screen coordinate of the node.</param>
  /// <param name="bottomRight">The bottom-right screen coordinate of the node.</param>
  /// <returns>True when the node is visible and the bounds are usable.</returns>
  private unsafe bool TryGetQuestPlateHoverBounds(
      AtkResNode* questCanvasNode,
      out Vector2 topLeft,
      out Vector2 bottomRight)
  {
    topLeft = default;
    bottomRight = default;

    if (questCanvasNode == null || !questCanvasNode->IsVisible())
    {
      return false;
    }

    const float hoverPaddingX = 48f;
    const float hoverPaddingY = 28f;

    topLeft = new Vector2(
        Math.Max(0f, questCanvasNode->ScreenX - hoverPaddingX),
        Math.Max(0f, questCanvasNode->ScreenY - hoverPaddingY));
    bottomRight = new Vector2(
        questCanvasNode->ScreenX +
            Math.Max(1f, questCanvasNode->Width) +
            hoverPaddingX,
        questCanvasNode->ScreenY +
            Math.Max(1f, questCanvasNode->Height) +
            hoverPaddingY);

    return true;
  }
}
