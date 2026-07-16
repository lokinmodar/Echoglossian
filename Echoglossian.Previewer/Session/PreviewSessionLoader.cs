// <copyright file="PreviewSessionLoader.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Configuration;

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

        var clonedConfigPath = Path.Combine(workingDirectory, "Echoglossian.json");
        File.WriteAllText(
            clonedConfigPath,
            JsonConvert.SerializeObject(editableConfiguration, Formatting.Indented));

        string? clonedDatabasePath = null;
        var databasePath = string.IsNullOrWhiteSpace(options.DatabasePath)
            ? GetDefaultDatabasePath()
            : Path.GetFullPath(options.DatabasePath);
        if (File.Exists(databasePath))
        {
            clonedDatabasePath = Path.Combine(
                workingDirectory,
                Path.GetFileName(databasePath));
            File.Copy(databasePath, clonedDatabasePath, overwrite: true);
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
}
