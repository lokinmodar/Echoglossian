// <copyright file="LlmCapabilityObservationWriterTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.DBHelpers;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.EFCoreSqlite;
using Echoglossian.Persistence;
using Echoglossian.Translators.Capabilities;

using FluentAssertions;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

using Xunit;

using System.Data.Common;

namespace Echoglossian.Tests.Persistence;

/// <summary>Verifies the coordinator-backed capability observation adapter.</summary>
public sealed class LlmCapabilityObservationWriterTests
{
    /// <summary>Ensures all seven identity fields are inserted as one row.</summary>
    [Fact]
    public async Task RecordAsync_NewIdentity_InsertsOneObservation()
    {
        await this.WithWriterAsync(async (writer, factory, _) =>
        {
            writer.TryRecord(CreateObservation(), out var completion);
            (await completion).Status.Should().Be(PersistenceCompletionStatus.Succeeded);
            await using var context = await factory.CreateDbContextAsync();
            var row = await context.LlmModelCapabilityObservations.SingleAsync();
            row.Engine.Should().Be("ChatGPT"); row.ProviderScope.Should().Be("OpenAI");
            row.EndpointScope.Should().Be("https://api.openai.com/v1"); row.ModelId.Should().Be("gpt-test");
            row.ParameterName.Should().Be("Temperature"); row.StatusCode.Should().Be(400); row.MessageExcerpt.Should().Be("unsupported temperature");
        });
    }

    /// <summary>Ensures a matching identity updates only mutable observation fields.</summary>
    [Fact]
    public async Task RecordAsync_ExistingIdentity_UpdatesOnlyErrorCodeAndObservedTime()
    {
        await this.WithWriterAsync(async (writer, factory, _) =>
        {
            var first = CreateObservation(); first.ObservedAtUtc = DateTime.UnixEpoch;
            writer.TryRecord(first, out var initial); await initial;
            var replacement = CreateObservation(); replacement.ProviderErrorCode = "new-code"; replacement.ObservedAtUtc = DateTime.UnixEpoch.AddMinutes(1);
            writer.TryRecord(replacement, out var completion); await completion;
            await using var context = await factory.CreateDbContextAsync();
            var row = await context.LlmModelCapabilityObservations.SingleAsync();
            row.Engine.Should().Be(first.Engine); row.ProviderScope.Should().Be(first.ProviderScope);
            row.EndpointScope.Should().Be(first.EndpointScope); row.ModelId.Should().Be(first.ModelId);
            row.ParameterName.Should().Be(first.ParameterName); row.StatusCode.Should().Be(first.StatusCode);
            row.MessageExcerpt.Should().Be(first.MessageExcerpt); row.Id.Should().BeGreaterThan(0);
            row.ProviderErrorCode.Should().Be("new-code"); row.ObservedAtUtc.Should().Be(replacement.ObservedAtUtc);
        });
    }

    /// <summary>Ensures equivalent writes do not add duplicate rows.</summary>
    [Fact]
    public async Task RecordAsync_RepeatedPendingIdentity_CoalescesToLatestObservation()
    {
        var dequeued = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await this.WithWriterAsync(async (writer, factory, _) =>
        {
            var first = CreateObservation(); first.ObservedAtUtc = DateTime.UnixEpoch;
            var latest = CreateObservation(); latest.ProviderErrorCode = "latest"; latest.ObservedAtUtc = DateTime.UnixEpoch.AddMinutes(1);
            writer.TryRecord(first, out var one).Should().Be(PersistenceAdmissionStatus.Accepted);
            await dequeued.Task;
            writer.TryRecord(latest, out var two).Should().Be(PersistenceAdmissionStatus.Replaced);
            ReferenceEquals(one, two).Should().BeTrue(); release.SetResult(true);
            await Task.WhenAll(one, two);
            await using var context = await factory.CreateDbContextAsync();
            (await context.LlmModelCapabilityObservations.CountAsync()).Should().Be(1);
            var row = await context.LlmModelCapabilityObservations.SingleAsync(); row.ProviderErrorCode.Should().Be("latest"); row.ObservedAtUtc.Should().Be(latest.ObservedAtUtc);
        }, writeDequeuedBeforeClaimAsync: async () => { dequeued.SetResult(true); await release.Task; });
    }

    /// <summary>Ensures committed writes publish the cache projection.</summary>
    [Fact]
    public async Task RecordAsync_DoesNotPublishBeforeCommit()
    {
        var gate = new CommitGateInterceptor { Enabled = true };
        await this.WithWriterAsync(async (writer, _, _) =>
        {
            LlmCapabilityCacheManager.Clear();
            writer.TryRecord(CreateObservation(), out var completion);
            await gate.CommitEntered.Task;
            LlmCapabilityCacheManager.GetObservationDefinitions().Should().BeEmpty();
            completion.IsCompleted.Should().BeFalse(); gate.ReleaseCommit.SetResult(true);
            await completion;
            LlmCapabilityCacheManager.GetObservationDefinitions().Should().ContainSingle();
        }, interceptor: gate);
    }

    /// <summary>Ensures disabling publication prevents late projection publication.</summary>
    [Fact]
    public async Task RecordAsync_FailedCommit_DoesNotPublish()
    {
        var gate = new CommitGateInterceptor { Enabled = true, ThrowOnCommit = true };
        await this.WithWriterAsync(async (writer, factory, _) =>
        {
            LlmCapabilityCacheManager.Clear(); writer.TryRecord(CreateObservation(), out var completion);
            var result = await completion; result.Status.Should().Be(PersistenceCompletionStatus.Failed);
            await using var context = await factory.CreateDbContextAsync();
            (await context.LlmModelCapabilityObservations.CountAsync()).Should().Be(0);
            LlmCapabilityCacheManager.GetObservationDefinitions().Should().BeEmpty();
        }, interceptor: gate);
    }

