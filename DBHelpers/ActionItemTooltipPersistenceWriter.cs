// <copyright file="ActionItemTooltipPersistenceWriter.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Persistence;
using Newtonsoft.Json;

namespace Echoglossian.DBHelpers;

/// <summary>Adapts canonical tooltip persistence to the shared bounded coordinator.</summary>
internal sealed class ActionItemTooltipPersistenceWriter
{
    private readonly IPersistenceCoordinator coordinator;
    private readonly CancellationTokenSource cancellation = new();
    private readonly object publicationGate = new();
    private readonly Dictionary<PersistenceWorkKey, Task<PersistenceWriteResult>> writes = new();
    private bool publicationEnabled = true;

    /// <summary>Initializes the process-lifetime persistence adapter.</summary>
    /// <param name="coordinator">The shared coordinator.</param>
    internal ActionItemTooltipPersistenceWriter(IPersistenceCoordinator coordinator)
    {
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    /// <summary>Gets cancellation for this plugin lifetime.</summary>
    internal CancellationToken ShutdownToken => this.cancellation.Token;

    /// <summary>Prevents stale publication and cancels accepted work on unload.</summary>
    internal void DisablePublication()
    {
        lock (this.publicationGate)
        {
            this.publicationEnabled = false;
        }

        this.cancellation.Cancel();
    }

    /// <summary>Builds a collision-safe canonical operation identity.</summary>
    /// <param name="kind">The canonical family.</param>
    /// <param name="id">The content id.</param>
    /// <param name="scope">The captured reuse scope.</param>
    /// <param name="version">The captured version.</param>
    /// <param name="hash">The captured source hash.</param>
    /// <returns>The normalized identity.</returns>
    internal static string BuildIdentity(string kind, uint id, TranslationReuseScope scope, string? version, string? hash)
    {
        return JsonConvert.SerializeObject(new
        {
            Kind = kind, Id = id,
            Source = RuntimeLanguageHelper.NormalizeLanguage(scope.SourceLanguageCode)?.ToLowerInvariant(),
            Target = RuntimeLanguageHelper.NormalizeLanguage(scope.TargetLanguageCode)?.ToLowerInvariant(),
            Engine = scope.RequireMatchingEngine ? scope.TranslationEngine?.ToString() ?? "<none>" : "*",
            Version = version, Hash = hash,
        });
    }

    /// <summary>Publishes successful managed state atomically with the unload gate.</summary>
    /// <param name="publish">The cache update.</param>
    private void Publish(Action publish)
    {
        lock (this.publicationGate)
        {
            if (this.publicationEnabled)
            {
                publish();
            }
        }
    }

    /// <summary>Returns a detached current-version cache hit without database admission.</summary>
    /// <param name="probe">The captured identity.</param>
    /// <param name="scope">The reuse policy.</param>
    /// <returns>The matching cached row.</returns>
    internal static ActionTooltip? FindCached(ActionTooltip probe, TranslationReuseScope scope)
    {
        var row = ActionTooltipCacheManager.TryFindCanonicalMatch(probe.ActionId, scope, probe.GameVersion, probe.SourceContentHash!);
        return row == null ? null : ActionTooltipPersistenceHelper.Clone(row);
    }

    /// <summary>Admits a nonblocking canonical read, with historical promotion after commit.</summary>
    /// <param name="probe">The captured identity.</param>
    /// <param name="scope">The reuse policy.</param>
    /// <param name="completion">The terminal read result.</param>
    /// <param name="priority">The caller's admission lane.</param>
    /// <returns>The admission result.</returns>
    internal PersistenceAdmissionStatus TryFind(ActionTooltip probe, TranslationReuseScope scope, out Task<PersistenceReadResult<ActionTooltip?>> completion, PersistencePriority priority = PersistencePriority.Background)
    {
        var snapshot = ActionTooltipPersistenceHelper.Clone(probe);
        var cached = FindCached(snapshot, scope);
        var historical = GameVersionLookupHelper.HasRequestedVersion(snapshot.GameVersion)
            ? ActionTooltipCacheManager.TryFindHistoricalCanonicalMatch(snapshot.ActionId, scope, snapshot.GameVersion, snapshot.SourceContentHash!)
            : null;
        if (cached != null && (ActionTooltipPersistenceHelper.HasTranslation(cached) || historical == null || !ActionTooltipPersistenceHelper.HasTranslation(historical)))
        {
            completion = Task.FromResult(new PersistenceReadResult<ActionTooltip?>(PersistenceCompletionStatus.Succeeded, cached, null));
            return PersistenceAdmissionStatus.Accepted;
        }

        if (historical != null && ActionTooltipPersistenceHelper.HasTranslation(historical))
        {
            var promoted = ActionTooltipPersistenceHelper.Clone(historical);
            promoted.Id = 0;
            promoted.GameVersion = snapshot.GameVersion;
            var admission = this.TryPersist(promoted, out var write, priority);
            completion = this.CompletePromotionAsync(promoted, scope, write);
            return admission;
        }

        return this.coordinator.TryScheduleRead(
            new PersistenceWorkKey("ActionTooltip-read", BuildIdentity("ActionTooltip", snapshot.ActionId, scope, snapshot.GameVersion, snapshot.SourceContentHash)),
            priority,
            async (context, token) =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, this.ShutdownToken);
                return await ActionTooltipPersistenceHelper.FindAsync(context, snapshot, scope, linked.Token).ConfigureAwait(false);
            },
            row =>
            {
                if (row != null)
                {
                    this.Publish(() => ActionTooltipCacheManager.Update(ActionTooltipPersistenceHelper.Clone(row)));
                }
            },
            out completion);
    }

    /// <summary>Releases a historical projection only after its canonical promotion commits.</summary>
    /// <param name="promoted">The promotion identity.</param>
    /// <param name="scope">The reuse policy.</param>
    /// <param name="write">The coordinator completion.</param>
    /// <returns>The committed cached row or a terminal failure.</returns>
    private async Task<PersistenceReadResult<ActionTooltip?>> CompletePromotionAsync(ActionTooltip promoted, TranslationReuseScope scope, Task<PersistenceWriteResult> write)
    {
        var result = await write.ConfigureAwait(false);
        return new PersistenceReadResult<ActionTooltip?>(
            result.Status,
            result.Status is PersistenceCompletionStatus.Succeeded or PersistenceCompletionStatus.Unchanged && !this.ShutdownToken.IsCancellationRequested
                ? FindCached(promoted, scope) : null, result.Error);
    }

    /// <summary>Schedules a captured canonical merge and publishes only after commit.</summary>
    /// <param name="row">The caller-owned canonical row.</param>
    /// <param name="completion">The terminal write result.</param>
    /// <param name="priority">The originating admission lane.</param>
    /// <returns>The admission result.</returns>
    internal PersistenceAdmissionStatus TryPersist(ActionTooltip row, out Task<PersistenceWriteResult> completion, PersistencePriority priority = PersistencePriority.Background)
    {
        var snapshot = ActionTooltipPersistenceHelper.Clone(row);
        var scope = new TranslationReuseScope(snapshot.OriginalLang!, snapshot.TranslationLang!, snapshot.TranslationEngine, true);
        ActionTooltip? persisted = null;
        return this.TryScheduleWrite(
            new PersistenceWriteRequest(
            new PersistenceWorkKey("ActionTooltip-write", BuildIdentity("ActionTooltip", snapshot.ActionId, scope, snapshot.GameVersion, snapshot.SourceContentHash)),
            priority,
            async (context, token) =>
            {
                var result = await ActionTooltipPersistenceHelper.UpsertAsync(context, snapshot, token).ConfigureAwait(false);
                persisted = result.Row;
                return new PersistenceWriteMutation(result.Changed);
            },
            () =>
            {
                if (persisted != null)
                {
                    this.Publish(() => ActionTooltipCacheManager.Update(ActionTooltipPersistenceHelper.Clone(persisted)));
                }
            })
        {
            CancellationToken = this.ShutdownToken,
        }, out completion);
    }

    /// <summary>Returns a detached current-version cache hit without database admission.</summary>
    /// <param name="probe">The captured identity.</param>
    /// <param name="scope">The reuse policy.</param>
    /// <returns>The matching cached row.</returns>
    internal static Trait? FindCached(Trait probe, TranslationReuseScope scope)
    {
        var row = TraitCacheManager.TryFindCanonicalMatch(probe.TraitId, scope, probe.GameVersion, probe.SourceContentHash!);
        return row == null ? null : TraitPersistenceHelper.Clone(row);
    }

    /// <summary>Admits a nonblocking canonical read, with historical promotion after commit.</summary>
    /// <param name="probe">The captured identity.</param>
    /// <param name="scope">The reuse policy.</param>
    /// <param name="completion">The terminal read result.</param>
    /// <param name="priority">The caller's admission lane.</param>
    /// <returns>The admission result.</returns>
    internal PersistenceAdmissionStatus TryFind(Trait probe, TranslationReuseScope scope, out Task<PersistenceReadResult<Trait?>> completion, PersistencePriority priority = PersistencePriority.Background)
    {
        var snapshot = TraitPersistenceHelper.Clone(probe);
        var cached = FindCached(snapshot, scope);
        var historical = GameVersionLookupHelper.HasRequestedVersion(snapshot.GameVersion)
            ? TraitCacheManager.TryFindHistoricalCanonicalMatch(snapshot.TraitId, scope, snapshot.GameVersion, snapshot.SourceContentHash!)
            : null;
        if (cached != null && (TraitPersistenceHelper.HasTranslation(cached) || historical == null || !TraitPersistenceHelper.HasTranslation(historical)))
        {
            completion = Task.FromResult(new PersistenceReadResult<Trait?>(PersistenceCompletionStatus.Succeeded, cached, null));
            return PersistenceAdmissionStatus.Accepted;
        }

        if (historical != null && TraitPersistenceHelper.HasTranslation(historical))
        {
            var promoted = TraitPersistenceHelper.Clone(historical);
            promoted.Id = 0;
            promoted.GameVersion = snapshot.GameVersion;
            var admission = this.TryPersist(promoted, out var write, priority);
            completion = this.CompletePromotionAsync(promoted, scope, write);
            return admission;
        }

        return this.coordinator.TryScheduleRead(
            new PersistenceWorkKey("Trait-read", BuildIdentity("Trait", snapshot.TraitId, scope, snapshot.GameVersion, snapshot.SourceContentHash)),
            priority,
            async (context, token) =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, this.ShutdownToken);
                return await TraitPersistenceHelper.FindAsync(context, snapshot, scope, linked.Token).ConfigureAwait(false);
            },
            row =>
            {
                if (row != null)
                {
                    this.Publish(() => TraitCacheManager.Update(TraitPersistenceHelper.Clone(row)));
                }
            },
            out completion);
    }

    /// <summary>Releases a historical projection only after its canonical promotion commits.</summary>
    /// <param name="promoted">The promotion identity.</param>
    /// <param name="scope">The reuse policy.</param>
    /// <param name="write">The coordinator completion.</param>
    /// <returns>The committed cached row or a terminal failure.</returns>
    private async Task<PersistenceReadResult<Trait?>> CompletePromotionAsync(Trait promoted, TranslationReuseScope scope, Task<PersistenceWriteResult> write)
    {
        var result = await write.ConfigureAwait(false);
        return new PersistenceReadResult<Trait?>(
            result.Status,
            result.Status is PersistenceCompletionStatus.Succeeded or PersistenceCompletionStatus.Unchanged && !this.ShutdownToken.IsCancellationRequested
                ? FindCached(promoted, scope) : null, result.Error);
    }

    /// <summary>Schedules a captured canonical merge and publishes only after commit.</summary>
    /// <param name="row">The caller-owned canonical row.</param>
    /// <param name="completion">The terminal write result.</param>
    /// <param name="priority">The originating admission lane.</param>
    /// <returns>The admission result.</returns>
    internal PersistenceAdmissionStatus TryPersist(Trait row, out Task<PersistenceWriteResult> completion, PersistencePriority priority = PersistencePriority.Background)
    {
        var snapshot = TraitPersistenceHelper.Clone(row);
        var scope = new TranslationReuseScope(snapshot.OriginalLang!, snapshot.TranslationLang!, snapshot.TranslationEngine, true);
        Trait? persisted = null;
        return this.TryScheduleWrite(
            new PersistenceWriteRequest(
            new PersistenceWorkKey("Trait-write", BuildIdentity("Trait", snapshot.TraitId, scope, snapshot.GameVersion, snapshot.SourceContentHash)),
            priority,
            async (context, token) =>
            {
                var result = await TraitPersistenceHelper.UpsertAsync(context, snapshot, token).ConfigureAwait(false);
                persisted = result.Row;
                return new PersistenceWriteMutation(result.Changed);
            },
            () =>
            {
                if (persisted != null)
                {
                    this.Publish(() => TraitCacheManager.Update(TraitPersistenceHelper.Clone(persisted)));
                }
            })
        {
            CancellationToken = this.ShutdownToken,
        }, out completion);
    }

    /// <summary>Returns a detached current-version cache hit without database admission.</summary>
    /// <param name="probe">The captured identity.</param>
    /// <param name="scope">The reuse policy.</param>
    /// <returns>The matching cached row.</returns>
    internal static ItemTooltip? FindCached(ItemTooltip probe, TranslationReuseScope scope)
    {
        var row = ItemTooltipCacheManager.TryFindCanonicalMatch(probe.ItemId, scope, probe.GameVersion, probe.SourceContentHash!);
        return row == null ? null : ItemTooltipPersistenceHelper.Clone(row);
    }

    /// <summary>Admits a nonblocking canonical read, with historical promotion after commit.</summary>
    /// <param name="probe">The captured identity.</param>
    /// <param name="scope">The reuse policy.</param>
    /// <param name="completion">The terminal read result.</param>
    /// <param name="priority">The caller's admission lane.</param>
    /// <returns>The admission result.</returns>
    internal PersistenceAdmissionStatus TryFind(ItemTooltip probe, TranslationReuseScope scope, out Task<PersistenceReadResult<ItemTooltip?>> completion, PersistencePriority priority = PersistencePriority.Background)
    {
        var snapshot = ItemTooltipPersistenceHelper.Clone(probe);
        var cached = FindCached(snapshot, scope);
        var historical = GameVersionLookupHelper.HasRequestedVersion(snapshot.GameVersion)
            ? ItemTooltipCacheManager.TryFindHistoricalCanonicalMatch(snapshot.ItemId, scope, snapshot.GameVersion, snapshot.SourceContentHash!)
            : null;
        if (cached != null && (ItemTooltipPersistenceHelper.HasTranslation(cached) || historical == null || !ItemTooltipPersistenceHelper.HasTranslation(historical)))
        {
            completion = Task.FromResult(new PersistenceReadResult<ItemTooltip?>(PersistenceCompletionStatus.Succeeded, cached, null));
            return PersistenceAdmissionStatus.Accepted;
        }

        if (historical != null && ItemTooltipPersistenceHelper.HasTranslation(historical))
        {
            var promoted = ItemTooltipPersistenceHelper.Clone(historical);
            promoted.Id = 0;
            promoted.GameVersion = snapshot.GameVersion;
            var admission = this.TryPersist(promoted, out var write, priority);
            completion = this.CompletePromotionAsync(promoted, scope, write);
            return admission;
        }

        return this.coordinator.TryScheduleRead(
            new PersistenceWorkKey("ItemTooltip-read", BuildIdentity("ItemTooltip", snapshot.ItemId, scope, snapshot.GameVersion, snapshot.SourceContentHash)),
            priority,
            async (context, token) =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, this.ShutdownToken);
                return await ItemTooltipPersistenceHelper.FindAsync(context, snapshot, scope, linked.Token).ConfigureAwait(false);
            },
            row =>
            {
                if (row != null)
                {
                    this.Publish(() => ItemTooltipCacheManager.Update(ItemTooltipPersistenceHelper.Clone(row)));
                }
            },
            out completion);
    }

    /// <summary>Releases a historical projection only after its canonical promotion commits.</summary>
    /// <param name="promoted">The promotion identity.</param>
    /// <param name="scope">The reuse policy.</param>
    /// <param name="write">The coordinator completion.</param>
    /// <returns>The committed cached row or a terminal failure.</returns>
    private async Task<PersistenceReadResult<ItemTooltip?>> CompletePromotionAsync(ItemTooltip promoted, TranslationReuseScope scope, Task<PersistenceWriteResult> write)
    {
        var result = await write.ConfigureAwait(false);
        return new PersistenceReadResult<ItemTooltip?>(
            result.Status,
            result.Status is PersistenceCompletionStatus.Succeeded or PersistenceCompletionStatus.Unchanged && !this.ShutdownToken.IsCancellationRequested
                ? FindCached(promoted, scope) : null, result.Error);
    }

    /// <summary>Schedules a captured canonical merge and publishes only after commit.</summary>
    /// <param name="row">The caller-owned canonical row.</param>
    /// <param name="completion">The terminal write result.</param>
    /// <param name="priority">The originating admission lane.</param>
    /// <returns>The admission result.</returns>
    internal PersistenceAdmissionStatus TryPersist(ItemTooltip row, out Task<PersistenceWriteResult> completion, PersistencePriority priority = PersistencePriority.Background)
    {
        var snapshot = ItemTooltipPersistenceHelper.Clone(row);
        var scope = new TranslationReuseScope(snapshot.OriginalLang!, snapshot.TranslationLang!, snapshot.TranslationEngine, true);
        ItemTooltip? persisted = null;
        return this.TryScheduleWrite(
            new PersistenceWriteRequest(
            new PersistenceWorkKey("ItemTooltip-write", BuildIdentity("ItemTooltip", snapshot.ItemId, scope, snapshot.GameVersion, snapshot.SourceContentHash)),
            priority,
            async (context, token) =>
            {
                var result = await ItemTooltipPersistenceHelper.UpsertAsync(context, snapshot, token).ConfigureAwait(false);
                persisted = result.Row;
                return new PersistenceWriteMutation(result.Changed);
            },
            () =>
            {
                if (persisted != null)
                {
                    this.Publish(() => ItemTooltipCacheManager.Update(ItemTooltipPersistenceHelper.Clone(persisted)));
                }
            })
        {
            CancellationToken = this.ShutdownToken,
        }, out completion);
    }

    /// <summary>Shares write ownership while the coordinator has claimed a transaction.</summary>
    /// <param name="request">The canonical write request.</param>
    /// <param name="completion">The shared terminal write.</param>
    /// <returns>The bounded coordinator admission outcome.</returns>
    private PersistenceAdmissionStatus TryScheduleWrite(PersistenceWriteRequest request, out Task<PersistenceWriteResult> completion)
    {
        lock (this.publicationGate)
        {
            if (!this.publicationEnabled)
            {
                completion = Task.FromResult(new PersistenceWriteResult(PersistenceCompletionStatus.Rejected, 0, null));
                return PersistenceAdmissionStatus.RejectedShutdown;
            }

            if (this.writes.TryGetValue(request.Key, out completion!))
            {
                return PersistenceAdmissionStatus.Joined;
            }

            var admission = this.coordinator.TryScheduleWrite(request, out completion);
            if (admission is not (PersistenceAdmissionStatus.RejectedCapacity or PersistenceAdmissionStatus.RejectedShutdown))
            {
                this.writes.Add(request.Key, completion);
                _ = this.ReleaseWriteOwnershipAsync(request.Key, completion);
            }

            return admission;
        }
    }

    /// <summary>Retains canonical ownership until persistence is terminal, including claimed work.</summary>
    /// <param name="key">The claimed canonical identity.</param>
    /// <param name="completion">The terminal coordinator completion.</param>
    private async Task ReleaseWriteOwnershipAsync(PersistenceWorkKey key, Task<PersistenceWriteResult> completion)
    {
        await completion.ConfigureAwait(false);
        lock (this.publicationGate)
        {
            if (this.writes.TryGetValue(key, out var current) && ReferenceEquals(current, completion))
            {
                this.writes.Remove(key);
            }
        }
    }
}
