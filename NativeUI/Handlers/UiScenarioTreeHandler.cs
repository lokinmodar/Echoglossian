// <copyright file="UiScenarioTreeHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

using Echoglossian.Cache;

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

        var questTodoProgressSnapshot = default(
            QuestTodoProgressSnapshot?);
        if (QuestTodoProgressResolver.TryResolveQuestTodoProgress(
                questNameText,
                out var resolvedTodoProgressSnapshot))
        {
            questTodoProgressSnapshot = resolvedTodoProgressSnapshot;
        }

        var questTodoProgressKey =
            questTodoProgressSnapshot?.CacheKey ?? questNameText;

        if (QuestUiTranslationCache.TryGetAppliedSnapshot(
                questTodoProgressKey + "|" + questNameText,
                out var cachedScenarioSnap))
        {
            if (this.ScenarioTreeUsesHoverTooltips)
            {
                var addon = AtkStage.Instance()->RaptureAtkUnitManager
                    ->GetAddonByName("ScenarioTree");
                this.RegisterTranslatedHoverTooltip(
                    $"ScenarioTree-{(nint)addon:X}-{valueIndex}-{questTodoProgressKey}",
                    addon,
                    questNameText,
                    cachedScenarioSnap.AppliedText,
                    swapEnabled: this.ScenarioTreeHoverShowsOriginal,
                    forceEnabled: true, denseHitbox: true);
            }

            return;
        }

        var questPlate = this.FormatQuestPlate(questNameText, string.Empty);
        var foundQuestPlate = this.FindQuestPlateByName(questPlate);
        var cacheKey =
            $"ScenarioTree|{valueIndex}|{questTodoProgressKey}|{questNameText}";
        if (foundQuestPlate != null)
        {
#if DEBUG
            // PluginLog.Debug(
            //     $"Name from database: {questNameText} -> {foundQuestPlate.TranslatedQuestName}");
#endif
            var translatedQuestName = foundQuestPlate.TranslatedQuestName;

            if (this.ScenarioTreeShouldRemoveDiacritics)
            {
                translatedQuestName = this.RemoveDiacritics(
                    translatedQuestName,
                    this.SpecialCharsSupportedByGameFont);
            }

            if (this.ScenarioTreeWritesNativeTranslation)
            {
                setupAtkValues[valueIndex].SetManagedString(
                    translatedQuestName);
            }
            QuestUiTranslationCache.Remember(
                questTodoProgressKey + "|" + questNameText,
                translatedQuestName);

            if (this.ScenarioTreeUsesHoverTooltips)
            {
                var addon = AtkStage.Instance()->RaptureAtkUnitManager
                    ->GetAddonByName("ScenarioTree");
                this.RegisterTranslatedHoverTooltip(
                    $"ScenarioTree-{(nint)addon:X}-{valueIndex}-{questTodoProgressKey}",
                    addon,
                    questNameText,
                    translatedQuestName,
                    swapEnabled: this.ScenarioTreeHoverShowsOriginal,
                    forceEnabled: true, denseHitbox: true);
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
            if (this.ScenarioTreeShouldRemoveDiacritics)
            {
                translatedNameText = this.RemoveDiacritics(
                    translatedNameText,
                    this.SpecialCharsSupportedByGameFont);
            }

            if (this.ScenarioTreeWritesNativeTranslation)
            {
                setupAtkValues[valueIndex].SetManagedString(
                    translatedNameText);
            }
            QuestUiTranslationCache.Remember(
                questTodoProgressKey + "|" + questNameText,
                translatedNameText);

            if (this.ScenarioTreeUsesHoverTooltips)
            {
                var addon = AtkStage.Instance()->RaptureAtkUnitManager
                    ->GetAddonByName("ScenarioTree");
                this.RegisterTranslatedHoverTooltip(
                    $"ScenarioTree-{(nint)addon:X}-{valueIndex}-{questTodoProgressKey}",
                    addon,
                    questNameText,
                    translatedNameText,
                    swapEnabled: this.ScenarioTreeHoverShowsOriginal,
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
                // PluginLog.Debug(
                //     $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
            });
    }

    private unsafe void UiScenarioTreeHandler(AddonEvent type, AddonArgs args)
    {
        // PluginLog.Debug($"UiScenarioTreeHandler AddonEvent: {type} {args.AddonName}");
        if (!this.configuration.TranslateScenarioTree)
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

