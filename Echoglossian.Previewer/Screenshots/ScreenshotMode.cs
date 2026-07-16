// <copyright file="ScreenshotMode.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Previewer.Screenshots;

/// <summary>
/// Describes the preview screenshot capture mode.
/// </summary>
internal enum ScreenshotMode
{
    /// <summary>
    /// Captures the full rendered frame.
    /// </summary>
    Full,

    /// <summary>
    /// Captures the selected overlay surface bounds.
    /// </summary>
    Surface,

    /// <summary>
    /// Captures all requested preview scenarios.
    /// </summary>
    Batch,
}
