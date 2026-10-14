// <copyright file="TraitDetailPrefetchRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using TraitSheet = Lumina.Excel.Sheets.Trait;
using TraitTransientSheet = Lumina.Excel.Sheets.TraitTransient;

using Echoglossian.DBHelpers;
using Echoglossian.Persistence;
using Echoglossian.Translators;

namespace Echoglossian;

/// <summary>
///     Provides DB-first background prefetch for canonical trait payloads.
/// </summary>
public partial class Echoglossian
{
    private const int TraitDetailPrefetchTraitsPerTick = 6;

    private static readonly TimeSpan TraitDetailPrefetchTickInterval =
        TimeSpan.FromSeconds(2);

    private readonly List<uint> traitDetailPrefetchQueue = [];

    private string traitDetailPrefetchSignature = string.Empty;

    private DateTime traitDetailPrefetchLastTickUtc = DateTime.MinValue;

    private int traitDetailPrefetchQueueIndex;

    /// <summary>
    ///     Ticks the trait prefetch runtime so current class/job traits are translated
    ///     into canonical storage ahead of tooltip and ActionMenu use.
    /// </summary>
    private void TickTraitDetailPrefetch()
    {
        if (!this.ShouldPrefetchActionAdjacentCanonicalTooltips() ||
            DateTime.UtcNow - this.traitDetailPrefetchLastTickUtc <
            TraitDetailPrefetchTickInterval)
        {
            return;
        }

        this.traitDetailPrefetchLastTickUtc = DateTime.UtcNow;

        if (!TryGetCurrentClassJobId(out var currentClassJobId) ||
            !TryCollectCurrentClassJobTraitIds(
                currentClassJobId,
                out var traitIds))
        {
            this.ClearTraitDetailPrefetchState();
            return;
        }

        var signature =
            $"{currentClassJobId}|{string.Join(',', traitIds)}";
        if (!string.Equals(
                this.traitDetailPrefetchSignature,
                signature,
                StringComparison.Ordinal))
        {
            this.traitDetailPrefetchSignature = signature;
            this.traitDetailPrefetchQueue.Clear();
            this.traitDetailPrefetchQueue.AddRange(traitIds);
            this.traitDetailPrefetchQueueIndex = 0;
        }

        if (this.traitDetailPrefetchQueueIndex >=
            this.traitDetailPrefetchQueue.Count)
        {
            return;
        }

        this.TickTraitDetailPrefetchQueue(id => this.PrefetchTraitDetail(id, currentClassJobId));
    }

    /// <summary>Advances the captured queue only when ownership does not reject capacity.</summary>
    /// <param name="capture">The Framework capture and admission callback.</param>
    /// <returns>The captures attempted within this tick's existing budget.</returns>
    internal int TickTraitDetailPrefetchQueue(Func<uint, StructuredTooltipOperationAdmission> capture)
    {
        var processedCount = 0;
        while (processedCount < TraitDetailPrefetchTraitsPerTick &&
               this.traitDetailPrefetchQueueIndex < this.traitDetailPrefetchQueue.Count)
        {
            var id = this.traitDetailPrefetchQueue[this.traitDetailPrefetchQueueIndex];
            var admission = capture(id);
            processedCount++;
            if (admission == StructuredTooltipOperationAdmission.RejectedCapacity)
            {
                break;
            }

            this.traitDetailPrefetchQueueIndex++;
        }

        return processedCount;
    }

    /// <summary>
    ///     Clears the trait prefetch runtime state.
    /// </summary>
    private void ClearTraitDetailPrefetchState()
    {
        this.traitDetailPrefetchQueue.Clear();
        this.traitDetailPrefetchQueueIndex = 0;
        this.traitDetailPrefetchSignature = string.Empty;
        this.traitDetailPrefetchLastTickUtc = DateTime.MinValue;
    }


    /// <summary>Captures a canonical sheet payload on the Framework before asynchronous work.</summary>
    /// <param name="traitId">The source row id.</param>
    /// <param name="currentClassJobId">The captured player class/job.</param>
    private StructuredTooltipOperationAdmission PrefetchTraitDetail(uint traitId, byte currentClassJobId)
    {
        if (!TryCapturePrefetchOperationScope(ResolveCurrentPrefetchSourceLanguage, this.configuration, out var source, out var scope) ||
            !TryBuildTraitCanonicalPayload(traitId, currentClassJobId, out var payload))
        {
            return StructuredTooltipOperationAdmission.RejectedCapture;
        }

        return this.StartTraitDetailOperation(payload, source, scope, GetGameVersion(), PersistencePriority.Background);
    }

