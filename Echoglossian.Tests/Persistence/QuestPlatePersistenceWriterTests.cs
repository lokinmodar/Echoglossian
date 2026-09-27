// <copyright file="QuestPlatePersistenceWriterTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.DBHelpers;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models.Journal;
using Echoglossian.NativeUI.Helpers;
using Echoglossian.Persistence;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Echoglossian.Tests.Persistence;

/// <summary>
///     Verifies the coordinator-backed QuestPlate projection contract against
///     a real temporary SQLite database.
/// </summary>
public sealed class QuestPlatePersistenceWriterTests
{
    /// <summary>
    ///     Ensures a cache miss schedules one asynchronous lookup and publishes
    ///     its immutable projection only after the read completes.
    /// </summary>
    [Fact]
    public async Task TryFind_CacheMiss_PublishesOnlyAfterAsyncReadCompletes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "EchoglossianTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var factory = new EchoglossianDbContextRuntimeFactory(directory);
            await using (var context = await factory.CreateDbContextAsync())
            {
                await context.Database.MigrateAsync();
                context.QuestPlate.Add(CreatePlate());
                await context.SaveChangesAsync();
            }

            await using var coordinator = new PersistenceCoordinator(factory, CreateOptions());
            var cache = new QuestPlateRuntimeCache();
            var writer = new QuestPlatePersistenceWriter(coordinator, cache);
            var probe = CreatePlate();
            var scope = new TranslationReuseScope("en", "pt-BR", 1, true);

            Assert.False(cache.TryGet(probe, scope, out _));
            Assert.Equal(
                PersistenceAdmissionStatus.Accepted,
                writer.TryFind(probe, scope, PersistencePriority.Background, out var completion));

            Assert.Equal(PersistenceCompletionStatus.Succeeded, (await completion.WaitAsync(TimeSpan.FromSeconds(5))).Status);
            Assert.True(cache.TryGet(probe, scope, out var projection));
            Assert.Equal("Missão traduzida", projection.TranslatedQuestName);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    ///     Ensures a same-key request joins the cache-owned operation instead
    ///     of admitting a second coordinator query.
    /// </summary>
    [Fact]
    public void TryFind_SameCanonicalKey_JoinsExistingOperation()
    {
        var cache = new QuestPlateRuntimeCache();
        var key = QuestPlateRuntimeCache.CreateKey(CreatePlate(), new TranslationReuseScope("en", "pt-BR", 1, true));
        var first = new TaskCompletionSource<QuestPlateRuntimeResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True(cache.TryRegister(key, first.Task));
        Assert.False(cache.TryRegister(key, Task.FromResult(default(QuestPlateRuntimeResult))));
        Assert.True(cache.TryGetCompletion(key, out var completion));
        Assert.Same(first.Task, completion);
    }

    /// <summary>
    ///     Ensures an upsert claimed while a same-key read is queued remains
    ///     the key owner until its committed terminal result, rather than being
    ///     discarded as an ordinary read join.
    /// </summary>
    [Fact]
    public void RuntimeCache_ReadThenWrite_ClaimsOneDeferredWriteUntilTerminalCommit()
    {
        var cache = new QuestPlateRuntimeCache();
        var key = QuestPlateRuntimeCache.CreateKey(CreatePlate(), new TranslationReuseScope("en", "pt-BR", 1, true));
        var read = new TaskCompletionSource<QuestPlateRuntimeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var write = new TaskCompletionSource<QuestPlateRuntimeResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True(cache.TryRegister(key, read, out var generation));
        Assert.True(cache.TryRegisterWriteOrJoin(key, write, out var writeGeneration, out var joined, out var readCompletion));
        Assert.Equal(generation, writeGeneration);
        Assert.Null(joined);
        Assert.Same(read.Task, readCompletion);

        Assert.True(cache.Complete(key, generation, new QuestPlateRuntimeResult(PersistenceCompletionStatus.Succeeded, null)));
        Assert.True(cache.TryPromoteDeferredWrite(key, generation, write));
        Assert.True(cache.Complete(key, generation, new QuestPlateRuntimeResult(PersistenceCompletionStatus.Succeeded, CreatePlate())));
        Assert.True(cache.TryGet(CreatePlate(), new TranslationReuseScope("en", "pt-BR", 1, true), out var projection));
        Assert.Equal("Missão traduzida", projection.TranslatedQuestName);
    }

