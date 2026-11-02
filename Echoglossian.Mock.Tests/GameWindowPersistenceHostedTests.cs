// <copyright file="GameWindowPersistenceHostedTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Echoglossian.Cache;
using Echoglossian.DBHelpers;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.Persistence;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Mock.Tests;

/// <summary>
///     Verifies hosted GameWindow persistence wiring and lifetime without claiming native payload coverage.
/// </summary>
public class GameWindowPersistenceHostedTests
{
    /// <summary>Repeated managed supplemental commits do not request a success-driven addon refresh loop.</summary>
    [Fact]
    public async Task Supplemental_success_does_not_request_refresh_when_repeated()
    {
        using var started = await new TestBoot().StartPluginAsync();
        var identity = Guid.NewGuid().ToString("N");
        var addonName = "SuccessfulSupplemental-" + identity;
        var handler = new SupplementalHandler(addonName);
        var gate = (DbFirstSourceRetryGate)typeof(DbFirstGameWindowAddonHandler)
            .GetField("retryGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler)!;
        var refresh = typeof(DbFirstGameWindowAddonHandler)
            .GetField("refreshRequested", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var scope = new TranslationReuseScope("en", "pt", 0, true);
        var operation = gate.TransitionTo(scope);
        var original = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string> { [1] = "Original" }, new(), new());
        var translated = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string> { [1] = "Traduzido" }, new(), new());

        for (var i = 0; i < 3; i++)
        {
            await handler.PersistSupplementalGameWindowPayloadAsync(
                scope, operation, original, translated, identity, 38).WaitAsync(TimeSpan.FromSeconds(10));

            gate.IsCoolingDown(operation, DateTime.UtcNow).Should().BeFalse();
            ((int)refresh.GetValue(handler)!).Should().Be(0);
            using var context = new EchoglossianDbContext(global::Echoglossian.Echoglossian.ConfigDirectory);
            var stored = context.GameWindow.Single(row => row.WindowAddonName == addonName);
            stored.TranslatedWindowStrings.Should().Be(translated.Serialize());
            GameWindowCacheManager.TryFindMatch(addonName, scope, stored.GameVersion, original.Serialize(), 38)!
                .TranslatedWindowStrings.Should().Be(translated.Serialize());
        }
    }

