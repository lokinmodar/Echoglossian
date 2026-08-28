// <copyright file="PreviewSessionLoaderTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI;
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
    /// Ensures config and database clones cannot collide when their source names match.
    /// </summary>
    [Fact]
    public void Load_ConfigAndDatabaseWithMatchingNames_UsesDistinctClonePaths()
    {
        var tempRoot = Directory.CreateTempSubdirectory();
        try
        {
            var configDirectory = Directory.CreateDirectory(
                Path.Combine(tempRoot.FullName, "config"));
            var databaseDirectory = Directory.CreateDirectory(
                Path.Combine(tempRoot.FullName, "database"));
            var configPath = Path.Combine(configDirectory.FullName, "Echoglossian.json");
            var databasePath = Path.Combine(databaseDirectory.FullName, "Echoglossian.json");
            File.WriteAllText(configPath, "{\"Lang\":28}");
            using (var sourceConnection = this.CreateWalDatabase(databasePath))
            using (var session = PreviewSessionLoader.Load(
                new PreviewSessionSourceOptions(configPath, databasePath, null)))
            {
                Assert.NotEqual(session.ClonedConfigPath, session.ClonedDatabasePath);
                Assert.True(File.Exists(session.ClonedConfigPath));
                Assert.True(File.Exists(session.ClonedDatabasePath));
                Assert.Equal("uncheckpointed", this.ReadStoredValue(session.ClonedDatabasePath!));
            }
        }
        finally
        {
            tempRoot.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Ensures database snapshot connection strings preserve source paths containing semicolons.
    /// </summary>
    [Fact]
    public void Load_DatabasePathContainingSemicolon_ClonesSnapshot()
    {
        var tempRoot = Directory.CreateTempSubdirectory();
        try
        {
            var configPath = Path.Combine(tempRoot.FullName, "Echoglossian.json");
            var databasePath = Path.Combine(tempRoot.FullName, "Echoglossian;preview.db");
            File.WriteAllText(configPath, "{\"Lang\":28}");

            using (var sourceConnection = this.CreateWalDatabase(databasePath))
            using (var session = PreviewSessionLoader.Load(
                new PreviewSessionSourceOptions(configPath, databasePath, null)))
            {
                Assert.NotNull(session.ClonedDatabasePath);
                Assert.Equal("uncheckpointed", this.ReadStoredValue(session.ClonedDatabasePath!));
            }
        }
        finally
        {
            tempRoot.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Ensures expected temporary-directory cleanup failures do not escape disposal.
    /// </summary>
    [Fact]
    public void TryDeleteWorkingDirectory_AccessFailure_DoesNotThrow()
    {
        var tempRoot = Directory.CreateTempSubdirectory();
        try
        {
            var exception = Record.Exception(
                () => PreviewSessionArtifacts.TryDeleteWorkingDirectory(
                    tempRoot.FullName,
                    static (_, _) => throw new UnauthorizedAccessException("locked")));

            Assert.Null(exception);
        }
        finally
        {
            tempRoot.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Ensures the preview save scope writes only to the session-owned config clone.
    /// </summary>
    [Fact]
    public void PushPreviewConfigSaveScope_RedirectsSavesToClone()
    {
        var tempRoot = Directory.CreateTempSubdirectory();
        try
        {
            var sourcePath = Path.Combine(tempRoot.FullName, "source.json");
            var clonePath = Path.Combine(tempRoot.FullName, "clone.json");
            File.WriteAllText(sourcePath, "{\"Lang\":28}");
            File.WriteAllText(clonePath, "{\"Lang\":28}");

            using (Program.PushPreviewConfigSaveScope(clonePath))
            {
                Assert.True(PluginConfigSaveScope.TrySave(new Config { Lang = 7 }));
            }

            Assert.Contains("\"Lang\": 7", File.ReadAllText(clonePath), StringComparison.Ordinal);
            Assert.Equal("{\"Lang\":28}", File.ReadAllText(sourcePath));
        }
        finally
        {
            tempRoot.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Ensures the tracked issue #274 preview sample loads the expected Arabic
    /// overlay settings without modifying the source file.
    /// </summary>
    [Fact]
    public void Load_Issue274ArabicSample_PreservesSourceAndExpectedSettings()
    {
        var repositoryRoot = FindRepositoryRoot();
        var samplePath = Path.Combine(
            repositoryRoot.FullName,
            "Echoglossian.Previewer",
            "Samples",
            "issue-274-arabic.json");
        var originalSample = File.Exists(samplePath)
            ? File.ReadAllText(samplePath)
            : string.Empty;

        using (var session = PreviewSessionLoader.Load(
            new PreviewSessionSourceOptions(samplePath, null, null)))
        {
            Assert.Equal(2, session.EditableConfiguration.Lang);
            Assert.True(session.EditableConfiguration.Translate);
            Assert.True(session.EditableConfiguration.TranslateTalk);
            Assert.Equal(
                JournalTranslationDisplayMode.TooltipTranslation,
                session.EditableConfiguration.TalkTranslationDisplayMode);
            Assert.Equal(originalSample, File.ReadAllText(samplePath));
        }

        Assert.Equal(originalSample, File.ReadAllText(samplePath));
    }

    /// <summary>
    /// Creates a WAL-mode database with a committed row that remains outside the main database file.
    /// </summary>
    /// <param name="databasePath">The SQLite database file to create.</param>
    /// <returns>An open source connection that keeps the WAL file live.</returns>
    private SqliteConnection CreateWalDatabase(string databasePath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
        }.ToString());
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
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM SessionData LIMIT 1;";
        return command.ExecuteScalar() as string;
    }

    /// <summary>
    /// Finds the repository root from the current test output directory.
    /// </summary>
    /// <returns>The absolute repository root path.</returns>
    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Echoglossian.sln")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the Echoglossian repository root.");
    }
}
