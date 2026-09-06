// <copyright file="ReferenceTextPersistenceWriterTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Data.Common;
using System.Security.Cryptography;
using System.Text;

using Echoglossian.Cache;
using Echoglossian.DBHelpers;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.Helpers;
using Echoglossian.Persistence;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

using Xunit;

namespace Echoglossian.Tests.Persistence;

/// <summary>
///     Verifies coordinator-backed persistence of canonical reference-text
///     rows against a real temporary SQLite database.
/// </summary>
public sealed class ReferenceTextPersistenceWriterTests
{
    /// <summary>
    ///     Ensures a new complete canonical row is inserted through the
    ///     background persistence lane.
    /// </summary>
    [Fact]
    public async Task TryPersist_NewCompleteRow_InsertsCanonicalPayload()
    {
        await this.WithWriterAsync(async (writer, factory, _) =>
        {
            var row = CreateRow("nome", "descricao");

            Assert.Equal(
                PersistenceAdmissionStatus.Accepted,
                writer.TryPersist(
                    row,
                    static context => context.MainCommandTexts,
                    publish: null,
                    out var completion));

            Assert.Equal(
                PersistenceCompletionStatus.Succeeded,
                (await completion.WaitAsync(TimeSpan.FromSeconds(5))).Status);

            await using var context = await factory.CreateDbContextAsync();
            var persisted = await context.MainCommandTexts.SingleAsync();
            Assert.Equal("nome", persisted.TranslatedName);
            Assert.Equal("descricao", persisted.TranslatedDescription);
            Assert.Equal(row.CanonicalPayloadAsText, persisted.CanonicalPayloadAsText);
            Assert.Equal((uint)42, persisted.IconId);
            Assert.Equal((uint)7, persisted.CategoryId);
            Assert.Equal((uint)3, persisted.MainCommandCategoryId);
            Assert.Equal((uint)9, persisted.Unknown0);
            Assert.Equal((uint)40, persisted.SortId);
        });
    }

    /// <summary>
    ///     Ensures an incomplete incoming payload cannot discard an already
    ///     completed translation.
    /// </summary>
    [Fact]
    public async Task TryPersist_IncompletePayload_PreservesCompletedStoredFields()
    {
        await this.WithWriterAsync(async (writer, factory, _) =>
        {
            writer.TryPersist(
                CreateRow("nome", "descricao"),
                static context => context.MainCommandTexts,
                publish: null,
                out var initial);
            await initial.WaitAsync(TimeSpan.FromSeconds(5));

            writer.TryPersist(
                CreateRow("nome", null),
                static context => context.MainCommandTexts,
                publish: null,
                out var completion);
            await completion.WaitAsync(TimeSpan.FromSeconds(5));

            await using var context = await factory.CreateDbContextAsync();
            var persisted = await context.MainCommandTexts.SingleAsync();
            Assert.Equal("nome", persisted.TranslatedName);
            Assert.Equal("descricao", persisted.TranslatedDescription);
        });
    }

    /// <summary>
    ///     Ensures a complete payload upgrades both translated fields in one
    ///     coordinator transaction.
    /// </summary>
    [Fact]
    public async Task TryPersist_CompletePayload_AtomicallyUpgradesIncompleteRow()
    {
        await this.WithWriterAsync(async (writer, factory, _) =>
        {
            writer.TryPersist(
                CreateRow("nome", null),
                static context => context.MainCommandTexts,
                publish: null,
                out var incomplete);
            await incomplete.WaitAsync(TimeSpan.FromSeconds(5));

            writer.TryPersist(
                CreateRow("nome completo", "descricao completa"),
                static context => context.MainCommandTexts,
                publish: null,
                out var complete);
            Assert.Equal(
                PersistenceCompletionStatus.Succeeded,
                (await complete.WaitAsync(TimeSpan.FromSeconds(5))).Status);

            await using var context = await factory.CreateDbContextAsync();
            var persisted = await context.MainCommandTexts.SingleAsync();
            Assert.Equal("nome completo", persisted.TranslatedName);
            Assert.Equal("descricao completa", persisted.TranslatedDescription);
            Assert.Equal(
                CreateRow("nome completo", "descricao completa")
                    .CanonicalPayloadAsText,
                persisted.CanonicalPayloadAsText);
        });
    }