    /// <summary>Admits one managed snapshot while preserving terminal canonical ownership.</summary>
    /// <param name="payload">The Framework-captured source payload.</param>
    /// <param name="source">The captured provider source.</param>
    /// <param name="scope">The immutable translation policy.</param>
    /// <param name="gameVersion">The captured source version.</param>
    /// <param name="priority">The originating persistence lane.</param>
    private StructuredTooltipOperationAdmission StartTraitDetailOperation(TraitCanonicalPayload payload, SourceClientLanguage source, TranslationReuseScope scope, string? gameVersion, PersistencePriority priority)
    {
        var writer = this.actionItemTooltipPersistenceWriter;
        if (writer == null || writer.ShutdownToken.IsCancellationRequested)
        {
            return StructuredTooltipOperationAdmission.RejectedShutdown;
        }

        var snapshot = payload.Serialize();
        var probe = TraitPersistenceHelper.CreateCanonicalRow(scope.SourceLanguageCode, scope.TargetLanguageCode, scope.TranslationEngine, gameVersion, payload);
        var key = ActionItemTooltipPersistenceWriter.BuildIdentity("Trait", payload.TraitId, scope, gameVersion, probe.SourceContentHash);
        var translationService = TranslationService;
        var resolution = translationService.CaptureTranslatorResolution(scope.TranslationEngine!.Value, TranslationSurfaceGroup.Default);
        var broker = this.queuedTranslationBroker;
        return this.structuredTooltipOperationOwner.TryStart(key, () => CompleteTraitDetailOperationAsync(
            writer, broker, snapshot, probe, scope, priority,
            (text, origin, token) => translationService.TranslateAsync(text, source, scope.TargetLanguageCode,
                TranslationSurfaceGroup.Default, resolution, token, origin)), writer.ShutdownToken).Status;
    }

    /// <summary>Completes captured managed work without consulting game state or mutating native UI.</summary>
    /// <param name="writer">The process-lifetime persistence adapter.</param>
    /// <param name="broker">The existing shared translation broker.</param>
    /// <param name="snapshot">The immutable serialized source payload.</param>
    /// <param name="probe">The operation-owned canonical identity.</param>
    /// <param name="scope">The captured translation policy.</param>
    /// <param name="priority">The originating persistence lane.</param>
    /// <param name="translate">The captured translator operation.</param>
    internal static async Task CompleteTraitDetailOperationAsync(
        ActionItemTooltipPersistenceWriter writer, QueuedTranslationBroker broker, string snapshot,
        Trait probe, TranslationReuseScope scope, PersistencePriority priority,
        Func<string, string, CancellationToken, Task<string>> translate)
    {
        var token = writer.ShutdownToken;
        writer.TryFind(probe, scope, out var read, priority);
        var result = await read.WaitAsync(token).ConfigureAwait(false);
        if (result.Status is not (PersistenceCompletionStatus.Succeeded or PersistenceCompletionStatus.Unchanged))
        {
            return;
        }

        var original = TraitCanonicalPayload.Deserialize(snapshot)!;
        var translated = TraitCanonicalPayload.Deserialize(snapshot)!;
        translated.TranslatedName = string.IsNullOrWhiteSpace(original.Description)
            ? StructuredTooltipTranslationValidation.GetMeaningfulTranslationOrNull(original.Name, result.Value?.TranslatedTraitName)
            : result.Value?.TranslatedTraitName;
        translated.TranslatedDescription = StructuredTooltipTranslationValidation.GetMeaningfulTranslationOrNull(original.Description, result.Value?.TranslatedTraitDescription);
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
            BuildTranslationReuseScopedKey($"TraitDetailPrefetch|{original.TraitId}|Name|{original.Name}", scope),
            original.Name, translated.TranslatedName, BuildTraitDetailOriginContext(original, "Name"), translate, token,
            preserveSourceEquivalent: !string.IsNullOrWhiteSpace(original.Description));
        var description = ResolveStructuredTooltipFieldAsync(
            broker,
            BuildTranslationReuseScopedKey($"TraitDetailPrefetch|{original.TraitId}|Description|{original.Description}", scope),
            original.Description, translated.TranslatedDescription, BuildTraitDetailOriginContext(original, "Description"), translate, token);
        await Task.WhenAll(name, description).ConfigureAwait(false);
        translated.TranslatedName = await name.ConfigureAwait(false);
        translated.TranslatedDescription = await description.ConfigureAwait(false);
        if (!translated.HasCompleteTranslation)
        {
            return;
        }

