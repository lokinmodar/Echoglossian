// <copyright file="UiScenarioTreeHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Echoglossian;

public partial class Echoglossian
{
    private unsafe void TranslateQuestOnScenarioTree(
        AtkValue* setupAtkValues,
        int valueIndex)
    {
        if (setupAtkValues[valueIndex].Type != ValueType.String ||
            setupAtkValues[valueIndex].String == null)
        {
            return;
        }

        var questNameText = MemoryHelper.ReadSeStringAsString(
            out _,
            (nint)setupAtkValues[valueIndex].String.Value);
        if (questNameText == null || questNameText.Length == 0)
        {
            return;
        }

        var questPlate = this.FormatQuestPlate(questNameText, string.Empty);
        var foundQuestPlate = this.FindQuestPlateByName(questPlate);
        var cacheKey = $"ScenarioTree|{valueIndex}|{questNameText}";
        if (foundQuestPlate != null)
        {
#if DEBUG
            // PluginLog.Debug(
            //     $"Name from database: {questNameText} -> {foundQuestPlate.TranslatedQuestName}");
#endif
            var translatedQuestName = foundQuestPlate.TranslatedQuestName;

            if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
            {
                translatedQuestName = this.RemoveDiacritics(
                    translatedQuestName,
                    this.SpecialCharsSupportedByGameFont);
            }

            setupAtkValues[valueIndex].SetManagedString(translatedQuestName);

            if (this.configuration.TranslateTooltips)
            {
                var addon = AtkStage.Instance()->RaptureAtkUnitManager
                    ->GetAddonByName("ScenarioTree");
                this.RegisterTranslatedHoverTooltip(
                    $"ScenarioTree-{(nint)addon:X}-{valueIndex}",
                    addon,
                    questNameText,
                    translatedQuestName);
            }
            return;
        }

        if (this.TryGetQueuedTranslation(cacheKey, out var cachedTranslatedName))
        {
            var translatedNameText = cachedTranslatedName;
#if DEBUG
            // PluginLog.Debug(
            //     $"Name translated: {questNameText} -> {translatedNameText}");
#endif
            if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
            {
                translatedNameText = this.RemoveDiacritics(
                    translatedNameText,
                    this.SpecialCharsSupportedByGameFont);
            }

            setupAtkValues[valueIndex].SetManagedString(translatedNameText);

            if (this.configuration.TranslateTooltips)
            {
                var addon = AtkStage.Instance()->RaptureAtkUnitManager
                    ->GetAddonByName("ScenarioTree");
                this.RegisterTranslatedHoverTooltip(
                    $"ScenarioTree-{(nint)addon:X}-{valueIndex}",
                    addon,
                    questNameText,
                    translatedNameText);
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
                // PluginLog.Debug(
                //     $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
            });
    }

    private unsafe void UiScenarioTreeHandler(AddonEvent type, AddonArgs args)
    {
        // PluginLog.Debug($"UiScenarioTreeHandler AddonEvent: {type} {args.AddonName}");
        if (!this.configuration.TranslateJournal)
        {
            return;
        }

        if (args is not AddonRefreshArgs setupArgs)
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
            // Translate MSQ
            this.TranslateQuestOnScenarioTree(setupAtkValues, 7);

            // Translate SubQuest
            this.TranslateQuestOnScenarioTree(setupAtkValues, 2);
        }
        catch (Exception e)
        {
            PluginLog.Error(
                "Exception at UiScenarioTreeHandler: " + e.StackTrace);
        }
    }
}
