// <copyright file="PluginStartupSmokeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginRuntime.Startup;

using FluentAssertions;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Echoglossian.Mock.Tests;

/// <summary>
/// Covers startup and shutdown milestones under DalaMock.
/// </summary>
public class PluginStartupSmokeTests
{
    [Fact]
    public void TryReadLauncherGamePath_reads_the_game_path_from_valid_launcher_json()
    {
        var stateRoot = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "Echoglossian.Mock.Tests", Guid.NewGuid().ToString("N")));
        stateRoot.Create();

        try
        {
            var launcherDirectory = stateRoot.CreateSubdirectory("XIVLauncher");
            var launcherConfigPath = Path.Combine(launcherDirectory.FullName, "launcherConfigV3.json");
            const string expectedGamePath = @"D:\Games\Final Fantasy XIV";

            File.WriteAllText(
                launcherConfigPath,
                JsonSerializer.Serialize(new
                {
                    GamePath = expectedGamePath,
                }));

            TestBoot.TryReadLauncherGamePath(launcherConfigPath).Should().Be(expectedGamePath);
        }
        finally
        {
            if (stateRoot.Exists)
            {
                stateRoot.Delete(true);
            }
        }
    }

    [Fact]
    public void TryReadLauncherGamePath_returns_null_for_malformed_launcher_json()
    {
        var stateRoot = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "Echoglossian.Mock.Tests", Guid.NewGuid().ToString("N")));
        stateRoot.Create();

        try
        {
            var launcherDirectory = stateRoot.CreateSubdirectory("XIVLauncher");
            var launcherConfigPath = Path.Combine(launcherDirectory.FullName, "launcherConfigV3.json");

            File.WriteAllText(launcherConfigPath, "{ not-valid-json");

            TestBoot.TryReadLauncherGamePath(launcherConfigPath).Should().BeNull();
        }
        finally
        {
            if (stateRoot.Exists)
            {
                stateRoot.Delete(true);
            }
        }
    }

    [Fact]
    public void StartedPlugin_exposes_deterministic_cleanup()
    {
        typeof(StartedPlugin).GetInterfaces().Should().Contain(typeof(IDisposable));
    }

    [Fact]
    public void StartedPlugin_exposes_rail_owned_host_state_paths()
    {
        typeof(StartedPlugin).GetProperty("PluginSavePath").Should().NotBeNull();
        typeof(StartedPlugin).GetProperty("ConfigPath").Should().NotBeNull();
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
        using var started = await new TestBoot().StartPluginAsync();

        var workingDirectoryPath = NormalizeDirectoryPath(Environment.CurrentDirectory);
        var stateRootPath = NormalizeDirectoryPath(started.StateRoot.FullName);
        var pluginSavePath = NormalizeDirectoryPath(started.PluginSavePath.FullName);
        var configPath = Path.GetFullPath(started.ConfigPath.FullName);

        stateRootPath.StartsWith(workingDirectoryPath, StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        pluginSavePath.StartsWith(stateRootPath, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        configPath.StartsWith(stateRootPath, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        started.PluginSavePath.Exists.Should().BeTrue();
        started.ConfigPath.Directory.Should().NotBeNull();
        NormalizeDirectoryPath(started.ConfigPath.Directory!.FullName)
            .Should()
            .Be(stateRootPath);
    }

    /// <summary>
    /// Normalizes a directory path to its fully-qualified trailing-separator form.
    /// </summary>
    /// <param name="path">The directory path to normalize.</param>
    /// <returns>The normalized directory path.</returns>
    private static string NormalizeDirectoryPath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
    }
}
