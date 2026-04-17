// <copyright file="ActionTooltipPrefetchRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;
using System.Reflection;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ActionSheet = Lumina.Excel.Sheets.Action;
using ActionTransientSheet = Lumina.Excel.Sheets.ActionTransient;
using ClassJobSheet = Lumina.Excel.Sheets.ClassJob;

namespace Echoglossian;

/// <summary>
///     Provides DB-first background prefetch for canonical action-tooltip payloads.
/// </summary>
public unsafe partial class Echoglossian
{
    private const int ActionTooltipPrefetchActionsPerTick = 6;

    private static readonly TimeSpan ActionTooltipPrefetchTickInterval =
        TimeSpan.FromSeconds(2);

    private static readonly Dictionary<Type, Dictionary<string, PropertyInfo?>> ActionClassJobCategoryPropertyCache =
        [];

    private readonly List<uint> actionTooltipPrefetchQueue = [];

    private string actionTooltipPrefetchSignature = string.Empty;

    private DateTime actionTooltipPrefetchLastTickUtc = DateTime.MinValue;

    private int actionTooltipPrefetchQueueIndex;

    /// <summary>
    ///     Ticks the action-tooltip prefetch runtime so the current class/job actions
    ///     are translated into canonical storage ahead of tooltip use.
    /// </summary>
    private void TickActionTooltipPrefetch()
    {
        if (!this.ShouldPrefetchStructuredTooltips() ||
            DateTime.UtcNow - this.actionTooltipPrefetchLastTickUtc <
            ActionTooltipPrefetchTickInterval)
        {
            return;
        }

        this.actionTooltipPrefetchLastTickUtc = DateTime.UtcNow;

        if (!TryGetCurrentClassJobInfo(
                out var currentClassJobId,
                out var currentClassJobAbbreviation) ||
            !TryCollectCurrentClassJobActionIds(
                currentClassJobId,
                currentClassJobAbbreviation,
                out var actionIds))
        {
            this.ClearActionTooltipPrefetchState();
            return;
        }

        var signature =
            $"{currentClassJobId}|{string.Join(',', actionIds)}";
        if (!string.Equals(
                this.actionTooltipPrefetchSignature,
                signature,
                StringComparison.Ordinal))
        {
            this.actionTooltipPrefetchSignature = signature;
            this.actionTooltipPrefetchQueue.Clear();
            this.actionTooltipPrefetchQueue.AddRange(actionIds);
            this.actionTooltipPrefetchQueueIndex = 0;
        }

        if (this.actionTooltipPrefetchQueueIndex >=
            this.actionTooltipPrefetchQueue.Count)
        {
            return;
        }

        var processedCount = 0;
        while (processedCount < ActionTooltipPrefetchActionsPerTick &&
               this.actionTooltipPrefetchQueueIndex <
               this.actionTooltipPrefetchQueue.Count)
        {
            var actionId =
                this.actionTooltipPrefetchQueue[this.actionTooltipPrefetchQueueIndex++];
            this.PrefetchActionTooltip(actionId, currentClassJobId);
            processedCount++;
        }
    }

    /// <summary>
    ///     Clears the action-tooltip prefetch runtime state.
    /// </summary>
    private void ClearActionTooltipPrefetchState()
    {
        this.actionTooltipPrefetchQueue.Clear();
        this.actionTooltipPrefetchQueueIndex = 0;
        this.actionTooltipPrefetchSignature = string.Empty;
        this.actionTooltipPrefetchLastTickUtc = DateTime.MinValue;
    }

