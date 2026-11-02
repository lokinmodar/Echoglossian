// <copyright file="GameWindowPersistenceWriterTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Data.Common;
using Echoglossian.Cache;
using Echoglossian.DBHelpers;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Echoglossian.Tests.Persistence;

/// <summary>Checks managed GameWindow requests against real SQLite and the production coordinator.</summary>
public sealed class GameWindowPersistenceWriterTests
{
    private static readonly TranslationReuseScope Scope = new("en", "pt", 0, true);

    /// <summary>Startup admission failures and query failures recover without frame-driven retries.</summary>
    /// <param name="rejected">Whether initial admission is rejected instead of the query failing.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PreloadRetriesUntilCommittedProjectionIsAvailable(bool rejected)
    {
        GameWindowCacheManager.Clear();
        await using var coordinator = new PreloadCoordinator(rejected ? PersistenceCompletionStatus.Rejected : PersistenceCompletionStatus.Failed);
        var writer = new GameWindowPersistenceWriter(coordinator);
        try
        {
            await writer.PreloadAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(2, coordinator.Attempts);
            Assert.True(GameWindowCacheManager.IsPreloaded);
            Assert.Equal("translated", GameWindowCacheManager.TryFindMatch("ActionMenu", Scope, "7.3", "original")!.TranslatedWindowStrings);
        }
        finally
        {
            writer.DisablePublication();
            GameWindowCacheManager.Clear();
        }
    }

