// <copyright file="PreviewViewportPreset.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Previewer.Scenarios;

/// <summary>
/// Describes one stable logical viewport size for preview scenarios.
/// </summary>
/// <param name="Key">The command-line and UI key.</param>
/// <param name="Width">The logical viewport width.</param>
/// <param name="Height">The logical viewport height.</param>
internal sealed record PreviewViewportPreset(string Key, int Width, int Height);
