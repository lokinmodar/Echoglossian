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
