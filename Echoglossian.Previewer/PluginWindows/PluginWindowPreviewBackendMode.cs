// <copyright file="PluginWindowPreviewBackendMode.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Previewer.PluginWindows;

/// <summary>
///     Identifies the backend requested for real plugin-window previews.
/// </summary>
internal enum PluginWindowPreviewBackendMode
{
    /// <summary>
    ///     Automatically selects the highest-fidelity available backend.
    /// </summary>
    Auto,

    /// <summary>
    ///     Uses the existing standalone preview backend.
    /// </summary>
    Standalone,

    /// <summary>
    ///     Uses the DalaMock-hosted plugin-window backend.
    /// </summary>
    DalaMockHosted,
}