    /// <summary>Ensures an unrelated unregister cannot disable the live writer.</summary>
    [Fact]
    public async Task Unregister_WrongWriter_DoesNotDisableRegisteredWriter()
    {
        await this.WithWriterAsync(async (writerA, factory, _) =>
        {
            await using var otherCoordinator = new PersistenceCoordinator(factory);
            var writerB = new LlmCapabilityObservationWriter(otherCoordinator);
            LlmCapabilityObservationRuntime.Register(writerA);
            try
            {
                LlmCapabilityObservationRuntime.Unregister(writerB);
                LlmCapabilityObservationRuntime.TryRecord(CreateObservation(), out var completion);
                await completion;
                LlmCapabilityCacheManager.GetObservationDefinitions().Should().ContainSingle();
                LlmCapabilityObservationRuntime.Unregister(writerA);
                LlmCapabilityObservationRuntime.Register(writerB);
                LlmCapabilityCacheManager.Clear();
                LlmCapabilityObservationRuntime.TryRecord(CreateObservation(), out completion);
                await completion;
                LlmCapabilityCacheManager.GetObservationDefinitions().Should().ContainSingle();
            }
            finally
            {
                LlmCapabilityObservationRuntime.Unregister(writerA);
                LlmCapabilityObservationRuntime.Unregister(writerB);
            }
        });
    }

    /// <summary>Ensures a correctly unregistered accepted write cannot publish late.</summary>
    [Fact]
    public async Task Unregister_CorrectWriter_BlocksPublicationAfterCommit()
    {
        var gate = new CommitGateInterceptor { Enabled = true };
        await this.WithWriterAsync(async (writer, factory, _) =>
        {
            LlmCapabilityCacheManager.Clear();
            LlmCapabilityObservationRuntime.Register(writer);
            try
            {
                LlmCapabilityObservationRuntime.TryRecord(CreateObservation(), out var completion);
                await gate.CommitEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                LlmCapabilityObservationRuntime.Unregister(writer);
                gate.ReleaseCommit.SetResult(true);
                (await completion).Status.Should().Be(PersistenceCompletionStatus.Succeeded);
                await using var context = await factory.CreateDbContextAsync();
                (await context.LlmModelCapabilityObservations.CountAsync()).Should().Be(1);
                LlmCapabilityCacheManager.GetObservationDefinitions().Should().BeEmpty();
            }
            finally
            {
                gate.ReleaseCommit.TrySetResult(true);
                LlmCapabilityObservationRuntime.Unregister(writer);
            }
        }, interceptor: gate);
    }

    private static LlmModelCapabilityObservation CreateObservation() => new()
    { Engine = "ChatGPT", ProviderScope = "OpenAI", EndpointScope = "https://api.openai.com/v1", ModelId = "gpt-test", ParameterName = "Temperature", StatusCode = 400, ProviderErrorCode = "unsupported_parameter", MessageExcerpt = "unsupported temperature" };

    private async Task WithWriterAsync(
        Func<LlmCapabilityObservationWriter, IDbContextFactory<EchoglossianDbContext>, string, Task> action,
        DbTransactionInterceptor? interceptor = null,
        Func<Task>? writeDequeuedBeforeClaimAsync = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        IDbContextFactory<EchoglossianDbContext> factory = interceptor is null
            ? new EchoglossianDbContextRuntimeFactory(directory)
            : new TestFactory(Path.Combine(directory, "Echoglossian.db"), interceptor);
        await using var coordinator = new PersistenceCoordinator(factory, writeDequeuedBeforeClaimAsync: writeDequeuedBeforeClaimAsync);
        try
        {
            if (interceptor is CommitGateInterceptor gate) { gate.Enabled = false; }
            await using (var context = await factory.CreateDbContextAsync()) { await context.Database.MigrateAsync(); }
            if (interceptor is CommitGateInterceptor enabledGate) { enabledGate.Enabled = true; }
            await action(new LlmCapabilityObservationWriter(coordinator), factory, directory);
        }
        finally { LlmCapabilityCacheManager.Clear(); SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    }

    private sealed class TestFactory : IDbContextFactory<EchoglossianDbContext>
    {
        private readonly DbContextOptions<EchoglossianDbContext> options;
        internal TestFactory(string path, DbTransactionInterceptor interceptor) => this.options = new DbContextOptionsBuilder<EchoglossianDbContext>().UseSqlite($"Data Source={path}").AddInterceptors(interceptor).Options;
        public EchoglossianDbContext CreateDbContext() => new(this.options);
        public Task<EchoglossianDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(this.CreateDbContext());
    }

    private sealed class CommitGateInterceptor : DbTransactionInterceptor
    {
        internal bool ThrowOnCommit { get; init; }
        internal bool Enabled { get; set; }
        internal TaskCompletionSource<bool> CommitEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> ReleaseCommit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (!this.Enabled) { return result; }
            this.CommitEntered.TrySetResult(true);
            if (this.ThrowOnCommit) { throw new InvalidOperationException("commit failed"); }
            await this.ReleaseCommit.Task.WaitAsync(cancellationToken); return result;
        }
    }
}
