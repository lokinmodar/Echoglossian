// <copyright file="HostedPreviewPluginOptions.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

namespace Echoglossian.Mock.Hosting;

/// <summary>
/// Describes the preview-owned paths and UI mode for a hosted DalaMock plugin session.
/// </summary>
/// <param name="StateRoot">The root directory owned by the preview session.</param>
/// <param name="PluginSavePath">The DalaMock plugin-save directory owned by the preview session.</param>
/// <param name="ConfigPath">The plugin configuration file owned by the preview session.</param>
/// <param name="DatabasePath">The optional preview-owned database path reserved for hosted startup.</param>
/// <param name="CreateWindow">Whether DalaMock should create its UI window.</param>
public sealed record HostedPreviewPluginOptions(
    DirectoryInfo StateRoot,
    DirectoryInfo PluginSavePath,
    FileInfo ConfigPath,
    string? DatabasePath,
    bool CreateWindow);
