// <copyright file="UiFontKind.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Runtime;

/// <summary>
/// Identifies the font family used by a plugin UI rendering operation.
/// </summary>
internal enum UiFontKind
{
    /// <summary>
    /// Uses the general font that contains original-language glyphs.
    /// </summary>
    General,

    /// <summary>
    /// Uses the target-language font.
    /// </summary>
    Language,
}
