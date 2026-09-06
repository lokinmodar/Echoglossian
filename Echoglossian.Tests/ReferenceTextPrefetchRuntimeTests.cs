// <copyright file="ReferenceTextPrefetchRuntimeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Cache;
using Echoglossian.DBHelpers;
using Echoglossian.EFCoreSqlite;
using Echoglossian.NativeUI.Helpers;
using Echoglossian.Persistence;
using Echoglossian.Translators;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>Verifies cache-first canonical prefetch behavior.</summary>
public class ReferenceTextPrefetchRuntimeTests
{
    /// <summary>Completed rows must not cause redundant writes or translation.</summary>
    [Fact]
    public void CompletedRow_DoesNotPersistOrTranslate()
    {
        using var languageFixture = new PrefetchBrokerSourceScopeTests();
        var payload = new ReferenceTextCanonicalPayload
        {
            ReferenceId = 12,
            Name = "Actions",
            Description = "Open actions.",
            TranslatedName = "Acoes",
            TranslatedDescription = "Abre acoes.",
        };
        var writes = 0;
        var result = PluginEntry.RunReferenceTextPrefetchOperationEntry(
            "MainCommandPrefetch", payload, "7.3",
            () => new SourceClientLanguage("en", "en"),
            new Config { Lang = 81, ChosenTransEngine = 4 },
            (string _, out string value) => { value = string.Empty; throw new InvalidOperationException("Broker reached for completed row."); },
            (_, _, _) => throw new InvalidOperationException("Translation queued for completed row."),
            (_, _, _, _) => throw new InvalidOperationException("Translation started for completed row."),
            (source, target, engine, version, original, translated) => ReferenceTextPersistenceHelper.CreateCanonicalRow<MainCommandText>(source, target, engine, version, original, translated),
            probe => ReferenceTextPersistenceHelper.CreateCanonicalRow<MainCommandText>(probe.OriginalLang!, probe.TranslationLang!, probe.TranslationEngine, probe.GameVersion, payload, payload),
            _ => writes++, out _, out _, out _);
        Assert.Equal(0, writes);
        Assert.Equal(PrefetchTranslationDispatchResult.Rejected, result);
    }

    /// <summary>Only one canonical write may publish a complete field batch.</summary>
    [Theory]
    [InlineData(null, null, 2)]
    [InlineData("Acoes", null, 1)]
    [InlineData(null, "Abre acoes.", 1)]
    public async Task MissingFields_AreTranslatedTogetherAndCommittedAtomically(string? name, string? description, int expectedFields)
    {
        await using var harness = await Harness.CreateAsync();
        if (name is not null || description is not null)
        {
            await harness.SeedAsync(name, description);
        }

        var calls = 0;
        var capturedPayload = Harness.Payload();
        var operation = harness.Start(capturedPayload, (fields, source, target, _, _) =>
        {
            calls++;
            Assert.Equal(expectedFields, fields.Count);
            Assert.Equal("en", source.PersistenceCode);
            Assert.Equal("pt", target);
            return Task.FromResult(new TranslationFieldBatchResult(fields.Select(field =>
                new TranslationField(field.Name, field.Name == "Name" ? "Acoes" : "Abre acoes.")), false));
        });
        capturedPayload.Name = "Changed after capture";
        capturedPayload.CategoryId = 999;
        Assert.True(await operation.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, calls);
        await using var context = await harness.Factory.CreateDbContextAsync();
        var persisted = await context.MainCommandTexts.SingleAsync();
        Assert.Equal("Actions", persisted.OriginalName);
        Assert.Equal("Acoes", persisted.TranslatedName);
        Assert.Equal("Abre acoes.", persisted.TranslatedDescription);
        var canonical = ReferenceTextCanonicalPayload.Deserialize(persisted.CanonicalPayloadAsText)!;
        Assert.Equal((uint)7, canonical.CategoryId);
        Assert.Equal((uint)42, canonical.IconId);
        Assert.Equal((uint)7, persisted.CategoryId);
        Assert.Equal((uint)42, persisted.IconId);
        Assert.Equal("7.3", persisted.GameVersion);
        Assert.Equal(0, persisted.TranslationEngine);
        Assert.Equal("Acoes", harness.Cache.TryFindCanonicalMatch(12, Harness.Scope, "7.3", harness.Probe.SourceContentHash!)!.TranslatedName);
    }