        var row = TraitPersistenceHelper.CreateCanonicalRow(scope.SourceLanguageCode, scope.TargetLanguageCode,
            scope.TranslationEngine, probe.GameVersion, original, translated);
        writer.TryPersist(row, out var write, priority);
        await write.WaitAsync(token).ConfigureAwait(false);
    }

    /// <summary>
    ///     Builds the stable queued-translation key for one trait-detail name.
    /// </summary>
    /// <param name="payload">The canonical payload.</param>
    /// <returns>The stable queue key.</returns>
    private static string BuildTraitDetailNameTranslationKey(
        TraitCanonicalPayload payload)
    {
        return $"TraitDetailPrefetch|{payload.TraitId}|Name|{payload.Name}";
    }

    /// <summary>
    ///     Builds the stable queued-translation key for one trait-detail description.
    /// </summary>
    /// <param name="payload">The canonical payload.</param>
    /// <returns>The stable queue key.</returns>
    private static string BuildTraitDetailDescriptionTranslationKey(
        TraitCanonicalPayload payload)
    {
        return
            $"TraitDetailPrefetch|{payload.TraitId}|Description|{payload.Description}";
    }

    /// <summary>
    ///     Builds the diagnostic surface identity for one trait-tooltip field.
    /// </summary>
    /// <param name="payload">The canonical trait-tooltip payload.</param>
    /// <param name="fieldName">The translated field name.</param>
    /// <returns>The diagnostic surface identity.</returns>
    private static string BuildTraitDetailOriginContext(
        TraitCanonicalPayload payload,
        string fieldName)
    {
        return $"TraitTooltip/{payload.TraitId}/{fieldName}";
    }

    /// <summary>
    ///     Tries to collect the current class/job trait ids from canonical sheets.
    /// </summary>
    /// <param name="currentClassJobId">The current class-job identifier.</param>
    /// <param name="traitIds">The collected trait ids.</param>
    /// <returns>True when trait ids were collected successfully.</returns>
    private static bool TryCollectCurrentClassJobTraitIds(
        byte currentClassJobId,
        out List<uint> traitIds)
    {
        traitIds = [];

        var traitSheet =
            DManager.GetExcelSheet<TraitSheet>(ClientStateInterface.ClientLanguage);
        if (traitSheet == null)
        {
            return false;
        }

        HashSet<uint> uniqueTraitIds = [];
        foreach (var traitRow in traitSheet)
        {
            if (traitRow.RowId == 0 ||
                string.IsNullOrWhiteSpace(traitRow.Name.ExtractText()))
            {
                continue;
            }

            var matchesClassJob = traitRow.ClassJob.RowId == currentClassJobId;
            var matchesCategory =
                ClassJobCategorySheetHelper.HasClassJob(
                    traitRow.ClassJobCategory.ValueNullable,
                    currentClassJobId);
            if (!matchesClassJob && !matchesCategory)
            {
                continue;
            }

            uniqueTraitIds.Add(traitRow.RowId);
        }

        traitIds = uniqueTraitIds.OrderBy(id => id).ToList();
        return traitIds.Count > 0;
    }

    /// <summary>
    ///     Tries to build one canonical trait payload from sheets.
    /// </summary>
    /// <param name="traitId">The trait row identifier.</param>
    /// <param name="currentClassJobId">The current class-job identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildTraitCanonicalPayload(
        uint traitId,
        byte currentClassJobId,
        out TraitCanonicalPayload payload)
    {
        payload = new TraitCanonicalPayload();

        var traitSheet =
            DManager.GetExcelSheet<TraitSheet>(ClientStateInterface.ClientLanguage);
        var traitTransientSheet =
            DManager.GetExcelSheet<TraitTransientSheet>(
                ClientStateInterface.ClientLanguage);
        if (traitSheet == null ||
            traitTransientSheet == null ||
            !traitSheet.TryGetRow(traitId, out var traitRow))
        {
            return false;
        }

        var description = traitTransientSheet.TryGetRow(traitId, out var transientRow)
            ? EvaluateSheetText(transientRow.Description)
            : string.Empty;
        payload = new TraitCanonicalPayload
        {
            TraitId = traitRow.RowId,
            IconId = (uint)traitRow.Icon,
            ClassJobId = SheetRowIdNormalizationHelper.NormalizeWithFallback(
                traitRow.ClassJob.RowId,
                currentClassJobId),
            ClassJobCategoryId = SheetRowIdNormalizationHelper.NormalizeOrZero(
                traitRow.ClassJobCategory.RowId),
            Name = traitRow.Name.ExtractText(),
            Description = description,
        };

        return !string.IsNullOrWhiteSpace(payload.Name);
    }
}