    /// <summary>
    ///     Prefetches one canonical action-tooltip payload and any missing translations.
    /// </summary>
    /// <param name="actionId">The action row identifier.</param>
    /// <param name="currentClassJobId">The current class-job identifier.</param>
    private void PrefetchActionTooltip(uint actionId, byte currentClassJobId)
    {
        if (!TryBuildActionTooltipCanonicalPayload(
                actionId,
                currentClassJobId,
                out var originalPayload))
        {
            return;
        }

        var originalRow = ActionTooltipPersistenceHelper.CreateCanonicalRow(
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code,
            this.configuration.ChosenTransEngine,
            GetGameVersion(),
            originalPayload);
        var existingRow = this.FindActionTooltip(originalRow) ?? originalRow;
        this.InsertActionTooltip(originalRow);

        this.PrefetchActionTooltipName(originalPayload, existingRow);
        this.PrefetchActionTooltipDescription(originalPayload, existingRow);
    }

    /// <summary>
    ///     Prefetches the translated action name when it is not yet persisted.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="existingRow">The currently persisted row, if any.</param>
    private void PrefetchActionTooltipName(
        ActionTooltipCanonicalPayload originalPayload,
        ActionTooltip existingRow)
    {
        if (string.IsNullOrWhiteSpace(originalPayload.Name) ||
            !string.IsNullOrWhiteSpace(existingRow.TranslatedActionName))
        {
            return;
        }

        var translationKey =
            $"ActionTooltipPrefetch|{originalPayload.ActionId}|Name|{originalPayload.Name}";
        if (this.TryGetQueuedTranslation(
                translationKey,
                out var cachedTranslatedName))
        {
            this.ApplyActionTooltipTranslation(
                originalPayload.ActionId,
                originalPayload.ClassJobId,
                translatedName: cachedTranslatedName);
            return;
        }

        this.QueueTranslation(
            translationKey,
            () => TranslationService.Translate(
                originalPayload.Name,
                ClientStateInterface.ClientLanguage.Humanize(),
                LangDict[LanguageInt].Code),
            translatedName => this.ApplyActionTooltipTranslation(
                originalPayload.ActionId,
                originalPayload.ClassJobId,
                translatedName: translatedName));
    }

    /// <summary>
    ///     Prefetches the translated action description when it is not yet persisted.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="existingRow">The currently persisted row, if any.</param>
    private void PrefetchActionTooltipDescription(
        ActionTooltipCanonicalPayload originalPayload,
        ActionTooltip existingRow)
    {
        if (string.IsNullOrWhiteSpace(originalPayload.Description) ||
            !string.IsNullOrWhiteSpace(existingRow.TranslatedActionDescription))
        {
            return;
        }

        var translationKey =
            $"ActionTooltipPrefetch|{originalPayload.ActionId}|Description|{originalPayload.Description}";
        if (this.TryGetQueuedTranslation(
                translationKey,
                out var cachedTranslatedDescription))
        {
            this.ApplyActionTooltipTranslation(
                originalPayload.ActionId,
                originalPayload.ClassJobId,
                translatedDescription: cachedTranslatedDescription);
            return;
        }

        this.QueueTranslation(
            translationKey,
            () => TranslationService.Translate(
                originalPayload.Description,
                ClientStateInterface.ClientLanguage.Humanize(),
                LangDict[LanguageInt].Code),
            translatedDescription => this.ApplyActionTooltipTranslation(
                originalPayload.ActionId,
                originalPayload.ClassJobId,
                translatedDescription: translatedDescription));
    }

