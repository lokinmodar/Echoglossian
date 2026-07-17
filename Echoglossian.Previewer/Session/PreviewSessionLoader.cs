// <copyright file="PreviewSessionLoader.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Configuration;

using Microsoft.Data.Sqlite;

using Newtonsoft.Json;

namespace Echoglossian.Previewer.Session;

/// <summary>
/// Creates isolated, disposable source snapshots for preview sessions.
/// </summary>
internal static class PreviewSessionLoader
{
    /// <summary>
    /// Gets the default Echoglossian database path in the XIVLauncher configuration directory.
    /// </summary>
    /// <returns>The default absolute database path.</returns>
    internal static string GetDefaultDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(
            appData,
            "XIVLauncher",
            "pluginConfigs",
            "Echoglossian",
            "Echoglossian.db");
    }

    /// <summary>
    /// Loads sources into a preview-owned workspace without modifying the sources.
    /// </summary>
    /// <param name="options">The optional source paths for the preview session.</param>
    /// <returns>The preview session artifacts.</returns>
    internal static PreviewSessionArtifacts Load(PreviewSessionSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var sourceConfiguration = PreviewConfigLoader.Load(options.ConfigPath);
        var editableConfiguration = sourceConfiguration.CreateEditableCopy();
        var diagnostics = sourceConfiguration.Diagnostics.ToList();
        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Previewer",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var clonedConfigPath = Path.Combine(
                workingDirectory,
                "Echoglossian.preview.json");
            File.WriteAllText(
                clonedConfigPath,
                JsonConvert.SerializeObject(editableConfiguration, Formatting.Indented));

            string? clonedDatabasePath = null;
            var databasePath = string.IsNullOrWhiteSpace(options.DatabasePath)
                ? GetDefaultDatabasePath()
                : Path.GetFullPath(options.DatabasePath);
            if (File.Exists(databasePath))
            {
                var databaseCloneCandidate = Path.Combine(
                    workingDirectory,
                    "Echoglossian.preview.db");
                try
                {
                    CloneDatabase(databasePath, databaseCloneCandidate);
                    clonedDatabasePath = databaseCloneCandidate;
                }
                catch (Exception exception) when (
                    exception is SqliteException or
                    IOException or
                    UnauthorizedAccessException or
                    InvalidOperationException)
                {
                    TryDeleteFile(databaseCloneCandidate);
                    diagnostics.Add(
                        "Preview database snapshot could not be created; " +
                        "DB-backed windows will be unavailable.");
                }
            }
            else
            {
                diagnostics.Add(
                    "Preview database file was not found; DB-backed windows will be unavailable.");
            }

            return new PreviewSessionArtifacts(
                workingDirectory,
                sourceConfiguration,
                editableConfiguration,
                clonedConfigPath,
                clonedDatabasePath,
                diagnostics);
        }
        catch
        {
            TryDeleteDirectory(workingDirectory);
            throw;
        }
    }

    /// <summary>
    /// Deletes a partial optional database clone without replacing the clone failure.
    /// </summary>
    /// <param name="path">The partial clone path.</param>
    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Deletes an incomplete session workspace without replacing the load failure.
    /// </summary>
    /// <param name="path">The session workspace path.</param>
    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Creates a consistent SQLite snapshot, including transactions committed to a live WAL file.
    /// </summary>
    /// <param name="sourceDatabasePath">The live SQLite database source path.</param>
    /// <param name="destinationDatabasePath">The preview-owned SQLite snapshot path.</param>
    private static void CloneDatabase(string sourceDatabasePath, string destinationDatabasePath)
    {
        using var sourceConnection = new SqliteConnection(
            $"Data Source={sourceDatabasePath};Mode=ReadOnly;Pooling=False");
        using var destinationConnection = new SqliteConnection(
            $"Data Source={destinationDatabasePath};Mode=ReadWriteCreate;Pooling=False");
        sourceConnection.Open();
        destinationConnection.Open();
        sourceConnection.BackupDatabase(destinationConnection);
    }
}
