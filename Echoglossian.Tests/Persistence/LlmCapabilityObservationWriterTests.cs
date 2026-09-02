// <copyright file="LlmCapabilityObservationWriterTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.DBHelpers;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Persistence;
using Echoglossian.Translators.Capabilities;

using FluentAssertions;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Xunit;

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
            row.ProviderErrorCode.Should().Be("new-code"); row.ObservedAtUtc.Should().Be(replacement.ObservedAtUtc); row.ModelId.Should().Be(first.ModelId);
        });
    }

    /// <summary>Ensures equivalent writes do not add duplicate rows.</summary>
    [Fact]
    public async Task RecordAsync_RepeatedPendingIdentity_CoalescesToLatestObservation()
    {
        await this.WithWriterAsync(async (writer, factory, _) =>
        {
            var first = CreateObservation(); var latest = CreateObservation(); latest.ProviderErrorCode = "latest";
            writer.TryRecord(first, out var one); writer.TryRecord(latest, out var two);
            await Task.WhenAll(one, two);
            await using var context = await factory.CreateDbContextAsync();
            (await context.LlmModelCapabilityObservations.CountAsync()).Should().Be(1);
        });
    }

    /// <summary>Ensures committed writes publish the cache projection.</summary>
    [Fact]
    public async Task RecordAsync_DoesNotPublishBeforeCommit()
    {
        await this.WithWriterAsync(async (writer, _, _) =>
        {
            LlmCapabilityCacheManager.Clear();
            writer.TryRecord(CreateObservation(), out var completion);
            LlmCapabilityCacheManager.GetObservationDefinitions().Should().BeEmpty();
            await completion;
            LlmCapabilityCacheManager.GetObservationDefinitions().Should().ContainSingle();
        });
    }

    /// <summary>Ensures disabling publication prevents late projection publication.</summary>
    [Fact]
    public async Task RecordAsync_FailedCommit_DoesNotPublish()
    {
        await this.WithWriterAsync(async (writer, _, _) =>
        {
            LlmCapabilityCacheManager.Clear(); writer.DisablePublication();
            writer.TryRecord(CreateObservation(), out var completion); await completion;
            LlmCapabilityCacheManager.GetObservationDefinitions().Should().BeEmpty();
        });
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

    private static LlmModelCapabilityObservation CreateObservation() => new()
    { Engine = "ChatGPT", ProviderScope = "OpenAI", EndpointScope = "https://api.openai.com/v1", ModelId = "gpt-test", ParameterName = "Temperature", StatusCode = 400, ProviderErrorCode = "unsupported_parameter", MessageExcerpt = "unsupported temperature" };

    private async Task WithWriterAsync(Func<LlmCapabilityObservationWriter, EchoglossianDbContextRuntimeFactory, string, Task> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var factory = new EchoglossianDbContextRuntimeFactory(directory);
        await using var coordinator = new PersistenceCoordinator(factory);
        try
        {
            await using (var context = await factory.CreateDbContextAsync()) { await context.Database.MigrateAsync(); }
            await action(new LlmCapabilityObservationWriter(coordinator), factory, directory);
        }
        finally { LlmCapabilityCacheManager.Clear(); SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    }
}
