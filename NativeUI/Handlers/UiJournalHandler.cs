// <copyright file="UiJournalHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;

namespace Echoglossian;

public partial class Echoglossian
{
    // keeps journal quest hover translations alive across refreshes
    private unsafe List<SummaryQuest> TranslateSummaries(
        AtkComponentBase* journalBox,
        QuestPlate foundQuestPlate,
        string summaryText)
    {
        List<SummaryQuest> summaries = new();
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
                foundQuestPlate.Summaries.TryGetValue(
                    originalText,
                    out var storedSummaryText))
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
                        questPlateToUpdate.Summaries[originalText] =
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
        List<string> translatedQuestProgressSections = new();
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

        if (!this.JournalUsesHoverTooltips &&
            QuestUiTranslationCache.TryGetAppliedSnapshot(
                questName,
                out _) &&
            QuestUiTranslationCache.TryGetAppliedSnapshot(
                questMessage,
                out _) &&
            QuestUiTranslationCache.TryGetAppliedSnapshot(
                objectiveText,
                out _) &&
            (summaryText == string.Empty ||
             QuestUiTranslationCache.TryGetAppliedSnapshot(
                 summaryText,
                 out _)))
        {
            return;
        }

        var summaries = this.TranslateSummaries(
            journalBox,
            foundQuestPlate,
            summaryText);

