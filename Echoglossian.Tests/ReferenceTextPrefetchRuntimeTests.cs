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

    /// <summary>Legitimate error-like words inside translated fields must survive broker transport.</summary>
    [Fact]
    public async Task BatchWithUnavailableVocabulary_PersistsThroughRealBroker()
    {
        await using var harness = await Harness.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var operation = harness.Start(Harness.Payload(), (fields, _, _, _, _) => Task.FromResult(
            new TranslationFieldBatchResult(fields.Select(field => new TranslationField(field.Name,
                field.Name == "Name" ? "Action unavailable" : "Displays why this action is unavailable.")), false)), cancellation.Token);
        try
        {
            Assert.True(await operation.WaitAsync(TimeSpan.FromSeconds(2)));
            await using var context = await harness.Factory.CreateDbContextAsync();
            var row = await context.MainCommandTexts.SingleAsync();
            Assert.Equal("Action unavailable", row.TranslatedName);
            Assert.Equal("Displays why this action is unavailable.", row.TranslatedDescription);
        }
        finally
        {
            cancellation.Cancel();
        }
    }

    /// <summary>A rate-limited attempt stays pending until the broker retry resolves successfully.</summary>
    [Fact]
    public async Task RateLimitRetry_RemainsPendingAndPersistsCompleteSuccess()
    {
        await using var harness = await Harness.CreateAsync(maxRateLimitRetries: 1);
        using var cancellation = new CancellationTokenSource();
        var retryEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRetry = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var operation = harness.Start(Harness.Payload(), async (fields, _, _, _, token) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new HttpRequestException("HTTP 429 Too Many Requests");
            }

            retryEntered.SetResult();
            await releaseRetry.Task.WaitAsync(token);
            return Harness.Translate(fields);
        }, cancellation.Token);
        try
        {
            await retryEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(operation.IsCompleted);
            releaseRetry.SetResult();
            Assert.True(await operation.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Equal(2, calls);
            await using var context = await harness.Factory.CreateDbContextAsync();
            var row = await context.MainCommandTexts.SingleAsync();
            Assert.Equal("Acoes", row.TranslatedName);
            Assert.Equal("Abre acoes.", row.TranslatedDescription);
            Assert.Equal(1, harness.Coordinator.GetMetrics().CommittedWrites);
        }
        finally
        {
            cancellation.Cancel();
            releaseRetry.TrySetResult();
        }
    }

    /// <summary>The broker timeout, rather than a separate operation timer, ends a hung provider attempt.</summary>
    [Fact]
    public async Task BrokerTimeout_CompletesAttemptWithoutLocalDeadline()
    {
        await using var harness = await Harness.CreateAsync(requestTimeout: TimeSpan.FromMilliseconds(50));
        using var cancellation = new CancellationTokenSource();
        var resolver = new TaskCompletionSource<TranslationFieldBatchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = harness.Start(Harness.Payload(), (_, _, _, _, _) => resolver.Task, cancellation.Token);
        try
        {
            Assert.True(await operation.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Equal(0, harness.Coordinator.GetMetrics().CommittedWrites);
        }
        finally
        {
            cancellation.Cancel();
            resolver.TrySetResult(Harness.Translate([new TranslationField("Name", "Actions")]));
        }
    }

    /// <summary>Exhausted coordinator failures terminate one row and let later rows proceed without repeated admission.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RuntimeCursor_ExhaustedReadOrWriteFailure_AdvancesWithoutRetry(int failingContext)
    {
        await using var harness = await Harness.CreateAsync(failingContext: failingContext);
        var state = new PluginEntry.ReferenceTextPrefetchState();
        state.Queue.AddRange([12, 13]);
        var scheduled = new List<uint>();
        Task<bool> Schedule(uint id)
        {
            scheduled.Add(id);
            var payload = Harness.Payload();
            payload.ReferenceId = id;
            return harness.Start(payload);
        }

        Assert.Equal(1, PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule));
        Assert.True(await state.Pending!.Completion.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Null(harness.Cache.TryFindCanonicalMatch(12, Harness.Scope, "7.3", harness.Probe.SourceContentHash!));
        Assert.Equal(1, PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule));
        Assert.Equal(1, state.QueueIndex);
        Assert.True(await state.Pending!.Completion.WaitAsync(TimeSpan.FromSeconds(2)));
        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        Assert.Equal(2, state.QueueIndex);
        Assert.Equal(new uint[] { 12, 13 }, scheduled);
        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.Equal((uint)13, (await context.MainCommandTexts.SingleAsync()).ReferenceId);
        state.Cancellation.Dispose();
    }

    /// <summary>An unexpected worker-side row construction exception is terminal and cannot repeatedly admit the failed row.</summary>
    [Fact]
    public async Task RuntimeCursor_UnexpectedCompletionException_AdvancesToNextRow()
    {
        await using var harness = await Harness.CreateAsync();
        var state = new PluginEntry.ReferenceTextPrefetchState();
        state.Queue.AddRange([12, 13]);
        var creations = 0;
        MainCommandText Create(string source, string target, int? engine, string? version,
            ReferenceTextCanonicalPayload original, ReferenceTextCanonicalPayload? translated)
        {
            if (++creations == 2)
            {
                throw new InvalidOperationException("Unexpected complete-row construction failure.");
            }

            return Harness.Row(source, target, engine, version, original, translated);
        }

        var scheduled = new List<uint>();
        Task<bool> Schedule(uint id)
        {
            scheduled.Add(id);
            var payload = Harness.Payload();
            payload.ReferenceId = id;
            return id == 12 ? ReferenceTextPrefetchOperation.Start(harness.Writer, harness.Broker, harness.Cache,
                context => context.MainCommandTexts, Create, payload, "7.3", new SourceClientLanguage("en", "en"),
                Harness.Scope, "ReferenceText/MainCommand/12", (fields, _, _, _, _) => Task.FromResult(Harness.Translate(fields)),
                CancellationToken.None) : harness.Start(payload);
        }

        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        Assert.True(await state.Pending!.Completion.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(0, harness.Coordinator.GetMetrics().CommittedWrites);
        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        Assert.True(await state.Pending!.Completion.WaitAsync(TimeSpan.FromSeconds(2)));
        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        Assert.Equal(new uint[] { 12, 13 }, scheduled);
        Assert.Equal(2, state.QueueIndex);
        state.Cancellation.Dispose();
    }

    /// <summary>Actual synthetic provider failures must not store a sibling or source fallback.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("[Translation Error: simulated provider failure]")]
    [InlineData("unavailable-fixture")]
    public async Task RejectedProviderPayload_DoesNotPersistPartialOrSourceFallback(string rejected)
    {
        if (rejected == "unavailable-fixture")
        {
            rejected = global::Echoglossian.Properties.Resources.ChatGPTTranslationUnavailablePleaseCheckYourAPIKey;
        }

        await using var harness = await Harness.CreateAsync(failureRetryCooldown: TimeSpan.FromSeconds(30));
        var translator = new PayloadTranslator(rejected);
        var service = new TranslationService(text => text, translator);
        Task<TranslationFieldBatchResult> Translate(IReadOnlyList<TranslationField> fields, SourceClientLanguage source,
            string target, string? origin, CancellationToken token) => service.TranslateFieldsAsync(fields, source, target, origin, token);
        Assert.True(await harness.Start(Harness.Payload(), Translate).WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(3, translator.Calls);
        Assert.Equal(0, harness.Coordinator.GetMetrics().CommittedWrites);
        Assert.Null(harness.Cache.TryFindCanonicalMatch(12, Harness.Scope, "7.3", harness.Probe.SourceContentHash!));
        Assert.True(await harness.Start(Harness.Payload(), Translate).WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(3, translator.Calls);
        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.Empty(await context.MainCommandTexts.ToListAsync());
    }

    /// <summary>A terminal broker failure advances the actual cursor once and admits the next row.</summary>
    [Fact]
    public async Task RuntimeCursor_TerminalTranslationFailure_AdvancesToNextRow()
    {
        await using var harness = await Harness.CreateAsync();
        var state = new PluginEntry.ReferenceTextPrefetchState();
        state.Queue.AddRange([12, 13]);
        var attempted = new List<uint>();
        Task<bool> Schedule(uint id)
        {
            attempted.Add(id);
            var payload = Harness.Payload();
            payload.ReferenceId = id;
            return harness.Start(payload, (fields, _, _, _, _) => id == 12
                ? throw new InvalidOperationException("Terminal provider failure")
                : Task.FromResult(Harness.Translate(fields)));
        }

        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        Assert.True(await state.Pending!.Completion.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(0, harness.Coordinator.GetMetrics().CommittedWrites);
        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        Assert.Equal(1, state.QueueIndex);
        Assert.True(await state.Pending!.Completion.WaitAsync(TimeSpan.FromSeconds(2)));
        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        Assert.Equal(2, state.QueueIndex);
        Assert.Equal(new uint[] { 12, 13 }, attempted);
        state.Cancellation.Dispose();
    }

    /// <summary>Pending and capacity-rejected work do not advance or cause per-tick resubmission.</summary>
    [Fact]
    public async Task RuntimeCursor_CapacityAndPendingRead_RemainNonblocking()
    {
        await using var harness = await Harness.CreateAsync(backgroundCapacity: 1);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Coordinator.TryScheduleRead(new PersistenceWorkKey("cursor", "active"), PersistencePriority.Background,
            async (_, token) => { entered.SetResult(); await release.Task.WaitAsync(token); return 1; }, null, out var active);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        harness.Coordinator.TryScheduleRead(new PersistenceWorkKey("cursor", "queued"), PersistencePriority.Background,
            (_, _) => Task.FromResult(1), null, out var queued);
        var state = new PluginEntry.ReferenceTextPrefetchState();
        state.Queue.Add(12);
        var schedules = 0;
        Task<bool> Schedule(uint _)
        {
            schedules++;
            return harness.Start(Harness.Payload());
        }

        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        Assert.Equal(0, state.QueueIndex);
        Assert.Equal(1, schedules);
        release.SetResult();
        await Task.WhenAll(active, queued).WaitAsync(TimeSpan.FromSeconds(2));
        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        var pending = state.Pending!;
        Assert.NotNull(pending);
        Assert.True(await pending.Completion.WaitAsync(TimeSpan.FromSeconds(2)));
        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        Assert.Equal(1, state.QueueIndex);
        Assert.Equal(2, schedules);
        state.Cancellation.Dispose();
    }

    /// <summary>Repeated Framework ticks do not block or reschedule one incomplete operation.</summary>
    [Fact]
    public async Task RuntimeCursor_PendingOperation_IsNotResubmittedOnRepeatedTicks()
    {
        var state = new PluginEntry.ReferenceTextPrefetchState();
        state.Queue.Add(12);
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var schedules = 0;
        Task<bool> Schedule(uint id)
        {
            Assert.Equal((uint)12, id);
            schedules++;
            return pending.Task;
        }

        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        for (var tick = 0; tick < 100; tick++)
        {
            Assert.Equal(0, PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule));
        }

        Assert.Equal(1, schedules);
        Assert.Equal(0, state.QueueIndex);
        pending.SetResult(true);
        Assert.True(await state.Pending!.Completion.WaitAsync(TimeSpan.FromSeconds(2)));
        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        Assert.Equal(1, state.QueueIndex);
        Assert.Equal(1, schedules);
        state.Cancellation.Dispose();
    }

    /// <summary>Cancelled pending work retains its cursor; lifecycle clear cancels and discards the generation.</summary>
    [Fact]
    public async Task RuntimeCursor_CancelAndClear_AllowsFreshGeneration()
    {
        await using var harness = await Harness.CreateAsync();
        var state = new PluginEntry.ReferenceTextPrefetchState();
        state.Queue.Add(12);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, _ => harness.Start(Harness.Payload(), async (fields, _, _, _, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Harness.Translate(fields);
        }, state.Cancellation.Token));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        state.Cancellation.Cancel();
        Assert.False(await state.Pending!.Completion.WaitAsync(TimeSpan.FromSeconds(2)));
        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, _ => throw new InvalidOperationException("Retried in same completion tick."));
        Assert.Equal(0, state.QueueIndex);

        var plugin = (PluginEntry)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(PluginEntry));
        var states = new Dictionary<string, PluginEntry.ReferenceTextPrefetchState> { ["test"] = state };
        var operations = new Dictionary<string, Task<bool>> { ["old"] = Task.FromResult(false) };
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(PluginEntry).GetField("referenceTextPrefetchStates", flags)!.SetValue(plugin, states);
        typeof(PluginEntry).GetField("referenceTextPrefetchOperations", flags)!.SetValue(plugin, operations);
        typeof(PluginEntry).GetMethod("ClearReferenceTextPrefetchState", flags)!.Invoke(plugin, null);
        Assert.Empty(states);
        Assert.Empty(operations);

        var restarted = new PluginEntry.ReferenceTextPrefetchState();
        restarted.Queue.Add(13);
        PluginEntry.TickReferenceTextPrefetchQueue(restarted, 8, id =>
        {
            var payload = Harness.Payload();
            payload.ReferenceId = id;
            return harness.Start(payload, cancellationToken: restarted.Cancellation.Token);
        });
        Assert.True(await restarted.Pending!.Completion.WaitAsync(TimeSpan.FromSeconds(2)));
        PluginEntry.TickReferenceTextPrefetchQueue(restarted, 8, _ => throw new InvalidOperationException("Completed row repeated."));
        Assert.Equal(1, restarted.QueueIndex);
        restarted.Cancellation.Dispose();
    }

    /// <summary>A cursor encountering an exact-key failure cooldown advances without repeatedly reading or translating it.</summary>
    [Fact]
    public async Task RuntimeCursor_FailureCooldown_AdvancesOnceWithoutRepeatedReads()
    {
        await using var harness = await Harness.CreateAsync(failureRetryCooldown: TimeSpan.FromSeconds(30));
        var calls = 0;
        Task<TranslationFieldBatchResult> Fail(IReadOnlyList<TranslationField> fields, SourceClientLanguage source,
            string target, string? origin, CancellationToken token)
        {
            calls++;
            throw new InvalidOperationException("Terminal provider failure.");
        }

        Assert.True(await harness.Start(Harness.Payload(), Fail).WaitAsync(TimeSpan.FromSeconds(2)));
        var state = new PluginEntry.ReferenceTextPrefetchState();
        state.Queue.AddRange([12, 13]);
        var scheduled = new List<uint>();
        Task<bool> Schedule(uint id)
        {
            scheduled.Add(id);
            var payload = Harness.Payload();
            payload.ReferenceId = id;
            return harness.Start(payload, id == 12 ? Fail : null);
        }

        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        Assert.True(await state.Pending!.Completion.WaitAsync(TimeSpan.FromSeconds(2)));
        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        Assert.Equal(1, state.QueueIndex);
        Assert.True(await state.Pending!.Completion.WaitAsync(TimeSpan.FromSeconds(2)));
        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        var admissions = harness.Coordinator.GetMetrics().AcceptedOperations;
        for (var tick = 0; tick < 10; tick++)
        {
            PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        }

        Assert.Equal(admissions, harness.Coordinator.GetMetrics().AcceptedOperations);
        Assert.Equal(1, calls);
        Assert.Equal(new uint[] { 12, 13 }, scheduled);
        state.Cancellation.Dispose();
    }

    /// <summary>A synchronous initial probe exception cannot escape the Framework admission boundary.</summary>
    [Fact]
    public async Task Start_InitialProbeThrows_ReturnsTerminalCompletionWithoutEscaping()
    {
        await using var harness = await Harness.CreateAsync();
        Task<bool>? operation = null;
        var exception = Record.Exception(() => { operation = ReferenceTextPrefetchOperation.Start<MainCommandText>(
            harness.Writer, harness.Broker, harness.Cache, context => context.MainCommandTexts,
            (_, _, _, _, _, _) => throw new InvalidOperationException("Initial probe failed."), Harness.Payload(), "7.3",
            new SourceClientLanguage("en", "en"), Harness.Scope, "ProbeTest",
            (_, _, _, _, _) => throw new InvalidOperationException("Translator must not run."), CancellationToken.None); });
        Assert.Null(exception);
        Assert.True(await operation!);
        Assert.Equal(0, harness.Coordinator.GetMetrics().AcceptedOperations);

        var state = new PluginEntry.ReferenceTextPrefetchState();
        state.Queue.AddRange([12, 13]);
        var calls = new List<uint>();
        Task<bool> Schedule(uint id)
        {
            calls.Add(id);
            return id == 13 ? Task.FromResult(true) : ReferenceTextPrefetchOperation.Start<MainCommandText>(
                harness.Writer, harness.Broker, harness.Cache, context => context.MainCommandTexts,
                (_, _, _, _, _, _) => throw new InvalidOperationException("Initial probe failed."), Harness.Payload(), "7.3",
                new SourceClientLanguage("en", "en"), Harness.Scope, "ProbeTest",
                (_, _, _, _, _) => throw new InvalidOperationException("Translator must not run."), CancellationToken.None);
        }

        Assert.Null(Record.Exception(() => PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule)));
        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        Assert.Equal(2, state.QueueIndex);
        Assert.Equal(new uint[] { 12, 13 }, calls);
        Assert.Equal(0, harness.Coordinator.GetMetrics().AcceptedOperations);
        state.Cancellation.Dispose();
    }

    /// <summary>Unexpected synchronous schedule exceptions finish only the bad row; cancellation retains it.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RuntimeCursor_SynchronousScheduleException_IsContained(bool cancelled)
    {
        var state = new PluginEntry.ReferenceTextPrefetchState();
        state.Queue.AddRange([12, 13]);
        var calls = new List<uint>();
        Task<bool> Schedule(uint id)
        {
            calls.Add(id);
            if (calls.Count == 1)
            {
                throw cancelled ? new OperationCanceledException() : new InvalidOperationException("Capture failed.");
            }

            return Task.FromResult(true);
        }

        Assert.Null(Record.Exception(() => PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule)));
        Assert.Equal(cancelled ? 0 : 2, state.QueueIndex);
        PluginEntry.TickReferenceTextPrefetchQueue(state, 8, Schedule);
        Assert.Equal(2, state.QueueIndex);
        Assert.Equal(cancelled ? new uint[] { 12, 12, 13 } : new uint[] { 12, 13 }, calls);
        state.Cancellation.Dispose();
    }

    /// <summary>Exhausted coordinator failures have one coordinator log and no duplicate ReferenceText warning.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ExhaustedPersistenceFailure_DoesNotDuplicateCoordinatorLog(int failingContext)
    {
        var log = new TestDoubles.CapturingPluginLog();
        var original = PluginEntry.PluginLog;
        PluginEntry.PluginLog = log;
        var coordinatorLogs = 0;
        try
        {
            await using var harness = await Harness.CreateAsync(failingContext: failingContext,
                coordinatorErrorLog: _ => Interlocked.Increment(ref coordinatorLogs));
            Assert.True(await harness.Start(Harness.Payload()).WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Equal(1, coordinatorLogs);
            Assert.Empty(log.WarningMessages);
        }
        finally
        {
            PluginEntry.PluginLog = original;
        }
    }

    /// <summary>Owns a real SQLite/coordinator/broker fixture for captured prefetch operations.</summary>
    private sealed class Harness : IAsyncDisposable
    {
        private readonly string directory;

        /// <summary>Initializes one temporary runtime fixture.</summary>
        private Harness(string directory, IDbContextFactory<EchoglossianDbContext> factory, PersistenceCoordinator coordinator,
            int maxRateLimitRetries, TimeSpan? requestTimeout, TimeSpan? failureRetryCooldown)
        {
            this.directory = directory;
            this.Factory = factory;
            this.Coordinator = coordinator;
            this.Writer = new ReferenceTextPersistenceWriter(coordinator);
            this.Broker = new QueuedTranslationBroker(TimeSpan.Zero, failureRetryCooldown ?? TimeSpan.Zero,
                requestTimeout ?? TimeSpan.FromSeconds(5), TimeSpan.Zero, maxRateLimitRetries);
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
        internal QueuedTranslationBroker Broker { get; }
        /// <summary>Gets a probe built by the production MainCommand row creator.</summary>
        internal MainCommandText Probe => Row("en", "pt", 0, "7.3", Payload(), null);

        /// <summary>Creates and migrates a temporary SQLite fixture.</summary>
        internal static async Task<Harness> CreateAsync(int backgroundCapacity = 8, int maxRateLimitRetries = 0, TimeSpan? requestTimeout = null, int failingContext = 0, TimeSpan? failureRetryCooldown = null, Action<string>? coordinatorErrorLog = null)
        {
            var directory = Path.Combine(Path.GetTempPath(), "EchoglossianTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var factory = new EchoglossianDbContextRuntimeFactory(directory);
            await using var context = await factory.CreateDbContextAsync();
            await context.Database.MigrateAsync();
            return new Harness(directory, factory, new PersistenceCoordinator(
                failingContext == 0 ? factory : new FailingFactory(factory, failingContext),
                new PersistenceCoordinatorOptions(8, backgroundCapacity, 1, 8, TimeSpan.FromMilliseconds(1), 1, [], 4, 1, TimeSpan.FromSeconds(5)),
                errorLog: coordinatorErrorLog),
                maxRateLimitRetries, requestTimeout, failureRetryCooldown);
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

    /// <summary>Returns real provider error payloads alongside a successfully translated sibling.</summary>
    private sealed class PayloadTranslator(string rejected) : ITranslator
    {
        /// <summary>Gets the actual provider invocation count.</summary>
        internal int Calls { get; private set; }

        /// <inheritdoc />
        public string? Translate(string text, string sourceLanguage, string targetLanguage)
        {
            this.Calls++;
            return text.StartsWith("EGLO-FIELDS-1", StringComparison.Ordinal) ? "invalid envelope"
                : text == "Actions" ? "Acoes" : rejected;
        }

        /// <inheritdoc />
        public Task<string?> TranslateAsync(string text, string sourceLanguage, string targetLanguage) =>
            Task.FromResult(this.Translate(text, sourceLanguage, targetLanguage));
    }

    /// <summary>Injects one real worker context-open failure to exercise cursor retry outcomes.</summary>
    private sealed class FailingFactory(IDbContextFactory<EchoglossianDbContext> inner, int failingContext) : IDbContextFactory<EchoglossianDbContext>
    {
        private int opens;

        /// <inheritdoc />
        public EchoglossianDbContext CreateDbContext() => throw new InvalidOperationException("Synchronous context creation is forbidden.");

        /// <inheritdoc />
        public Task<EchoglossianDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref this.opens) == failingContext)
            {
                throw new InvalidOperationException("Injected worker context-open failure.");
            }

            return inner.CreateDbContextAsync(cancellationToken);
        }
    }
}
