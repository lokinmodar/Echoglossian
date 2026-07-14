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
///     Reads one completed translation from the shared prefetch broker.
/// </summary>
/// <param name="key">The complete broker identity.</param>
/// <param name="translatedText">The cached translation, when present.</param>
/// <returns><see langword="true" /> when a cached translation exists.</returns>
internal delegate bool TryGetPrefetchTranslationDelegate(
    string key,
    out string translatedText);

/// <summary>
///     Queues one resolver and completion callback on the shared broker.
/// </summary>
/// <param name="key">The complete broker identity.</param>
/// <param name="resolver">The captured translation resolver.</param>
/// <param name="onResolved">The callback invoked after broker completion.</param>
/// <returns><see langword="true" /> when work was queued.</returns>
internal delegate bool QueuePrefetchTranslationDelegate(
    string key,
    Func<string> resolver,
    Action<string>? onResolved);

/// <summary>
///     Resolves one prefetch translation from an operation-captured source and
///     target contract.
/// </summary>
/// <param name="sourceText">The source text to translate.</param>
/// <param name="sourceLanguage">The captured source language.</param>
/// <param name="targetLanguage">The captured target language.</param>
/// <returns>The translated text.</returns>
internal delegate string ResolvePrefetchTranslationDelegate(
    string sourceText,
    SourceClientLanguage sourceLanguage,
    string targetLanguage);

/// <summary>
///     Describes how one production prefetch dispatch entered the broker.
/// </summary>
internal enum PrefetchTranslationDispatchResult
{
    /// <summary>
    ///     The captured source or reuse scope was incomplete or inconsistent.
    /// </summary>
    Rejected,

    /// <summary>
    ///     A source-scoped cached completion was applied synchronously.
    /// </summary>
    Cached,

    /// <summary>
    ///     New source-scoped work was queued.
    /// </summary>
    Queued,

