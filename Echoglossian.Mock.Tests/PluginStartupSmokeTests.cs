// <copyright file="PluginStartupSmokeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginRuntime.Startup;

using FluentAssertions;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Echoglossian.Mock.Tests;

/// <summary>
/// Covers startup and shutdown milestones under DalaMock.
/// </summary>
public class PluginStartupSmokeTests
{
    [Fact]
    public void StartedPlugin_exposes_deterministic_cleanup()
    {
        typeof(StartedPlugin).GetInterfaces().Should().Contain(typeof(IDisposable));
    }

    [Fact]
    public async Task StartPluginAsync_marks_expected_startup_stages()
    {
        using var started = await new TestBoot().StartPluginAsync();

        var snapshot = started.Plugin.StartupAudit.CaptureSnapshot();

        snapshot.HasStage(PluginStartupStage.CommandHandlersRegistered).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.PluginUiRegistered).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.RuntimeServicesBuilt).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.RuntimeCachesPreloaded).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.FrameworkUpdateRegistered).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.AddonHandlersRegistered).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.OverlaysRegistered).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.StartupComplete).Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_marks_expected_shutdown_stages()
    {
        var started = await new TestBoot().StartPluginAsync();

        started.Dispose();

        var snapshot = started.Plugin.StartupAudit.CaptureSnapshot();

        snapshot.HasStage(PluginStartupStage.DisposeStarted).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.PluginUiUnregistered).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.FrameworkUpdateUnregistered).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.RuntimeServicesDisposed).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.DisposeComplete).Should().BeTrue();
    }

    [Fact]
    public async Task StartPluginAsync_keeps_host_state_out_of_the_test_working_directory()
    {
        var legacyPluginSavePath = Path.Combine(Environment.CurrentDirectory, ".dalamock");
        var legacyPluginConfigPath = Path.Combine(Environment.CurrentDirectory, "test.json");

        DeleteLegacyHostState(legacyPluginSavePath, legacyPluginConfigPath);

        using var started = await new TestBoot().StartPluginAsync();

        Directory.Exists(legacyPluginSavePath).Should().BeFalse();
        File.Exists(legacyPluginConfigPath).Should().BeFalse();

        DeleteLegacyHostState(legacyPluginSavePath, legacyPluginConfigPath);
    }

    /// <summary>
    /// Deletes any legacy host-state paths left under the current test working directory.
    /// </summary>
    /// <param name="legacyPluginSavePath">The legacy plugin-save directory path.</param>
    /// <param name="legacyPluginConfigPath">The legacy plugin configuration file path.</param>
    private static void DeleteLegacyHostState(string legacyPluginSavePath, string legacyPluginConfigPath)
    {
        if (Directory.Exists(legacyPluginSavePath))
        {
            Directory.Delete(legacyPluginSavePath, true);
        }

        if (File.Exists(legacyPluginConfigPath))
        {
            File.Delete(legacyPluginConfigPath);
        }
    }
}
