// <copyright file="PreviewSessionArtifacts.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Configuration;

namespace Echoglossian.Previewer.Session;

/// <summary>
/// Owns the isolated files and configuration state used by one preview session.
/// </summary>
internal sealed class PreviewSessionArtifacts : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewSessionArtifacts" /> class.
    /// </summary>
    /// <param name="workingDirectory">The session-owned working directory.</param>
    /// <param name="configuration">The read-only source configuration snapshot.</param>
    /// <param name="editableConfiguration">The session-owned editable configuration.</param>
    /// <param name="clonedConfigPath">The session-owned configuration file path.</param>
    /// <param name="clonedDatabasePath">The optional session-owned database file path.</param>
    /// <param name="diagnostics">Non-secret session diagnostics.</param>
    internal PreviewSessionArtifacts(
        string workingDirectory,
        PreviewConfiguration configuration,
        Config editableConfiguration,
        string clonedConfigPath,
        string? clonedDatabasePath,
        IReadOnlyList<string> diagnostics)
    {
        this.WorkingDirectory = workingDirectory;
        this.Configuration = configuration;
        this.EditableConfiguration = editableConfiguration;
        this.ClonedConfigPath = clonedConfigPath;
        this.ClonedDatabasePath = clonedDatabasePath;
        this.Diagnostics = diagnostics;
    }

    /// <summary>
    /// Gets the session-owned working directory.
    /// </summary>
    internal string WorkingDirectory { get; }

    /// <summary>
    /// Gets the read-only source configuration snapshot.
    /// </summary>
    internal PreviewConfiguration Configuration { get; }

    /// <summary>
    /// Gets the editable session-owned configuration.
    /// </summary>
    internal Config EditableConfiguration { get; }

    /// <summary>
    /// Gets the session-owned configuration file path.
    /// </summary>
    internal string ClonedConfigPath { get; }

    /// <summary>
    /// Gets the optional session-owned database file path.
    /// </summary>
    internal string? ClonedDatabasePath { get; }

    /// <summary>
    /// Gets non-secret session diagnostics.
    /// </summary>
    internal IReadOnlyList<string> Diagnostics { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(this.WorkingDirectory))
        {
            Directory.Delete(this.WorkingDirectory, recursive: true);
        }
    }
}
