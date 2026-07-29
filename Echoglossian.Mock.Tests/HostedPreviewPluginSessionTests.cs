// <copyright file="HostedPreviewPluginSessionTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Mock.Hosting;
using Echoglossian.NativeUI.AddonHandlers.Quest;
using Echoglossian.NativeUI.AddonHandlers.SelectionDialogs;
using Echoglossian.NativeUI.Handlers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Echoglossian.Mock.Tests;

/// <summary>
/// Covers the reusable DalaMock hosted-session bootstrap.
/// </summary>
public sealed class HostedPreviewPluginSessionTests
{
    /// <summary>
    /// Verifies that hosted startup seeds the plugin config file DalaMock reads.
    /// </summary>
    /// <returns>A task that completes after hosted startup loads the preview-owned config clone.</returns>
    [Fact]
    public async Task StartAsync_copies_supplied_config_to_effective_dalamock_config_path()
    {
        using var fixture = PreviewOwnedHostedSessionFixture.Create(
            config: new global::Echoglossian.Config
            {
                Lang = 7,
                FontSize = 31,
            });

        await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
            fixture.Options);

        fixture.EffectiveConfigurationPath.Should().NotBeNull();
        File.Exists(fixture.EffectiveConfigurationPath!).Should().BeTrue();

        var seededConfig = JsonConvert.DeserializeObject<global::Echoglossian.Config>(
            File.ReadAllText(fixture.EffectiveConfigurationPath!));
        seededConfig.Should().NotBeNull();
        seededConfig!.Lang.Should().Be(7);
        seededConfig.FontSize.Should().Be(31);

