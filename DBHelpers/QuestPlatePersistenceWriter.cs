// <copyright file="QuestPlatePersistenceWriter.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models.Journal;
using Echoglossian.NativeUI.Helpers;
using Echoglossian.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Echoglossian.DBHelpers;

/// <summary>
///     Schedules cache-first QuestPlate reads through the shared persistence
///     coordinator. Worker payloads are managed QuestPlate clones only.
/// </summary>
internal sealed class QuestPlatePersistenceWriter
{
    private const string ReadDomain = "quest-plate-read";
    private readonly IPersistenceCoordinator coordinator;
    private readonly QuestPlateRuntimeCache cache;
    private readonly CancellationTokenSource cancellation = new();
    private int publicationEnabled = 1;

    /// <summary>Initializes a new instance of the writer.</summary>
    /// <param name="coordinator">The shared process-lifetime coordinator.</param>
    /// <param name="cache">The shared committed projection registry.</param>
    internal QuestPlatePersistenceWriter(IPersistenceCoordinator coordinator, QuestPlateRuntimeCache cache)
    {
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>Stops cache publication for the owning runtime generation.</summary>
    internal void DisablePublication()
    {
        Interlocked.Exchange(ref this.publicationEnabled, 0);
        _ = this.cache.AdvanceGeneration();
        this.cancellation.Cancel();
    }

    /// <summary>Attempts a non-blocking cache-first QuestPlate lookup.</summary>
    /// <param name="probe">A game-thread-captured managed lookup payload.</param>
    /// <param name="scope">The captured translation reuse scope.</param>
    /// <param name="priority">The coordinator admission priority.</param>
    /// <param name="completion">The terminal operation completion.</param>
    /// <returns>The non-blocking admission outcome.</returns>
    internal PersistenceAdmissionStatus TryFind(
        QuestPlate probe,
        TranslationReuseScope scope,
        PersistencePriority priority,
        out Task<PersistenceReadResult<QuestPlate?>> completion)
    {
        ArgumentNullException.ThrowIfNull(probe);
        var capturedProbe = probe.Clone();
        var key = QuestPlateRuntimeCache.CreateKey(capturedProbe, scope);
        if (this.cache.TryGet(capturedProbe, scope, out var cached))
        {
            completion = Task.FromResult(new PersistenceReadResult<QuestPlate?>(PersistenceCompletionStatus.Succeeded, cached, null));
            return PersistenceAdmissionStatus.Joined;
        }

        var gate = new TaskCompletionSource<QuestPlateRuntimeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!this.cache.TryRegisterOrJoin(key, gate, out var generation, out var existing))
        {
            completion = existing!.ContinueWith(
                static task => new PersistenceReadResult<QuestPlate?>(task.Result.Status, task.Result.Projection, null),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return PersistenceAdmissionStatus.Joined;
        }

        var status = this.coordinator.TryScheduleRead(
            new PersistenceWorkKey(ReadDomain, key.Value),
            priority,
            async (context, token) =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, this.cancellation.Token);
                var candidates = await QuestPlatePersistencePolicy.LoadReadCandidatesAsync(context, capturedProbe, scope, linked.Token).ConfigureAwait(false);
                return QuestPlatePersistencePolicy.SelectForRead(candidates, capturedProbe, scope)?.Clone();
            },
            projection =>
            {
                // The cache is completed only by the terminal continuation below.
            },
            out completion);

