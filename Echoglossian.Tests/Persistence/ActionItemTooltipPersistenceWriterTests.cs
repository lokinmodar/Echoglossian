// <copyright file="ActionItemTooltipPersistenceWriterTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Data.Common;
using Echoglossian.Cache;
using Echoglossian.DBHelpers;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.Helpers;
using Echoglossian.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Echoglossian.Tests.Persistence;

/// <summary>Exercises all three tooltip writer families against real SQLite and coordinator workers.</summary>
public sealed class ActionItemTooltipPersistenceWriterTests
{
    /// <summary>A current canonical cache hit returns an isolated copy without opening SQLite.</summary>
    /// <param name="kind">The canonical family.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    [InlineData("Item")]
    public async Task CacheHitDoesNotAdmit(string kind)
    {
        await using var harness = await Harness.CreateAsync();
        var row = CreateRow(kind);
        UpdateCache(row);
        var before = harness.Factory.Opens;
        Assert.Equal(PersistenceAdmissionStatus.Accepted, Find(harness.Writer, row, Scope, out var completion));
        Assert.True(completion.IsCompleted);
        var result = await ReadResultAsync(completion);
        Assert.Equal(PersistenceCompletionStatus.Succeeded, result.Status);
        Assert.NotSame(row, result.Row);
        SetTranslatedName(result.Row!, "mutated consumer");
        Assert.Equal("Traduzido", Describe(FindCached(row)!).Name);
        Assert.Equal(before, harness.Factory.Opens);
        Assert.Equal(0, harness.Coordinator.GetMetrics().AcceptedOperations);
    }

    /// <summary>Missing rows produce no synthetic cache projection.</summary>
    /// <param name="kind">The canonical family.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    [InlineData("Item")]
    public async Task ReadMissDoesNotPublish(string kind)
    {
        await using var harness = await Harness.CreateAsync();
        var probe = CreateRow(kind, translated: false);
        Assert.Equal(PersistenceAdmissionStatus.Accepted, Find(harness.Writer, probe, Scope, out var completion));
        var result = await ReadResultAsync(completion);
        Assert.Equal(PersistenceCompletionStatus.Succeeded, result.Status);
        Assert.Null(result.Row);
        Assert.Null(FindCached(probe));
        Assert.Empty(await harness.StoredRowsAsync(kind));
    }