    /// <summary>
    ///     Ensures a deferred write remains owned after its read terminal
    ///     cooldown expires, so a competing lookup cannot evict it before the
    ///     deferred coordinator write is promoted and completed.
    /// </summary>
    [Fact]
    public async Task RuntimeCache_DeferredWrite_SurvivesReadCooldownUntilPromotion()
    {
        var now = DateTimeOffset.UnixEpoch;
        var cache = new QuestPlateRuntimeCache(() => now, TimeSpan.FromSeconds(1));
        var key = QuestPlateRuntimeCache.CreateKey(CreatePlate(), new TranslationReuseScope("en", "pt-BR", 1, true));
        var read = new TaskCompletionSource<QuestPlateRuntimeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var write = new TaskCompletionSource<QuestPlateRuntimeResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True(cache.TryRegister(key, read, out var generation));
        Assert.True(cache.TryRegisterWriteOrJoin(key, write, out _, out var joinedWrite, out var readCompletion));
        Assert.Null(joinedWrite);
        Assert.Same(read.Task, readCompletion);
        Assert.True(cache.Complete(key, generation, new QuestPlateRuntimeResult(PersistenceCompletionStatus.Succeeded, null)));

        now = now.AddSeconds(1);
        var competingLookup = new TaskCompletionSource<QuestPlateRuntimeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.False(cache.TryRegisterOrJoin(key, competingLookup, out _, out var joinedLookup));
        Assert.Same(read.Task, joinedLookup);
        var competingWrite = new TaskCompletionSource<QuestPlateRuntimeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.False(cache.TryRegisterWriteOrJoin(key, competingWrite, out _, out var joinedDeferredWrite, out _));
        Assert.Same(write.Task, joinedDeferredWrite);

        Assert.True(cache.TryPromoteDeferredWrite(key, generation, write));
        var committed = CreatePlate();
        Assert.True(cache.Complete(key, generation, new QuestPlateRuntimeResult(PersistenceCompletionStatus.Succeeded, committed)));
        Assert.Equal(PersistenceCompletionStatus.Succeeded, (await write.Task).Status);
        Assert.True(cache.TryGet(committed, new TranslationReuseScope("en", "pt-BR", 1, true), out var projection));
        Assert.Equal(committed.TranslatedQuestName, projection.TranslatedQuestName);
    }