    /// <summary>Failed supplemental persistence keeps retry armed and wakes an already applied state after cooldown.</summary>
    [Fact]
    public async Task Supplemental_failure_rearms_retry_and_delayed_refresh()
    {
        using var started = await new TestBoot().StartPluginAsync();
        var handler = new SupplementalHandler("FailedSupplemental-" + Guid.NewGuid().ToString("N"));
        var gate = (DbFirstSourceRetryGate)typeof(DbFirstGameWindowAddonHandler)
            .GetField("retryGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler)!;
        var scope = new TranslationReuseScope("en", "pt", 0, true);
        var operation = gate.TransitionTo(scope);
        var completion = handler.PersistSupplementalGameWindowPayloadAsync(
            scope, operation, DbFirstGameWindowPayload.Empty, DbFirstGameWindowPayload.Empty,
            Guid.NewGuid().ToString("N"), null);

        completion.IsCompleted.Should().BeFalse();
        gate.IsCoolingDown(operation, DateTime.UtcNow).Should().BeTrue();
        var refresh = typeof(DbFirstGameWindowAddonHandler)
            .GetField("refreshRequested", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((int)refresh.GetValue(handler)!).Should().Be(0);
        await completion.WaitAsync(TimeSpan.FromSeconds(10));
        ((int)refresh.GetValue(handler)!).Should().Be(1);
    }

    /// <summary>
    ///     Drives the supplemental persistence completion against the hosted production writer under real queue pressure.
    /// </summary>
    /// <param name="retireScope">Whether ownership changes while the writer lane is saturated.</param>
    /// <param name="reopenSameScope">Whether the same payload is reopened under a newer lifecycle generation.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Hosted_supplemental_persistence_waits_for_capacity_and_respects_scope_retirement(
        bool retireScope, bool reopenSameScope)
    {
        using var started = await new TestBoot().StartPluginAsync();
        var coordinator = (PersistenceCoordinator)typeof(global::Echoglossian.Echoglossian)
            .GetField("persistenceCoordinator", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(started.Plugin)!;
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var identity = Guid.NewGuid().ToString("N");
        var addonName = "HostedSupplemental-" + identity;
        var pending = new List<Task<PersistenceWriteResult>>();
        Task? persistence = null;
        Task? reopenedPersistence = null;
        coordinator.TryScheduleWrite(new PersistenceWriteRequest(
            new PersistenceWorkKey("hosted-pressure", identity + "-blocker"),
            PersistencePriority.Interactive,
            async (_, token) =>
            {
                entered.TrySetResult(true);
                await release.Task.WaitAsync(token);
                return PersistenceWriteMutation.UnchangedResult;
            },
            () => { }), out var blocker).Should().Be(PersistenceAdmissionStatus.Accepted);
        pending.Add(blocker);

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            for (var i = 0; i < PersistenceCoordinatorOptions.Default.InteractiveCapacity; i++)
            {
                coordinator.TryScheduleWrite(new PersistenceWriteRequest(
                    new PersistenceWorkKey("hosted-pressure", identity + "-" + i),
                    PersistencePriority.Interactive,
                    (_, _) => Task.FromResult(PersistenceWriteMutation.UnchangedResult),
                    () => { }), out var completion).Should().Be(PersistenceAdmissionStatus.Accepted);
                pending.Add(completion);
            }

            coordinator.TryScheduleWrite(new PersistenceWriteRequest(
                new PersistenceWorkKey("hosted-pressure", identity + "-overflow"),
                PersistencePriority.Interactive,
                (_, _) => Task.FromResult(PersistenceWriteMutation.UnchangedResult),
                () => { }), out var rejected).Should().Be(PersistenceAdmissionStatus.RejectedCapacity);
            (await rejected).Status.Should().Be(PersistenceCompletionStatus.Rejected);

            var handler = new SupplementalHandler(addonName);
            var gate = (DbFirstSourceRetryGate)typeof(DbFirstGameWindowAddonHandler)
                .GetField("retryGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler)!;
            var scope = new TranslationReuseScope("en", "pt", 0, true);
            var operation = gate.TransitionTo(scope);
            var original = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string> { [1] = "Original" }, new(), new());
            var translated = new DbFirstGameWindowPayload(
                new SortedDictionary<int, string> { [1] = "Traduzido" }, new(), new());
            var before = coordinator.GetMetrics();
            persistence = handler.PersistSupplementalGameWindowPayloadAsync(
                scope, operation, original, translated, identity, 38);

            persistence.IsCompleted.Should().BeFalse();
            coordinator.GetMetrics().RejectedOperations.Should().BeGreaterThan(before.RejectedOperations);
            gate.IsCoolingDown(operation, DateTime.UtcNow).Should().BeTrue();
            var accepted = coordinator.GetMetrics().AcceptedOperations;
            var duplicate = handler.PersistSupplementalGameWindowPayloadAsync(
                scope, operation, original, translated, identity, 38);
            duplicate.IsCompletedSuccessfully.Should().BeTrue();
            coordinator.GetMetrics().AcceptedOperations.Should().Be(accepted);
            coordinator.GetMetrics().WriteInteractiveDepth.Should().Be(PersistenceCoordinatorOptions.Default.InteractiveCapacity);

            var currentOperation = operation;
            if (retireScope)
            {
                if (reopenSameScope)
                {
                    gate.Reset();
                    currentOperation = gate.TransitionTo(scope);
                    translated = new DbFirstGameWindowPayload(
                        new SortedDictionary<int, string> { [1] = "Reaberto" }, new(), new());
                    reopenedPersistence = handler.PersistSupplementalGameWindowPayloadAsync(
                        scope, currentOperation, original, translated, identity, 38);
                    reopenedPersistence.IsCompleted.Should().BeFalse();
                }
                else
                {
                    currentOperation = gate.TransitionTo(new TranslationReuseScope("en", "fr", 0, true));
                }

                gate.TrySetRetry(currentOperation, DateTime.UtcNow.AddMinutes(1)).Should().BeTrue();
                await persistence.WaitAsync(TimeSpan.FromSeconds(10));
                gate.IsCoolingDown(currentOperation, DateTime.UtcNow).Should().BeTrue();
                ((int)typeof(DbFirstGameWindowAddonHandler)
                    .GetField("refreshRequested", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler)!)
                    .Should().Be(0);
                if (reopenSameScope)
                {
                    reopenedPersistence!.IsCompleted.Should().BeFalse();
                    handler.PersistSupplementalGameWindowPayloadAsync(
                        scope, currentOperation, original, translated, identity, 38)
                        .IsCompletedSuccessfully.Should().BeTrue();
                }
            }

            release.TrySetResult(true);
            await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(15));
            await persistence.WaitAsync(TimeSpan.FromSeconds(15));
            if (reopenedPersistence != null)
            {
                await reopenedPersistence.WaitAsync(TimeSpan.FromSeconds(15));
            }

            var inFlight = (ConcurrentDictionary<string, byte>)typeof(DbFirstGameWindowAddonHandler)
                .GetField("InFlightPayloads", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            inFlight.Keys.Should().NotContain(key => key.Contains(identity, StringComparison.Ordinal));
            using var context = new EchoglossianDbContext(global::Echoglossian.Echoglossian.ConfigDirectory);
            var stored = context.GameWindow.SingleOrDefault(row => row.WindowAddonName == addonName);
            var refresh = (int)typeof(DbFirstGameWindowAddonHandler)
                .GetField("refreshRequested", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler)!;
            if (retireScope && !reopenSameScope)
            {
                stored.Should().BeNull();
                gate.IsCoolingDown(currentOperation, DateTime.UtcNow).Should().BeTrue();
                refresh.Should().Be(0);
                GameWindowCacheManager.GetCandidates(addonName, scope, null, 38, true).Should().BeEmpty();
            }
            else
            {
                stored.Should().NotBeNull();
                stored!.TranslatedWindowStrings.Should().Be(translated.Serialize());
                GameWindowCacheManager.TryFindMatch(addonName, scope, stored.GameVersion, original.Serialize(), 38)!
                    .TranslatedWindowStrings.Should().Be(translated.Serialize());
                gate.IsCoolingDown(currentOperation, DateTime.UtcNow).Should().BeFalse();
                refresh.Should().Be(0);
            }
        }
        finally
        {
            release.TrySetResult(true);
            await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(15));
            if (persistence != null)
            {
                await persistence.WaitAsync(TimeSpan.FromSeconds(15));
            }

            if (reopenedPersistence != null)
            {
                await reopenedPersistence.WaitAsync(TimeSpan.FromSeconds(15));
            }
        }
    }