    /// <summary>SQLite commit must complete before the inserted row becomes visible in cache.</summary>
    /// <param name="kind">The canonical family.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    [InlineData("Item")]
    public async Task PublishesOnlyAfterCommit(string kind)
    {
        var gate = new CommitGate();
        await using var harness = await Harness.CreateAsync(gate);
        gate.Enabled = true;
        var row = CreateRow(kind);
        Assert.Equal(PersistenceAdmissionStatus.Accepted, Persist(harness.Writer, row, out var completion));
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(completion.IsCompleted);
            Assert.Null(FindCached(row));
            Assert.Equal(PersistenceAdmissionStatus.Joined, Persist(harness.Writer, CreateRow(kind), out var joined));
            Assert.Same(completion, joined);
            gate.Release.TrySetResult(true);
            Assert.Equal(PersistenceCompletionStatus.Succeeded, (await completion.WaitAsync(TimeSpan.FromSeconds(5))).Status);
            var stored = Assert.Single(await harness.StoredRowsAsync(kind));
            Assert.Equal("Traduzido", Describe(stored).Name);
            Assert.Equal(Describe(stored).Id, Describe(FindCached(row)!).Id);
            Assert.True(Describe(stored).Id > 0);
        }
        finally
        {
            gate.Release.TrySetResult(true);
        }
    }

    /// <summary>Equivalent rows publish the stored projection without an UPDATE or timestamp change.</summary>
    /// <param name="kind">The canonical family.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    [InlineData("Item")]
    public async Task UnchangedWritePreservesTimestamp(string kind)
    {
        var counter = new UpdateCounter();
        await using var harness = await Harness.CreateAsync(counter);
        Persist(harness.Writer, CreateRow(kind), out var initial);
        Assert.Equal(PersistenceCompletionStatus.Succeeded, (await initial.WaitAsync(TimeSpan.FromSeconds(5))).Status);
        var before = Describe(Assert.Single(await harness.StoredRowsAsync(kind)));
        ClearCaches();
        counter.Updates = 0;
        Persist(harness.Writer, CreateRow(kind), out var unchanged);
        Assert.Equal(PersistenceCompletionStatus.Unchanged, (await unchanged.WaitAsync(TimeSpan.FromSeconds(5))).Status);
        Assert.Equal(0, counter.Updates);
        var after = Describe(Assert.Single(await harness.StoredRowsAsync(kind)));
        Assert.Equal(before.Updated, after.Updated);
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.Id, Describe(FindCached(CreateRow(kind))!).Id);
    }

    /// <summary>Reads coalesce while SQLite is blocked and publish only after successful completion.</summary>
    /// <param name="kind">The canonical family.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    [InlineData("Item")]
    public async Task ReadCoalescesAndDoesNotPublishBeforeCompletion(string kind)
    {
        var gate = new ReaderGate();
        await using var harness = await Harness.CreateAsync(gate);
        var row = CreateRow(kind);
        await harness.SeedAsync(row);
        gate.Enabled = true;
        Assert.Equal(PersistenceAdmissionStatus.Accepted, Find(harness.Writer, CreateRow(kind), Scope, out var first));
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(first.IsCompleted);
            Assert.Null(FindCached(row));
            Assert.Equal(PersistenceAdmissionStatus.Joined, Find(harness.Writer, CreateRow(kind), Scope, out var second));
            Assert.Same(first, second);
            Assert.Equal(1, harness.Coordinator.GetMetrics().AcceptedOperations);
            gate.Release.TrySetResult(true);
            var result = await ReadResultAsync(first);
            Assert.Equal(PersistenceCompletionStatus.Succeeded, result.Status);
            Assert.Equal("Traduzido", Describe(result.Row!).Name);
            Assert.Equal("Traduzido", Describe(FindCached(row)!).Name);
            Assert.Equal(1, gate.Selects);
        }
        finally
        {
            gate.Release.TrySetResult(true);
        }
    }

    /// <summary>Caller mutation after admission cannot change worker input or the committed projection.</summary>
    /// <param name="kind">The canonical family.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    [InlineData("Item")]
    public async Task AdmittedWriteOwnsSnapshot(string kind)
    {
        var gate = new ReaderGate();
        await using var harness = await Harness.CreateAsync(gate);
        gate.Enabled = true;
        var row = CreateRow(kind);
        Persist(harness.Writer, row, out var completion);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            SetTranslatedName(row, "mutated caller");
            gate.Release.TrySetResult(true);
            Assert.Equal(PersistenceCompletionStatus.Succeeded, (await completion.WaitAsync(TimeSpan.FromSeconds(5))).Status);
            Assert.Equal("Traduzido", Describe(Assert.Single(await harness.StoredRowsAsync(kind))).Name);
            Assert.Equal("Traduzido", Describe(FindCached(CreateRow(kind))!).Name);
        }
        finally
        {
            gate.Release.TrySetResult(true);
        }
    }

    /// <summary>Mutable probe changes cannot redirect an already admitted canonical lookup.</summary>
    /// <param name="kind">The canonical family.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    [InlineData("Item")]
    public async Task AdmittedReadOwnsSnapshot(string kind)
    {
        var gate = new ReaderGate();
        await using var harness = await Harness.CreateAsync(gate);
        await harness.SeedAsync(CreateRow(kind));
        gate.Enabled = true;
        var probe = CreateRow(kind, translated: false);
        Find(harness.Writer, probe, Scope, out var completion);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            SetHash(probe, "caller-changed-hash");
            gate.Release.TrySetResult(true);
            var result = await ReadResultAsync(completion);
            Assert.Equal("Traduzido", Describe(result.Row!).Name);
            Assert.Equal(Describe(CreateRow(kind)).Hash, Describe(result.Row!).Hash);
            Assert.NotNull(FindCached(CreateRow(kind)));
        }
        finally
        {
            gate.Release.TrySetResult(true);
        }
    }

    /// <summary>A failed real transaction commit neither persists nor publishes translated content.</summary>
    /// <param name="kind">The canonical family.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    [InlineData("Item")]
    public async Task FailedCommitDoesNotPublish(string kind)
    {
        var gate = new CommitGate { ThrowOnCommit = true };
        await using var harness = await Harness.CreateAsync(gate);
        gate.Enabled = true;
        var row = CreateRow(kind);
        Persist(harness.Writer, row, out var completion);
        Assert.Equal(PersistenceCompletionStatus.Failed, (await completion.WaitAsync(TimeSpan.FromSeconds(5))).Status);
        Assert.Null(FindCached(row));
        Assert.Empty(await harness.StoredRowsAsync(kind));
    }

    /// <summary>Historical cache translations promote to the requested version only after commit.</summary>
    /// <param name="kind">The canonical family.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    [InlineData("Item")]
    public async Task HistoricalPromotionPreservesIdentity(string kind)
    {
        var gate = new CommitGate();
        await using var harness = await Harness.CreateAsync(gate);
        var historical = CreateRow(kind, version: "7.2");
        await harness.SeedAsync(historical);
        UpdateCache(historical);
        var probe = CreateRow(kind, translated: false);
        gate.Enabled = true;
        Assert.Equal(PersistenceAdmissionStatus.Accepted, Find(harness.Writer, probe, Scope, out var completion));
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Null(FindCached(probe));
            Assert.False(completion.IsCompleted);
            gate.Release.TrySetResult(true);
            var result = await ReadResultAsync(completion);
            Assert.Equal(PersistenceCompletionStatus.Succeeded, result.Status);
            var promoted = Describe(result.Row!);
            var original = Describe(historical);
            Assert.Equal("7.3", promoted.Version);
            Assert.Equal(original.Hash, promoted.Hash);
            Assert.Equal(original.ContentId, promoted.ContentId);
            Assert.Equal(original.Source, promoted.Source);
            Assert.Equal(original.Target, promoted.Target);
            Assert.Equal(original.Engine, promoted.Engine);
            Assert.Equal("Traduzido", promoted.Name);
            var rows = await harness.StoredRowsAsync(kind);
            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, value => Describe(value).Version == "7.2");
            Assert.Contains(rows, value => Describe(value).Version == "7.3");
        }
        finally
        {
            gate.Release.TrySetResult(true);
        }
    }

    /// <summary>Canonical reads preserve regional aliases, source separation and engine reuse policy.</summary>
    /// <param name="kind">The canonical family.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    [InlineData("Item")]
    public async Task ScopeAndLanguageAliasesPreserveSelection(string kind)
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SeedAsync(CreateRow(kind, target: "pt-BR"));
        var probe = CreateRow(kind);
        Find(harness.Writer, probe, Scope, out var alias);
        Assert.Equal("Traduzido", Describe((await ReadResultAsync(alias)).Row!).Name);
        ClearCaches();
        Find(harness.Writer, CreateRow(kind, source: "de"), new TranslationReuseScope("de", "pt", 0, true), out var otherSource);
        Assert.Null((await ReadResultAsync(otherSource)).Row);
        Find(harness.Writer, probe, new TranslationReuseScope("en", "pt", 99, true), out var matchingEngine);
        Assert.Null((await ReadResultAsync(matchingEngine)).Row);
        Find(harness.Writer, probe, new TranslationReuseScope("en", "pt", 99, false), out var sharedEngine);
        Assert.Equal("Traduzido", Describe((await ReadResultAsync(sharedEngine)).Row!).Name);
        var wrongHash = CreateRow(kind);
        SetHash(wrongHash, "changed-source");
        Find(harness.Writer, wrongHash, Scope, out var changedSource);
        Assert.Null((await ReadResultAsync(changedSource)).Row);
    }

    /// <summary>Equivalent aliases share identities while every canonical scope component remains distinct.</summary>
    [Fact]
    public void IdentitySeparatesCanonicalScope()
    {
        var baseline = ActionItemTooltipPersistenceWriter.BuildIdentity("Action", 1, Scope, "7.3", "hash");
        Assert.Equal(baseline, ActionItemTooltipPersistenceWriter.BuildIdentity(
            "Action", 1, new TranslationReuseScope("EN", "PT-br", 0, true), "7.3", "hash"));
        Assert.NotEqual(baseline, ActionItemTooltipPersistenceWriter.BuildIdentity("Trait", 1, Scope, "7.3", "hash"));
        Assert.NotEqual(baseline, ActionItemTooltipPersistenceWriter.BuildIdentity("Action", 2, Scope, "7.3", "hash"));
        Assert.NotEqual(baseline, ActionItemTooltipPersistenceWriter.BuildIdentity("Action", 1, Scope, "7.4", "hash"));
        Assert.NotEqual(baseline, ActionItemTooltipPersistenceWriter.BuildIdentity("Action", 1, Scope, "7.3", "other"));
        Assert.NotEqual(baseline, ActionItemTooltipPersistenceWriter.BuildIdentity(
            "Action", 1, new TranslationReuseScope("de", "pt", 0, true), "7.3", "hash"));
        Assert.NotEqual(baseline, ActionItemTooltipPersistenceWriter.BuildIdentity(
            "Action", 1, new TranslationReuseScope("en", "fr", 0, true), "7.3", "hash"));
        Assert.NotEqual(baseline, ActionItemTooltipPersistenceWriter.BuildIdentity(
            "Action", 1, new TranslationReuseScope("en", "pt", 1, true), "7.3", "hash"));
        Assert.NotEqual(baseline, ActionItemTooltipPersistenceWriter.BuildIdentity(
            "Action", 1, new TranslationReuseScope("en", "pt", 0, false), "7.3", "hash"));
    }

    /// <summary>Unload cancels active reads and writes and prevents stale cache publication.</summary>
    /// <param name="kind">The canonical family.</param>
    /// <param name="read">Whether to exercise the reader rather than the writer.</param>
    [Theory]
    [InlineData("Action", true)]
    [InlineData("Trait", true)]
    [InlineData("Item", true)]
    [InlineData("Action", false)]
    [InlineData("Trait", false)]
    [InlineData("Item", false)]
    public async Task UnloadSuppressesPublication(string kind, bool read)
    {
        var gate = new ReaderGate();
        await using var harness = await Harness.CreateAsync(gate);
        var row = CreateRow(kind);
        if (read)
        {
            await harness.SeedAsync(row);
        }

        gate.Enabled = true;
        Task readCompletion = Task.CompletedTask;
        Task<PersistenceWriteResult>? writeCompletion = null;
        if (read)
        {
            Find(harness.Writer, row, Scope, out readCompletion);
        }
        else
        {
            Persist(harness.Writer, row, out writeCompletion);
        }

        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            harness.Writer.DisablePublication();
            var status = read
                ? (await ReadResultAsync(readCompletion)).Status
                : (await writeCompletion!.WaitAsync(TimeSpan.FromSeconds(5))).Status;
            Assert.Equal(PersistenceCompletionStatus.Cancelled, status);
            Assert.Null(FindCached(row));
            gate.Release.TrySetResult(true);
            if (!read)
            {
                Assert.Empty(await harness.StoredRowsAsync(kind));
            }
        }
        finally
        {
            gate.Release.TrySetResult(true);
        }
    }

    /// <summary>Coordinator shutdown rejects both overload paths without cache or disk fallback.</summary>
    /// <param name="kind">The canonical family.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    [InlineData("Item")]
    public async Task RejectsAfterShutdown(string kind)
    {
        await using var harness = await Harness.CreateAsync();
        await harness.Coordinator.CompleteAsync();
        var opens = harness.Factory.Opens;
        var row = CreateRow(kind);
        Assert.Equal(PersistenceAdmissionStatus.RejectedShutdown, Find(harness.Writer, row, Scope, out var read));
        Assert.Equal(PersistenceCompletionStatus.Rejected, (await ReadResultAsync(read)).Status);
        Assert.Equal(PersistenceAdmissionStatus.RejectedShutdown, Persist(harness.Writer, row, out var write));
        Assert.Equal(PersistenceCompletionStatus.Rejected, (await write).Status);
        Assert.Null(FindCached(row));
        Assert.Equal(opens, harness.Factory.Opens);
    }

    /// <summary>A full bounded reader lane rejects new identities without opening another context.</summary>
    /// <param name="kind">The canonical family.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    [InlineData("Item")]
    public async Task CapacityRejectionDoesNotPublish(string kind)
    {
        var gate = new ReaderGate();
        await using var harness = await Harness.CreateAsync(gate, capacity: 1);
        gate.Enabled = true;
        Find(harness.Writer, CreateRow(kind), Scope, out var first);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(PersistenceAdmissionStatus.Accepted, Find(harness.Writer, CreateRow(kind, id: 2), Scope, out var queued));
            var rejected = CreateRow(kind, id: 3);
            var opens = harness.Factory.Opens;
            Assert.Equal(PersistenceAdmissionStatus.RejectedCapacity, Find(harness.Writer, rejected, Scope, out var completion));
            Assert.Equal(PersistenceCompletionStatus.Rejected, (await ReadResultAsync(completion)).Status);
            Assert.Null(FindCached(rejected));
            Assert.Equal(opens, harness.Factory.Opens);
            gate.Release.TrySetResult(true);
            await first.WaitAsync(TimeSpan.FromSeconds(5));
            await queued.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            gate.Release.TrySetResult(true);
        }
    }

    /// <summary>Workers translate each missing field once while persisting the captured canonical identity.</summary>
    /// <param name="kind">The migrated family.</param>
    /// <param name="seedName">Whether a committed name already exists.</param>
    [Theory]
    [InlineData("Action", false)]
    [InlineData("Trait", false)]
    [InlineData("Action", true)]
    [InlineData("Trait", true)]
    public async Task WorkerPersistsCapturedIdentityAndOnlyMissingFields(string kind, bool seedName)
    {
        await using var harness = await Harness.CreateAsync();
        using var broker = CreateBroker();
        var scope = new TranslationReuseScope("de", "fr", 0, true);
        var probe = CreateRow(kind, translated: false, source: "de", target: "fr", id: 77);
        if (seedName)
        {
            await harness.SeedAsync(CreateRow(kind, source: "de", target: "fr", id: 77, translateDescription: false));
        }

        var calls = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
        Task<string> Translate(string text, string origin, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Assert.Contains("/77/", origin, StringComparison.Ordinal);
            calls.AddOrUpdate(text, 1, (_, count) => count + 1);
            return Task.FromResult(text == "Original" ? "Traduzido" : "Descricao");
        }

        await RunWorker(kind, harness, broker, probe, scope, Translate).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(seedName ? 1 : 2, calls.Count);
        Assert.All(calls.Values, count => Assert.Equal(1, count));
        var stored = Describe(Assert.Single(await harness.StoredRowsAsync(kind)));
        var captured = Describe(probe);
        Assert.Equal(captured.ContentId, stored.ContentId);
        Assert.Equal(captured.Hash, stored.Hash);
        Assert.Equal(captured.Source, stored.Source);
        Assert.Equal(captured.Target, stored.Target);
        Assert.Equal(captured.Engine, stored.Engine);
        Assert.Equal(captured.Version, stored.Version);
        Assert.Equal("Traduzido", stored.Name);
    }

    /// <summary>A complete committed cache projection skips translator invocation entirely.</summary>
    /// <param name="kind">The migrated family.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    public async Task WorkerCompleteCacheHitSkipsBroker(string kind)
    {
        await using var harness = await Harness.CreateAsync();
        using var broker = CreateBroker();
        var complete = CreateRow(kind);
        await harness.SeedAsync(complete);
        UpdateCache(complete);
        await RunWorker(kind, harness, broker, CreateRow(kind, translated: false), Scope,
            (_, _, _) => throw new InvalidOperationException("A complete cache hit must not translate."));
        Assert.Equal(0, harness.Coordinator.GetMetrics().AcceptedOperations);
        Assert.Equal("Traduzido", Describe(Assert.Single(await harness.StoredRowsAsync(kind))).Name);
    }

    /// <summary>Provider failures retain only the registered original and suppress repeated owned work.</summary>
    /// <param name="kind">The migrated family.</param>
    /// <param name="rejected">The rejected external response.</param>
    [Theory]
    [InlineData("Action", "")]
    [InlineData("Trait", "")]
    [InlineData("Action", "[Translation Error: unavailable]")]
    [InlineData("Trait", "[Translation Error: unavailable]")]
    public async Task WorkerUnusableResponseKeepsOriginalAndTerminatesOwnership(string kind, string rejected)
    {
        await using var harness = await Harness.CreateAsync();
        using var broker = CreateBroker();
        var owner = new StructuredTooltipOperationOwner();
        var probe = CreateRow(kind, translated: false);
        var calls = 0;
        Task<string> Translate(string text, string origin, CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(rejected);
        }

        var completion = owner.TryStart("canonical", () => RunWorker(kind, harness, broker, probe, Scope, Translate), harness.Writer.ShutdownToken)!;
        await completion.WaitAsync(TimeSpan.FromSeconds(5));
        var after = calls;
        Assert.Equal(2, after);
        Assert.Null(Describe(Assert.Single(await harness.StoredRowsAsync(kind))).Name);
        Assert.Null(Describe(FindCached(probe)!).Name);
        Assert.Same(completion, owner.TryStart("canonical", () => throw new InvalidOperationException("Cooldown must suppress retry."), harness.Writer.ShutdownToken));
        Assert.Equal(after, calls);
    }

    /// <summary>Ownership remains active until the translated transaction commits and retains terminal cooldown.</summary>
    /// <param name="kind">The migrated family.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    public async Task WorkerOwnershipIncludesTranslationAndFinalCommit(string kind)
    {
        var gate = new CommitGate();
        await using var harness = await Harness.CreateAsync(gate);
        using var broker = CreateBroker();
        var owner = new StructuredTooltipOperationOwner();
        var probe = CreateRow(kind, translated: false);
        var calls = 0;
        Task<string> Translate(string text, string origin, CancellationToken token)
        {
            gate.Enabled = true;
            Interlocked.Increment(ref calls);
            return Task.FromResult(text == "Original" ? "Traduzido" : "Descricao");
        }

        var completion = owner.TryStart("canonical", () => RunWorker(kind, harness, broker, probe, Scope, Translate), harness.Writer.ShutdownToken)!;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(completion.IsCompleted);
            Assert.Equal(2, calls);
            Assert.Null(Describe(FindCached(probe)!).Name);
            Assert.Same(completion, owner.TryStart("canonical", () => throw new InvalidOperationException("Commit is still active."), harness.Writer.ShutdownToken));
            gate.Release.TrySetResult(true);
            await completion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("Traduzido", Describe(FindCached(probe)!).Name);
            Assert.Same(completion, owner.TryStart("canonical", () => throw new InvalidOperationException("Terminal cooldown must remain owned."), harness.Writer.ShutdownToken));
        }
        finally
        {
            gate.Release.TrySetResult(true);
        }
    }

    /// <summary>Lifetime cancellation ends broker orchestration without publishing translated content.</summary>
    /// <param name="kind">The migrated family.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    public async Task WorkerCancellationReachesTerminalOwnership(string kind)
    {
        await using var harness = await Harness.CreateAsync();
        using var broker = CreateBroker();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new StructuredTooltipOperationOwner();
        var probe = CreateRow(kind, translated: false);
        async Task<string> Translate(string text, string origin, CancellationToken token)
        {
            entered.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return "unreachable";
        }

        var completion = owner.TryStart("canonical", () => RunWorker(kind, harness, broker, probe, Scope, Translate), harness.Writer.ShutdownToken)!;
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.Writer.DisablePublication();
        await completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(completion.IsCompletedSuccessfully);
        Assert.Null(Describe(Assert.Single(await harness.StoredRowsAsync(kind))).Name);
        Assert.Null(owner.TryStart("canonical", () => Task.CompletedTask, harness.Writer.ShutdownToken));
    }

    /// <summary>Gets the fixed canonical policy used by all overload families.</summary>
    private static TranslationReuseScope Scope => new("en", "pt", 0, true);

    /// <summary>Creates a domain row through its production canonical payload builder.</summary>
    private static object CreateRow(string kind, bool translated = true, string version = "7.3",
        string source = "en", string target = "pt", uint id = 1, bool translateDescription = true)
    {
        switch (kind)
        {
            case "Action":
                var action = new ActionTooltipCanonicalPayload
                {
                    ActionId = id, IconId = 42, ClassJobId = 7, Name = "Original", Description = "Original description",
                    TranslatedName = translated ? "Traduzido" : null, TranslatedDescription = translated && translateDescription ? "Descricao" : null,
                };
                return ActionTooltipPersistenceHelper.CreateCanonicalRow(source, target, 0, version, action, translated ? action : null);
            case "Trait":
                var trait = new TraitCanonicalPayload
                {
                    TraitId = id, IconId = 42, ClassJobId = 7, Name = "Original", Description = "Original description",
                    TranslatedName = translated ? "Traduzido" : null, TranslatedDescription = translated && translateDescription ? "Descricao" : null,
                };
                return TraitPersistenceHelper.CreateCanonicalRow(source, target, 0, version, trait, translated ? trait : null);
            case "Item":
                var item = new ItemTooltipCanonicalPayload
                {
                    ItemId = id, IconId = 42, ItemUiCategoryId = 7, Name = "Original", Description = "Original description",
                    TranslatedName = translated ? "Traduzido" : null, TranslatedDescription = translated && translateDescription ? "Descricao" : null,
                };
                return ItemTooltipPersistenceHelper.CreateCanonicalRow(source, target, 0, version, item, translated ? item : null);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    /// <summary>Creates the production broker with deterministic test pacing and no provider retries.</summary>
    private static QueuedTranslationBroker CreateBroker() => new(
        TimeSpan.Zero, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), TimeSpan.Zero, 0);

    /// <summary>Runs the production managed worker from a canonical serialized capture.</summary>
    private static Task RunWorker(string kind, Harness harness, QueuedTranslationBroker broker, object probe,
        TranslationReuseScope scope, Func<string, string, CancellationToken, Task<string>> translate) => probe switch
    {
        ActionTooltip action when kind == "Action" => Echoglossian.CompleteActionDetailOperationAsync(
            harness.Writer, broker, action.CanonicalPayloadAsText!, action, scope, PersistencePriority.Background, translate),
        Trait trait when kind == "Trait" => Echoglossian.CompleteTraitDetailOperationAsync(
            harness.Writer, broker, trait.CanonicalPayloadAsText!, trait, scope, PersistencePriority.Background, translate),
        _ => throw new ArgumentException("Worker family has not migrated.", nameof(kind)),
    };

    /// <summary>Invokes the production typed read overload while retaining its actual shared completion.</summary>
    private static PersistenceAdmissionStatus Find(ActionItemTooltipPersistenceWriter writer, object row,
        TranslationReuseScope scope, out Task completion)
    {
        PersistenceAdmissionStatus admission;
        switch (row)
        {
            case ActionTooltip action:
                admission = writer.TryFind(action, scope, out var actionCompletion);
                completion = actionCompletion;
                break;
            case Trait trait:
                admission = writer.TryFind(trait, scope, out var traitCompletion);
                completion = traitCompletion;
                break;
            case ItemTooltip item:
                admission = writer.TryFind(item, scope, out var itemCompletion);
                completion = itemCompletion;
                break;
            default:
                throw new ArgumentException("Unexpected canonical family.", nameof(row));
        }

        return admission;
    }

    /// <summary>Invokes the production typed write overload.</summary>
    private static PersistenceAdmissionStatus Persist(ActionItemTooltipPersistenceWriter writer, object row,
        out Task<PersistenceWriteResult> completion) => row switch
    {
        ActionTooltip action => writer.TryPersist(action, out completion),
        Trait trait => writer.TryPersist(trait, out completion),
        ItemTooltip item => writer.TryPersist(item, out completion),
        _ => throw new ArgumentException("Unexpected canonical family.", nameof(row)),
    };

    /// <summary>Awaits a typed result without replacing its underlying coordinator completion.</summary>
    private static async Task<(PersistenceCompletionStatus Status, object? Row)> ReadResultAsync(Task completion)
    {
        await completion.WaitAsync(TimeSpan.FromSeconds(5));
        return completion switch
        {
            Task<PersistenceReadResult<ActionTooltip?>> action => (action.Result.Status, action.Result.Value),
            Task<PersistenceReadResult<Trait?>> trait => (trait.Result.Status, trait.Result.Value),
            Task<PersistenceReadResult<ItemTooltip?>> item => (item.Result.Status, item.Result.Value),
            _ => throw new ArgumentException("Unexpected canonical completion.", nameof(completion)),
        };
    }

    /// <summary>Reads a detached cache projection using the production overload.</summary>
    private static object? FindCached(object row) => row switch
    {
        ActionTooltip action => ActionItemTooltipPersistenceWriter.FindCached(action, Scope),
        Trait trait => ActionItemTooltipPersistenceWriter.FindCached(trait, Scope),
        ItemTooltip item => ActionItemTooltipPersistenceWriter.FindCached(item, Scope),
        _ => throw new ArgumentException("Unexpected canonical family.", nameof(row)),
    };

    /// <summary>Seeds the existing shared canonical projection for cache-path tests.</summary>
    private static void UpdateCache(object row)
    {
        switch (row)
        {
            case ActionTooltip action: ActionTooltipCacheManager.Update(action); break;
            case Trait trait: TraitCacheManager.Update(trait); break;
            case ItemTooltip item: ItemTooltipCacheManager.Update(item); break;
            default: throw new ArgumentException("Unexpected canonical family.", nameof(row));
        }
    }

    /// <summary>Mutates caller-owned state to prove snapshot isolation.</summary>
    private static void SetTranslatedName(object row, string value)
    {
        switch (row)
        {
            case ActionTooltip action: action.TranslatedActionName = value; break;
            case Trait trait: trait.TranslatedTraitName = value; break;
            case ItemTooltip item: item.TranslatedItemName = value; break;
            default: throw new ArgumentException("Unexpected canonical family.", nameof(row));
        }
    }

    /// <summary>Changes a canonical source identity for source-content rejection tests.</summary>
    private static void SetHash(object row, string value)
    {
        switch (row)
        {
            case ActionTooltip action: action.SourceContentHash = value; break;
            case Trait trait: trait.SourceContentHash = value; break;
            case ItemTooltip item: item.SourceContentHash = value; break;
            default: throw new ArgumentException("Unexpected canonical family.", nameof(row));
        }
    }

    /// <summary>Projects only the observable persisted fields needed by domain-independent assertions.</summary>
    private static (int Id, uint ContentId, string? Version, string? Hash, string? Source,
        string? Target, int? Engine, string? Name, DateTime Updated) Describe(object row) => row switch
    {
        ActionTooltip action => (action.Id, action.ActionId, action.GameVersion, action.SourceContentHash,
            action.OriginalLang, action.TranslationLang, action.TranslationEngine, action.TranslatedActionName, action.UpdatedDate),
        Trait trait => (trait.Id, trait.TraitId, trait.GameVersion, trait.SourceContentHash,
            trait.OriginalLang, trait.TranslationLang, trait.TranslationEngine, trait.TranslatedTraitName, trait.UpdatedDate),
        ItemTooltip item => (item.Id, item.ItemId, item.GameVersion, item.SourceContentHash,
            item.OriginalLang, item.TranslationLang, item.TranslationEngine, item.TranslatedItemName, item.UpdatedDate),
        _ => throw new ArgumentException("Unexpected canonical family.", nameof(row)),
    };

    /// <summary>Resets process-wide projections between serialized test cases.</summary>
    private static void ClearCaches()
    {
        ActionTooltipCacheManager.Clear();
        TraitCacheManager.Clear();
        ItemTooltipCacheManager.Clear();
    }

    /// <summary>Owns the real SQLite database, short-lived factory and bounded production coordinator.</summary>
    private sealed class Harness : IAsyncDisposable
    {
        private readonly string directory;

        /// <summary>Initializes a migrated isolated fixture.</summary>
        private Harness(string directory, TestFactory factory, PersistenceCoordinator coordinator)
        {
            this.directory = directory;
            this.Factory = factory;
            this.Coordinator = coordinator;
            this.Writer = new ActionItemTooltipPersistenceWriter(coordinator);
        }

        /// <summary>Gets the real SQLite context factory.</summary>
        internal TestFactory Factory { get; }
        /// <summary>Gets the production bounded coordinator.</summary>
        internal PersistenceCoordinator Coordinator { get; }
        /// <summary>Gets the production domain adapter.</summary>
        internal ActionItemTooltipPersistenceWriter Writer { get; }

        /// <summary>Creates and migrates a new SQLite fixture before enabling its gates.</summary>
        internal static async Task<Harness> CreateAsync(IInterceptor? interceptor = null, int capacity = 8)
        {
            ClearCaches();
            var directory = Path.Combine(Path.GetTempPath(), "EchoglossianTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var factory = new TestFactory(Path.Combine(directory, "test.db"), interceptor);
            await using var context = await factory.CreateDbContextAsync();
            await context.Database.MigrateAsync();
            var options = new PersistenceCoordinatorOptions(8, capacity, 1, 8,
                TimeSpan.FromMilliseconds(1), 1, [], 4, 1, TimeSpan.FromSeconds(5));
            return new Harness(directory, factory, new PersistenceCoordinator(factory, options));
        }

        /// <summary>Seeds SQLite directly so reads must use the database rather than cache.</summary>
        internal async Task SeedAsync(object row)
        {
            await using var context = await this.Factory.CreateDbContextAsync();
            context.Add(row);
            await context.SaveChangesAsync();
        }

        /// <summary>Reads the actual stored canonical rows for a family.</summary>
        internal async Task<IReadOnlyList<object>> StoredRowsAsync(string kind)
        {
            await using var context = await this.Factory.CreateDbContextAsync();
            return kind switch
            {
                "Action" => (await context.ActionTooltip.AsNoTracking().ToListAsync()).Cast<object>().ToArray(),
                "Trait" => (await context.Traits.AsNoTracking().ToListAsync()).Cast<object>().ToArray(),
                "Item" => (await context.ItemTooltip.AsNoTracking().ToListAsync()).Cast<object>().ToArray(),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            this.Writer.DisablePublication();
            await this.Coordinator.DisposeAsync();
            ClearCaches();
            SqliteConnection.ClearAllPools();
            Directory.Delete(this.directory, recursive: true);
        }
    }

    /// <summary>Creates real contexts asynchronously and rejects synchronous worker fallback.</summary>
    private sealed class TestFactory : IDbContextFactory<EchoglossianDbContext>
    {
        private readonly DbContextOptions<EchoglossianDbContext> options;
        private int opens;

        /// <summary>Initializes SQLite with the requested external-boundary interceptor.</summary>
        internal TestFactory(string databasePath, IInterceptor? interceptor)
        {
            var builder = new DbContextOptionsBuilder<EchoglossianDbContext>().UseSqlite("Data Source=" + databasePath);
            if (interceptor != null)
            {
                builder.AddInterceptors(interceptor);
            }

            this.options = builder.Options;
        }

        /// <summary>Gets the observed context admissions.</summary>
        internal int Opens => Volatile.Read(ref this.opens);

        /// <inheritdoc />
        public EchoglossianDbContext CreateDbContext() => throw new InvalidOperationException("Synchronous context fallback is forbidden.");

        /// <inheritdoc />
        public Task<EchoglossianDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref this.opens);
            return Task.FromResult(new EchoglossianDbContext(this.options));
        }
    }

    /// <summary>Blocks SELECT execution at the real SQLite command boundary.</summary>
    private sealed class ReaderGate : DbCommandInterceptor
    {
        /// <summary>Gets or sets whether test-operation gating is enabled.</summary>
        internal bool Enabled { get; set; }
        /// <summary>Gets the observed SELECT count.</summary>
        internal int Selects { get; private set; }
        /// <summary>Gets notification that a worker reached SQLite.</summary>
        internal TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>Gets the test-controlled reader release.</summary>
        internal TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (this.Enabled && command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                this.Selects++;
                this.Entered.TrySetResult(true);
                await this.Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
    }

    /// <summary>Blocks the actual transaction commit so cache visibility can be observed independently.</summary>
    private sealed class CommitGate : DbTransactionInterceptor
    {
        /// <summary>Gets or sets whether test-operation gating is enabled.</summary>
        internal bool Enabled { get; set; }
        /// <summary>Gets whether the transaction should fail at the commit boundary.</summary>
        internal bool ThrowOnCommit { get; init; }
        /// <summary>Gets notification that SQLite reached commit.</summary>
        internal TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>Gets the test-controlled transaction release.</summary>
        internal TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (this.Enabled)
            {
                this.Entered.TrySetResult(true);
                if (this.ThrowOnCommit)
                {
                    throw new InvalidOperationException("Injected commit failure.");
                }

                await this.Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
    }

    /// <summary>Counts emitted UPDATE statements rather than relying on in-memory mutation reporting.</summary>
    private sealed class UpdateCounter : DbCommandInterceptor
    {
        /// <summary>Gets or sets the observed update count.</summary>
        internal int Updates { get; set; }

        /// <inheritdoc />
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            this.Count(command);
            return ValueTask.FromResult(result);
        }

        /// <inheritdoc />
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            this.Count(command);
            return ValueTask.FromResult(result);
        }

        /// <summary>Counts updates through either SQLite command execution shape.</summary>
        private void Count(DbCommand command)
        {
            if (command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                this.Updates++;
            }
        }
    }
}

