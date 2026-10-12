// <copyright file="ActionDetailPrefetchRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.DBHelpers;
using Echoglossian.Persistence;
using Echoglossian.Translators;
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
/// <param name="originContext">The surface and element context for diagnostics.</param>
/// <returns>The translated text.</returns>
internal delegate string ResolvePrefetchTranslationDelegate(
    string sourceText,
    SourceClientLanguage sourceLanguage,
    string targetLanguage,
    string originContext);

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
public partial class Echoglossian
{
    private readonly StructuredTooltipOperationOwner structuredTooltipOperationOwner = new();

    private const int ActionDetailPrefetchActionsPerTick = 6;

    private static readonly TimeSpan ActionDetailPrefetchTickInterval =
        TimeSpan.FromSeconds(2);

    private readonly List<uint> actionDetailPrefetchQueue = [];

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
        this.actionDetailPrefetchQueueIndex = 0;
        this.actionDetailPrefetchSignature = string.Empty;
        this.actionDetailPrefetchLastTickUtc = DateTime.MinValue;
    }


    /// <summary>Captures a canonical sheet payload on the Framework before asynchronous work.</summary>
    /// <param name="actionId">The source row id.</param>
    /// <param name="currentClassJobId">The captured player class/job.</param>
    private void PrefetchActionDetail(uint actionId, byte currentClassJobId)
    {
        if (!TryCapturePrefetchOperationScope(ResolveCurrentPrefetchSourceLanguage, this.configuration, out var source, out var scope) ||
            !TryBuildActionTooltipCanonicalPayload(actionId, currentClassJobId, out var payload))
        {
            return;
        }

        this.StartActionDetailOperation(payload, source, scope, GetGameVersion(), PersistencePriority.Background);
    }

    /// <summary>Admits one managed snapshot while preserving terminal canonical ownership.</summary>
    /// <param name="payload">The Framework-captured source payload.</param>
    /// <param name="source">The captured provider source.</param>
    /// <param name="scope">The immutable translation policy.</param>
    /// <param name="gameVersion">The captured source version.</param>
    /// <param name="priority">The originating persistence lane.</param>
    private void StartActionDetailOperation(ActionTooltipCanonicalPayload payload, SourceClientLanguage source, TranslationReuseScope scope, string? gameVersion, PersistencePriority priority)
    {
        var writer = this.actionItemTooltipPersistenceWriter;
        if (writer == null || writer.ShutdownToken.IsCancellationRequested)
        {
            return;
        }

        var snapshot = payload.Serialize();
        var probe = ActionTooltipPersistenceHelper.CreateCanonicalRow(scope.SourceLanguageCode, scope.TargetLanguageCode, scope.TranslationEngine, gameVersion, payload);
        var key = ActionItemTooltipPersistenceWriter.BuildIdentity("ActionTooltip", payload.ActionId, scope, gameVersion, probe.SourceContentHash);
        var translationService = TranslationService;
        var resolution = translationService.CaptureTranslatorResolution(scope.TranslationEngine!.Value, TranslationSurfaceGroup.Default);
        var broker = this.queuedTranslationBroker;
        this.structuredTooltipOperationOwner.TryStart(key, () => CompleteActionDetailOperationAsync(
            writer, broker, snapshot, probe, scope, priority,
            (text, origin, token) => translationService.TranslateAsync(text, source, scope.TargetLanguageCode,
                TranslationSurfaceGroup.Default, resolution, token, origin)), writer.ShutdownToken);
    }

    /// <summary>Completes captured managed work without consulting game state or mutating native UI.</summary>
    /// <param name="writer">The process-lifetime persistence adapter.</param>
    /// <param name="broker">The existing shared translation broker.</param>
    /// <param name="snapshot">The immutable serialized source payload.</param>
    /// <param name="probe">The operation-owned canonical identity.</param>
    /// <param name="scope">The captured translation policy.</param>
    /// <param name="priority">The originating persistence lane.</param>
    /// <param name="translate">The captured translator operation.</param>
    internal static async Task CompleteActionDetailOperationAsync(
        ActionItemTooltipPersistenceWriter writer, QueuedTranslationBroker broker, string snapshot,
        ActionTooltip probe, TranslationReuseScope scope, PersistencePriority priority,
        Func<string, string, CancellationToken, Task<string>> translate)
    {
        var token = writer.ShutdownToken;
        writer.TryFind(probe, scope, out var read, priority);
        var result = await read.WaitAsync(token).ConfigureAwait(false);
        if (result.Status is not (PersistenceCompletionStatus.Succeeded or PersistenceCompletionStatus.Unchanged))
        {
            return;
        }

        var original = ActionTooltipCanonicalPayload.Deserialize(snapshot)!;
        var translated = ActionTooltipCanonicalPayload.Deserialize(snapshot)!;
        translated.TranslatedName = string.IsNullOrWhiteSpace(original.Description)
            ? StructuredTooltipTranslationValidation.GetMeaningfulTranslationOrNull(original.Name, result.Value?.TranslatedActionName)
            : result.Value?.TranslatedActionName;
        translated.TranslatedDescription = StructuredTooltipTranslationValidation.GetMeaningfulTranslationOrNull(original.Description, result.Value?.TranslatedActionDescription);
        if (translated.HasCompleteTranslation)
        {
            return;
        }

        // Preserve original-row registration, but let the coordinator suppress unchanged writes.
        writer.TryPersist(probe, out var registration, priority);
        var registered = await registration.WaitAsync(token).ConfigureAwait(false);
        if (registered.Status is not (PersistenceCompletionStatus.Succeeded or PersistenceCompletionStatus.Unchanged))
        {
            return;
        }

        var name = ResolveStructuredTooltipFieldAsync(
            broker,
            BuildTranslationReuseScopedKey($"ActionDetailPrefetch|{original.ActionId}|Name|{original.Name}", scope),
            original.Name, translated.TranslatedName, $"ActionTooltip/{original.ActionId}/Name", translate, token,
            preserveSourceEquivalent: !string.IsNullOrWhiteSpace(original.Description));
        var description = ResolveStructuredTooltipFieldAsync(
            broker,
            BuildTranslationReuseScopedKey($"ActionDetailPrefetch|{original.ActionId}|Description|{original.Description}", scope),
            original.Description, translated.TranslatedDescription, $"ActionTooltip/{original.ActionId}/Description", translate, token);
        await Task.WhenAll(name, description).ConfigureAwait(false);
        translated.TranslatedName = await name.ConfigureAwait(false);
        translated.TranslatedDescription = await description.ConfigureAwait(false);
        if (!translated.HasCompleteTranslation)
        {
            return;
        }

        var row = ActionTooltipPersistenceHelper.CreateCanonicalRow(scope.SourceLanguageCode, scope.TargetLanguageCode,
            scope.TranslationEngine, probe.GameVersion, original, translated);
        writer.TryPersist(row, out var write, priority);
        await write.WaitAsync(token).ConfigureAwait(false);
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
        translatedPayload.TranslatedName =
            StructuredTooltipTranslationValidation
                .GetMeaningfulTranslationOrNull(
                    originalPayload.Name,
                    translatedPayload.TranslatedName);
        translatedPayload.TranslatedDescription =
            StructuredTooltipTranslationValidation
                .GetMeaningfulTranslationOrNull(
                    originalPayload.Description,
                    translatedPayload.TranslatedDescription);
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
    ///     Captures live scope and runs one production ActionDetail operation
    ///     from canonical persistence through name broker completion.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="gameVersion">The captured game version.</param>
    /// <param name="sourceLanguageResolver">Resolves the live client source.</param>
    /// <param name="configuration">The live translation configuration.</param>
    /// <param name="tryGetTranslation">The shared broker cache lookup.</param>
    /// <param name="queueTranslation">The shared broker queue operation.</param>
    /// <param name="translate">The production translation operation.</param>
    /// <param name="findRow">The production action-row lookup.</param>
    /// <param name="persistRow">The production action-row persistence operation.</param>
    /// <param name="sourceLanguage">The captured source contract.</param>
    /// <param name="scope">The captured reuse scope.</param>
    /// <param name="existingRow">The canonical row used by sibling work units.</param>
    /// <returns>The production dispatch result.</returns>
    internal static PrefetchTranslationDispatchResult
        RunActionDetailPrefetchOperationEntry(
            ActionTooltipCanonicalPayload originalPayload,
            string? gameVersion,
            Func<SourceClientLanguage?> sourceLanguageResolver,
            Config configuration,
            TryGetPrefetchTranslationDelegate tryGetTranslation,
            QueuePrefetchTranslationDelegate queueTranslation,
            ResolvePrefetchTranslationDelegate translate,
            Func<ActionTooltip, ActionTooltip?> findRow,
            Action<ActionTooltip> persistRow,
            out SourceClientLanguage sourceLanguage,
            out TranslationReuseScope scope,
            out ActionTooltip existingRow)
    {
        if (!TryCapturePrefetchOperationScope(
                sourceLanguageResolver,
                configuration,
                out sourceLanguage,
                out scope))
        {
            existingRow = null!;
            return PrefetchTranslationDispatchResult.Rejected;
        }

        return RunActionDetailPrefetchOperationEntry(
            originalPayload,
            gameVersion,
            sourceLanguage,
            scope,
            tryGetTranslation,
            queueTranslation,
            translate,
            findRow,
            persistRow,
            out existingRow);
    }

    /// <summary>
    ///     Runs one captured ActionDetail operation from canonical persistence
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
    private static PrefetchTranslationDispatchResult
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
                scope.TargetLanguageCode,
                BuildActionDetailOriginContext(originalPayload, "Name")),
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
    ///     Resolves the current live client source for a prefetch operation.
    /// </summary>
    /// <returns>The resolved source contract, or <see langword="null" />.</returns>
    private static SourceClientLanguage? ResolveCurrentPrefetchSourceLanguage()
    {
        return RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage)
            ? sourceLanguage
            : null;
    }

    /// <summary>
    ///     Builds the diagnostic surface identity for one action-tooltip field.
    /// </summary>
    /// <param name="payload">The canonical action-tooltip payload.</param>
    /// <param name="fieldName">The translated field name.</param>
    /// <returns>The diagnostic surface identity.</returns>
    private static string BuildActionDetailOriginContext(
        ActionTooltipCanonicalPayload payload,
        string fieldName)
    {
        return $"ActionTooltip/{payload.ActionId}/{fieldName}";
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
    ///     Captures one immutable prefetch scope from the live source resolver
    ///     and translation configuration.
    /// </summary>
    /// <param name="sourceLanguageResolver">Resolves the live client source.</param>
    /// <param name="configuration">The live translation configuration.</param>
    /// <param name="sourceLanguage">The captured source contract.</param>
    /// <param name="scope">The captured translation reuse scope.</param>
    /// <returns><see langword="true" /> when the live scope is complete.</returns>
    private static bool TryCapturePrefetchOperationScope(
        Func<SourceClientLanguage?> sourceLanguageResolver,
        Config configuration,
        out SourceClientLanguage sourceLanguage,
        out TranslationReuseScope scope)
    {
        var resolvedSourceLanguage = sourceLanguageResolver();
        if (!resolvedSourceLanguage.HasValue)
        {
            sourceLanguage = default;
            scope = default;
            return false;
        }

        sourceLanguage = resolvedSourceLanguage.Value;
        var targetLanguage =
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                configuration.Lang);
        scope = new TranslationReuseScope(
            sourceLanguage.PersistenceCode,
            targetLanguage,
            configuration.ChosenTransEngine,
            configuration.TranslateAlreadyTranslatedTexts);
        if (IsValidCapturedPrefetchScope(sourceLanguage, scope))
        {
            return true;
        }

        sourceLanguage = default;
        scope = default;
        return false;
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
        return EvaluateStructuredTooltipSourceText(text.AsSpan());
    }

    /// <summary>
    ///     Tries to resolve the current class/job id.
    /// </summary>
    /// <param name="currentClassJobId">The current class/job id.</param>
    /// <returns>True when the current class/job was resolved.</returns>
    private static unsafe bool TryGetCurrentClassJobId(out byte currentClassJobId)
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

    /// <summary>Resolves one source-scoped field through the existing broker, observing terminal failure.</summary>
    /// <param name="broker">The shared broker.</param>
    /// <param name="key">The source-scoped field identity.</param>
    /// <param name="original">The captured source text.</param>
    /// <param name="existing">The persisted usable translation.</param>
    /// <param name="origin">The canonical diagnostic origin.</param>
    /// <param name="translate">The captured translator.</param>
    /// <param name="token">The plugin lifetime.</param>
    /// <param name="preserveSourceEquivalent">Whether an unchanged name can accompany a translated description.</param>
    /// <returns>The usable translated field or null.</returns>
    private static async Task<string?> ResolveStructuredTooltipFieldAsync(
        QueuedTranslationBroker broker, string key, string? original, string? existing, string origin,
        Func<string, string, CancellationToken, Task<string>> translate, CancellationToken token, bool preserveSourceEquivalent = false)
    {
        if (string.IsNullOrWhiteSpace(original) || !string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        if (broker.TryGetCached(key, out var cached))
        {
            return preserveSourceEquivalent ? cached : StructuredTooltipTranslationValidation.GetMeaningfulTranslationOrNull(original, cached);
        }

        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var admission = broker.TryQueueOrJoin(key, async () =>
        {
            var value = await translate(original, origin, token).ConfigureAwait(false);
            return preserveSourceEquivalent
                ? value
                : StructuredTooltipTranslationValidation.GetMeaningfulTranslationOrNull(original, value) ?? string.Empty;
        }, value => completion.TrySetResult(value), origin,
            _ => completion.TrySetResult(null), retainExisting: true, tryRetainOnJoin: () => true);
        if (admission is not (QueuedTranslationAdmission.Accepted or QueuedTranslationAdmission.JoinedInFlight))
        {
            return null;
        }

        return await completion.Task.WaitAsync(token).ConfigureAwait(false);
    }
}
