// <copyright file="UiRecommendListHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;

namespace Echoglossian;

public partial class Echoglossian
{
    private unsafe void UpdateRecommendList()
    {
        var atkStage = AtkStage.Instance();
        var recommendList =
            atkStage->RaptureAtkUnitManager->GetAddonByName("RecommendList");
        if (recommendList == null || !recommendList->IsVisible)
        {
            return;
        }

        try
        {
            // Replace the text in the nodes reading from the DB
            var questListNode = recommendList->GetNodeById(5);
            if (questListNode == null || !questListNode->IsVisible())
            {
                return;
            }

            var questListComponent =
                questListNode->GetAsAtkComponentNode()->Component;
            for (var i = 0;
                 i < questListComponent->UldManager.NodeListCount;
                 i++)
            {
                if (!questListComponent->UldManager.NodeList[i]->IsVisible())
                {
                    continue;
                }

                if (questListComponent->UldManager.NodeList[i]->Type ==
                    NodeType.Collision ||
                    questListComponent->UldManager.NodeList[i]->Type ==
                    NodeType.Res)
                {
                    continue;
                }

                var questItemNode =
                    questListComponent->UldManager.NodeList[i]->
                        GetAsAtkComponentNode();
                var questNameNode =
                    questItemNode->Component->UldManager.SearchNodeById(5);
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
                if (this.RecommendListUsesHoverTooltips)
                {
                    this.RegisterTranslatedHoverTooltip(
                        $"RecommendList-{questNameNodeKey:X}",
                        questName,
                        questNameText,
                        questNameText,
                        swapEnabled: this.RecommendListHoverShowsOriginal,
                        forceEnabled: true, denseHitbox: true);
                }
                if (QuestUiTranslationCache.TryGetAppliedSnapshot(
                        questNameText,
                        out var translatedQuestSnapshot))
                {
                    if (this.RecommendListUsesHoverTooltips &&
                        QuestHoverTranslationCache.TryGet(
                            questNameNodeKey,
                            out var cachedHoverTranslation))
                    {
                        this.RegisterTranslatedHoverTooltip(
                            $"RecommendList-{questNameNodeKey:X}",
                            questName,
                            cachedHoverTranslation.OriginalText,
                            cachedHoverTranslation.TranslatedText,
                            swapEnabled: this.RecommendListHoverShowsOriginal,
                            forceEnabled: true, denseHitbox: true);
                    }
                    else if (this.RecommendListUsesHoverTooltips)
                    {
                        this.RegisterTranslatedHoverTooltip(
                            $"RecommendList-{questNameNodeKey:X}",
                            questName,
                            translatedQuestSnapshot.OriginalText,
                            translatedQuestSnapshot.AppliedText,
                            swapEnabled: this.RecommendListHoverShowsOriginal,
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
                    //     $"Name from database: {questNameText} -> {foundQuestPlate.TranslatedQuestName}");
#endif
                    var translatedQuestName =
                        foundQuestPlate.TranslatedQuestName;

                    if (this.RecommendListShouldRemoveDiacritics)
                    {
                        translatedQuestName = this.RemoveDiacritics(
                            translatedQuestName,
                            this.SpecialCharsSupportedByGameFont);
                    }

                if (this.RecommendListWritesNativeTranslation)
                    {
                        // because we are translating names, it's safer to use SetString instead of SetText
                        questName->NodeText.SetString(translatedQuestName);
                    }
                    QuestHoverTranslationCache.Remember(
                        questNameNodeKey,
                        questNameText,
                        translatedQuestName);
                    QuestUiTranslationCache.Remember(
                        questNameText,
                        translatedQuestName);
                    if (this.RecommendListUsesHoverTooltips)
                    {
                        this.RegisterTranslatedHoverTooltip(
                            $"RecommendList-{questNameNodeKey:X}",
                            questName,
                            questNameText,
                            translatedQuestName,
                            swapEnabled: this.RecommendListHoverShowsOriginal,
                            forceEnabled: true, denseHitbox: true);
                    }
                }
            }
        }
        catch (Exception e)
        {
            PluginLog.Error($"Error: {e}");
        }
    }

    private unsafe void TranslateRecommendListHandler()
    {
        var atkStage = AtkStage.Instance();
        var recommendList =
            atkStage->RaptureAtkUnitManager->GetAddonByName("RecommendList");
        if (recommendList == null || !recommendList->IsVisible)
        {
            return;
        }

        try
        {
            // First we store the non translated quest names in the DB
            var questListNode = recommendList->GetNodeById(5);
            if (questListNode == null || !questListNode->IsVisible())
            {
                return;
            }

            var questListComponent =
                questListNode->GetAsAtkComponentNode()->Component;
            for (var i = 0;
                 i < questListComponent->UldManager.NodeListCount;
                 i++)
            {
                if (!questListComponent->UldManager.NodeList[i]->IsVisible())
                {
                    continue;
                }

                if (questListComponent->UldManager.NodeList[i]->Type ==
                    NodeType.Collision ||
                    questListComponent->UldManager.NodeList[i]->Type ==
                    NodeType.Res)
                {
                    continue;
                }

                var questItemNode =
                    questListComponent->UldManager.NodeList[i]->
                        GetAsAtkComponentNode();
                var questNameNode =
                    questItemNode->Component->UldManager.SearchNodeById(5);
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
                if (this.RecommendListUsesHoverTooltips)
                {
                    this.RegisterTranslatedHoverTooltip(
                        $"RecommendList-{questNameNodeKey:X}",
                        questName,
                        questNameText,
                        questNameText,
                        swapEnabled: this.RecommendListHoverShowsOriginal,
                        forceEnabled: true, denseHitbox: true);
                }
                if (QuestUiTranslationCache.TryGetAppliedSnapshot(
                        questNameText,
                        out var translatedQuestSnapshot))
                {
                    if (this.RecommendListUsesHoverTooltips &&
                        QuestHoverTranslationCache.TryGet(
                            questNameNodeKey,
                            out var cachedHoverTranslation))
                    {
                        this.RegisterTranslatedHoverTooltip(
                            $"RecommendList-{questNameNodeKey:X}",
                            questName,
                            cachedHoverTranslation.OriginalText,
                            cachedHoverTranslation.TranslatedText,
                            swapEnabled: this.RecommendListHoverShowsOriginal,
                            forceEnabled: true, denseHitbox: true);
                    }
                    else if (this.RecommendListUsesHoverTooltips)
                    {
                        this.RegisterTranslatedHoverTooltip(
                            $"RecommendList-{questNameNodeKey:X}",
                            questName,
                            translatedQuestSnapshot.OriginalText,
                            translatedQuestSnapshot.AppliedText,
                            swapEnabled: this.RecommendListHoverShowsOriginal,
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
                    var translatedQuestName = foundQuestPlate.TranslatedQuestName;
                    if (this.RecommendListShouldRemoveDiacritics)
                    {
                        translatedQuestName = this.RemoveDiacritics(
                            translatedQuestName,
                            this.SpecialCharsSupportedByGameFont);
                    }

                    // because we are translating names, it's safer to use SetString instead of SetText
                    questName->NodeText.SetString(translatedQuestName);
                    QuestHoverTranslationCache.Remember(
                        questNameNodeKey,
                        questNameText,
                        translatedQuestName);
                    QuestUiTranslationCache.Remember(
                        questNameText,
                        translatedQuestName);
                    if (this.RecommendListUsesHoverTooltips)
                    {
                        this.RegisterTranslatedHoverTooltip(
                            $"RecommendList-{questNameNodeKey:X}",
                            questName,
                            questNameText,
                            translatedQuestName,
                            swapEnabled: this.RecommendListHoverShowsOriginal,
                            forceEnabled: true, denseHitbox: true);
                    }

                    continue;
                }

                var cacheKey = $"RecommendList|{questNameText}";
                if (this.TryGetQueuedTranslation(
                        cacheKey,
                        out var cachedTranslatedName))
                {
                    var translatedNameText = cachedTranslatedName;
                    if (this.RecommendListShouldRemoveDiacritics)
                    {
                        translatedNameText = this.RemoveDiacritics(
                            translatedNameText,
                            this.SpecialCharsSupportedByGameFont);
                    }

                    if (this.RecommendListWritesNativeTranslation)
                    {
                        // because we are translating names, it's safer to use SetString instead of SetText
                        questName->NodeText.SetString(translatedNameText);
                    }
                    QuestHoverTranslationCache.Remember(
                        questNameNodeKey,
                        questNameText,
                        translatedNameText);
                    QuestUiTranslationCache.Remember(
                        questNameText,
                        translatedNameText);
                    if (this.RecommendListUsesHoverTooltips)
                    {
                        this.RegisterTranslatedHoverTooltip(
                            $"RecommendList-{questNameNodeKey:X}",
                            questName,
                            questNameText,
                            translatedNameText,
                            swapEnabled: this.RecommendListHoverShowsOriginal,
                            forceEnabled: true, denseHitbox: true);
                    }

                    continue;
                }

                this.QueueTranslation(
                    cacheKey,
                    () => this.Translate(questNameText),
                    translatedNameText =>
                    {
                        QuestPlate translatedQuestPlate = new(
                            questNameText,
                            string.Empty,
                            ClientStateInterface.ClientLanguage.Humanize(),
                            translatedNameText,
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
#if DEBUG
                // PluginLog.Debug(
                //     $"Name translated queued: {questNameText}");
#endif
            }

            // Then we replace the text in the nodes
            this.UpdateRecommendList();
        }
        catch (Exception e)
        {
            PluginLog.Error($"Error in UIRecommendListHandler: {e}");
        }
    }

    private void UiRecommendListHandler(AddonEvent type, AddonArgs args)
    {
#if DEBUG
        // PluginLog.Debug(
        //     $"UiRecommendListHandler AddonEvent: {type} {args.AddonName}");
#endif

        if (this.DisableTranslationAccordingToState())
        {
            return;
        }

        if (!this.configuration.TranslateRecommendList)
        {
            return;
        }

        this.TranslateRecommendListHandler();
    }

    private void UiRecommendListHandlerAsync(AddonEvent type, AddonArgs args)
    {
#if DEBUG
        // PluginLog.Debug(
        //     $"UiRecommendListHandlerAsync AddonEvent: {type} {args.AddonName}");
#endif
        if (!this.configuration.TranslateRecommendList)
        {
            return;
        }

        // delay added to be sure the nodes are loaded when the player changes zones
        Task.Delay(200).ContinueWith(t => this.TranslateRecommendListHandler());
    }
}