        var configurationField = typeof(global::Echoglossian.Echoglossian).GetField(
            "configuration",
            BindingFlags.Instance | BindingFlags.NonPublic);
        configurationField.Should().NotBeNull();
        var activeConfiguration = configurationField!.GetValue(session.Plugin)
            as global::Echoglossian.Config;
        activeConfiguration.Should().NotBeNull();
        activeConfiguration!.Lang.Should().Be(7);
        activeConfiguration.FontSize.Should().Be(31);
    }

    /// <summary>
    /// Verifies that startup does not forcibly disable the ActionDetail /
    /// ItemDetail runtime after the user enables it in configuration.
    /// </summary>
    /// <returns>A task that completes after hosted startup loads the configuration.</returns>
    [Fact]
    public async Task StartAsync_preserves_action_item_detail_translation_settings()
    {
        using var fixture = PreviewOwnedHostedSessionFixture.Create(
            config: new global::Echoglossian.Config
            {
                TranslateTooltips = true,
                TooltipTranslationDisplayMode =
                    global::Echoglossian.JournalTranslationDisplayMode.TooltipTranslation,
            });

        await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
            fixture.Options);

        var activeConfiguration = GetActiveConfiguration(session.Plugin);

        activeConfiguration.TranslateTooltips.Should().BeTrue();
        activeConfiguration.TooltipTranslationDisplayMode.Should().Be(
            global::Echoglossian.JournalTranslationDisplayMode.TooltipTranslation);
    }

    /// <summary>
    /// Verifies that hosted startup seeds the database used by the production plugin configuration directory.
    /// </summary>
    /// <returns>A task that completes after hosted startup copies the preview-owned database clone.</returns>
    [Fact]
    public async Task StartAsync_copies_supplied_database_to_effective_production_database_path()
    {
        using var fixture = PreviewOwnedHostedSessionFixture.Create(withDatabase: true);

        await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
            fixture.Options);

        fixture.EffectiveDatabasePath.Should().NotBeNull();
        using var connection = new SqliteConnection($"Data Source={fixture.EffectiveDatabasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM preview_marker";
        command.ExecuteScalar().Should().Be("preview database");
    }

    /// <summary>
    /// Verifies that hosted startup uses only explicitly supplied preview paths.
    /// </summary>
    /// <returns>A task that completes after the hosted session starts.</returns>
    [Fact]
    public async Task StartAsync_uses_explicit_preview_owned_paths_or_reports_known_dalamock_blocker()
    {
        using var fixture = PreviewOwnedHostedSessionFixture.Create();
        fixture.Options.StateRoot.FullName.Should().Be(fixture.StateRoot.FullName);
        fixture.Options.PluginSavePath.Parent!.FullName.Should().Be(fixture.StateRoot.FullName);
        fixture.Options.ConfigPath.DirectoryName.Should().Be(fixture.StateRoot.FullName);

        await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
            fixture.Options);

        session.StateRoot.FullName.Should().Be(fixture.Options.StateRoot.FullName);
        session.PluginSavePath.FullName.Should().Be(fixture.Options.PluginSavePath.FullName);
        session.ConfigPath.FullName.Should().Be(fixture.Options.ConfigPath.FullName);
    }

    /// <summary>
    /// Verifies that the remaining quest-family handlers are active in the
    /// real plugin when their config toggles are enabled under DalaMock.
    /// </summary>
    /// <returns>A task that completes after hosted startup wires the addon handlers.</returns>
    [Fact]
    public async Task StartAsync_registers_remaining_quest_family_handlers_when_enabled()
    {
        using var fixture = PreviewOwnedHostedSessionFixture.Create(
            config: new global::Echoglossian.Config
            {
                TranslateJournalAccept = true,
                TranslateJournalResult = true,
                TranslateRecommendList = true,
                TranslateAreaMap = true,
            });

        await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
            fixture.Options);

        var registeredHandlers = GetRegisteredAddonHandlers(session.Plugin);
        registeredHandlers.Should().Contain(entry =>
            entry.AddonName == "JournalAccept" &&
            entry.Handler is JournalAcceptHandler);
        registeredHandlers.Should().Contain(entry =>
            entry.AddonName == "JournalResult" &&
            entry.Handler is JournalResultHandler);
        registeredHandlers.Should().Contain(entry =>
            entry.AddonName == "RecommendList" &&
            entry.Handler is RecommendListHandler);
        registeredHandlers.Should().Contain(entry =>
            entry.AddonName == "AreaMap" &&
            entry.Handler is MapSurfaceStringArrayHandler);
        registeredHandlers.Should().Contain(entry =>
            entry.AddonName == "_NaviMap" &&
            entry.Handler is MapSurfaceStringArrayHandler);
    }

    /// <summary>
    /// Verifies that hosted startup wires the generic selection-dialog
    /// handlers when their config toggles are enabled.
    /// </summary>
    /// <returns>A task that completes after hosted startup wires the addon handlers.</returns>
    [Fact]
    public async Task StartAsync_registers_selection_dialog_handlers_when_enabled()
    {
        using var fixture = PreviewOwnedHostedSessionFixture.Create(
            config: new global::Echoglossian.Config
            {
                TranslateYesNoScreen = true,
                TranslateSelectOk = true,
                TranslateSelectString = true,
            });

        await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
            fixture.Options);

        var registeredHandlers = GetRegisteredAddonHandlers(session.Plugin);
        registeredHandlers.Should().Contain(entry =>
            entry.AddonName == "SelectYesno" &&
            entry.Handler is SelectYesNoHandler);
        registeredHandlers.Should().Contain(entry =>
            entry.AddonName == "SelectOk" &&
            entry.Handler is SelectOkHandler);
        registeredHandlers.Should().Contain(entry =>
            entry.AddonName == "SelectString" &&
            entry.Handler is SelectStringHandler);
    }

    private static IReadOnlyList<(string AddonName, IAddonTranslationHandler Handler)>
        GetRegisteredAddonHandlers(global::Echoglossian.Echoglossian plugin)
    {
        var handlersField = typeof(global::Echoglossian.Echoglossian).GetField(
            "registeredAddonHandlers",
            BindingFlags.Instance | BindingFlags.NonPublic);
        handlersField.Should().NotBeNull();
        return handlersField!.GetValue(plugin)
            .Should()
            .BeAssignableTo<List<(string AddonName, IAddonTranslationHandler Handler)>>()
            .Subject;
    }

    /// <summary>
    /// Gets the active production configuration from the hosted plugin.
    /// </summary>
    /// <param name="plugin">The hosted production plugin.</param>
    /// <returns>The active configuration instance.</returns>
    private static global::Echoglossian.Config GetActiveConfiguration(
        global::Echoglossian.Echoglossian plugin)
    {
        var configurationField = typeof(global::Echoglossian.Echoglossian).GetField(
            "configuration",
            BindingFlags.Instance | BindingFlags.NonPublic);
        configurationField.Should().NotBeNull();
        return configurationField!.GetValue(plugin)
            .Should()
            .BeAssignableTo<global::Echoglossian.Config>()
            .Subject;
    }
}

