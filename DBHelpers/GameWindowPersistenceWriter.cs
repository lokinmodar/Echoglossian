// <copyright file="GameWindowPersistenceWriter.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Persistence;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Echoglossian.DBHelpers;

/// <summary>Adapts managed GameWindow persistence to the shared bounded coordinator.</summary>
internal sealed class GameWindowPersistenceWriter
{
    private readonly IPersistenceCoordinator coordinator;
    private readonly CancellationTokenSource cancellation = new();
    private readonly object publicationGate = new();
    private bool publicationEnabled = true;

    /// <summary>Initializes the process-lifetime adapter.</summary>
    /// <param name="coordinator">The shared coordinator.</param>
    internal GameWindowPersistenceWriter(IPersistenceCoordinator coordinator)
    {
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    /// <summary>Gets cancellation for this plugin lifetime.</summary>
    internal CancellationToken ShutdownToken => this.cancellation.Token;

    /// <summary>Prevents late publication and retires the owning runtime.</summary>
    internal void DisablePublication()
    {
        lock (this.publicationGate)
        {
            this.publicationEnabled = false;
        }

        this.cancellation.Cancel();
    }

    /// <summary>Builds a collision-safe identity with the captured reuse semantics.</summary>
    /// <param name="row">The managed payload identity.</param>
    /// <param name="scope">The captured translation reuse policy.</param>
    /// <returns>The normalized operation identity.</returns>
    internal static string BuildIdentity(GameWindow row, TranslationReuseScope scope) => JsonConvert.SerializeObject(new
    {
        Addon = row.WindowAddonName,
        Source = RuntimeLanguageHelper.NormalizeLanguage(scope.SourceLanguageCode).ToLowerInvariant(),
        Target = RuntimeLanguageHelper.NormalizeLanguage(scope.TargetLanguageCode).ToLowerInvariant(),
        Engine = scope.RequireMatchingEngine ? scope.TranslationEngine?.ToString() ?? "<none>" : "*",
        Version = string.IsNullOrWhiteSpace(row.GameVersion) ? null : row.GameVersion,
        Job = row.ClassJobId,
        Original = row.OriginalWindowStrings,
    });

    /// <summary>Retries startup preload off the game thread until committed data is available or shutdown begins.</summary>
    /// <returns>A task completed after successful preload or owner shutdown.</returns>
    internal async Task PreloadAsync()
    {
        try
        {
            while (!this.ShutdownToken.IsCancellationRequested && !GameWindowCacheManager.IsPreloaded)
            {
                var admission = this.TryLoadAll(out var completion);
                _ = await completion.ConfigureAwait(false);
                if (admission == PersistenceAdmissionStatus.RejectedShutdown || GameWindowCacheManager.IsPreloaded)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(5), this.ShutdownToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (this.ShutdownToken.IsCancellationRequested)
        {
            // Owner shutdown retires preload without producing a faulted cleanup task.
        }
    }

    /// <summary>Loads the initial committed projection without blocking startup callbacks.</summary>
    /// <param name="completion">The terminal read completion.</param>
    /// <param name="priority">The originating admission lane.</param>
    /// <returns>The bounded coordinator admission outcome.</returns>
    internal PersistenceAdmissionStatus TryLoadAll(
        out Task<PersistenceReadResult<List<GameWindow>>> completion,
        PersistencePriority priority = PersistencePriority.Background)
    {
        var generation = GameWindowCacheManager.Generation;
        return this.coordinator.TryScheduleRead(
            new PersistenceWorkKey("game-window-preload", generation.ToString()),
            priority,
            async (context, token) =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, this.ShutdownToken);
                return await context.GameWindow.AsNoTracking().OrderBy(row => row.Id).ToListAsync(linked.Token).ConfigureAwait(false);
            }, rows => this.Publish(() => GameWindowCacheManager.PublishPreloaded(rows, generation)), out completion);
    }

    /// <summary>Coalesces pending writes and publishes detached values only after successful commit.</summary>
    /// <param name="row">The caller-owned payload to snapshot.</param>
    /// <param name="completion">The terminal write completion.</param>
    /// <param name="priority">The originating admission lane.</param>
    /// <returns>The bounded coordinator admission outcome.</returns>
    internal PersistenceAdmissionStatus TryPersist(
        GameWindow row,
        out Task<PersistenceWriteResult> completion,
        PersistencePriority priority = PersistencePriority.Interactive)
    {
        var snapshot = GameWindowPersistenceHelper.Clone(row);
        var scope = new TranslationReuseScope(snapshot.OriginalWindowStringsLang!, snapshot.TranslationLang!, snapshot.TranslationEngine, true);
        var generation = GameWindowCacheManager.Generation;
        GameWindow? persisted = null;
        lock (this.publicationGate)
        {
            if (!this.publicationEnabled)
            {
                completion = Task.FromResult(new PersistenceWriteResult(PersistenceCompletionStatus.Rejected, 0, null));
                return PersistenceAdmissionStatus.RejectedShutdown;
            }

            return this.coordinator.TryScheduleWrite(
                new PersistenceWriteRequest(
                new PersistenceWorkKey("game-window-write", BuildIdentity(snapshot, scope)),
                priority,
                async (context, token) =>
                {
                    var result = await GameWindowPersistenceHelper.UpsertAsync(context, snapshot, token).ConfigureAwait(false);
                    persisted = result.Row;
                    return new PersistenceWriteMutation(result.Changed);
                }, () =>
                {
                    if (persisted != null)
                    {
                        this.Publish(() => GameWindowCacheManager.Update(GameWindowPersistenceHelper.Clone(persisted), generation));
                    }
                }), out completion);
        }
    }

    /// <summary>Serializes cache publication with the owning runtime shutdown gate.</summary>
    /// <param name="publish">The managed projection update.</param>
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
}