    /// <summary>
    ///     Applies one resolved action-tooltip translation into canonical storage.
    /// </summary>
    /// <param name="actionId">The action row identifier.</param>
    /// <param name="currentClassJobId">The current class-job identifier.</param>
    /// <param name="translatedName">The translated name, if any.</param>
    /// <param name="translatedDescription">The translated description, if any.</param>
    private void ApplyActionTooltipTranslation(
        uint actionId,
        uint currentClassJobId,
        string? translatedName = null,
        string? translatedDescription = null)
    {
        if (!TryBuildActionTooltipCanonicalPayload(
                actionId,
                (byte)currentClassJobId,
                out var originalPayload))
        {
            return;
        }

        var existingProbe = ActionTooltipPersistenceHelper.CreateCanonicalRow(
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code,
            this.configuration.ChosenTransEngine,
            GetGameVersion(),
            originalPayload);
        var existingRow = this.FindActionTooltip(existingProbe);
        var translatedPayload = existingRow == null
            ? originalPayload
            : ActionTooltipCanonicalPayload.Deserialize(
                    existingRow.CanonicalPayloadAsText) ??
                originalPayload;

        translatedPayload.ActionId = originalPayload.ActionId;
        translatedPayload.IconId = originalPayload.IconId;
        translatedPayload.ActionCategoryId = originalPayload.ActionCategoryId;
        translatedPayload.ClassJobId = originalPayload.ClassJobId;
        translatedPayload.ClassJobCategoryId =
            originalPayload.ClassJobCategoryId;
        translatedPayload.Name = originalPayload.Name;
        translatedPayload.Description = originalPayload.Description;
        translatedPayload.TranslatedName =
            !string.IsNullOrWhiteSpace(translatedName)
                ? translatedName
                : translatedPayload.TranslatedName;
        translatedPayload.TranslatedDescription =
            !string.IsNullOrWhiteSpace(translatedDescription)
                ? translatedDescription
                : translatedPayload.TranslatedDescription;

        var translatedRow = ActionTooltipPersistenceHelper.CreateCanonicalRow(
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code,
            this.configuration.ChosenTransEngine,
            GetGameVersion(),
            originalPayload,
            translatedPayload);
        this.InsertActionTooltip(translatedRow);
    }

    /// <summary>
    ///     Tries to collect the current class/job action ids from canonical sheets.
    /// </summary>
    /// <param name="currentClassJobId">The current class-job identifier.</param>
    /// <param name="currentClassJobAbbreviation">The current class/job abbreviation.</param>
    /// <param name="actionIds">The collected action ids.</param>
    /// <returns>True when action ids were collected successfully.</returns>
    private static bool TryCollectCurrentClassJobActionIds(
        byte currentClassJobId,
        string currentClassJobAbbreviation,
        out List<uint> actionIds)
    {
        actionIds = [];

        var actionSheet =
            DManager.GetExcelSheet<ActionSheet>(ClientStateInterface.ClientLanguage);
        if (actionSheet == null)
        {
            return false;
        }

        HashSet<uint> uniqueActionIds = [];
        foreach (var actionRow in actionSheet)
        {
            if (actionRow.RowId == 0 ||
                string.IsNullOrWhiteSpace(actionRow.Name.ExtractText()) ||
                !actionRow.IsPlayerAction ||
                actionRow.IsPvP)
            {
                continue;
            }

            var matchesClassJob = actionRow.ClassJob.RowId == currentClassJobId;
            var matchesCategory =
                DoesActionCategoryMatchCurrentJob(
                    actionRow.ClassJobCategory.ValueNullable,
                    currentClassJobAbbreviation);
            if (!matchesClassJob && !matchesCategory)
            {
                continue;
            }

            uniqueActionIds.Add(actionRow.RowId);
        }

        actionIds = uniqueActionIds.OrderBy(id => id).ToList();
        return actionIds.Count > 0;
    }