        if (foundQuestPlate != null)
        {
            translatedQuestName = foundQuestPlate.TranslatedQuestName;
            translatedQuestMessage = foundQuestPlate.TranslatedQuestMessage;

            var objectiveCacheKey = questProgressSnapshot.HasValue
                ? $"JournalDetailObjective|{questProgressSnapshot.Value.CacheKey}|{objectiveText}"
                : $"JournalDetailObjective|{objectiveText}";
            if (foundQuestPlate.Objectives.TryGetValue(
                    objectiveText,
                    out var storedObjectiveText))
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
            if (foundQuestPlate.Summaries.TryGetValue(
                        summaryText,
                        out var storedSummaryText))
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
                            questPlateToUpdate.Summaries[summaryText] =
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

            if (summaryText != string.Empty)
            {
                journalDetailBatchSources.Add(summaryText);
            }

            journalDetailBatchSources.AddRange(
                summaries.Select(summary => summary.OriginalText));

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

                for (var i = 0; i < summaries.Count; i++)
                {
                    summaries[i].TranslatedText = cachedTranslatedTexts[textIndex++];
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

                if (summaryText != string.Empty)
                {
                    translatedQuestPlate.Summaries.Add(
                        summaryText,
                        translatedQuestSummary);
                }

                foreach (var summary in summaries)
                {
                    translatedQuestPlate.Summaries.Add(
                        summary.OriginalText,
                        summary.TranslatedText);
                }

                translatedQuestPlate.Objectives.Add(
                    objectiveText,
                    translatedQuestObjective);
                var result = this.InsertQuestPlate(translatedQuestPlate);
            }
            else
            {
                this.QueueTranslationBatch(
                    cacheKey,
                    journalDetailBatchSources,
                    translatedTexts =>
                    {
                        if (translatedTexts.Length !=
                            journalDetailBatchSources.Count)
                        {
                            return;
                        }

                        var translatedIndex = 0;
                        var batchTranslatedQuestName =
                            translatedTexts[translatedIndex++];
                        var batchTranslatedQuestMessage =
                            translatedTexts[translatedIndex++];
                        var batchTranslatedQuestObjective =
                            translatedTexts[translatedIndex++];
                        var batchTranslatedQuestSummary =
                            string.Empty;

                        if (summaryText != string.Empty)
                        {
                            batchTranslatedQuestSummary =
                                translatedTexts[translatedIndex++];
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

                        if (summaryText != string.Empty)
                        {
                            translatedQuestPlate.Summaries.Add(
                                summaryText,
                                batchTranslatedQuestSummary);
                        }

                        translatedQuestPlate.Objectives.Add(
                            objectiveText,
                            batchTranslatedQuestObjective);

                        foreach (var summary in summaries)
                        {
                            translatedQuestPlate.Summaries.Add(
                                summary.OriginalText,
                                translatedTexts[translatedIndex++]);
                        }

                        var result = this.InsertQuestPlate(
                            translatedQuestPlate);
                    });
                return;
            }
        }

        if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
        {
            translatedQuestName = this.RemoveDiacritics(
                translatedQuestName,
                this.SpecialCharsSupportedByGameFont);
            translatedQuestMessage = this.RemoveDiacritics(
                translatedQuestMessage,
                this.SpecialCharsSupportedByGameFont);
            translatedQuestObjective = this.RemoveDiacritics(
                translatedQuestObjective,
                this.SpecialCharsSupportedByGameFont);
            translatedQuestSummary = this.RemoveDiacritics(
                translatedQuestSummary,
                this.SpecialCharsSupportedByGameFont);

            foreach (var summary in summaries)
            {
                summary.TranslatedText = this.RemoveDiacritics(
                    summary.TranslatedText,
                    this.SpecialCharsSupportedByGameFont);
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

        QuestUiTranslationCache.Remember(questName, translatedQuestName);
        QuestUiTranslationCache.Remember(questMessage, translatedQuestMessage);
        QuestUiTranslationCache.Remember(
            objectiveText,
            translatedQuestObjective);
        if (summaryText != string.Empty)
        {
            QuestUiTranslationCache.Remember(
                summaryText,
                translatedQuestSummary);
        }

        foreach (var summary in summaries)
        {
            if (this.JournalWritesNativeTranslation)
            {
                summary.Node->SetText(summary.TranslatedText);
            }
            QuestUiTranslationCache.Remember(
                summary.OriginalText,
                summary.TranslatedText);
        }

        if (this.JournalUsesHoverTooltips)
        {
            this.RegisterTranslatedHoverTooltip(
                $"JournalDetail-QuestName-{(nint)questNameNode:X}",
                questNameNode,
                questName,
                translatedQuestName,
                swapEnabled: this.JournalHoverShowsOriginal,
                forceEnabled: true, denseHitbox: true);

            var originalQuestProgressSections = questProgressSnapshot.HasValue
                ? questProgressSnapshot.Value.QuestSteps
                    .Select(step => step.Text)
                    .Where(stepText => !string.IsNullOrWhiteSpace(stepText))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
                : [];
            var translatedQuestProgressSections = questProgressSnapshot.HasValue
                ? this.TranslateQuestProgressSections(
                    foundQuestPlate,
                    questProgressSnapshot.Value)
                : [];
            var originalQuestBody = BuildQuestPlateHoverBody(
                new[]
                {
                    questMessage,
                    objectiveText,
                    summaryText,
                }
                    .Concat(summaries.Select(summary => summary.OriginalText))
                    .Concat(originalQuestProgressSections)
                    .ToArray());
            var translatedQuestBody = BuildQuestPlateHoverBody(
                new[]
                {
                    translatedQuestMessage,
                    translatedQuestObjective,
                    translatedQuestSummary,
                }
                    .Concat(summaries.Select(summary => summary.TranslatedText))
                    .Concat(translatedQuestProgressSections)
                    .ToArray());

            if (!string.IsNullOrWhiteSpace(originalQuestBody) ||
                !string.IsNullOrWhiteSpace(translatedQuestBody))
            {
                var questCanvasNode =
                    journalBox->UldManager.SearchNodeById(14);
                var questBodyHoverKey = questCanvasNode != null
                    ? $"JournalDetail-QuestBody-{(nint)questCanvasNode:X}-{questProgressSnapshot?.CacheKey ?? questName}"
                    : $"JournalDetail-QuestBody-{(nint)descriptionNode:X}-{questProgressSnapshot?.CacheKey ?? questName}";
                PluginLog.Debug(
                    $"[JournalDetail] body candidate key='{questBodyHoverKey}' mode={this.configuration.JournalTranslationDisplayMode} " +
                    $"hover={this.JournalUsesHoverTooltips} native={this.JournalWritesNativeTranslation} swap={this.JournalHoverShowsOriginal} " +
                    $"summaryCount={summaries.Count} progressSections={translatedQuestProgressSections.Count} " +
                    $"trigger={(questCanvasNode != null ? "JournalCanvasComponentNode14" : "descriptionFallback")}");
                if (this.TryGetQuestPlateHoverBounds(
                        questCanvasNode,
                        out var bodyTopLeft,
                        out var bodyBottomRight))
                {
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

                    foreach (var summary in summaries)
                    {
                        ExpandBodyBounds(summary.Node);
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
            var questPlate = this.FormatQuestPlate(questName, questMessage);
            // TODO: Journal lookups still depend on exact raw text matching, so
            // sub-entries like objectives and summaries can trigger their own
            // requests even when the main quest row already exists in the DB.
            // If that stays noisy, move this to a normalized or quest-identity
            // keyed cache instead of raw string equality.
            var foundQuestPlate = this.FindQuestPlate(questPlate);
            QuestProgressSnapshot? questProgressSnapshot = null;
            if (QuestProgressResolver.TryResolveQuestProgress(
                    foundQuestPlate ?? questPlate,
                    out var resolvedQuestProgressSnapshot))
            {
                questProgressSnapshot = resolvedQuestProgressSnapshot;
            }

            PluginLog.Debug(
                $"[JournalDetail] scan mode={this.configuration.JournalTranslationDisplayMode} " +
                $"hover={this.JournalUsesHoverTooltips} native={this.JournalWritesNativeTranslation} swap={this.JournalHoverShowsOriginal} " +
                $"summaryPresent={!string.IsNullOrWhiteSpace(summaryText)} progressKey='{questProgressSnapshot?.CacheKey ?? string.Empty}'");

#if DEBUG
            // PluginLog.Debug($"Quest name: {questName}");
            // PluginLog.Debug($"Quest message: {questMessage}");
            // PluginLog.Debug($"Objective text: {objectiveText}");
            // PluginLog.Debug($"Summary text: {summaryText}");
#endif
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
            var questPlate = this.FormatQuestPlate(questName, questMessage);
            var foundQuestPlate = this.FindQuestPlate(questPlate);
#if DEBUG
            // PluginLog.Debug($"Quest name: {questName}");
            // PluginLog.Debug($"Quest message: {questMessage}");
#endif

            string translatedQuestName;
            string translatedQuestMessage;

            if (foundQuestPlate != null)
            {
                translatedQuestName = foundQuestPlate.TranslatedQuestName;
                translatedQuestMessage = foundQuestPlate.TranslatedQuestMessage;
#if DEBUG
                // PluginLog.Debug(
                //     $"From database - Name: {foundQuestPlate.TranslatedQuestName}, Message: {foundQuestPlate.TranslatedQuestMessage}");
#endif
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
#if DEBUG
                    // PluginLog.Debug(
                    //     $"Translated quest name: {translatedQuestName}");
                    // PluginLog.Debug(
                    //     $"Translated quest message: {translatedQuestMessage}");
#endif
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

                            QuestPlate translatedQuestPlate = new(
                                questName,
                                questMessage,
                                ClientStateInterface.ClientLanguage.Humanize(),
                                resolvedQuestName,
                                resolvedQuestMessage,
                                string.Empty,
                                LangDict[LanguageInt].Code,
                                this.configuration.ChosenTransEngine,
                                DateTime.Now,
                                DateTime.Now);
                            var result = this.InsertQuestPlate(
                                translatedQuestPlate);
#if DEBUG
                            // PluginLog.Debug(
                            //     $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
                        });

                    return;
                }
            }

            if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
            {
                translatedQuestName = this.RemoveDiacritics(
                    translatedQuestName,
                    this.SpecialCharsSupportedByGameFont);
                translatedQuestMessage = this.RemoveDiacritics(
                    translatedQuestMessage,
                    this.SpecialCharsSupportedByGameFont);
            }

            if (this.JournalWritesNativeTranslation)
            {
                questNameNode->SetText(translatedQuestName);
                descriptionNode->SetText(translatedQuestMessage);
            }

            if (this.JournalUsesHoverTooltips)
            {
                this.RegisterTranslatedHoverTooltip(
                    $"JournalDetail-CompletedQuestName-{(nint)questNameNode:X}",
                    questNameNode,
                    questName,
                    translatedQuestName,
                    swapEnabled: this.JournalHoverShowsOriginal,
                    forceEnabled: true, denseHitbox: true);
                this.RegisterTranslatedHoverTooltip(
                    $"JournalDetail-CompletedQuestMessage-{(nint)descriptionNode:X}",
                    descriptionNode,
                    questMessage,
                    translatedQuestMessage,
                    swapEnabled: this.JournalHoverShowsOriginal,
                    forceEnabled: true, denseHitbox: true);

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
#if DEBUG
        // PluginLog.Debug(
        //     $"Language: {ClientStateInterface.ClientLanguage.Humanize()}");
        // PluginLog.Debug("Translate JournalDetail");
#endif

        if (!this.TranslateJournalBox(journalDetail))
        {
            this.TranslateCompletedQuest(journalDetail);
        }
    }

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

#if DEBUG
        // PluginLog.Debug(
        //     $"Language: {ClientStateInterface.ClientLanguage.Humanize()}");
        // PluginLog.Debug("Translate JournalQuests");
#endif
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
                    questListNode->UldManager
                        .NodeList[i]->GetAsAtkComponentNode();
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
                if (QuestUiTranslationCache.TryGetAppliedSnapshot(
                        questNameText,
                        out var translatedQuestSnapshot))
                {
                    if (this.JournalUsesHoverTooltips &&
                        QuestHoverTranslationCache.TryGet(
                            questNameNodeKey,
                            out var cachedHoverTranslation))
                    {
                        this.RegisterTranslatedHoverTooltip(
                            $"JournalList-{questNameNodeKey:X}",
                            questName,
                            cachedHoverTranslation.OriginalText,
                            cachedHoverTranslation.TranslatedText,
                            swapEnabled: this.JournalHoverShowsOriginal,
                            forceEnabled: true, denseHitbox: true);
                    }
                    else if (this.JournalUsesHoverTooltips)
                    {
                        this.RegisterTranslatedHoverTooltip(
                            $"JournalList-{questNameNodeKey:X}",
                            questName,
                            translatedQuestSnapshot.OriginalText,
                            translatedQuestSnapshot.AppliedText,
                            swapEnabled: this.JournalHoverShowsOriginal,
                            forceEnabled: true, denseHitbox: true);
                    }

                    continue;
                }

                var questPlate = this.FormatQuestPlate(
                    questNameText,
                    string.Empty);
                var foundQuestPlate = this.FindQuestPlateByName(questPlate);
                if (foundQuestPlate != null)
                {
#if DEBUG
                    // PluginLog.Debug(
                    //     $"Name from database: {questName->NodeText} -> {foundQuestPlate.TranslatedQuestName}");
#endif
                    var translQuestName = foundQuestPlate.TranslatedQuestName;
                    if (this.configuration
                        .RemoveDiacriticsWhenUsingReplacementQuest)
                    {
                        translQuestName = this.RemoveDiacritics(
                            foundQuestPlate.TranslatedQuestName,
                            this.SpecialCharsSupportedByGameFont);
                    }

                    if (this.JournalWritesNativeTranslation)
                    {
                        questName->SetText(translQuestName);
                    }
                    QuestHoverTranslationCache.Remember(
                        questNameNodeKey,
                        questNameText,
                        translQuestName);

                    QuestUiTranslationCache.Remember(
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
                            forceEnabled: true, denseHitbox: true);
                    }
                    continue;
                }

                var journalQuestCacheKey = $"Journal|{questNameText}";
                if (!this.TryGetQueuedTranslation(
                        journalQuestCacheKey,
                        out var translatedNameText))
                {
                    this.QueueTranslation(
                        journalQuestCacheKey,
                        () => this.Translate(questNameText),
                        resolvedTranslatedNameText =>
                        {
                            QuestPlate translatedQuestPlate = new(
                                questNameText,
                                string.Empty,
                                ClientStateInterface.ClientLanguage.Humanize(),
                                resolvedTranslatedNameText,
                                string.Empty,
                                string.Empty,
                                LangDict[LanguageInt].Code,
                                this.configuration.ChosenTransEngine,
                                DateTime.Now,
                                DateTime.Now);

                            var result = this.InsertQuestPlate(
                                translatedQuestPlate);
#if DEBUG
                            // PluginLog.Debug(
                            //     $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
                        });
                    continue;
                }
#if DEBUG
                    // PluginLog.Debug(
                    //     $"Name translated: {questNameText} -> {translatedNameText}");
#endif
                if (this.configuration
                    .RemoveDiacriticsWhenUsingReplacementQuest)
                {
                    translatedNameText = this.RemoveDiacritics(
                        translatedNameText,
                        this.SpecialCharsSupportedByGameFont);
                }

                if (this.JournalWritesNativeTranslation)
                {
                    questName->SetText(translatedNameText);
                }
                QuestHoverTranslationCache.Remember(
                    questNameNodeKey,
                    questNameText,
                    translatedNameText);
                QuestUiTranslationCache.Remember(
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
                        forceEnabled: true, denseHitbox: true);
                }
            }
        }
        catch (Exception e)
        {
            PluginLog.Error($"Error: {e}");
        }
    }

    private void UiJournalDetailHandler(AddonEvent type, AddonArgs args)
    {
#if DEBUG
        // PluginLog.Debug(
        //     $"UiJournalDetailHandler AddonEvent: {type} {args.AddonName}");
#endif
        this.TranslateJournalDetail();
    }

    private void UiJournalQuestHandler(AddonEvent type, AddonArgs args)
    {
#if DEBUG
        // PluginLog.Debug(
        //     $"UiJournalQuestHandler AddonEvent: {type} {args.AddonName}");
#endif
        this.TranslateJournalQuests();
    }

    /// <summary>
    /// Builds a single multi-paragraph tooltip body from the quest plate text
    /// sections that are currently visible.
    /// </summary>
    /// <param name="sections">Quest plate text sections to join.</param>
    /// <returns>A multi-paragraph tooltip body.</returns>
    private static string BuildQuestPlateHoverBody(params string?[] sections)
    {
        List<string> lines = new();
        foreach (var section in sections)
        {
            if (string.IsNullOrWhiteSpace(section))
            {
                continue;
            }

            lines.Add(section.Trim());
        }

        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    /// <summary>
    /// Gets the bounds of the JournalCanvasComponentNode used as the quest
    /// plate hover trigger.
    /// </summary>
    /// <param name="journalBox">The journal detail component.</param>
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

        topLeft = new Vector2(
            questCanvasNode->ScreenX,
            questCanvasNode->ScreenY);
        bottomRight = new Vector2(
            questCanvasNode->ScreenX + Math.Max(1f, questCanvasNode->Width),
            questCanvasNode->ScreenY + Math.Max(1f, questCanvasNode->Height));

        return true;
    }
}

