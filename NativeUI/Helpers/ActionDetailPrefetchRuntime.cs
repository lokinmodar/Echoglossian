// <copyright file="ActionDetailPrefetchRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ActionSheet = Lumina.Excel.Sheets.Action;
using ActionTransientSheet = Lumina.Excel.Sheets.ActionTransient;
using Lumina.Text.ReadOnly;

namespace Echoglossian;

/// <summary>
///     Provides DB-first background prefetch for canonical ActionDetail
///     payloads.
/// </summary>
public unsafe partial class Echoglossian
{
    private const int ActionDetailPrefetchActionsPerTick = 6;

    private static readonly TimeSpan ActionDetailPrefetchTickInterval =
        TimeSpan.FromSeconds(2);

    private static readonly TimeSpan ActionDetailOnDemandPrefetchCooldown =
        TimeSpan.FromSeconds(10);

    private readonly List<uint> actionDetailPrefetchQueue = [];

    private readonly Dictionary<string, DateTime> actionDetailOnDemandPrefetchUtcByScope =
        [];

    private string actionDetailPrefetchSignature = string.Empty;

    private DateTime actionDetailPrefetchLastTickUtc = DateTime.MinValue;

    private int actionDetailPrefetchQueueIndex;

    /// <summary>
    ///     Ticks the action-tooltip prefetch runtime so the current class/job actions
    ///     are translated into canonical storage ahead of tooltip use.
    /// </summary>
    private void TickActionDetailPrefetch()
    {
        if (!this.ShouldPrefetchActionAdjacentCanonicalTooltips() ||
            DateTime.UtcNow - this.actionDetailPrefetchLastTickUtc <
            ActionDetailPrefetchTickInterval)
        {
            return;
        }

        this.actionDetailPrefetchLastTickUtc = DateTime.UtcNow;

        if (!TryGetCurrentClassJobId(out var currentClassJobId) ||
            !TryCollectCurrentClassJobActionIds(
                currentClassJobId,
                out var actionIds))
        {
            this.ClearActionDetailPrefetchState();
            return;
        }

        var signature =
            $"{currentClassJobId}|{string.Join(',', actionIds)}";
        if (!string.Equals(
                this.actionDetailPrefetchSignature,
                signature,
                StringComparison.Ordinal))
        {
            this.actionDetailPrefetchSignature = signature;
            this.actionDetailPrefetchQueue.Clear();
            this.actionDetailPrefetchQueue.AddRange(actionIds);
            this.actionDetailPrefetchQueueIndex = 0;
        }

        if (this.actionDetailPrefetchQueueIndex >=
            this.actionDetailPrefetchQueue.Count)
        {
            return;
        }

        var processedCount = 0;
        while (processedCount < ActionDetailPrefetchActionsPerTick &&
               this.actionDetailPrefetchQueueIndex <
               this.actionDetailPrefetchQueue.Count)
        {
            var actionId =
                this.actionDetailPrefetchQueue[this.actionDetailPrefetchQueueIndex++];
            this.PrefetchActionDetail(actionId, currentClassJobId);
            processedCount++;
        }
    }

    /// <summary>
    ///     Clears the action-tooltip prefetch runtime state.
    /// </summary>
    private void ClearActionDetailPrefetchState()
    {
        this.actionDetailPrefetchQueue.Clear();
        this.actionDetailOnDemandPrefetchUtcByScope.Clear();
        this.actionDetailPrefetchQueueIndex = 0;
        this.actionDetailPrefetchSignature = string.Empty;
        this.actionDetailPrefetchLastTickUtc = DateTime.MinValue;
    }