    /// <summary>Owner shutdown cancels the startup retry delay and leaves no faulted task or further admission.</summary>
    [Fact]
    public async Task PreloadRetryStopsGracefullyOnShutdown()
    {
        GameWindowCacheManager.Clear();
        await using var coordinator = new PreloadCoordinator(PersistenceCompletionStatus.Rejected);
        var writer = new GameWindowPersistenceWriter(coordinator);
        var completion = writer.PreloadAsync();
        Assert.Equal(1, coordinator.Attempts);
        writer.DisablePublication();
        await completion.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, coordinator.Attempts);
        Assert.False(GameWindowCacheManager.IsPreloaded);
    }

    /// <summary>Preload publishes only the committed database projection.</summary>
    [Fact]
    public async Task PreloadPublishesCommittedRows()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SeedAsync(Row());
        Assert.False(GameWindowCacheManager.IsPreloaded);
        harness.Writer.TryLoadAll(out var completion);
        Assert.Equal(PersistenceCompletionStatus.Succeeded, (await completion).Status);
        Assert.True(GameWindowCacheManager.IsPreloaded);
        Assert.Equal("translated", GameWindowCacheManager.TryFindMatch("ActionMenu", Scope, "7.3", "original")!.TranslatedWindowStrings);
    }

    /// <summary>Accepted writes drain after runtime retirement without late cache publication.</summary>
    [Fact]
    public async Task ShutdownDrainsAcceptedWriteWithoutPublication()
    {
        var gate = new CommitGate();
        await using var harness = await Harness.CreateAsync(gate);
        gate.Enabled = true;
        harness.Writer.TryPersist(Row(), out var completion);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            harness.Writer.DisablePublication();
            harness.Coordinator.StopAccepting();
            gate.Release.TrySetResult(true);
            await harness.Coordinator.CompleteAsync();
            Assert.Equal(PersistenceCompletionStatus.Succeeded, (await completion).Status);
            Assert.Single(await harness.StoredAsync());
            Assert.Null(GameWindowCacheManager.TryFindMatch("ActionMenu", Scope, "7.3", "original"));
        }
        finally
        {
            gate.Release.TrySetResult(true);
        }
    }

    /// <summary>Pending same-identity writes retain the newest payload and distinct payload variants remain separate.</summary>
    [Fact]
    public async Task PendingWritesCoalesceLatestCompleteValue()
    {
        var gate = new CommitGate();
        await using var harness = await Harness.CreateAsync(gate);
        gate.Enabled = true;
        var blocker = Row();
        blocker.OriginalWindowStrings = "blocker";
        harness.Writer.TryPersist(blocker, out var blocked);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            harness.Writer.TryPersist(Row(), out var first);
            var latest = Row();
            latest.TranslatedWindowStrings = "latest";
            Assert.Equal(PersistenceAdmissionStatus.Replaced, harness.Writer.TryPersist(latest, out var replacement));
            Assert.Same(first, replacement);
            var variant = Row();
            variant.OriginalWindowStrings = "variant";
            Assert.Equal(PersistenceAdmissionStatus.Accepted, harness.Writer.TryPersist(variant, out var distinct));
            gate.Release.TrySetResult(true);
            await Task.WhenAll(blocked, first, distinct).WaitAsync(TimeSpan.FromSeconds(5));
            var rows = await harness.StoredAsync();
            Assert.Equal(3, rows.Count);
            Assert.Equal("latest", rows.Single(row => row.OriginalWindowStrings == "original").TranslatedWindowStrings);
        }
        finally
        {
            gate.Release.TrySetResult(true);
        }
    }

    /// <summary>Snapshots isolate pending writes and publication waits for the actual SQLite commit.</summary>
    [Fact]
    public async Task WritePublishesOnlyAfterCommitAndCapturesCaller()
    {
        var gate = new CommitGate();
        await using var harness = await Harness.CreateAsync(gate);
        gate.Enabled = true;
        var row = Row();
        harness.Writer.TryPersist(row, out var completion);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            row.TranslatedWindowStrings = "caller mutated";
            Assert.False(completion.IsCompleted);
            Assert.Null(GameWindowCacheManager.TryFindMatch("ActionMenu", Scope, "7.3", "original"));
            gate.Release.TrySetResult(true);
            Assert.Equal(PersistenceCompletionStatus.Succeeded, (await completion.WaitAsync(TimeSpan.FromSeconds(5))).Status);
            var stored = Assert.Single(await harness.StoredAsync());
            Assert.Equal("translated", stored.TranslatedWindowStrings);
            Assert.Equal(stored.Id, GameWindowCacheManager.TryFindMatch("ActionMenu", Scope, "7.3", "original")!.Id);
        }
        finally
        {
            gate.Release.TrySetResult(true);
        }
    }

    /// <summary>An unchanged payload leaves its timestamp and SQL UPDATE count unchanged.</summary>
    [Fact]
    public async Task EquivalentWriteEmitsNoUpdate()
    {
        var counter = new UpdateCounter();
        await using var harness = await Harness.CreateAsync(counter);
        harness.Writer.TryPersist(Row(), out var first);
        Assert.Equal(PersistenceCompletionStatus.Succeeded, (await first).Status);
        var before = Assert.Single(await harness.StoredAsync());
        harness.Writer.TryPersist(Row(), out var second);
        Assert.Equal(PersistenceCompletionStatus.Unchanged, (await second).Status);
        var after = Assert.Single(await harness.StoredAsync());
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.UpdatedDate, after.UpdatedDate);
        Assert.Equal(0, counter.Updates);
    }

    /// <summary>A failed SQLite commit rolls back the transaction and publishes no cache entry.</summary>
    [Fact]
    public async Task FailedCommitDoesNotPublish()
    {
        var gate = new CommitGate { ThrowOnCommit = true };
        await using var harness = await Harness.CreateAsync(gate);
        gate.Enabled = true;
        harness.Writer.TryPersist(Row(), out var completion);
        Assert.Equal(PersistenceCompletionStatus.Failed, (await completion.WaitAsync(TimeSpan.FromSeconds(5))).Status);
        Assert.Empty(await harness.StoredAsync());
        Assert.Null(GameWindowCacheManager.TryFindMatch("ActionMenu", Scope, "7.3", "original"));
    }

    /// <summary>Legacy version-agnostic storage is updated in place with normalized language aliases.</summary>
    [Fact]
    public async Task SavePreservesVersionAgnosticIdentity()
    {
        await using var harness = await Harness.CreateAsync();
        var legacy = Row();
        legacy.GameVersion = null;
        legacy.OriginalWindowStringsLang = "English";
        legacy.TranslationLang = "pt-BR";
        await harness.SeedAsync(legacy);
        harness.Writer.TryPersist(Row(), out var completion);
        Assert.Equal(PersistenceCompletionStatus.Succeeded, (await completion).Status);
        var stored = Assert.Single(await harness.StoredAsync());
        Assert.Equal(legacy.Id, stored.Id);
        Assert.Null(stored.GameVersion);
        Assert.Equal("pt-BR", stored.TranslationLang);
    }


    /// <summary>Distinct payload, job, engine, source, target and version identities cannot coalesce.</summary>
    /// <param name="field">The identity component changed.</param>
    [Theory]
    [InlineData("addon")]
    [InlineData("source")]
    [InlineData("target")]
    [InlineData("engine")]
    [InlineData("version")]
    [InlineData("job")]
    [InlineData("payload")]
    public void IdentityKeepsAllLookupDiscriminators(string field)
    {
        var row = Row();
        var scope = Scope;
        switch (field)
        {
            case "addon": row.WindowAddonName = "_MainCommand"; break;
            case "source": scope = new("ja", "pt", 0, true); break;
            case "target": scope = new("en", "fr", 0, true); break;
            case "engine": scope = new("en", "pt", 1, true); break;
            case "version": row.GameVersion = "7.4"; break;
            case "job": row.ClassJobId = 19; break;
            case "payload": row.OriginalWindowStrings = "other"; break;
        }

        Assert.NotEqual(GameWindowPersistenceWriter.BuildIdentity(Row(), Scope), GameWindowPersistenceWriter.BuildIdentity(row, scope));
    }

    /// <summary>Aliases share admission identity while engine-agnostic reads remain distinct from strict reads.</summary>
    [Fact]
    public void IdentityNormalizesAliasesAndSeparatesReusePolicy()
    {
        Assert.Equal(GameWindowPersistenceWriter.BuildIdentity(Row(), Scope),
            GameWindowPersistenceWriter.BuildIdentity(Row(), new("English", "pt-BR", 0, true)));
        Assert.NotEqual(GameWindowPersistenceWriter.BuildIdentity(Row(), Scope),
            GameWindowPersistenceWriter.BuildIdentity(Row(), new("en", "pt", 0, false)));
    }

    /// <summary>Blank versions share the version-agnostic persisted write identity.</summary>
    [Fact]
    public void IdentityNormalizesBlankVersions()
    {
        var row = Row();
        row.GameVersion = null;
        var identity = GameWindowPersistenceWriter.BuildIdentity(row, Scope);
        row.GameVersion = string.Empty;
        Assert.Equal(identity, GameWindowPersistenceWriter.BuildIdentity(row, Scope));
        row.GameVersion = " ";
        Assert.Equal(identity, GameWindowPersistenceWriter.BuildIdentity(row, Scope));
    }

    /// <summary>Creates a complete captured request.</summary>
    private static GameWindow Row() => new("ActionMenu", "original", "en", "translated", "pt", 0, "7.3", null, null);

    /// <summary>Owns an isolated SQLite database and the production persistence workers.</summary>
    private sealed class Harness : IAsyncDisposable
    {
        private readonly string directory;
        private readonly Factory factory;

        /// <summary>Initializes an isolated migrated fixture.</summary>
        /// <param name="directory">The isolated fixture directory.</param>
        /// <param name="factory">The short-lived SQLite context factory.</param>
        private Harness(string directory, Factory factory)
        {
            this.directory = directory;
            this.factory = factory;
            this.Coordinator = new PersistenceCoordinator(factory);
            this.Writer = new GameWindowPersistenceWriter(this.Coordinator);
        }

        /// <summary>Gets the production coordinator.</summary>
        internal PersistenceCoordinator Coordinator { get; }

        /// <summary>Gets the production domain adapter.</summary>
        internal GameWindowPersistenceWriter Writer { get; }

        /// <summary>Creates the SQLite schema before enabling interception gates.</summary>
        /// <param name="interceptor">The optional database boundary interceptor.</param>
        /// <returns>An isolated migrated fixture.</returns>
        internal static async Task<Harness> CreateAsync(IInterceptor? interceptor = null)
        {
            GameWindowCacheManager.Clear();
            var directory = Path.Combine(Path.GetTempPath(), "EchoglossianTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var factory = new Factory(Path.Combine(directory, "test.db"), interceptor);
            await using var context = await factory.CreateDbContextAsync();
            await context.Database.MigrateAsync();
            return new Harness(directory, factory);
        }

        /// <summary>Seeds committed data without publishing it to the cache.</summary>
        /// <param name="row">The row to insert.</param>
        /// <returns>A task completed after the row commits.</returns>
        internal async Task SeedAsync(GameWindow row)
        {
            await using var context = await this.factory.CreateDbContextAsync();
            context.GameWindow.Add(row);
            await context.SaveChangesAsync();
        }

        /// <summary>Reads actual persisted rows independently of the runtime projection.</summary>
        /// <returns>The detached committed rows.</returns>
        internal async Task<List<GameWindow>> StoredAsync()
        {
            await using var context = await this.factory.CreateDbContextAsync();
            return await context.GameWindow.AsNoTracking().ToListAsync();
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            this.Writer.DisablePublication();
            await this.Coordinator.DisposeAsync();
            GameWindowCacheManager.Clear();
            SqliteConnection.ClearAllPools();
            Directory.Delete(this.directory, recursive: true);
        }
    }

    /// <summary>Rejects synchronous factory use so tests prove the coordinator worker path.</summary>
    private sealed class Factory : IDbContextFactory<EchoglossianDbContext>
    {
        private readonly DbContextOptions<EchoglossianDbContext> options;

        /// <summary>Captures SQLite options and optional external-boundary interception.</summary>
        /// <param name="path">The isolated SQLite database path.</param>
        /// <param name="interceptor">The optional database boundary interceptor.</param>
        internal Factory(string path, IInterceptor? interceptor)
        {
            var builder = new DbContextOptionsBuilder<EchoglossianDbContext>().UseSqlite("Data Source=" + path);
            if (interceptor != null)
            {
                builder.AddInterceptors(interceptor);
            }

            this.options = builder.Options;
        }

        /// <inheritdoc />
        public EchoglossianDbContext CreateDbContext() => throw new InvalidOperationException("Synchronous fallback is forbidden.");

        /// <inheritdoc />
        public Task<EchoglossianDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new EchoglossianDbContext(this.options));
        }
    }

    /// <summary>Returns one terminal startup failure followed by a successful committed projection.</summary>
    private sealed class PreloadCoordinator : IPersistenceCoordinator
    {
        private readonly PersistenceCompletionStatus initialStatus;

        /// <summary>Captures the terminal first-attempt result.</summary>
        /// <param name="initialStatus">The terminal outcome of the first attempt.</param>
        internal PreloadCoordinator(PersistenceCompletionStatus initialStatus)
        {
            this.initialStatus = initialStatus;
        }

        /// <summary>Gets the number of preload admissions.</summary>
        internal int Attempts { get; private set; }

        /// <inheritdoc />
        public PersistenceAdmissionStatus TryScheduleRead<T>(PersistenceWorkKey key, PersistencePriority priority,
            Func<EchoglossianDbContext, CancellationToken, Task<T>> readAsync, Action<T>? publish,
            out Task<PersistenceReadResult<T>> completion)
        {
            this.Attempts++;
            if (this.Attempts == 1)
            {
                completion = Task.FromResult(new PersistenceReadResult<T>(this.initialStatus, default, null));
                return this.initialStatus == PersistenceCompletionStatus.Rejected
                    ? PersistenceAdmissionStatus.RejectedCapacity : PersistenceAdmissionStatus.Accepted;
            }

            var rows = (T)(object)new List<GameWindow> { Row() };
            publish?.Invoke(rows);
            completion = Task.FromResult(new PersistenceReadResult<T>(PersistenceCompletionStatus.Succeeded, rows, null));
            return PersistenceAdmissionStatus.Accepted;
        }

        /// <inheritdoc />
        public PersistenceAdmissionStatus TryScheduleWrite(PersistenceWriteRequest request, out Task<PersistenceWriteResult> completion)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public PersistenceMetricsSnapshot GetMetrics() => throw new NotSupportedException();

        /// <inheritdoc />
        public void StopAccepting()
        {
        }

        /// <inheritdoc />
        public Task CompleteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Pauses or fails the actual database commit.</summary>
    private sealed class CommitGate : DbTransactionInterceptor
    {
        /// <summary>Gets or sets whether commits are intercepted.</summary>
        internal bool Enabled { get; set; }

        /// <summary>Gets or sets whether the intercepted commit fails.</summary>
        internal bool ThrowOnCommit { get; set; }

        /// <summary>Gets the signal that the transaction reached its commit.</summary>
        internal TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the gate releasing the pending commit.</summary>
        internal TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (this.Enabled)
            {
                this.Entered.TrySetResult(true);
                if (this.ThrowOnCommit)
                {
                    throw new InvalidOperationException("Injected commit failure.");
                }

                await this.Release.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }

    /// <summary>Counts SQL UPDATE commands to prove unchanged rows remain unmodified.</summary>
    private sealed class UpdateCounter : DbCommandInterceptor
    {
        /// <summary>Gets the observed UPDATE count.</summary>
        internal int Updates { get; private set; }

        /// <inheritdoc />
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                this.Updates++;
            }

            return ValueTask.FromResult(result);
        }
    }
}