/// <summary>
/// Owns isolated paths for hosted-session tests.
/// </summary>
internal sealed class PreviewOwnedHostedSessionFixture : IDisposable
{
    private PreviewOwnedHostedSessionFixture(
        DirectoryInfo stateRoot,
        bool withDatabase,
        global::Echoglossian.Config? config)
    {
        this.StateRoot = stateRoot;
        var pluginSavePath = stateRoot.CreateSubdirectory(".dalamock");
        var configPath = Path.Combine(stateRoot.FullName, "test.json");
        if (config is not null)
        {
            File.WriteAllText(
                configPath,
                JsonConvert.SerializeObject(config, Formatting.Indented));
            this.EffectiveConfigurationPath = Path.Combine(
                stateRoot.FullName,
                "Echoglossian.json");
        }

        var databasePath = withDatabase
            ? Path.Combine(stateRoot.FullName, "Echoglossian.preview.db")
            : null;
        if (databasePath is not null)
        {
            using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE preview_marker (value TEXT NOT NULL); INSERT INTO preview_marker VALUES ('preview database');";
            command.ExecuteNonQuery();
            this.EffectiveDatabasePath = Path.Combine(
                stateRoot.FullName,
                "Echoglossian",
                "Echoglossian.db");
        }

        this.Options = new HostedPreviewPluginOptions(
            stateRoot,
            pluginSavePath,
            new FileInfo(configPath),
            databasePath,
            CreateWindow: false);
    }

    /// <summary>
    /// Gets the hosted-session options for the fixture.
    /// </summary>
    public HostedPreviewPluginOptions Options { get; }

    /// <summary>
    /// Gets the fixture's isolated state root.
    /// </summary>
    public DirectoryInfo StateRoot { get; }

    /// <summary>
    /// Gets the DalaMock config file path used to load the production plugin configuration.
    /// </summary>
    public string? EffectiveConfigurationPath { get; }

    /// <summary>
    /// Gets the production plugin database path that DalaMock resolves from its plugin save path.
    /// </summary>
    public string? EffectiveDatabasePath { get; }

    /// <summary>
    /// Creates an isolated fixture.
    /// </summary>
    /// <returns>The created fixture.</returns>
    public static PreviewOwnedHostedSessionFixture Create(
        bool withDatabase = false,
        global::Echoglossian.Config? config = null)
    {
        var stateRoot = new DirectoryInfo(Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Mock.Tests",
            Guid.NewGuid().ToString("N")));
        stateRoot.Create();
        return new PreviewOwnedHostedSessionFixture(stateRoot, withDatabase, config);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (this.StateRoot.Exists)
                {
                    this.StateRoot.Delete(true);
                }

                return;
            }
            catch (IOException)
            {
                if (attempt == 9)
                {
                    throw;
                }

                SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Thread.Sleep(TimeSpan.FromMilliseconds(50));
                this.StateRoot.Refresh();
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt == 9)
                {
                    throw;
                }

                SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Thread.Sleep(TimeSpan.FromMilliseconds(50));
                this.StateRoot.Refresh();
            }
        }
    }
}