    /// <summary>
    ///     Requests one on-demand canonical action-tooltip prefetch when the
    ///     live tooltip runtime encounters a hovered action that does not yet
    ///     exist in translated storage.
    /// </summary>
    /// <param name="actionId">The hovered action identifier.</param>
    /// <param name="currentClassJobId">The current class-job identifier.</param>
    /// <returns>
    ///     <see langword="true" /> when one prefetch was scheduled for this
    ///     scope; otherwise <see langword="false" />.
    /// </returns>
    private bool TryRequestActionDetailOnDemandPrefetch(
        uint actionId,
        byte currentClassJobId)
    {
        if (actionId == 0 ||
            !this.ShouldPrefetchActionAdjacentCanonicalTooltips() ||
            !TryBuildActionTooltipCanonicalPayload(
                actionId,
                currentClassJobId,
                out _))
        {
            return false;
        }

        var scopeKey =
            $"{actionId}|{LangDict[LanguageInt].Code}|{this.configuration.ChosenTransEngine}|{GetGameVersion() ?? string.Empty}";
        var utcNow = DateTime.UtcNow;
        if (this.actionDetailOnDemandPrefetchUtcByScope.TryGetValue(
                scopeKey,
                out var lastQueuedUtc) &&
            utcNow - lastQueuedUtc < ActionDetailOnDemandPrefetchCooldown)
        {
            return false;
        }

        this.actionDetailOnDemandPrefetchUtcByScope[scopeKey] = utcNow;
        this.PrefetchActionDetail(actionId, currentClassJobId);
        return true;
    }

    /// <summary>
    ///     Prefetches one canonical action-tooltip payload and any missing translations.
    /// </summary>
    /// <param name="actionId">The action row identifier.</param>
    /// <param name="currentClassJobId">The current class-job identifier.</param>
    private void PrefetchActionDetail(uint actionId, byte currentClassJobId)
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