        _ = completion.ContinueWith(
            task =>
            {
                var result = task.Result;
                var projection = result.Status == PersistenceCompletionStatus.Succeeded ? result.Value?.Clone() : null;
                var terminal = new QuestPlateRuntimeResult(result.Status, projection);
                if (Volatile.Read(ref this.publicationEnabled) != 0)
                {
                    _ = this.cache.Complete(key, generation, terminal);
                }

                _ = gate.TrySetResult(terminal);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return status;
    }

    /// <summary>Attempts a non-blocking coordinator-backed QuestPlate upsert.</summary>
    internal PersistenceAdmissionStatus TryPersist(
        QuestPlate plate,
        TranslationReuseScope scope,
        PersistencePriority priority,
        out Task<PersistenceWriteResult> completion)
    {
        ArgumentNullException.ThrowIfNull(plate);
        var captured = plate.Clone();
        var key = QuestPlateRuntimeCache.CreateKey(captured, scope);
        var gate = new TaskCompletionSource<QuestPlateRuntimeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!this.cache.TryRegisterWriteOrJoin(key, gate, out var generation, out var existing, out var readCompletion))
        {
            completion = existing!.ContinueWith(
                static task => new PersistenceWriteResult(task.Result.Status, 0, null),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return PersistenceAdmissionStatus.Joined;
        }

        if (readCompletion is not null)
        {
            completion = gate.Task.ContinueWith(
                static task => new PersistenceWriteResult(task.Result.Status, 0, null),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            _ = this.ScheduleDeferredPersistAsync(key, generation, gate, readCompletion, captured, priority);
            return PersistenceAdmissionStatus.Joined;
        }

        return this.SchedulePersist(key, generation, gate, captured, priority, out completion);
    }

    private async Task ScheduleDeferredPersistAsync(
        QuestPlateRuntimeKey key,
        long generation,
        TaskCompletionSource<QuestPlateRuntimeResult> gate,
        Task<QuestPlateRuntimeResult> readCompletion,
        QuestPlate captured,
        PersistencePriority priority)
    {
        _ = await readCompletion.ConfigureAwait(false);
        if (!this.cache.TryPromoteDeferredWrite(key, generation, gate))
        {
            _ = gate.TrySetResult(new QuestPlateRuntimeResult(PersistenceCompletionStatus.Cancelled, null));
            return;
        }

        _ = this.SchedulePersist(key, generation, gate, captured, priority, out _);
    }

    private PersistenceAdmissionStatus SchedulePersist(
        QuestPlateRuntimeKey key,
        long generation,
        TaskCompletionSource<QuestPlateRuntimeResult> gate,
        QuestPlate captured,
        PersistencePriority priority,
        out Task<PersistenceWriteResult> completion)
    {

        QuestPlate? persisted = null;
        var status = this.coordinator.TryScheduleWrite(
            new PersistenceWriteRequest(
                new PersistenceWorkKey("quest-plate", key.Value),
                priority,
                async (context, token) =>
                {
                    var candidates = await QuestPlatePersistencePolicy.LoadSaveCandidatesAsync(context, captured, token).ConfigureAwait(false);
                    var existing = QuestPlatePersistencePolicy.SelectForSave(candidates, captured);
                    if (existing is null)
                    {
                        captured.UpdateFieldsAsText();
                        context.QuestPlate.Add(captured);
                        persisted = captured.Clone();
                        return PersistenceWriteMutation.ChangedResult;
                    }

                    Echoglossian.MergeQuestPlateValues(existing, captured);
                    existing.UpdatedDate = DateTime.Now;
                    existing.UpdateFieldsAsText();
                    persisted = existing.Clone();
                    return PersistenceWriteMutation.ChangedResult;
                },
                () =>
                {
                    // Completion below publishes only after this committed callback.
                }) { CancellationToken = this.cancellation.Token },
            out completion);

        _ = completion.ContinueWith(
            task =>
            {
                var result = task.Result;
                var projection = result.Status is PersistenceCompletionStatus.Succeeded or PersistenceCompletionStatus.Unchanged
                    ? persisted?.Clone()
                    : null;
                var terminal = new QuestPlateRuntimeResult(result.Status, projection);
                if (Volatile.Read(ref this.publicationEnabled) != 0)
                {
                    _ = this.cache.Complete(key, generation, terminal);
                }

                _ = gate.TrySetResult(terminal);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return status;
    }
}

/// <summary>Contains pure QuestPlate read candidate and selection policy.</summary>
internal static class QuestPlatePersistencePolicy
{
    /// <summary>Selects a runtime row with the legacy QuestId/message/name fallback order.</summary>
    internal static QuestPlate? SelectForRead(
        IEnumerable<QuestPlate> candidates,
        QuestPlate probe,
        TranslationReuseScope scope)
    {
        var hasQuestId = !string.IsNullOrWhiteSpace(probe.QuestId);
        if (hasQuestId)
        {
            var byId = Echoglossian.SelectPreferredQuestPlate(candidates.Where(row => row.QuestId == probe.QuestId), probe, scope);
            if (byId is not null)
            {
                return byId;
            }
        }

        if (!string.IsNullOrWhiteSpace(probe.OriginalQuestMessage))
        {
            var messageCandidates = candidates.Where(row =>
                row.QuestName == probe.QuestName &&
                row.OriginalQuestMessage == probe.OriginalQuestMessage &&
                (!hasQuestId || row.QuestId == probe.QuestId || string.IsNullOrEmpty(row.QuestId)));
            var byMessage = Echoglossian.SelectPreferredQuestPlate(messageCandidates, probe, scope);
            if (byMessage is not null)
            {
                return byMessage;
            }
        }

        return hasQuestId
            ? null
            : Echoglossian.SelectPreferredQuestPlate(candidates.Where(row => row.QuestName == probe.QuestName), probe, scope);
    }

    /// <summary>Loads the finite candidate set needed by the legacy read order.</summary>
    internal static Task<List<QuestPlate>> LoadReadCandidatesAsync(EchoglossianDbContext context, QuestPlate probe, TranslationReuseScope scope, CancellationToken cancellationToken)
    {
        return context.QuestPlate.AsNoTracking()
            .Where(row => row.TranslationLang == scope.TargetLanguageCode &&
                ((probe.QuestId != null && row.QuestId == probe.QuestId) ||
                 (probe.QuestName != null && row.QuestName == probe.QuestName)))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Loads the complete finite candidate set used by legacy save lookup.</summary>
    internal static Task<List<QuestPlate>> LoadSaveCandidatesAsync(EchoglossianDbContext context, QuestPlate probe, CancellationToken cancellationToken)
    {
        var query = context.QuestPlate.Where(row =>
            row.TranslationEngine == probe.TranslationEngine &&
            ((probe.QuestId != null && row.QuestId == probe.QuestId) ||
             (probe.QuestName != null && row.QuestName == probe.QuestName)));
        if (!string.IsNullOrWhiteSpace(probe.GameVersion))
        {
            query = query.Where(row => row.GameVersion == probe.GameVersion);
        }

        return query.ToListAsync(cancellationToken);
    }

    /// <summary>Selects a row using the established QuestId, message, name order.</summary>
    internal static QuestPlate? SelectForSave(IEnumerable<QuestPlate> candidates, QuestPlate probe)
    {
        var hasQuestId = !string.IsNullOrWhiteSpace(probe.QuestId);
        var matching = candidates.Where(row => row.TranslationEngine == probe.TranslationEngine);
        if (hasQuestId)
        {
            var byId = Echoglossian.SelectPreferredQuestPlateForSave(matching.Where(row => row.QuestId == probe.QuestId), probe);
            if (byId is not null) return byId;
        }

        if (!string.IsNullOrWhiteSpace(probe.OriginalQuestMessage))
        {
            var byMessage = Echoglossian.SelectPreferredQuestPlateForSave(matching.Where(row => row.QuestName == probe.QuestName && row.OriginalQuestMessage == probe.OriginalQuestMessage && (!hasQuestId || string.IsNullOrEmpty(row.QuestId) || row.QuestId == probe.QuestId)), probe);
            if (byMessage is not null) return byMessage;
        }

        return Echoglossian.SelectPreferredQuestPlateForSave(matching.Where(row => row.QuestName == probe.QuestName && (!hasQuestId || string.IsNullOrEmpty(row.QuestId) || row.QuestId == probe.QuestId)), probe);
    }

    internal static bool Equivalent(QuestPlate left, QuestPlate right)
    {
        left.UpdateFieldsAsText();
        right.UpdateFieldsAsText();
        return left.QuestId == right.QuestId && left.QuestName == right.QuestName && left.OriginalQuestMessage == right.OriginalQuestMessage && left.OriginalLang == right.OriginalLang && left.TranslatedQuestName == right.TranslatedQuestName && left.TranslatedQuestMessage == right.TranslatedQuestMessage && left.TranslationLang == right.TranslationLang && left.TranslationEngine == right.TranslationEngine && left.GameVersion == right.GameVersion && left.QuestTextSheetName == right.QuestTextSheetName && left.SourceContentHash == right.SourceContentHash && left.CanonicalRowsAsText == right.CanonicalRowsAsText && left.ObjectivesAsText == right.ObjectivesAsText && left.TranslatedObjectivesAsText == right.TranslatedObjectivesAsText && left.SummariesAsText == right.SummariesAsText && left.TranslatedSummariesAsText == right.TranslatedSummariesAsText && left.SystemRowsAsText == right.SystemRowsAsText && left.TranslatedSystemRowsAsText == right.TranslatedSystemRowsAsText;
    }

    private static QuestPlate? SelectPreferred(IEnumerable<QuestPlate> candidates, QuestPlate probe, TranslationReuseScope scope)
    {
        return candidates
            .Where(row => scope.Matches(row.OriginalLang, row.TranslationLang, row.TranslationEngine) && RuntimeLanguageHelper.LanguagesMatch(row.OriginalLang, probe.OriginalLang) && (string.IsNullOrWhiteSpace(probe.SourceContentHash) || string.Equals(row.SourceContentHash, probe.SourceContentHash, StringComparison.Ordinal)))
            .OrderByDescending(row => IdentityScore(row, probe))
            .ThenByDescending(CompletenessScore)
            .ThenByDescending(row => row.UpdatedDate ?? row.CreatedDate ?? DateTime.MinValue)
            .ThenByDescending(row => row.Id)
            .FirstOrDefault();
    }

    private static int IdentityScore(QuestPlate row, QuestPlate probe)
    {
        var score = 0;
        score += !string.IsNullOrWhiteSpace(probe.QuestId) && row.QuestId == probe.QuestId ? 256 : 0;
        score += !string.IsNullOrWhiteSpace(probe.SourceContentHash) && row.SourceContentHash == probe.SourceContentHash ? 128 : 0;
        score += !string.IsNullOrWhiteSpace(probe.QuestTextSheetName) && row.QuestTextSheetName == probe.QuestTextSheetName ? 64 : 0;
        score += !string.IsNullOrWhiteSpace(probe.GameVersion) && row.GameVersion == probe.GameVersion ? 32 : 0;
        score += !string.IsNullOrWhiteSpace(probe.OriginalQuestMessage) && row.OriginalQuestMessage == probe.OriginalQuestMessage ? 16 : 0;
        score += !string.IsNullOrWhiteSpace(probe.QuestName) && row.QuestName == probe.QuestName ? 8 : 0;
        score += !string.IsNullOrWhiteSpace(row.QuestId) ? 4 : 0;
        score += !string.IsNullOrWhiteSpace(row.SourceContentHash) ? 2 : 0;
        return score + (!string.IsNullOrWhiteSpace(row.QuestTextSheetName) ? 1 : 0);
    }

    private static int CompletenessScore(QuestPlate row)
    {
        row.UpdateFieldsFromText();
        var score = 0;
        score += !string.IsNullOrWhiteSpace(row.TranslatedQuestName) ? 64 : 0;
        score += !string.IsNullOrWhiteSpace(row.TranslatedQuestMessage) ? 64 : 0;
        score += Math.Min(row.TranslatedObjectiveRowsByKey.Count, 32) * 4;
        score += Math.Min(row.TranslatedSummaryRowsByKey.Count, 32) * 4;
        score += Math.Min(row.TranslatedSystemRowsByKey.Count, 32) * 4;
        score += Math.Min(row.CanonicalRows.Count, 32) * 2;
        score += !string.IsNullOrWhiteSpace(row.QuestTextSheetName) ? 8 : 0;
        score += !string.IsNullOrWhiteSpace(row.SourceContentHash) ? 8 : 0;
        return score;
    }
}
