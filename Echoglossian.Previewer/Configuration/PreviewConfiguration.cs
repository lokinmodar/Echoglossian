// <copyright file="PreviewConfiguration.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Newtonsoft.Json;

namespace Echoglossian.Previewer.Configuration;

/// <summary>
/// Represents a read-only source configuration loaded for previewing.
/// </summary>
public sealed class PreviewConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewConfiguration" /> class.
    /// </summary>
    /// <param name="config">The isolated configuration snapshot.</param>
    /// <param name="sourcePath">The resolved source configuration path.</param>
    /// <param name="loaded">Whether a source file was successfully loaded.</param>
    /// <param name="diagnostics">Non-secret loader diagnostics.</param>
    internal PreviewConfiguration(
        Config config,
        string sourcePath,
        bool loaded,
        IReadOnlyList<string> diagnostics)
    {
        this.Config = config;
        this.SourcePath = sourcePath;
        this.Loaded = loaded;
        this.Diagnostics = diagnostics;
    }

    /// <summary>
    /// Gets the isolated loaded configuration snapshot.
    /// </summary>
    public Config Config { get; }

    /// <summary>
    /// Gets the absolute path of the inspected source configuration.
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    /// Gets a value indicating whether a source configuration was loaded.
    /// </summary>
    public bool Loaded { get; }

    /// <summary>
    /// Gets non-secret configuration-loader diagnostics.
    /// </summary>
    public IReadOnlyList<string> Diagnostics { get; }

    /// <summary>
    /// Creates an independent configuration instance for preview-only edits.
    /// </summary>
    /// <returns>A deep clone of the loaded configuration snapshot.</returns>
    public Config CreateEditableCopy()
    {
        var serialized = JsonConvert.SerializeObject(this.Config);
        return JsonConvert.DeserializeObject<Config>(serialized) ?? new Config();
    }
}
