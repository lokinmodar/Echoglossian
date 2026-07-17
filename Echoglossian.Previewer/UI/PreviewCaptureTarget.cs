// <copyright file="PreviewCaptureTarget.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Previewer.UI;

/// <summary>
/// Identifies the preview surface targeted by screenshot capture.
/// </summary>
internal enum PreviewCaptureTarget
{
    /// <summary>Captures the complete preview framebuffer.</summary>
    FullFrame,

    /// <summary>Captures the rendered translation overlay surface.</summary>
    OverlaySurface,

    /// <summary>Captures the plugin configuration window.</summary>
    ConfigWindow,

    /// <summary>Captures the database manager window.</summary>
    DbManagerWindow,

    /// <summary>Captures the translator metrics window.</summary>
    TranslatorMetricsWindow,
}
