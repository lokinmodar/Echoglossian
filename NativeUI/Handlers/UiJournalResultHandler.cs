// <copyright file="UiJournalResultHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

using Echoglossian.Cache;

namespace Echoglossian;

public partial class Echoglossian
{
    private unsafe void UiJournalResultHandler(AddonEvent type, AddonArgs args)
    {
        if (!this.configuration.TranslateJournalResult)
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
            if (setupAtkValues[1].Type != ValueType.String ||
                setupAtkValues[1].String == null)
            {
                return;
            }

            var questNameText = MemoryHelper.ReadSeStringAsString(
                out _,
                (nint)setupAtkValues[1].String.Value);
            if (questNameText == string.Empty)
            {
                return;
            }

            if (QuestUiTranslationCache.TryGetAppliedSnapshot(
                    questNameText,
                    out _))
            {
                return;
            }

            var questPlate = this.FormatQuestPlate(questNameText, string.Empty);
            var foundQuestPlate = this.FindQuestPlateByName(questPlate);
            var cacheKey = $"JournalResult|{questNameText}";
            if (foundQuestPlate != null)
            {
#if DEBUG
                PluginLog.Debug(
                    $"Name from database: {questNameText} -> {foundQuestPlate.TranslatedQuestName}");
#endif
                if (this.configuration
                    .RemoveDiacriticsWhenUsingReplacementQuest)
                {
                    foundQuestPlate.TranslatedQuestName = this.RemoveDiacritics(
                        foundQuestPlate.TranslatedQuestName,
                        this.SpecialCharsSupportedByGameFont);
                }

                if (this.JournalResultWritesNativeTranslation)
                {
                    setupAtkValues[1]
                        .SetManagedString(foundQuestPlate.TranslatedQuestName);
                }
                QuestUiTranslationCache.Remember(
                    questNameText,
                    foundQuestPlate.TranslatedQuestName);

                if (this.JournalResultUsesHoverTooltips)
                {
                    var addon = AtkStage.Instance()->RaptureAtkUnitManager
                        ->GetAddonByName("JournalResult");
                    this.RegisterTranslatedHoverTooltip(
                        $"JournalResult-{(nint)addon:X}",
                        addon,
                        questNameText,
                        foundQuestPlate.TranslatedQuestName,
                        swapEnabled: this.JournalResultHoverShowsOriginal,
                        forceEnabled: true, denseHitbox: true);
                }
                return;
            }

            if (this.TryGetQueuedTranslation(cacheKey, out var cachedTranslatedName))
            {
                var translatedNameText = cachedTranslatedName;
#if DEBUG
                PluginLog.Debug(
                    $"Name translated: {questNameText} -> {translatedNameText}");
#endif
                if (this.configuration
                    .RemoveDiacriticsWhenUsingReplacementQuest)
                {
                    translatedNameText = this.RemoveDiacritics(
                        translatedNameText,
                        this.SpecialCharsSupportedByGameFont);
                }

                if (this.JournalResultWritesNativeTranslation)
                {
                    setupAtkValues[1].SetManagedString(translatedNameText);
                }
                QuestUiTranslationCache.Remember(
                    questNameText,
                    translatedNameText);

                if (this.JournalResultUsesHoverTooltips)
                {
                    var addon = AtkStage.Instance()->RaptureAtkUnitManager
                        ->GetAddonByName("JournalResult");
                    this.RegisterTranslatedHoverTooltip(
                        $"JournalResult-{(nint)addon:X}",
                        addon,
                        questNameText,
                        translatedNameText,
                        swapEnabled: this.JournalResultHoverShowsOriginal,
                        forceEnabled: true, denseHitbox: true);
                }
                return;
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

                    var result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
                    PluginLog.Debug(
                        $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
                });
        }
        catch (Exception e)
        {
            PluginLog.Error(
                "UiJournalResultHandler Exception: " + e.StackTrace);
        }
    }
}