    /// <summary>
    ///     Ensures an equivalent completed payload produces no SQLite update
    ///     and retains the stored update timestamp.
    /// </summary>
    [Fact]
    public async Task TryPersist_UnchangedPayload_SuppressesUpdateAndTimestampChange()
    {
        var updates = new UpdateCounterInterceptor();
        await this.WithWriterAsync(async (writer, factory, _) =>
        {
            var row = CreateRow("nome", "descricao");
            writer.TryPersist(
                row,
                static context => context.MainCommandTexts,
                publish: null,
                out var initial);
            await initial.WaitAsync(TimeSpan.FromSeconds(5));

            await using var beforeContext = await factory.CreateDbContextAsync();
            var before = await beforeContext.MainCommandTexts.SingleAsync();
            var updatedDate = before.UpdatedDate;
            updates.Reset();

            writer.TryPersist(
                CreateRow("nome", "descricao"),
                static context => context.MainCommandTexts,
                publish: null,
                out var unchanged);

            Assert.Equal(
                PersistenceCompletionStatus.Unchanged,
                (await unchanged.WaitAsync(TimeSpan.FromSeconds(5))).Status);
            Assert.Equal(0, updates.UpdateCount);

            await using var afterContext = await factory.CreateDbContextAsync();
            Assert.Equal(updatedDate, (await afterContext.MainCommandTexts.SingleAsync()).UpdatedDate);
        }, interceptor: updates);
    }

    /// <summary>
    ///     Ensures duplicate pending work keeps only the latest complete
    ///     payload and shares its terminal completion.
    /// </summary>
    [Fact]
    public async Task TryPersist_PendingDuplicate_ReplacesWithLatestCompletePayload()
    {
        var dequeued = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await this.WithWriterAsync(async (writer, factory, _) =>
        {
            Assert.Equal(
                PersistenceAdmissionStatus.Accepted,
                writer.TryPersist(
                    CreateRow("primeiro", "primeira descricao"),
                    static context => context.MainCommandTexts,
                    publish: null,
                    out var first));
            await dequeued.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(
                PersistenceAdmissionStatus.Replaced,
                writer.TryPersist(
                    CreateRow("ultimo", "ultima descricao"),
                    static context => context.MainCommandTexts,
                    publish: null,
                    out var latest));
            Assert.Same(first, latest);

            release.SetResult(true);
            await latest.WaitAsync(TimeSpan.FromSeconds(5));

            await using var context = await factory.CreateDbContextAsync();
            var persisted = await context.MainCommandTexts.SingleAsync();
            Assert.Equal("ultimo", persisted.TranslatedName);
            Assert.Equal("ultima descricao", persisted.TranslatedDescription);
        }, writeDequeuedBeforeClaimAsync: async () =>
        {
            dequeued.SetResult(true);
            await release.Task.ConfigureAwait(false);
        });
    }

    /// <summary>
    ///     Ensures equivalent target aliases with mixed casing coalesce before
    ///     either unsaved insert can create duplicate logical rows.
    /// </summary>
    [Fact]
    public async Task TryPersist_MixedCaseEquivalentAlias_CoalescesBeforeInsert()
    {
        var dequeued = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await this.WithWriterAsync(async (writer, factory, _) =>
        {
            var firstRow = CreateRow("primeiro", "primeira descricao");
            firstRow.TranslationLang = "pt";
            var latestRow = CreateRow("ultimo", "ultima descricao");
            latestRow.TranslationLang = "PT-br";

            Assert.Equal(
                PersistenceAdmissionStatus.Accepted,
                writer.TryPersist(
                    firstRow,
                    static context => context.MainCommandTexts,
                    publish: null,
                    out var first));
            await dequeued.Task.WaitAsync(TimeSpan.FromSeconds(5));
            try
            {
                Assert.Equal(
                    PersistenceAdmissionStatus.Replaced,
                    writer.TryPersist(
                        latestRow,
                        static context => context.MainCommandTexts,
                        publish: null,
                        out var latest));
                Assert.Same(first, latest);

                release.SetResult(true);
                await latest.WaitAsync(TimeSpan.FromSeconds(5));

                await using var context = await factory.CreateDbContextAsync();
                var persisted = await context.MainCommandTexts.SingleAsync();
                Assert.Equal("ultimo", persisted.TranslatedName);
                Assert.Equal("ultima descricao", persisted.TranslatedDescription);
            }
            finally
            {
                release.TrySetResult(true);
            }
        }, writeDequeuedBeforeClaimAsync: async () =>
        {
            dequeued.SetResult(true);
            await release.Task.ConfigureAwait(false);
        });
    }

