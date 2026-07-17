// <copyright file="PreviewSessionSourceOptions.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Previewer.Session;

/// <summary>
/// Represents optional source paths used to create a preview session snapshot.
/// </summary>
/// <param name="ConfigPath">The optional source configuration path.</param>
/// <param name="DatabasePath">The optional source database path.</param>
/// <param name="OutputDirectory">The optional screenshot output directory.</param>
internal sealed record PreviewSessionSourceOptions(
    string? ConfigPath,
    string? DatabasePath,
    string? OutputDirectory);
