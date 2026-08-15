// <copyright file="PluginStartupSmokeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginRuntime.Startup;
using Echoglossian.Cache;
using Echoglossian.DBHelpers;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Mock.Hosting;
using Echoglossian.Translators.Capabilities;

using DalaMock.Core.Plugin;
using FluentAssertions;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public async Task StartPluginAsync_initializes_the_llm_capability_cache()
    {
        var stateRoot = new DirectoryInfo(Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Mock.Tests",
            Guid.NewGuid().ToString("N")));
        var seedDirectory = stateRoot.CreateSubdirectory("seed");
        var seedDatabasePath = Path.Combine(seedDirectory.FullName, "Echoglossian.db");
        LlmCapabilityPersistenceHelper.UpsertRules(
            seedDirectory.FullName,
            [
                LlmModelCapabilityRule.CreateExactModel(
                    "ChatGPT",
                    "OpenAI",
                    "https://api.openai.com/v1",
                    "gpt-5.6-terra",
                    LlmCapabilityParameterName.Temperature,
                    LlmCapabilitySupportState.Unsupported,
                    omitWhenDefaultOnly: true,
                    source: "Observed400",
                    reason: "provider rejected non-default temperature"),
                ]);

        try
        {
            using (var seedContext = new EchoglossianDbContext(seedDirectory.FullName))
            {
                seedContext.LlmModelCapabilityRules
                    .Should()
                    .ContainSingle(rule => rule.MatchValue == "gpt-5.6-terra");
            }

            LlmCapabilityCacheManager.Clear();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
                new HostedPreviewPluginOptions(
                    stateRoot,
                    new DirectoryInfo(Path.Combine(stateRoot.FullName, ".dalamock")),
                    new FileInfo(Path.Combine(stateRoot.FullName, "test.json")),
                    seedDatabasePath,
                    CreateWindow: false));

            using (var context = new EchoglossianDbContext(
                global::Echoglossian.Echoglossian.ConfigDirectory))
            {
                context.LlmModelCapabilityRules
                    .Should()
                    .ContainSingle(rule => rule.MatchValue == "gpt-5.6-terra");
            }

            LlmCapabilityCacheManager.GetRuleDefinitions()
                .Should()
                .ContainSingle(rule => rule.MatchValue == "gpt-5.6-terra");
        }
        finally
        {
            LlmCapabilityCacheManager.Clear();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (stateRoot.Exists)
            {
                stateRoot.Delete(recursive: true);
            }
        }
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

    [Fact]
    public async Task StartPluginAsync_cleans_up_host_state_when_start_plugin_throws()
    {
        var stateRoot = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "Echoglossian.Mock.Tests", Guid.NewGuid().ToString("N")));
        Func<Func<Task>, Task> startPluginFailure = _ => throw new InvalidOperationException("synthetic start failure");

        var boot = CreateConfigurableTestBoot(
            () => stateRoot,
            startPluginFailure,
            static (_, _, _, _, _) => Task.CompletedTask);

        Func<Task> act = async () => await boot.StartPluginAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("synthetic start failure");
        Directory.Exists(stateRoot.FullName).Should().BeFalse();
    }

    [Fact]
    public async Task StartPluginAsync_cleans_up_host_state_when_post_start_validation_throws()
    {
        var stateRoot = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "Echoglossian.Mock.Tests", Guid.NewGuid().ToString("N")));
        Func<MockContainer, global::Echoglossian.Echoglossian, DirectoryInfo, DirectoryInfo, FileInfo, Task> postStartValidationFailure =
            static (_, _, _, _, _) => throw new InvalidOperationException("synthetic validation failure");

        var boot = CreateConfigurableTestBoot(
            () => stateRoot,
            static startPlugin => startPlugin(),
            postStartValidationFailure);

        Func<Task> act = async () => await boot.StartPluginAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("synthetic validation failure");
        Directory.Exists(stateRoot.FullName).Should().BeFalse();
    }

    /// <summary>
    /// Creates a configurable <see cref="TestBoot"/> instance through its
    /// non-public test seam constructor.
    /// </summary>
    /// <param name="stateRootFactory">Creates the isolated state root for the run under test.</param>
    /// <param name="startPluginRunner">Runs or overrides the plugin-startup action.</param>
    /// <param name="postStartValidation">Runs additional validation before the rail returns a started plugin.</param>
    /// <returns>The configured <see cref="TestBoot"/> instance.</returns>
    private static TestBoot CreateConfigurableTestBoot(
        Func<DirectoryInfo> stateRootFactory,
        Func<Func<Task>, Task> startPluginRunner,
        Func<MockContainer, global::Echoglossian.Echoglossian, DirectoryInfo, DirectoryInfo, FileInfo, Task> postStartValidation)
    {
        var seamConstructor = typeof(TestBoot)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .SingleOrDefault(static constructor =>
            {
                var parameters = constructor.GetParameters();
                return parameters.Length == 3
                    && parameters[0].ParameterType == typeof(Func<DirectoryInfo>)
                    && parameters[1].ParameterType == typeof(Func<Func<Task>, Task>)
                    && parameters[2].ParameterType == typeof(Func<MockContainer, global::Echoglossian.Echoglossian, DirectoryInfo, DirectoryInfo, FileInfo, Task>);
            });

        seamConstructor.Should().NotBeNull("the startup cleanup path needs a deterministic test seam");

        return (TestBoot)seamConstructor!.Invoke(
            [
                stateRootFactory,
                startPluginRunner,
                postStartValidation,
            ]);
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
