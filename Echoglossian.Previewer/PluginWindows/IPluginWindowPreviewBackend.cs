// <copyright file="IPluginWindowPreviewBackend.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.UI;

using System.Drawing;

namespace Echoglossian.Previewer.PluginWindows;

/// <summary>
///     Hosts real plugin windows for the previewer.
/// </summary>
internal interface IPluginWindowPreviewBackend : IDisposable
{
    /// <summary>
    ///     Gets the current backend availability and selection status.
    /// </summary>
    PluginWindowBackendStatus Status { get; }

    /// <summary>
    ///     Gets a value indicating whether the database manager window is available.
    /// </summary>
    bool DbManagerAvailable { get; }

    /// <summary>
    ///     Gets a value indicating whether the active capture failed to stabilize.
    /// </summary>
    bool CaptureFailed { get; }

    /// <summary>
    ///     Draws the windows requested by the preview workbench.
    /// </summary>
    /// <param name="state">The shared workbench state.</param>
    void Draw(PreviewWorkbenchState state);

    /// <summary>
    ///     Starts deterministic capture for a plugin window.
    /// </summary>
    /// <param name="target">The requested plugin-window capture target.</param>
    void BeginCapture(PreviewCaptureTarget target);

    /// <summary>
    ///     Ends deterministic capture for the active plugin window.
    /// </summary>
    void EndCapture();

    /// <summary>
    ///     Gets stable capture bounds for a plugin window.
    /// </summary>
    /// <param name="target">The requested plugin-window capture target.</param>
    /// <returns>The stable bounds, or <see langword="null" /> when unavailable.</returns>
    Rectangle? TryGetStableCrop(PreviewCaptureTarget target);
}