    /// <summary>Complete rows, including unchanged text, require neither translation nor another write.</summary>
    [Theory]
    [InlineData(false, "Acoes", "Abre acoes.")]
    [InlineData(true, "Acoes", "Abre acoes.")]
    [InlineData(true, "Actions", "Open actions.")]
    public async Task CompleteCanonicalRow_SkipsTranslationAndWrites(bool prepopulateCache, string name, string description)
    {
        await using var harness = await Harness.CreateAsync();
        var row = await harness.SeedAsync(name, description);
        if (prepopulateCache)
        {
            harness.Cache.Update(row);
        }

        var before = harness.Coordinator.GetMetrics();
        Assert.True(await harness.Start(Harness.Payload(), (_, _, _, _, _) => throw new InvalidOperationException("Complete row translated.")).WaitAsync(TimeSpan.FromSeconds(5)));
        var after = harness.Coordinator.GetMetrics();
        Assert.Equal(before.CommittedWrites, after.CommittedWrites);
        Assert.NotNull(harness.Cache.TryFindCanonicalMatch(12, Harness.Scope, "7.3", harness.Probe.SourceContentHash!));
    }

    /// <summary>A saturated background lane must leave the request retryable and allow interactive reads.</summary>
    [Fact]
    public async Task BackgroundCapacity_RejectedRequestRetries_InteractiveRequestIsAdmitted()
    {
        await using var harness = await Harness.CreateAsync(backgroundCapacity: 1);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Coordinator.TryScheduleRead(new PersistenceWorkKey("test", "active"), PersistencePriority.Background,
            async (_, token) => { entered.SetResult(); await release.Task.WaitAsync(token); return 1; }, null, out var active);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.Coordinator.TryScheduleRead(new PersistenceWorkKey("test", "queued"), PersistencePriority.Background,
            (_, _) => Task.FromResult(1), null, out var queued);
        var rejected = harness.Start(Harness.Payload());
        Assert.True(rejected.IsCompletedSuccessfully);
        Assert.False(await rejected);
        var interactivePayload = Harness.Payload();
        interactivePayload.ReferenceId = 13;
        var interactive = harness.Start(interactivePayload, priority: PersistencePriority.Interactive);
        Assert.False(interactive.IsCompleted);
        release.SetResult();
        await Task.WhenAll(active, queued).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(await interactive.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(await harness.Start(Harness.Payload()).WaitAsync(TimeSpan.FromSeconds(5)));
    }

    /// <summary>Admission never opens a context or translates on the caller while the reader is busy.</summary>
    [Fact]
    public async Task PendingRead_ReturnsImmediately_AndDefersTranslation()
    {
        await using var harness = await Harness.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Coordinator.TryScheduleRead(new PersistenceWorkKey("test", "active"), PersistencePriority.Background,
            async (_, token) => { entered.SetResult(); await release.Task.WaitAsync(token); return 1; }, null, out _);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var translated = 0;
        var operation = harness.Start(Harness.Payload(), (fields, _, _, _, _) =>
        {
            Interlocked.Increment(ref translated);
            return Task.FromResult(Harness.Translate(fields));
        });
        Assert.False(operation.IsCompleted);
        Assert.Equal(0, Volatile.Read(ref translated));
        release.SetResult();
        Assert.True(await operation.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    /// <summary>Rejected writes retry from broker cache without losing or retranslating either field.</summary>
    [Fact]
    public async Task WriteBackpressure_RetriesCompleteBatchWithoutRetranslation()
    {
        await using var harness = await Harness.CreateAsync(backgroundCapacity: 1);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Coordinator.TryScheduleWrite(new PersistenceWriteRequest(new PersistenceWorkKey("test", "active"), PersistencePriority.Background,
            async (_, token) => { entered.SetResult(); await release.Task.WaitAsync(token); return PersistenceWriteMutation.UnchangedResult; }, () => { }), out var active);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.Coordinator.TryScheduleWrite(new PersistenceWriteRequest(new PersistenceWorkKey("test", "queued"), PersistencePriority.Background,
            (_, _) => Task.FromResult(PersistenceWriteMutation.UnchangedResult), () => { }), out var queued);
        var calls = 0;
        Task<TranslationFieldBatchResult> Translate(IReadOnlyList<TranslationField> fields, SourceClientLanguage source, string target, string? origin, CancellationToken token)
        {
            calls++;
            return Task.FromResult(Harness.Translate(fields));
        }

        Assert.False(await harness.Start(Harness.Payload(), Translate).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Null(harness.Cache.TryFindCanonicalMatch(12, Harness.Scope, "7.3", harness.Probe.SourceContentHash!));
        release.SetResult();
        await Task.WhenAll(active, queued).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(await harness.Start(Harness.Payload(), Translate).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, calls);
        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.Equal("Abre acoes.", (await context.MainCommandTexts.SingleAsync()).TranslatedDescription);
    }

    /// <summary>Same-row concurrent read continuations share coordinator and broker work.</summary>
    [Fact]
    public async Task DuplicateReadAndTranslation_ProduceOneCompleteRow()
    {
        await using var harness = await Harness.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Coordinator.TryScheduleRead(new PersistenceWorkKey("test", "active"), PersistencePriority.Background,
            async (_, token) => { entered.SetResult(); await release.Task.WaitAsync(token); return 1; }, null, out _);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var translations = 0;
        Task<TranslationFieldBatchResult> Translate(IReadOnlyList<TranslationField> fields, SourceClientLanguage source, string target, string? origin, CancellationToken token)
        {
            Interlocked.Increment(ref translations);
            return Task.FromResult(Harness.Translate(fields));
        }

        var first = harness.Start(Harness.Payload(), Translate);
        var second = harness.Start(Harness.Payload(), Translate);
        Assert.True(harness.Coordinator.GetMetrics().CoalescedOperations >= 1);
        release.SetResult();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains(true, results);
        Assert.Equal(1, translations);
        await using var context = await harness.Factory.CreateDbContextAsync();
        var row = await context.MainCommandTexts.SingleAsync();
        Assert.Equal("Acoes", row.TranslatedName);
        Assert.Equal("Abre acoes.", row.TranslatedDescription);
    }

    /// <summary>Cancellation and unload prevent late translation from repopulating cleared caches.</summary>
    [Fact]
    public async Task CancelledGeneration_DoesNotPublish_AndRestartCanTranslate()
    {
        await using var harness = await Harness.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = harness.Start(Harness.Payload(), async (fields, _, _, _, token) =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return Harness.Translate(fields);
        }, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.Writer.DisablePublication();
        cancellation.Cancel();
        harness.Cache.Clear();
        Assert.False(await operation.WaitAsync(TimeSpan.FromSeconds(5)));
        release.SetResult();
        Assert.Null(harness.Cache.TryFindCanonicalMatch(12, Harness.Scope, "7.3", harness.Probe.SourceContentHash!));
        await using var restarted = await Harness.CreateAsync();
        Assert.True(await restarted.Start(Harness.Payload()).WaitAsync(TimeSpan.FromSeconds(5)));
    }

    /// <summary>Disabling publication waits for an active publisher before returning to cache clear.</summary>
    [Fact]
    public async Task DisablePublication_SerializesWithActivePublisher()
    {
        await using var harness = await Harness.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var publication = Task.Run(() => harness.Writer.PublishRead(() =>
        {
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
        }));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var disablingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disabled = Task.Run(() => { disablingStarted.SetResult(); harness.Writer.DisablePublication(); });
        await disablingStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(disabled.IsCompleted);
        release.Set();
        await Task.WhenAll(publication, disabled).WaitAsync(TimeSpan.FromSeconds(5));
        harness.Writer.PublishRead(() => throw new InvalidOperationException("Publication occurred after disable."));
    }

    /// <summary>A replacement generation joining an old read can still publish the persisted row.</summary>
    [Fact]
    public async Task JoinedRead_CancelledFirstSubscriber_DoesNotSuppressReplacementCache()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SeedAsync("Acoes", "Abre acoes.");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Coordinator.TryScheduleRead(new PersistenceWorkKey("test", "active"), PersistencePriority.Background,
            async (_, token) => { entered.SetResult(); await release.Task.WaitAsync(token); return 1; }, null, out _);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        var first = harness.Start(Harness.Payload(), cancellationToken: cancellation.Token);
        cancellation.Cancel();
        var replacement = harness.Start(Harness.Payload());
        release.SetResult();
        Assert.False(await first.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(await replacement.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("Acoes", harness.Cache.TryFindCanonicalMatch(12, Harness.Scope, "7.3", harness.Probe.SourceContentHash!)!.TranslatedName);
    }

    /// <summary>Concurrent snapshots remain safe while workers replace canonical rows.</summary>
    [Fact]
    public async Task Cache_ConcurrentReadPublishAndClear_RemainsConsistent()
    {
        var cache = new ReferenceTextCacheStore<MainCommandText>("ConcurrentReferenceTextTest");
        var row = Harness.Row("en", "pt", 0, "7.3", Harness.Payload(), Harness.Payload());
        row.TranslatedName = "Acoes";
        row.TranslatedDescription = "Abre acoes.";
        await Task.WhenAll(Task.Run(() =>
        {
            for (var index = 0; index < 1500; index++)
            {
                cache.Update(row);
                if (index % 10 == 0) { cache.Clear(); }
            }
        }), Task.Run(() =>
        {
            for (var index = 0; index < 1500; index++)
            {
                var found = cache.TryFindCanonicalMatch(12, Harness.Scope, "7.3", row.SourceContentHash!);
                if (found is not null) { Assert.Equal("Acoes", found.TranslatedName); }
                cache.GetTextLookupSnapshot(Harness.Scope, "7.3");
                cache.TryFindTranslatedText(Harness.Scope, "7.3", "Actions", out _);
                cache.TryFindOriginalText(Harness.Scope, "7.3", "Acoes", out _);
                cache.ContainsOriginalText(Harness.Scope, "7.3", "Actions");
            }
        }));
    }

    /// <summary>A terminal provider failure finishes the attempt without writing an incomplete row.</summary>
    [Fact]
    public async Task TerminalTranslationFailure_CompletesAttemptWithoutWrite()
    {
        await using var harness = await Harness.CreateAsync();
        var calls = 0;
        Assert.True(await harness.Start(Harness.Payload(), (_, _, _, _, _) =>
        {
            calls++;
            throw new InvalidOperationException("Provider failed.");
        }).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, calls);
        Assert.Equal(0, harness.Coordinator.GetMetrics().CommittedWrites);
        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.Empty(await context.MainCommandTexts.ToListAsync());
    }

    /// <summary>Owns a real SQLite/coordinator/broker fixture for captured prefetch operations.</summary>
    private sealed class Harness : IAsyncDisposable
    {
        private readonly string directory;

        /// <summary>Initializes one temporary runtime fixture.</summary>
        private Harness(string directory, IDbContextFactory<EchoglossianDbContext> factory, PersistenceCoordinator coordinator)
        {
            this.directory = directory;
            this.Factory = factory;
            this.Coordinator = coordinator;
            this.Writer = new ReferenceTextPersistenceWriter(coordinator);
        }

        /// <summary>Gets the fixed source/target/engine test scope.</summary>
        internal static TranslationReuseScope Scope => new("en", "pt", 0, true);
        /// <summary>Gets the real short-lived context factory.</summary>
        internal IDbContextFactory<EchoglossianDbContext> Factory { get; }
        /// <summary>Gets the bounded shared coordinator.</summary>
        internal PersistenceCoordinator Coordinator { get; }
        /// <summary>Gets the production writer adapter.</summary>
        internal ReferenceTextPersistenceWriter Writer { get; }
        /// <summary>Gets the concurrent canonical cache.</summary>
        internal ReferenceTextCacheStore<MainCommandText> Cache { get; } = new("ReferenceTextOperationTest");
        /// <summary>Gets the shared broker with test pacing.</summary>
        internal QueuedTranslationBroker Broker { get; } = new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromSeconds(5), TimeSpan.Zero, 0);
        /// <summary>Gets a probe built by the production MainCommand row creator.</summary>
        internal MainCommandText Probe => Row("en", "pt", 0, "7.3", Payload(), null);

        /// <summary>Creates and migrates a temporary SQLite fixture.</summary>
        internal static async Task<Harness> CreateAsync(int backgroundCapacity = 8)
        {
            var directory = Path.Combine(Path.GetTempPath(), "EchoglossianTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var factory = new EchoglossianDbContextRuntimeFactory(directory);
            await using var context = await factory.CreateDbContextAsync();
            await context.Database.MigrateAsync();
            return new Harness(directory, factory, new PersistenceCoordinator(factory,
                new PersistenceCoordinatorOptions(8, backgroundCapacity, 1, 8, TimeSpan.FromMilliseconds(1), 1, [], 4, 1, TimeSpan.FromSeconds(5))));
        }

        /// <summary>Admits one captured request through the production operation.</summary>
        internal Task<bool> Start(ReferenceTextCanonicalPayload payload,
            Func<IReadOnlyList<TranslationField>, SourceClientLanguage, string, string?, CancellationToken, Task<TranslationFieldBatchResult>>? translate = null,
            CancellationToken cancellationToken = default, PersistencePriority priority = PersistencePriority.Background)
        {
            return ReferenceTextPrefetchOperation.Start(this.Writer, this.Broker, this.Cache,
                static context => context.MainCommandTexts, Row, payload, "7.3", new SourceClientLanguage("en", "en"), Scope,
                "ReferenceText/MainCommand/" + payload.ReferenceId, translate ?? ((fields, _, _, _, _) => Task.FromResult(Translate(fields))), cancellationToken, priority);
        }

        /// <summary>Seeds canonical data through the production coordinator writer.</summary>
        internal async Task<MainCommandText> SeedAsync(string? name, string? description)
        {
            var translated = Payload();
            translated.TranslatedName = name;
            translated.TranslatedDescription = description;
            var row = Row("en", "pt", 0, "7.3", Payload(), translated);
            this.Writer.TryPersist(row, static context => context.MainCommandTexts, null, out var completion);
            Assert.Equal(PersistenceCompletionStatus.Succeeded, (await completion.WaitAsync(TimeSpan.FromSeconds(5))).Status);
            return row;
        }

        /// <summary>Creates an independent source payload with MainCommand metadata.</summary>
        internal static ReferenceTextCanonicalPayload Payload() => new()
        {
            ReferenceId = 12, Name = "Actions", Description = "Open actions.", IconId = 42, CategoryId = 7,
        };

        /// <summary>Provides deterministic engine output at the external-provider boundary.</summary>
        internal static TranslationFieldBatchResult Translate(IReadOnlyList<TranslationField> fields) => new(
            fields.Select(field => new TranslationField(field.Name, field.Name == "Name" ? "Acoes" : "Abre acoes.")), false);

        /// <summary>Invokes the production metadata-sensitive MainCommand row creator.</summary>
        internal static MainCommandText Row(string source, string target, int? engine, string? version,
            ReferenceTextCanonicalPayload original, ReferenceTextCanonicalPayload? translated)
        {
            return (MainCommandText)typeof(PluginEntry).GetMethod("CreateMainCommandTextRow",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(null, [source, target, engine, version, original, translated])!;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            this.Writer.DisablePublication();
            this.Broker.Dispose();
            await this.Coordinator.DisposeAsync();
            SqliteConnection.ClearAllPools();
            Directory.Delete(this.directory, true);
        }
    }
}
