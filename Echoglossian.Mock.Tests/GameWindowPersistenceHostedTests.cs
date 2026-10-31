// <copyright file="GameWindowPersistenceHostedTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Echoglossian.Cache;
using Echoglossian.DBHelpers;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Persistence;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Mock.Tests;

/// <summary>
///     Verifies hosted GameWindow persistence wiring and lifetime without claiming native payload coverage.
/// </summary>
public class GameWindowPersistenceHostedTests
{
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
}
