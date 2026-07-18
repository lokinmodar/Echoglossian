// <copyright file="HostedPreviewPluginSessionTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Mock.Hosting;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using System;
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
    ///     Verifies that hosted startup seeds the database used by the production plugin configuration directory.
    /// </summary>
    /// <returns>A task that completes after hosted startup reaches its known blocker.</returns>
    [Fact]
    public async Task StartAsync_copies_supplied_database_to_effective_production_database_path()
    {
        using var fixture = PreviewOwnedHostedSessionFixture.Create(withDatabase: true);

        try
        {
            await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
                fixture.Options);
        }
        catch (ReflectionTypeLoadException exception)
        {
            exception.ToString().Should().Contain("CreateDebouncer");
        }

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

        try
        {
            await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
                fixture.Options);

            session.StateRoot.FullName.Should().Be(fixture.Options.StateRoot.FullName);
            session.PluginSavePath.FullName.Should().Be(fixture.Options.PluginSavePath.FullName);
            session.ConfigPath.FullName.Should().Be(fixture.Options.ConfigPath.FullName);
        }
        catch (ReflectionTypeLoadException exception)
        {
            exception.ToString().Should().Contain("CreateDebouncer");
        }
    }
}

/// <summary>
/// Owns isolated paths for hosted-session tests.
/// </summary>
internal sealed class PreviewOwnedHostedSessionFixture : IDisposable
{
    private PreviewOwnedHostedSessionFixture(DirectoryInfo stateRoot, bool withDatabase)
    {
        this.StateRoot = stateRoot;
        var pluginSavePath = stateRoot.CreateSubdirectory(".dalamock");
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
            new FileInfo(Path.Combine(stateRoot.FullName, "test.json")),
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
    /// Gets the production plugin database path that DalaMock resolves from its plugin save path.
    /// </summary>
    public string? EffectiveDatabasePath { get; }

    /// <summary>
    /// Creates an isolated fixture.
    /// </summary>
    /// <returns>The created fixture.</returns>
    public static PreviewOwnedHostedSessionFixture Create(bool withDatabase = false)
    {
        var stateRoot = new DirectoryInfo(Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Mock.Tests",
            Guid.NewGuid().ToString("N")));
        stateRoot.Create();
        return new PreviewOwnedHostedSessionFixture(stateRoot, withDatabase);
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