    /// <summary>
    ///     Identical source-scoped work was already pending.
    /// </summary>
    AlreadyPending,
}

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
                out _) ||
            !RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
                out var sourceLanguage) ||
            !this.TryCreateCapturedTranslationScope(
                sourceLanguage,
                out var scope))
        {
            return false;
        }

        var scopeKey = BuildTranslationReuseScopedKey(
            $"ActionDetailOnDemand|{actionId}|{GetGameVersion() ?? string.Empty}",
            scope);
        var utcNow = DateTime.UtcNow;
        if (this.actionDetailOnDemandPrefetchUtcByScope.TryGetValue(
                scopeKey,
                out var lastQueuedUtc) &&
            utcNow - lastQueuedUtc < ActionDetailOnDemandPrefetchCooldown)
        {
            return false;
        }

        this.actionDetailOnDemandPrefetchUtcByScope[scopeKey] = utcNow;
        this.PrefetchActionDetail(
            actionId,
            currentClassJobId,
            sourceLanguage,
            scope);
        return true;
    }

    /// <summary>
    ///     Prefetches one canonical action-tooltip payload and any missing translations.
    /// </summary>
    /// <param name="actionId">The action row identifier.</param>
    /// <param name="currentClassJobId">The current class-job identifier.</param>
    private void PrefetchActionDetail(uint actionId, byte currentClassJobId)
    {
        if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
                out var sourceLanguage) ||
            !this.TryCreateCapturedTranslationScope(
                sourceLanguage,
                out var scope))
        {
            return;
        }

        this.PrefetchActionDetail(
            actionId,
            currentClassJobId,
            sourceLanguage,
            scope);
    }

    /// <summary>
    ///     Prefetches one canonical action-tooltip payload using an immutable
    ///     operation scope.
    /// </summary>
    /// <param name="actionId">The action row identifier.</param>
    /// <param name="currentClassJobId">The current class-job identifier.</param>
    /// <param name="sourceLanguage">The resolved source language.</param>
    /// <param name="scope">The immutable operation reuse scope.</param>
    private void PrefetchActionDetail(
        uint actionId,
        byte currentClassJobId,
        SourceClientLanguage sourceLanguage,
        TranslationReuseScope scope)
    {

        if (!TryBuildActionTooltipCanonicalPayload(
                actionId,
                currentClassJobId,
                out var originalPayload))
        {
            return;
        }

        var translationService = TranslationService;
        RunActionDetailPrefetchOperationEntry(
            originalPayload,
            GetGameVersion(),
            sourceLanguage,
            scope,
            this.TryGetQueuedTranslation,
            this.QueueTranslation,
            (sourceText, capturedSource, targetLanguage) =>
                translationService.Translate(
                    sourceText,
                    capturedSource,
                    targetLanguage),
            this.FindActionTooltip,
            row => this.InsertActionTooltip(row),
            out var existingRow);
        this.PrefetchActionDetailDescription(
            originalPayload,
            existingRow,
            sourceLanguage,
            scope);
    }

    /// <summary>
    ///     Prefetches the translated action description when it is not yet persisted.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="existingRow">The currently persisted row, if any.</param>
    /// <param name="sourceLanguage">The resolved source language.</param>
    /// <param name="scope">The immutable operation reuse scope.</param>
    private void PrefetchActionDetailDescription(
        ActionTooltipCanonicalPayload originalPayload,
        ActionTooltip existingRow,
        SourceClientLanguage sourceLanguage,
        TranslationReuseScope scope)
    {
        if (string.IsNullOrWhiteSpace(originalPayload.Description) ||
            !string.IsNullOrWhiteSpace(existingRow.TranslatedActionDescription))
        {
            return;
        }

        var translationService = TranslationService;
        DispatchActionDetailPrefetchTranslation(
            $"ActionDetailPrefetch|{originalPayload.ActionId}|Description|{originalPayload.Description}",
            sourceLanguage,
            scope,
            this.TryGetQueuedTranslation,
            this.QueueTranslation,
            () => translationService.Translate(
                originalPayload.Description,
                sourceLanguage,
                scope.TargetLanguageCode),
            (translatedDescription, capturedScope, _) =>
                this.ApplyActionDetailTranslation(
                    originalPayload.ActionId,
                    originalPayload.ClassJobId,
                    capturedScope,
                    translatedDescription: translatedDescription));
    }

    /// <summary>
    ///     Applies one resolved action-tooltip translation into canonical storage.
    /// </summary>
    /// <param name="actionId">The action row identifier.</param>
    /// <param name="currentClassJobId">The current class-job identifier.</param>
    /// <param name="scope">The immutable operation reuse scope.</param>
    /// <param name="translatedName">The translated name, if any.</param>
    /// <param name="translatedDescription">The translated description, if any.</param>
    private void ApplyActionDetailTranslation(
        uint actionId,
        uint currentClassJobId,
        TranslationReuseScope scope,
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

        var translatedRow = CreateActionDetailTranslationRow(
            originalPayload,
            GetGameVersion(),
            scope,
            this.FindActionTooltip,
            this.TryGetQueuedTranslation,
            translatedName,
            translatedDescription);
        if (translatedRow == null)
        {
            return;
        }

        this.InsertActionTooltip(translatedRow);
    }

    /// <summary>
    ///     Builds the canonical translated action row sent to production
    ///     persistence after broker completion.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="gameVersion">The captured game version.</param>
    /// <param name="scope">The immutable operation reuse scope.</param>
    /// <param name="findRow">The production row lookup.</param>
    /// <param name="tryGetTranslation">The shared broker cache lookup.</param>
    /// <param name="translatedName">The translated action name, if any.</param>
    /// <param name="translatedDescription">The translated description, if any.</param>
    /// <returns>The complete canonical row, or <see langword="null" />.</returns>
    private static ActionTooltip? CreateActionDetailTranslationRow(
        ActionTooltipCanonicalPayload originalPayload,
        string? gameVersion,
        TranslationReuseScope scope,
        Func<ActionTooltip, ActionTooltip?> findRow,
        TryGetPrefetchTranslationDelegate tryGetTranslation,
        string? translatedName = null,
        string? translatedDescription = null)
    {
        var existingProbe = ActionTooltipPersistenceHelper.CreateCanonicalRow(
            scope.SourceLanguageCode,
            scope.TargetLanguageCode,
            scope.TranslationEngine!.Value,
            gameVersion,
            originalPayload);
        var existingRow = findRow(existingProbe);
        var translatedPayload = existingRow == null
            ? ActionTooltipCanonicalPayload.Deserialize(
                originalPayload.Serialize()) ?? originalPayload
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
        TryPopulatePendingActionDetailTranslations(
            originalPayload,
            translatedPayload,
            scope,
            tryGetTranslation);
        if (!translatedPayload.HasCompleteTranslation)
        {
            return null;
        }

        return ActionTooltipPersistenceHelper.CreateCanonicalRow(
            scope.SourceLanguageCode,
            scope.TargetLanguageCode,
            scope.TranslationEngine!.Value,
            gameVersion,
            originalPayload,
            translatedPayload);
    }

    /// <summary>
    ///     Tries to enrich one action-detail payload with any queued counterpart
    ///     translation so canonical persistence only happens when the payload is complete.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="translatedPayload">The partially translated payload.</param>
    /// <param name="scope">The immutable operation reuse scope.</param>
    /// <param name="tryGetTranslation">The shared broker cache lookup.</param>
    private static void TryPopulatePendingActionDetailTranslations(
        ActionTooltipCanonicalPayload originalPayload,
        ActionTooltipCanonicalPayload translatedPayload,
        TranslationReuseScope scope,
        TryGetPrefetchTranslationDelegate tryGetTranslation)
    {
        if (string.IsNullOrWhiteSpace(translatedPayload.TranslatedName) &&
            !string.IsNullOrWhiteSpace(originalPayload.Name) &&
            tryGetTranslation(
                BuildActionDetailNameTranslationKey(originalPayload, scope),
                out var cachedTranslatedName))
        {
            translatedPayload.TranslatedName = cachedTranslatedName;
        }

        if (string.IsNullOrWhiteSpace(translatedPayload.TranslatedDescription) &&
            !string.IsNullOrWhiteSpace(originalPayload.Description) &&
            tryGetTranslation(
                BuildActionDetailDescriptionTranslationKey(originalPayload, scope),
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
    /// <param name="scope">The immutable operation reuse scope.</param>
    /// <returns>The stable queue key.</returns>
    private static string BuildActionDetailNameTranslationKey(
        ActionTooltipCanonicalPayload payload,
        TranslationReuseScope scope)
    {
        return BuildActionDetailScopedTranslationKey(
            $"ActionDetailPrefetch|{payload.ActionId}|Name|{payload.Name}",
            scope);
    }

    /// <summary>
    ///     Builds the stable queued-translation key for one action-detail description.
    /// </summary>
    /// <param name="payload">The canonical payload.</param>
    /// <param name="scope">The immutable operation reuse scope.</param>
    /// <returns>The stable queue key.</returns>
    private static string BuildActionDetailDescriptionTranslationKey(
        ActionTooltipCanonicalPayload payload,
        TranslationReuseScope scope)
    {
        return BuildActionDetailScopedTranslationKey(
            $"ActionDetailPrefetch|{payload.ActionId}|Description|{payload.Description}",
            scope);
    }

    /// <summary>
    ///     Adds the complete operation scope to one action-detail payload key.
    /// </summary>
    /// <param name="payloadIdentity">The existing payload identity.</param>
    /// <param name="scope">The immutable operation reuse scope.</param>
    /// <returns>The source-scoped broker key.</returns>
    private static string BuildActionDetailScopedTranslationKey(
        string payloadIdentity,
        TranslationReuseScope scope)
    {
        return BuildTranslationReuseScopedKey(payloadIdentity, scope);
    }

    /// <summary>
    ///     Dispatches action-detail work through the production scoped prefetch
    ///     orchestrator.
    /// </summary>
    /// <param name="payloadIdentity">The action payload identity.</param>
    /// <param name="sourceLanguage">The operation-captured source contract.</param>
    /// <param name="scope">The operation-captured reuse scope.</param>
    /// <param name="tryGetTranslation">The shared broker cache lookup.</param>
    /// <param name="queueTranslation">The shared broker queue operation.</param>
    /// <param name="resolver">The translation resolver.</param>
    /// <param name="onCompleted">The persistence callback.</param>
    /// <returns>The production dispatch result.</returns>
    internal static PrefetchTranslationDispatchResult
        DispatchActionDetailPrefetchTranslation(
            string payloadIdentity,
            SourceClientLanguage sourceLanguage,
            TranslationReuseScope scope,
            TryGetPrefetchTranslationDelegate tryGetTranslation,
            QueuePrefetchTranslationDelegate queueTranslation,
            Func<string> resolver,
            Action<string, TranslationReuseScope, bool> onCompleted)
    {
        return DispatchScopedPrefetchTranslation(
            BuildActionDetailScopedTranslationKey(payloadIdentity, scope),
            sourceLanguage,
            scope,
            tryGetTranslation,
            queueTranslation,
            resolver,
            onCompleted);
    }

    /// <summary>
    ///     Runs one production ActionDetail operation from canonical persistence
    ///     through name broker completion.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="gameVersion">The captured game version.</param>
    /// <param name="sourceLanguage">The operation-captured source contract.</param>
    /// <param name="scope">The operation-captured reuse scope.</param>
    /// <param name="tryGetTranslation">The shared broker cache lookup.</param>
    /// <param name="queueTranslation">The shared broker queue operation.</param>
    /// <param name="translate">The production translation operation.</param>
    /// <param name="findRow">The production action-row lookup.</param>
    /// <param name="persistRow">The production action-row persistence operation.</param>
    /// <param name="existingRow">The canonical row used by sibling work units.</param>
    /// <returns>The production dispatch result.</returns>
    internal static PrefetchTranslationDispatchResult
        RunActionDetailPrefetchOperationEntry(
            ActionTooltipCanonicalPayload originalPayload,
            string? gameVersion,
            SourceClientLanguage sourceLanguage,
            TranslationReuseScope scope,
            TryGetPrefetchTranslationDelegate tryGetTranslation,
            QueuePrefetchTranslationDelegate queueTranslation,
            ResolvePrefetchTranslationDelegate translate,
            Func<ActionTooltip, ActionTooltip?> findRow,
            Action<ActionTooltip> persistRow,
            out ActionTooltip existingRow)
    {
        if (!IsValidCapturedPrefetchScope(sourceLanguage, scope))
        {
            existingRow = null!;
            return PrefetchTranslationDispatchResult.Rejected;
        }

        var originalRow = ActionTooltipPersistenceHelper.CreateCanonicalRow(
            scope.SourceLanguageCode,
            scope.TargetLanguageCode,
            scope.TranslationEngine!.Value,
            gameVersion,
            originalPayload);
        existingRow = findRow(originalRow) ?? originalRow;
        persistRow(originalRow);

        if (string.IsNullOrWhiteSpace(originalPayload.Name) ||
            !string.IsNullOrWhiteSpace(existingRow.TranslatedActionName))
        {
            return PrefetchTranslationDispatchResult.Rejected;
        }

        return DispatchActionDetailPrefetchTranslation(
            $"ActionDetailPrefetch|{originalPayload.ActionId}|Name|{originalPayload.Name}",
            sourceLanguage,
            scope,
            tryGetTranslation,
            queueTranslation,
            () => translate(
                originalPayload.Name,
                sourceLanguage,
                scope.TargetLanguageCode),
            (translatedName, capturedScope, _) =>
            {
                var translatedRow = CreateActionDetailTranslationRow(
                    originalPayload,
                    gameVersion,
                    capturedScope,
                    findRow,
                    tryGetTranslation,
                    translatedName: translatedName);
                if (translatedRow != null)
                {
                    persistRow(translatedRow);
                }
            });
    }

    /// <summary>
    ///     Adds a complete translation reuse scope to an existing payload key.
    /// </summary>
    /// <param name="payloadIdentity">The existing payload identity.</param>
    /// <param name="scope">The immutable operation reuse scope.</param>
    /// <returns>The source-scoped broker key.</returns>
    private static string BuildTranslationReuseScopedKey(
        string payloadIdentity,
        TranslationReuseScope scope)
    {
        return $"{payloadIdentity}|Scope|{scope.SourceLanguageCode}|{scope.TargetLanguageCode}|{scope.TranslationEngine?.ToString(CultureInfo.InvariantCulture) ?? "<none>"}|{scope.RequireMatchingEngine}";
    }

    /// <summary>
    ///     Routes one captured prefetch operation through cache lookup, broker
    ///     queueing, and completion without rebuilding mutable runtime scope.
    /// </summary>
    /// <param name="translationKey">The complete source-scoped broker key.</param>
    /// <param name="sourceLanguage">The operation-captured source contract.</param>
    /// <param name="scope">The operation-captured reuse scope.</param>
    /// <param name="tryGetTranslation">The shared broker cache lookup.</param>
    /// <param name="queueTranslation">The shared broker queue operation.</param>
    /// <param name="resolver">The translation resolver.</param>
    /// <param name="onCompleted">The persistence callback.</param>
    /// <returns>The production dispatch result.</returns>
    private static PrefetchTranslationDispatchResult
        DispatchScopedPrefetchTranslation(
            string translationKey,
            SourceClientLanguage sourceLanguage,
            TranslationReuseScope scope,
            TryGetPrefetchTranslationDelegate tryGetTranslation,
            QueuePrefetchTranslationDelegate queueTranslation,
            Func<string> resolver,
            Action<string, TranslationReuseScope, bool> onCompleted)
    {
        if (!IsValidCapturedPrefetchScope(sourceLanguage, scope))
        {
            return PrefetchTranslationDispatchResult.Rejected;
        }

        if (tryGetTranslation(translationKey, out var cachedTranslation))
        {
            onCompleted(cachedTranslation, scope, true);
            return PrefetchTranslationDispatchResult.Cached;
        }

        return queueTranslation(
            translationKey,
            resolver,
            translatedText => onCompleted(translatedText, scope, false))
            ? PrefetchTranslationDispatchResult.Queued
            : PrefetchTranslationDispatchResult.AlreadyPending;
    }

    /// <summary>
    ///     Validates one immutable prefetch source and reuse-scope pair.
    /// </summary>
    /// <param name="sourceLanguage">The operation-captured source contract.</param>
    /// <param name="scope">The operation-captured reuse scope.</param>
    /// <returns><see langword="true" /> when the pair is complete and consistent.</returns>
    private static bool IsValidCapturedPrefetchScope(
        SourceClientLanguage sourceLanguage,
        TranslationReuseScope scope)
    {
        return TranslationService.IsKnownCapturedSourceLanguage(sourceLanguage) &&
               RuntimeLanguageHelper.LanguagesMatch(
                   sourceLanguage.PersistenceCode,
                   scope.SourceLanguageCode) &&
               !string.IsNullOrWhiteSpace(scope.TargetLanguageCode) &&
               scope.TranslationEngine.HasValue;
    }

    /// <summary>
    ///     Captures the current target and engine policy around a resolved
    ///     source identity.
    /// </summary>
    /// <param name="sourceLanguage">The resolved source language.</param>
    /// <param name="scope">The complete immutable scope, when available.</param>
    /// <returns><see langword="true" /> when the scope is complete.</returns>
    private bool TryCreateCapturedTranslationScope(
        SourceClientLanguage sourceLanguage,
        out TranslationReuseScope scope)
    {
        var targetLanguage =
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.configuration.Lang);
        if (string.IsNullOrWhiteSpace(sourceLanguage.PersistenceCode) ||
            string.IsNullOrWhiteSpace(sourceLanguage.ProviderCode) ||
            string.IsNullOrWhiteSpace(targetLanguage))
        {
            scope = default;
            return false;
        }

        scope = new TranslationReuseScope(
            sourceLanguage.PersistenceCode,
            targetLanguage,
            this.configuration.ChosenTransEngine,
            this.configuration.TranslateAlreadyTranslatedTexts);
        return true;
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