    /// <summary>
    ///     Ensures distinct source, target, and engine identities remain
    ///     isolated even when their canonical payload hash is the same.
    /// </summary>
    [Fact]
    public async Task TryPersist_SourceTargetAndEngineVariants_RemainIsolated()
    {
        await this.WithWriterAsync(async (writer, factory, _) =>
        {
            var englishToPortuguese = CreateRow("nome", "descricao");
            var germanToPortuguese = CreateRow("name", "beschreibung");
            germanToPortuguese.OriginalLang = "de";
            var englishToFrench = CreateRow("nom", "description");
            englishToFrench.TranslationLang = "fr";
            var otherEngine = CreateRow("nome por outro engine", "descricao");
            otherEngine.TranslationEngine = 1;

            foreach (var row in new[]
                     {
                         englishToPortuguese,
                         germanToPortuguese,
                         englishToFrench,
                         otherEngine,
                     })
            {
                writer.TryPersist(
                    row,
                    static context => context.MainCommandTexts,
                    publish: null,
                    out var completion);
                await completion.WaitAsync(TimeSpan.FromSeconds(5));
            }

            await using var context = await factory.CreateDbContextAsync();
            Assert.Equal(4, await context.MainCommandTexts.CountAsync());
        });
    }

    /// <summary>
    ///     Ensures a full background lane rejects new work without dropping
    ///     an already accepted row.
    /// </summary>
    [Fact]
    public async Task TryPersist_BackgroundCapacityExceeded_RejectsOnlyNewWork()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await this.WithWriterAsync(async (writer, factory, coordinator) =>
        {
            coordinator.TryScheduleWrite(
                new PersistenceWriteRequest(
                    new PersistenceWorkKey("reference-text-test", "blocker"),
                    PersistencePriority.Background,
                    async (_, _) =>
                    {
                        started.SetResult(true);
                        await release.Task.ConfigureAwait(false);
                        return PersistenceWriteMutation.UnchangedResult;
                    },
                    () => { }),
                out _);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(
                PersistenceAdmissionStatus.Accepted,
                writer.TryPersist(
                    CreateRow("kept", "kept description", 1),
                    static context => context.MainCommandTexts,
                    publish: null,
                    out var kept));
            Assert.Equal(
                PersistenceAdmissionStatus.RejectedCapacity,
                writer.TryPersist(
                    CreateRow("rejected", "rejected description", 2),
                    static context => context.MainCommandTexts,
                    publish: null,
                    out var rejected));
            Assert.Equal(PersistenceCompletionStatus.Rejected, (await rejected).Status);

            release.SetResult(true);
            Assert.Equal(
                PersistenceCompletionStatus.Succeeded,
                (await kept.WaitAsync(TimeSpan.FromSeconds(5))).Status);
        }, options: CreateOptions(backgroundCapacity: 1));
    }

    /// <summary>
    ///     Ensures a cancelled queued write does not publish and a replacement
    ///     coordinator can subsequently persist the same row.
    /// </summary>
    [Fact]
    public async Task TryPersist_CancelledCoordinator_DoesNotPublishAndRestartPersists()
    {
        var dequeued = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await this.WithWriterAsync(async (writer, factory, coordinator) =>
        {
            var publications = 0;
            writer.TryPersist(
                CreateRow("nome", "descricao"),
                static context => context.MainCommandTexts,
                _ => Interlocked.Increment(ref publications),
                out var cancelled);
            await dequeued.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var shutdown = coordinator.CompleteAsync();
            release.SetResult(true);
            await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(PersistenceCompletionStatus.Cancelled, (await cancelled).Status);
            Assert.Equal(0, publications);

            await using var restarted = new PersistenceCoordinator(factory, CreateOptions());
            var replacement = new ReferenceTextPersistenceWriter(restarted);
            replacement.TryPersist(
                CreateRow("nome", "descricao"),
                static context => context.MainCommandTexts,
                publish: null,
                out var persisted);
            Assert.Equal(
                PersistenceCompletionStatus.Succeeded,
                (await persisted.WaitAsync(TimeSpan.FromSeconds(5))).Status);
        },
        options: CreateOptions(shutdownTimeout: TimeSpan.Zero),
        writeDequeuedBeforeClaimAsync: async () =>
        {
            dequeued.SetResult(true);
            await release.Task.ConfigureAwait(false);
        });
    }

    /// <summary>
    ///     Ensures cache publication occurs only after a successful commit and
    ///     can be disabled before that commit completes.
    /// </summary>
    [Fact]
    public async Task TryPersist_SuccessfulCommitPublishesCacheOnlyAfterCommit()
    {
        var gate = new CommitGateInterceptor();
        await this.WithWriterAsync(async (writer, _, _) =>
        {
            var cache = new ReferenceTextCacheStore<MainCommandText>("ReferenceTextPersistenceWriterTests");
            writer.TryPersist(
                CreateRow("nome", "descricao"),
                static context => context.MainCommandTexts,
                cache.Update,
                out var completion);
            await gate.CommitEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Null(cache.TryFindCanonicalMatch(
                1,
                new TranslationReuseScope("en", "pt", 0, true),
                "7.3",
                CreateRow("nome", "descricao").SourceContentHash!));

            gate.ReleaseCommit.SetResult(true);
            Assert.Equal(
                PersistenceCompletionStatus.Succeeded,
                (await completion.WaitAsync(TimeSpan.FromSeconds(5))).Status);
            Assert.NotNull(cache.TryFindCanonicalMatch(
                1,
                new TranslationReuseScope("en", "pt", 0, true),
                "7.3",
                CreateRow("nome", "descricao").SourceContentHash!));
        }, interceptor: gate);
    }

    /// <summary>
    ///     Ensures publication can be disabled while an accepted write waits
    ///     to commit.
    /// </summary>
    [Fact]
    public async Task TryPersist_DisabledPublication_DoesNotPublishAfterCommit()
    {
        var gate = new CommitGateInterceptor();
        await this.WithWriterAsync(async (writer, _, _) =>
        {
            var cache = new ReferenceTextCacheStore<MainCommandText>("ReferenceTextPersistenceWriterTests");
            writer.TryPersist(
                CreateRow("nome", "descricao"),
                static context => context.MainCommandTexts,
                cache.Update,
                out var completion);
            await gate.CommitEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            writer.DisablePublication();
            gate.ReleaseCommit.SetResult(true);
            await completion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Null(cache.TryFindCanonicalMatch(
                1,
                new TranslationReuseScope("en", "pt", 0, true),
                "7.3",
                CreateRow("nome", "descricao").SourceContentHash!));
        }, interceptor: gate);
    }

    /// <summary>
    ///     Ensures a failed commit neither persists a row nor publishes its
    ///     cache projection.
    /// </summary>
    [Fact]
    public async Task TryPersist_FailedCommit_DoesNotPersistOrPublishCache()
    {
        var gate = new CommitGateInterceptor { ThrowOnCommit = true };
        await this.WithWriterAsync(async (writer, factory, _) =>
        {
            var cache = new ReferenceTextCacheStore<MainCommandText>("ReferenceTextPersistenceWriterTests");
            writer.TryPersist(
                CreateRow("nome", "descricao"),
                static context => context.MainCommandTexts,
                cache.Update,
                out var completion);

            Assert.Equal(
                PersistenceCompletionStatus.Failed,
                (await completion.WaitAsync(TimeSpan.FromSeconds(5))).Status);
            await using var context = await factory.CreateDbContextAsync();
            Assert.Equal(0, await context.MainCommandTexts.CountAsync());
            Assert.Null(cache.TryFindCanonicalMatch(
                1,
                new TranslationReuseScope("en", "pt", 0, true),
                "7.3",
                CreateRow("nome", "descricao").SourceContentHash!));
        }, interceptor: gate);
    }

    /// <summary>
    ///     Ensures an unchanged write publishes the DB-backed stored row to an
    ///     empty cache projection.
    /// </summary>
    [Fact]
    public async Task TryPersist_UnchangedRow_PublishesStoredCommittedProjection()
    {
        await this.WithWriterAsync(async (writer, _, _) =>
        {
            writer.TryPersist(
                CreateRow("nome", "descricao"),
                static context => context.MainCommandTexts,
                publish: null,
                out var initial);
            await initial.WaitAsync(TimeSpan.FromSeconds(5));

            var cache = new ReferenceTextCacheStore<MainCommandText>("ReferenceTextPersistenceWriterTests");
            writer.TryPersist(
                CreateRow("nome", "descricao"),
                static context => context.MainCommandTexts,
                cache.Update,
                out var unchanged);
            Assert.Equal(
                PersistenceCompletionStatus.Unchanged,
                (await unchanged.WaitAsync(TimeSpan.FromSeconds(5))).Status);

            var cached = cache.TryFindCanonicalMatch(
                1,
                new TranslationReuseScope("en", "pt", 0, true),
                "7.3",
                CreateRow("nome", "descricao").SourceContentHash!);
            Assert.NotNull(cached);
            Assert.True(cached.Id > 0);
            Assert.Equal("nome", cached.TranslatedName);
            Assert.Equal("descricao", cached.TranslatedDescription);
        });
    }

    /// <summary>
    ///     Ensures current-version lookups include a null-version fallback and
    ///     prefer the more complete translation over an incomplete exact row.
    /// </summary>
    [Fact]
    public async Task TryFind_NullVersionFallback_PrefersCompleteTranslation()
    {
        await this.WithWriterAsync(async (writer, _, _) =>
        {
            var currentVersion = CreateRow("nome", null);
            var fallbackVersion = CreateRow("nome completo", "descricao completa");
            fallbackVersion.GameVersion = null;
            writer.TryPersist(
                currentVersion,
                static context => context.MainCommandTexts,
                publish: null,
                out var currentCompletion);
            await currentCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            writer.TryPersist(
                fallbackVersion,
                static context => context.MainCommandTexts,
                publish: null,
                out var fallbackCompletion);
            await fallbackCompletion.WaitAsync(TimeSpan.FromSeconds(5));

            var probe = CreateRow("nome", "descricao");
            MainCommandText? published = null;
            writer.TryFind(
                probe,
                new TranslationReuseScope("en", "pt", 0, true),
                static context => context.MainCommandTexts,
                row => published = row,
                out var completion);
            Assert.Equal(
                PersistenceCompletionStatus.Succeeded,
                (await completion.WaitAsync(TimeSpan.FromSeconds(5))).Status);
            Assert.NotNull(published);
            Assert.Null(published.GameVersion);
            Assert.Equal("nome completo", published.TranslatedName);
            Assert.Equal("descricao completa", published.TranslatedDescription);
        });
    }

    /// <summary>
    ///     Ensures an asynchronous canonical read publishes only after its
    ///     worker query has completed.
    /// </summary>
    [Fact]
    public async Task TryFind_PersistedRow_PublishesCompletedRead()
    {
        await this.WithWriterAsync(async (writer, _, _) =>
        {
            var stored = CreateRow("nome", "descricao");
            writer.TryPersist(
                stored,
                static context => context.MainCommandTexts,
                publish: null,
                out var persisted);
            await persisted.WaitAsync(TimeSpan.FromSeconds(5));

            MainCommandText? published = null;
            var scope = new TranslationReuseScope("en", "pt", 0, true);
            Assert.Equal(
                PersistenceAdmissionStatus.Accepted,
                writer.TryFind(
                    CreateRow("nome", "descricao"),
                    scope,
                    static context => context.MainCommandTexts,
                    row => published = row,
                    out var completion));

            var result = await completion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(PersistenceCompletionStatus.Succeeded, result.Status);
            Assert.NotNull(result.Value);
            Assert.NotNull(published);
            Assert.Equal("nome", published.TranslatedName);
        });
    }

    /// <summary>
    ///     Runs one test action with a real temporary SQLite database and a
    ///     short-lived-context persistence coordinator.
    /// </summary>
    /// <param name="action">The test action to execute.</param>
    /// <param name="interceptor">The optional SQLite command interceptor.</param>
    /// <param name="options">The coordinator bounds to use.</param>
    /// <param name="writeDequeuedBeforeClaimAsync">The optional writer-race seam.</param>
    /// <returns>A task that completes after cleanup.</returns>
    private async Task WithWriterAsync(
        Func<ReferenceTextPersistenceWriter, IDbContextFactory<EchoglossianDbContext>, IPersistenceCoordinator, Task> action,
        IInterceptor? interceptor = null,
        PersistenceCoordinatorOptions? options = null,
        Func<Task>? writeDequeuedBeforeClaimAsync = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), "EchoglossianTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "Echoglossian.db");
        IDbContextFactory<EchoglossianDbContext> factory = interceptor is null
            ? new EchoglossianDbContextRuntimeFactory(directory)
            : new TestContextFactory(databasePath, interceptor);

        try
        {
            await using (var context = await factory.CreateDbContextAsync())
            {
                await context.Database.MigrateAsync();
            }

            if (interceptor is CommitGateInterceptor commitGate)
            {
                commitGate.Enabled = true;
            }

            await using var coordinator = new PersistenceCoordinator(
                factory,
                options ?? CreateOptions(),
                writeDequeuedBeforeClaimAsync: writeDequeuedBeforeClaimAsync);
            await action(new ReferenceTextPersistenceWriter(coordinator), factory, coordinator);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    ///     Creates bounded coordinator options suitable for deterministic
    ///     adapter tests.
    /// </summary>
    /// <param name="backgroundCapacity">The background queue capacity.</param>
    /// <param name="shutdownTimeout">The coordinator shutdown timeout.</param>
    /// <returns>The test coordinator options.</returns>
    private static PersistenceCoordinatorOptions CreateOptions(
        int backgroundCapacity = 8,
        TimeSpan? shutdownTimeout = null)
    {
        return new PersistenceCoordinatorOptions(
            8,
            backgroundCapacity,
            1,
            32,
            TimeSpan.FromMilliseconds(1),
            1,
            [],
            4,
            1,
            shutdownTimeout ?? TimeSpan.FromSeconds(5));
    }

    /// <summary>
    ///     Creates one metadata-sensitive MainCommand canonical row.
    /// </summary>
    /// <param name="translatedName">The translated name.</param>
    /// <param name="translatedDescription">The translated description.</param>
    /// <param name="referenceId">The stable row identifier.</param>
    /// <returns>The canonical row.</returns>
    private static MainCommandText CreateRow(
        string? translatedName,
        string? translatedDescription,
        uint referenceId = 1)
    {
        var original = new ReferenceTextCanonicalPayload
        {
            ReferenceId = referenceId,
            IconId = 42,
            CategoryId = 7,
            MainCommandCategoryId = 3,
            Unknown0 = 9,
            SortId = 40,
            Name = "Original name",
            Description = "Original description",
        };
        var translated = new ReferenceTextCanonicalPayload
        {
            ReferenceId = referenceId,
            IconId = 42,
            CategoryId = 7,
            MainCommandCategoryId = 3,
            Unknown0 = 9,
            SortId = 40,
            Name = "Original name",
            Description = "Original description",
            TranslatedName = translatedName,
            TranslatedDescription = translatedDescription,
        };
        var row = ReferenceTextPersistenceHelper.CreateCanonicalRow<MainCommandText>(
            "en",
            "pt",
            0,
            "7.3",
            original,
            translated);
        row.IconId = original.IconId;
        row.CategoryId = original.CategoryId;
        row.MainCommandCategoryId = original.MainCommandCategoryId;
        row.Unknown0 = original.Unknown0;
        row.SortId = original.SortId;
        row.SourceContentHash = ComputeMainCommandSourceHash(original);
        return row;
    }

    /// <summary>
    ///     Computes the metadata-sensitive MainCommand source hash used by
    ///     the live runtime.
    /// </summary>
    /// <param name="payload">The source payload.</param>
    /// <returns>The stable source hash.</returns>
    private static string ComputeMainCommandSourceHash(ReferenceTextCanonicalPayload payload)
    {
        var serialized = string.Join(
            "|",
            payload.SchemaVersion,
            payload.ReferenceId,
            payload.IconId?.ToString() ?? string.Empty,
            payload.CategoryId?.ToString() ?? string.Empty,
            payload.MainCommandCategoryId?.ToString() ?? string.Empty,
            payload.Unknown0?.ToString() ?? string.Empty,
            payload.SortId?.ToString() ?? string.Empty,
            payload.Name,
            payload.Description ?? string.Empty);
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(serialized)));
    }

    /// <summary>
    ///     Provides SQLite contexts with an optional test interceptor.
    /// </summary>
    private sealed class TestContextFactory : IDbContextFactory<EchoglossianDbContext>
    {
        private readonly DbContextOptions<EchoglossianDbContext> options;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TestContextFactory" /> class.
        /// </summary>
        /// <param name="databasePath">The SQLite database path.</param>
        /// <param name="interceptor">The interceptor to register.</param>
        internal TestContextFactory(string databasePath, IInterceptor interceptor)
        {
            this.options = new DbContextOptionsBuilder<EchoglossianDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .AddInterceptors(interceptor)
                .Options;
        }

        /// <inheritdoc />
        public EchoglossianDbContext CreateDbContext()
        {
            return new EchoglossianDbContext(this.options);
        }

        /// <inheritdoc />
        public Task<EchoglossianDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(this.CreateDbContext());
        }
    }

    /// <summary>
    ///     Counts SQLite update statements emitted by a test operation.
    /// </summary>
    private sealed class UpdateCounterInterceptor : DbCommandInterceptor
    {
        private int updateCount;

        /// <summary>
        ///     Gets the observed update statement count.
        /// </summary>
        internal int UpdateCount => Volatile.Read(ref this.updateCount);

        /// <summary>
        ///     Resets the observed update statement count.
        /// </summary>
        internal void Reset()
        {
            Interlocked.Exchange(ref this.updateCount, 0);
        }

        /// <inheritdoc />
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            this.Count(command);
            return ValueTask.FromResult(result);
        }

        /// <inheritdoc />
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            this.Count(command);
            return ValueTask.FromResult(result);
        }

        /// <summary>
        ///     Counts update commands without affecting their execution.
        /// </summary>
        /// <param name="command">The command being executed.</param>
        private void Count(DbCommand command)
        {
            if (command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref this.updateCount);
            }
        }
    }

    /// <summary>
    ///     Delays SQLite transaction commit until a test explicitly releases
    ///     the gate.
    /// </summary>
    private sealed class CommitGateInterceptor : DbTransactionInterceptor
    {
        /// <summary>
        ///     Gets or sets a value indicating whether commit interception is
        ///     enabled after test database migration.
        /// </summary>
        internal bool Enabled { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the intercepted commit
        ///     throws instead of completing.
        /// </summary>
        internal bool ThrowOnCommit { get; init; }

        /// <summary>
        ///     Gets the task completed when a commit enters the gate.
        /// </summary>
        internal TaskCompletionSource<bool> CommitEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        ///     Gets the task completion source that releases the commit.
        /// </summary>
        internal TaskCompletionSource<bool> ReleaseCommit { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (!this.Enabled)
            {
                return result;
            }

            this.CommitEntered.TrySetResult(true);
            if (this.ThrowOnCommit)
            {
                throw new InvalidOperationException("Commit failed.");
            }

            await this.ReleaseCommit.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
    }
}