    /// <summary>
    ///     Ensures a commit-only legacy merge advances and publishes the
    ///     persisted timestamp instead of exposing an uncommitted projection.
    /// </summary>
    [Fact]
    public async Task TryPersist_IdenticalExistingRow_CommitsLegacyTimestampBeforeCachePublication()
    {
        var directory = Path.Combine(Path.GetTempPath(), "EchoglossianTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var factory = new EchoglossianDbContextRuntimeFactory(directory);
            var stored = CreatePlate();
            stored.UpdatedDate = DateTime.UtcNow.AddDays(-1);
            await using (var context = await factory.CreateDbContextAsync())
            {
                await context.Database.MigrateAsync();
                context.QuestPlate.Add(stored);
                await context.SaveChangesAsync();
            }

            await using var coordinator = new PersistenceCoordinator(factory, CreateOptions());
            var cache = new QuestPlateRuntimeCache();
            var writer = new QuestPlatePersistenceWriter(coordinator, cache);
            var incoming = CreatePlate();
            var scope = new TranslationReuseScope("en", "pt-BR", 1, true);

            Assert.Equal(PersistenceAdmissionStatus.Accepted, writer.TryPersist(incoming, scope, PersistencePriority.Background, out var completion));
            Assert.Equal(PersistenceCompletionStatus.Succeeded, (await completion.WaitAsync(TimeSpan.FromSeconds(5))).Status);
            Assert.True(cache.TryGet(incoming, scope, out var cached));
            Assert.True(cached.UpdatedDate > stored.UpdatedDate);
            await using var verification = await factory.CreateDbContextAsync();
            var persisted = await verification.QuestPlate.SingleAsync();
            Assert.Equal(persisted.UpdatedDate, cached.UpdatedDate);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    ///     Ensures a write arriving behind a same-key read is serialized through
    ///     the registry and persists its requested translation after the read's
    ///     terminal miss.
    /// </summary>
    [Fact]
    public async Task TryFind_ThenTryPersist_SameKey_CommitsDeferredUpsert()
    {
        var directory = Path.Combine(Path.GetTempPath(), "EchoglossianTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var factory = new EchoglossianDbContextRuntimeFactory(directory);
            await using (var context = await factory.CreateDbContextAsync())
            {
                await context.Database.MigrateAsync();
            }

            await using var coordinator = new PersistenceCoordinator(factory, CreateOptions());
            var cache = new QuestPlateRuntimeCache();
            var writer = new QuestPlatePersistenceWriter(coordinator, cache);
            var incoming = CreatePlate();
            incoming.TranslatedQuestMessage = "Persistida após leitura";
            var scope = new TranslationReuseScope("en", "pt-BR", 1, true);

            Assert.Equal(PersistenceAdmissionStatus.Accepted, writer.TryFind(incoming, scope, PersistencePriority.Background, out var read));
            Assert.Equal(PersistenceAdmissionStatus.Joined, writer.TryPersist(incoming, scope, PersistencePriority.Background, out var write));
            Assert.Equal(PersistenceCompletionStatus.Succeeded, (await read.WaitAsync(TimeSpan.FromSeconds(5))).Status);
            Assert.Equal(PersistenceCompletionStatus.Succeeded, (await write.WaitAsync(TimeSpan.FromSeconds(5))).Status);
            await using var verification = await factory.CreateDbContextAsync();
            Assert.Equal("Persistida após leitura", (await verification.QuestPlate.SingleAsync()).TranslatedQuestMessage);
            Assert.True(cache.TryGet(incoming, scope, out var cached));
            Assert.Equal("Persistida após leitura", cached.TranslatedQuestMessage);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    ///     Ensures a terminal empty operation blocks frame-style retry only until
    ///     the injected cooldown clock expires.
    /// </summary>
    [Fact]
    public void RuntimeCache_EmptyCompletion_AdmitsRetryAfterCooldown()
    {
        var now = DateTimeOffset.UnixEpoch;
        var cache = new QuestPlateRuntimeCache(() => now, TimeSpan.FromSeconds(1));
        var key = QuestPlateRuntimeCache.CreateKey(CreatePlate(), new TranslationReuseScope("en", "pt-BR", 1, true));
        var first = new TaskCompletionSource<QuestPlateRuntimeResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True(cache.TryRegister(key, first, out var generation));
        Assert.True(cache.Complete(key, generation, new QuestPlateRuntimeResult(PersistenceCompletionStatus.Succeeded, null)));
        Assert.True(cache.TryGetCompletion(key, out _));

        now = now.AddSeconds(1);
        Assert.False(cache.TryGetCompletion(key, out _));
        Assert.True(cache.TryRegister(key, new TaskCompletionSource<QuestPlateRuntimeResult>(), out _));
    }

    /// <summary>
    ///     Ensures a generation replacement atomically rejects an old owner and
    ///     lets the replacement own the same key.
    /// </summary>
    [Fact]
    public void RuntimeCache_ReloadRace_DoesNotPublishOrBlockReplacement()
    {
        var cache = new QuestPlateRuntimeCache();
        var key = QuestPlateRuntimeCache.CreateKey(CreatePlate(), new TranslationReuseScope("en", "pt-BR", 1, true));
        var oldOwner = new TaskCompletionSource<QuestPlateRuntimeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(cache.TryRegister(key, oldOwner, out var oldGeneration));

        _ = cache.AdvanceGeneration();
        var replacement = new TaskCompletionSource<QuestPlateRuntimeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(cache.TryRegister(key, replacement, out var replacementGeneration));
        Assert.False(cache.Complete(key, oldGeneration, new QuestPlateRuntimeResult(PersistenceCompletionStatus.Succeeded, CreatePlate())));
        Assert.True(cache.Complete(key, replacementGeneration, new QuestPlateRuntimeResult(PersistenceCompletionStatus.Succeeded, CreatePlate())));
        Assert.True(cache.TryGet(CreatePlate(), new TranslationReuseScope("en", "pt-BR", 1, true), out _));
    }

    /// <summary>
    ///     Ensures the async read policy retains the legacy message fallback
    ///     exclusion for a row belonging to a different canonical QuestId.
    /// </summary>
    [Fact]
    public void SelectForRead_QuestIdMiss_DoesNotReuseDifferentQuestIdMessageRow()
    {
        var probe = CreatePlate();
        var other = CreatePlate();
        other.QuestId = "other";
        other.Id = 99;

        var selected = QuestPlatePersistencePolicy.SelectForRead(
            [other],
            probe,
            new TranslationReuseScope("en", "pt-BR", 1, true));

        Assert.Null(selected);
    }

    /// <summary>
    ///     Ensures the coordinator-backed write merges a later translation and
    ///     publishes the committed result to the shared projection.
    /// </summary>
    [Fact]
    public async Task TryPersist_ExistingRow_MergesAndPublishesAfterCommit()
    {
        var directory = Path.Combine(Path.GetTempPath(), "EchoglossianTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var factory = new EchoglossianDbContextRuntimeFactory(directory);
            await using (var context = await factory.CreateDbContextAsync())
            {
                await context.Database.MigrateAsync();
                context.QuestPlate.Add(CreatePlate());
                await context.SaveChangesAsync();
            }

            await using var coordinator = new PersistenceCoordinator(factory, CreateOptions());
            var cache = new QuestPlateRuntimeCache();
            var writer = new QuestPlatePersistenceWriter(coordinator, cache);
            var incoming = CreatePlate();
            incoming.TranslatedQuestMessage = "Mensagem nova";
            var scope = new TranslationReuseScope("en", "pt-BR", 1, true);

            Assert.Equal(PersistenceAdmissionStatus.Accepted, writer.TryPersist(incoming, scope, PersistencePriority.Background, out var completion));
            Assert.Equal(PersistenceCompletionStatus.Succeeded, (await completion.WaitAsync(TimeSpan.FromSeconds(5))).Status);
            Assert.True(cache.TryGet(incoming, scope, out var cached));
            Assert.Equal("Mensagem nova", cached.TranslatedQuestMessage);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Creates a stable canonical QuestPlate fixture.</summary>
    /// <returns>The requested quest plate.</returns>
    private static QuestPlate CreatePlate()
    {
        return new QuestPlate(
            "Quest original",
            "Mensagem original",
            "en",
            "Missão traduzida",
            "Mensagem traduzida",
            "42",
            "pt-BR",
            1,
            DateTime.UtcNow,
            DateTime.UtcNow,
            "7.4")
        {
            SourceContentHash = "hash-42",
            QuestTextSheetName = "quest/042",
        };
    }

    /// <summary>Creates deterministic bounded coordinator options.</summary>
    /// <returns>The configured options.</returns>
    private static PersistenceCoordinatorOptions CreateOptions()
    {
        return new PersistenceCoordinatorOptions(8, 8, 1, 32, TimeSpan.FromMilliseconds(1), 1, [], 4, 1, TimeSpan.FromSeconds(5));
    }
}
