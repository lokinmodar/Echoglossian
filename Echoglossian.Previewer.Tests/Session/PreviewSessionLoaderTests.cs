// <copyright file="PreviewSessionLoaderTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Session;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Echoglossian.Previewer.Tests.Session;

/// <summary>
/// Covers isolated preview session snapshots.
/// </summary>
public sealed class PreviewSessionLoaderTests
{
    /// <summary>
    /// Ensures config and database sources are cloned into preview-owned session storage.
    /// </summary>
    [Fact]
    public void Load_ClonesCommittedWalDataIntoSessionWorkspace()
    {
        var tempRoot = Directory.CreateTempSubdirectory();
        try
        {
            var configPath = Path.Combine(tempRoot.FullName, "Echoglossian.json");
            var databasePath = Path.Combine(tempRoot.FullName, "Echoglossian.db");
            File.WriteAllText(configPath, "{\"Lang\":28,\"FontSize\":24}");

            using (var sourceConnection = this.CreateWalDatabase(databasePath))
            using (var session = PreviewSessionLoader.Load(
                new PreviewSessionSourceOptions(configPath, databasePath, null)))
            {
                Assert.NotEqual(configPath, session.ClonedConfigPath);
                Assert.NotEqual(databasePath, session.ClonedDatabasePath);
                Assert.True(File.Exists(session.ClonedConfigPath));
                Assert.True(File.Exists(session.ClonedDatabasePath));
                Assert.Equal(28, session.EditableConfiguration.Lang);
                Assert.Equal("uncheckpointed", this.ReadStoredValue(session.ClonedDatabasePath!));
            }
        }
        finally
        {
            tempRoot.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Ensures a missing database remains unavailable without modifying live sources.
    /// </summary>
    [Fact]
    public void Load_AllowsMissingDatabaseWithoutTouchingLiveSources()
    {
        var tempRoot = Directory.CreateTempSubdirectory();
        try
        {
            var configPath = Path.Combine(tempRoot.FullName, "Echoglossian.json");
            var databasePath = Path.Combine(tempRoot.FullName, "missing.db");
            File.WriteAllText(configPath, "{\"Lang\":28}");

            using (var session = PreviewSessionLoader.Load(
                new PreviewSessionSourceOptions(configPath, databasePath, null)))
            {
                Assert.Null(session.ClonedDatabasePath);
                Assert.Contains(
                    "database",
                    string.Join(" ", session.Diagnostics),
                    StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            tempRoot.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Ensures a corrupt optional database is discarded without blocking config preview.
    /// </summary>
    [Fact]
    public void Load_CorruptDatabase_ContinuesWithoutPartialClone()
    {
        var tempRoot = Directory.CreateTempSubdirectory();
        try
        {
            var configPath = Path.Combine(tempRoot.FullName, "Echoglossian.json");
            var databasePath = Path.Combine(tempRoot.FullName, "Echoglossian.db");
            File.WriteAllText(configPath, "{\"Lang\":28}");
            File.WriteAllText(databasePath, "not a sqlite database");

            using var session = PreviewSessionLoader.Load(
                new PreviewSessionSourceOptions(configPath, databasePath, null));

            Assert.Equal(28, session.EditableConfiguration.Lang);
            Assert.Null(session.ClonedDatabasePath);
            Assert.False(File.Exists(Path.Combine(
                session.WorkingDirectory,
                "Echoglossian.db")));
            Assert.Contains(
                "database",
                string.Join(" ", session.Diagnostics),
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            tempRoot.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Creates a WAL-mode database with a committed row that remains outside the main database file.
    /// </summary>
    /// <param name="databasePath">The SQLite database file to create.</param>
    /// <returns>An open source connection that keeps the WAL file live.</returns>
    private SqliteConnection CreateWalDatabase(string databasePath)
    {
        var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE SessionData (Value TEXT NOT NULL); PRAGMA wal_checkpoint(TRUNCATE); INSERT INTO SessionData (Value) VALUES ('uncheckpointed');";
            command.ExecuteNonQuery();
        }

        return connection;
    }

    /// <summary>
    /// Reads the test value from a cloned database.
    /// </summary>
    /// <param name="databasePath">The cloned SQLite database file.</param>
    /// <returns>The stored test value.</returns>
    private string? ReadStoredValue(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM SessionData LIMIT 1;";
        return command.ExecuteScalar() as string;
    }
}