    /// <summary>
    ///     Tries to build one canonical action-tooltip payload from sheets.
    /// </summary>
    /// <param name="actionId">The action row identifier.</param>
    /// <param name="currentClassJobId">The current class-job identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildActionTooltipCanonicalPayload(
        uint actionId,
        byte currentClassJobId,
        out ActionTooltipCanonicalPayload payload)
    {
        payload = new ActionTooltipCanonicalPayload();

        var actionSheet =
            DManager.GetExcelSheet<ActionSheet>(ClientStateInterface.ClientLanguage);
        var actionTransientSheet =
            DManager.GetExcelSheet<ActionTransientSheet>(
                ClientStateInterface.ClientLanguage);
        if (actionSheet == null ||
            actionTransientSheet == null ||
            !actionSheet.TryGetRow(actionId, out var actionRow))
        {
            return false;
        }

        var description = actionTransientSheet.TryGetRow(actionId, out var transientRow)
            ? transientRow.Description.ExtractText()
            : string.Empty;
        payload = new ActionTooltipCanonicalPayload
        {
            ActionId = actionRow.RowId,
            IconId = actionRow.Icon,
            ActionCategoryId = actionRow.ActionCategory.RowId,
            ClassJobId = actionRow.ClassJob.RowId != 0
                ? actionRow.ClassJob.RowId
                : currentClassJobId,
            ClassJobCategoryId = actionRow.ClassJobCategory.RowId,
            Name = actionRow.Name.ExtractText(),
            Description = description,
        };

        return !string.IsNullOrWhiteSpace(payload.Name);
    }

    /// <summary>
    ///     Tries to resolve the current class/job id and abbreviation.
    /// </summary>
    /// <param name="currentClassJobId">The current class/job id.</param>
    /// <param name="currentClassJobAbbreviation">The current class/job abbreviation.</param>
    /// <returns>True when the current class/job was resolved.</returns>
    private static bool TryGetCurrentClassJobInfo(
        out byte currentClassJobId,
        out string currentClassJobAbbreviation)
    {
        currentClassJobId = 0;
        currentClassJobAbbreviation = string.Empty;

        var playerState = PlayerState.Instance();
        if (playerState == null || playerState->CurrentClassJobId == 0)
        {
            return false;
        }

        var classJobSheet =
            DManager.GetExcelSheet<ClassJobSheet>(ClientStateInterface.ClientLanguage);
        if (classJobSheet == null ||
            !classJobSheet.TryGetRow(playerState->CurrentClassJobId, out var classJobRow))
        {
            return false;
        }

        currentClassJobId = playerState->CurrentClassJobId;
        currentClassJobAbbreviation = classJobRow.Abbreviation.ExtractText()
            .Trim()
            .ToUpperInvariant();
        return !string.IsNullOrWhiteSpace(currentClassJobAbbreviation);
    }

    /// <summary>
    ///     Determines whether an action class-job-category includes the current job.
    /// </summary>
    /// <param name="classJobCategory">The action class-job-category row.</param>
    /// <param name="abbreviation">The current class/job abbreviation.</param>
    /// <returns>True when the category contains the current job.</returns>
    private static bool DoesActionCategoryMatchCurrentJob(
        object? classJobCategory,
        string abbreviation)
    {
        if (classJobCategory == null || string.IsNullOrWhiteSpace(abbreviation))
        {
            return false;
        }

        var categoryType = classJobCategory.GetType();
        if (!ActionClassJobCategoryPropertyCache.TryGetValue(
                categoryType,
                out var propertyMap))
        {
            propertyMap = new Dictionary<string, PropertyInfo?>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var jobAbbreviation in new[]
                     {
                         "GLA", "PGL", "MRD", "LNC", "ARC", "CNJ", "THM",
                         "PLD", "MNK", "WAR", "DRG", "BRD", "WHM", "BLM",
                         "ACN", "SMN", "SCH", "ROG", "NIN", "MCH", "DRK",
                         "AST", "SAM", "RDM", "BLU", "GNB", "DNC", "RPR",
                         "SGE", "VPR", "PCT",
                     })
            {
                propertyMap[jobAbbreviation] = categoryType.GetProperty(
                    jobAbbreviation,
                    BindingFlags.Public | BindingFlags.Instance);
            }

            ActionClassJobCategoryPropertyCache[categoryType] = propertyMap;
        }

        if (!propertyMap.TryGetValue(abbreviation, out var property) ||
            property == null)
        {
            return false;
        }

        return property.GetValue(classJobCategory) is bool matches && matches;
    }
}
