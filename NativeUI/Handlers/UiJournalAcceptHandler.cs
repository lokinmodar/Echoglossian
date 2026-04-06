// <copyright file="UiJournalAcceptHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;

namespace Echoglossian;

public partial class Echoglossian
{
    private unsafe void UiJournalAcceptHandler(AddonEvent type, AddonArgs args)
    {
        if (!this.configuration.TranslateJournal)
        {
            return;
        }

        if (args is not AddonSetupArgs setupArgs)
        {
            return;
        }

        var setupAtkValues = (AtkValue*)setupArgs.AtkValues;
        if (setupAtkValues == null)
        {
            return;
        }

        try
        {
            var questName = MemoryHelper.ReadSeStringAsString(
                out _,
                (nint)setupAtkValues[5].String.Value);
            var questMessage = MemoryHelper.ReadSeStringAsString(
                out _,
                (nint)setupAtkValues[12].String.Value);

#if DEBUG
            PluginLog.Debug(
                $"Language: {ClientStateInterface.ClientLanguage.Humanize()}");
            PluginLog.Debug($"Quest name: {questName}");
            PluginLog.Debug($"Quest message: {questMessage}");
#endif

            var questPlate = this.FormatQuestPlate(questName, questMessage);
            var foundQuestPlate = this.FindQuestPlate(questPlate);
            var cacheKey = $"JournalAccept|{questName}|{questMessage}";

            if (QuestUiTranslationCache.TryGetAppliedSnapshot(
                    questName,
                    out _) &&
                QuestUiTranslationCache.TryGetAppliedSnapshot(
                    questMessage,
                    out _))
            {
                return;
            }

            string translatedQuestName;
            string translatedQuestMessage;

            // If the quest is not saved
            if (foundQuestPlate == null)
            {
                if (this.TryGetQueuedTranslation(
                        cacheKey,
                        out var cachedTranslatedPayload) &&
                    TryDeserializeTranslationPair(
                        cachedTranslatedPayload,
                        out translatedQuestName,
                        out translatedQuestMessage))
                {
#if DEBUG
                    PluginLog.Debug(
                        $"Translated quest name: {translatedQuestName}");
                    PluginLog.Debug(
                        $"Translated quest message: {translatedQuestMessage}");
#endif
                }
                else
                {
                    this.QueueTranslation(
                        cacheKey,
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

                            var result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
                            PluginLog.Debug(
                                $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
                        });

                    return;
                }
            }
            else
            {
                // if the data is already in the DB
                translatedQuestName = foundQuestPlate.TranslatedQuestName;
                translatedQuestMessage = foundQuestPlate.TranslatedQuestMessage;
#if DEBUG
                PluginLog.Debug(
                    $"From database - Name: {translatedQuestName}, Message: {translatedQuestMessage}");
#endif
            }
#if DEBUG
            PluginLog.Debug(
                $"Using QuestPlate Replace - {translatedQuestName}: {translatedQuestMessage}");
#endif
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
                setupAtkValues[5].SetManagedString(translatedQuestName);
                setupAtkValues[12].SetManagedString(translatedQuestMessage);
            }
            QuestUiTranslationCache.Remember(questName, translatedQuestName);
            QuestUiTranslationCache.Remember(
                questMessage,
                translatedQuestMessage);

            if (this.JournalUsesHoverTooltips)
            {
                var addon = AtkStage.Instance()->RaptureAtkUnitManager
                    ->GetAddonByName("JournalAccept");
                this.RegisterTranslatedHoverTooltip(
                    $"JournalAccept-{(nint)addon:X}",
                    addon,
                    $"{questName}\n{questMessage}",
                    $"{translatedQuestName}\n{translatedQuestMessage}",
                    swapEnabled: this.JournalHoverShowsOriginal,
                    forceEnabled: true);
            }
        }
        catch (Exception e)
        {
            PluginLog.Error("Exception: " + e.StackTrace);
        }
    }
}