    /// <summary>
    ///     Verifies that the hosted writer commits and publishes before completion and rejects work after shutdown.
    /// </summary>
    [Fact]
    public async Task Hosted_writer_commits_and_publishes_then_rejects_after_shutdown()
    {
        using var started = await new TestBoot().StartPluginAsync();
        var writer = (GameWindowPersistenceWriter)typeof(global::Echoglossian.Echoglossian)
            .GetField("gameWindowPersistenceWriter", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(started.Plugin)!;
        writer.Should().NotBeNull();
        writer.ShutdownToken.IsCancellationRequested.Should().BeFalse();
        var addonName = "HostedGameWindow-" + Guid.NewGuid().ToString("N");
        var row = new GameWindow(addonName, "original", "en", "traduzido", "pt", 0, "7.3", null, null);
        (await global::Echoglossian.Echoglossian.PersistGameWindowAsync(row)).Should().BeTrue();

        using (var context = new EchoglossianDbContext(global::Echoglossian.Echoglossian.ConfigDirectory))
        {
            context.GameWindow.Single(window => window.WindowAddonName == addonName)
                .TranslatedWindowStrings.Should().Be("traduzido");
        }

        GameWindowCacheManager.TryFindMatch(
            addonName,
            new TranslationReuseScope("en", "pt", 0, true),
            "7.3",
            "original")!.TranslatedWindowStrings.Should().Be("traduzido");

        started.Dispose();

        writer.ShutdownToken.IsCancellationRequested.Should().BeTrue();
        writer.TryPersist(row, out var rejected).Should().Be(PersistenceAdmissionStatus.RejectedShutdown);
        (await rejected).Status.Should().Be(PersistenceCompletionStatus.Rejected);
    }

    /// <summary>Hosts the shared managed persistence completion without touching native addon nodes.</summary>
    private sealed class SupplementalHandler : DbFirstGameWindowAddonHandler
    {
        /// <summary>Initializes one isolated supplemental payload owner.</summary>
        /// <param name="addonName">The test's unique addon identity.</param>
        internal SupplementalHandler(string addonName)
            : base(addonName, new Config(), null!, null!, _ => true, useAtkValues: true)
        {
        }
    }
}