        this.PrefetchActionDetailName(originalPayload, existingRow);
        this.PrefetchActionDetailDescription(originalPayload, existingRow);
    }

    /// <summary>
    ///     Prefetches the translated action name when it is not yet persisted.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="existingRow">The currently persisted row, if any.</param>
    private void PrefetchActionDetailName(
        ActionTooltipCanonicalPayload originalPayload,
        ActionTooltip existingRow)
    {
        if (string.IsNullOrWhiteSpace(originalPayload.Name) ||
            !string.IsNullOrWhiteSpace(existingRow.TranslatedActionName))
        {
            return;
        }

        var translationKey =
            BuildActionDetailNameTranslationKey(originalPayload);
        if (this.TryGetQueuedTranslation(
                translationKey,
                out var cachedTranslatedName))
        {
            this.ApplyActionDetailTranslation(
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
            translatedName => this.ApplyActionDetailTranslation(
                originalPayload.ActionId,
                originalPayload.ClassJobId,
                translatedName: translatedName));
    }

    /// <summary>
    ///     Prefetches the translated action description when it is not yet persisted.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="existingRow">The currently persisted row, if any.</param>
    private void PrefetchActionDetailDescription(
        ActionTooltipCanonicalPayload originalPayload,
        ActionTooltip existingRow)
    {
        if (string.IsNullOrWhiteSpace(originalPayload.Description) ||
            !string.IsNullOrWhiteSpace(existingRow.TranslatedActionDescription))
        {
            return;
        }

        var translationKey =
            BuildActionDetailDescriptionTranslationKey(originalPayload);
        if (this.TryGetQueuedTranslation(
                translationKey,
                out var cachedTranslatedDescription))
        {
            this.ApplyActionDetailTranslation(
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
            translatedDescription => this.ApplyActionDetailTranslation(
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
    private void ApplyActionDetailTranslation(
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
        this.TryPopulatePendingActionDetailTranslations(
            originalPayload,
            translatedPayload);
        if (!translatedPayload.HasCompleteTranslation)
        {
            return;
        }

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
    ///     Tries to enrich one action-detail payload with any queued counterpart
    ///     translation so canonical persistence only happens when the payload is complete.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="translatedPayload">The partially translated payload.</param>
    private void TryPopulatePendingActionDetailTranslations(
        ActionTooltipCanonicalPayload originalPayload,
        ActionTooltipCanonicalPayload translatedPayload)
    {
        if (string.IsNullOrWhiteSpace(translatedPayload.TranslatedName) &&
            !string.IsNullOrWhiteSpace(originalPayload.Name) &&
            this.TryGetQueuedTranslation(
                BuildActionDetailNameTranslationKey(originalPayload),
                out var cachedTranslatedName))
        {
            translatedPayload.TranslatedName = cachedTranslatedName;
        }

        if (string.IsNullOrWhiteSpace(translatedPayload.TranslatedDescription) &&
            !string.IsNullOrWhiteSpace(originalPayload.Description) &&
            this.TryGetQueuedTranslation(
                BuildActionDetailDescriptionTranslationKey(originalPayload),
                out var cachedTranslatedDescription))
        {
            translatedPayload.TranslatedDescription =
                cachedTranslatedDescription;
        }
    }

    /// <summary>
    ///     Builds the stable queued-translation key for one action-detail name.
    /// </summary>
    /// <param name="payload">The canonical payload.</param>
    /// <returns>The stable queue key.</returns>
    private static string BuildActionDetailNameTranslationKey(
        ActionTooltipCanonicalPayload payload)
    {
        return
            $"ActionDetailPrefetch|{payload.ActionId}|Name|{payload.Name}";
    }

    /// <summary>
    ///     Builds the stable queued-translation key for one action-detail description.
    /// </summary>
    /// <param name="payload">The canonical payload.</param>
    /// <returns>The stable queue key.</returns>
    private static string BuildActionDetailDescriptionTranslationKey(
        ActionTooltipCanonicalPayload payload)
    {
        return
            $"ActionDetailPrefetch|{payload.ActionId}|Description|{payload.Description}";
    }

    /// <summary>
    ///     Tries to collect the current class/job action ids from canonical sheets.
    /// </summary>
    /// <param name="currentClassJobId">The current class-job identifier.</param>
    /// <param name="actionIds">The collected action ids.</param>
    /// <returns>True when action ids were collected successfully.</returns>
    private static bool TryCollectCurrentClassJobActionIds(
        byte currentClassJobId,
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
                ClassJobCategorySheetHelper.HasClassJob(
                    actionRow.ClassJobCategory.ValueNullable,
                    currentClassJobId);
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
            ? EvaluateSheetText(transientRow.Description)
            : string.Empty;
        payload = new ActionTooltipCanonicalPayload
        {
            ActionId = actionRow.RowId,
            IconId = actionRow.Icon,
            ActionCategoryId = SheetRowIdNormalizationHelper.NormalizeOrZero(
                actionRow.ActionCategory.RowId),
            ClassJobId = SheetRowIdNormalizationHelper.NormalizeWithFallback(
                actionRow.ClassJob.RowId,
                currentClassJobId),
            ClassJobCategoryId = SheetRowIdNormalizationHelper.NormalizeOrZero(
                actionRow.ClassJobCategory.RowId),
            Name = actionRow.Name.ExtractText(),
            Description = description,
        };

        return !string.IsNullOrWhiteSpace(payload.Name);
    }

    /// <summary>
    ///     Evaluates one sheet-backed SeString before extracting visible text,
    ///     so transient descriptions with macros do not lose numeric values.
    /// </summary>
    /// <param name="text">The raw sheet text.</param>
    /// <returns>The evaluated visible text.</returns>
    private static string EvaluateSheetText(ReadOnlySeString text)
    {
        var evaluator = SeStringEvaluator;
        if (evaluator == null)
        {
            return text.ExtractText();
        }

        try
        {
            return evaluator.Evaluate(
                    text,
                    language: ClientStateInterface.ClientLanguage)
                .ExtractText();
        }
        catch
        {
            return text.ExtractText();
        }
    }

    /// <summary>
    ///     Tries to resolve the current class/job id.
    /// </summary>
    /// <param name="currentClassJobId">The current class/job id.</param>
    /// <returns>True when the current class/job was resolved.</returns>
    private static bool TryGetCurrentClassJobId(out byte currentClassJobId)
    {
        currentClassJobId = 0;

        var playerState = PlayerState.Instance();
        if (playerState == null || playerState->CurrentClassJobId == 0)
        {
            return false;
        }

        currentClassJobId = playerState->CurrentClassJobId;
        return true;
    }
}
