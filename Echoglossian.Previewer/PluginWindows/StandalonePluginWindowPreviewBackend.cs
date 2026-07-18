// <copyright file="StandalonePluginWindowPreviewBackend.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.UI;

using System.Drawing;

namespace Echoglossian.Previewer.PluginWindows;

/// <summary>
///     Provides plugin-window previews through the standalone ImGui host.
/// </summary>
internal sealed class StandalonePluginWindowPreviewBackend : IPluginWindowPreviewBackend
{
    private readonly PreviewPluginWindowHost host;

    private StandalonePluginWindowPreviewBackend(PreviewPluginWindowHost host)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        this.Status = new PluginWindowBackendStatus(
            PluginWindowPreviewBackendMode.Standalone,
            PluginWindowPreviewBackendMode.Standalone,
            HostedRequested: false,
            HostedAvailable: true,
            FallbackReason: null);
    }

    /// <summary>
    ///     Gets the current backend availability and selection status.
    /// </summary>
    public PluginWindowBackendStatus Status { get; }

    /// <summary>
    ///     Gets a value indicating whether the database manager window is available.
    /// </summary>
    public bool DbManagerAvailable => this.host.DbManagerAvailable;

    /// <summary>
    ///     Gets a value indicating whether the active capture failed to stabilize.
    /// </summary>
    public bool CaptureFailed => this.host.CaptureFailed;

    /// <summary>
    ///     Creates a standalone backend around a real preview window host.
    /// </summary>
    /// <param name="host">The standalone preview window host.</param>
    /// <returns>The standalone backend.</returns>
    internal static StandalonePluginWindowPreviewBackend Create(PreviewPluginWindowHost host)
    {
        return new StandalonePluginWindowPreviewBackend(host);
    }

    /// <summary>
    ///     Creates a standalone backend for status-focused tests.
    /// </summary>
    /// <param name="dbManagerAvailable">Whether the database manager is available.</param>
    /// <returns>The standalone backend.</returns>
    internal static StandalonePluginWindowPreviewBackend CreateForTests(bool dbManagerAvailable)
    {
        return new StandalonePluginWindowPreviewBackend(
            PreviewPluginWindowHost.CreateForTests(dbManagerAvailable));
    }

    /// <inheritdoc />
    public void Draw(PreviewWorkbenchState state) => this.host.Draw(state);

    /// <inheritdoc />
    public void BeginCapture(PreviewCaptureTarget target) => this.host.BeginCapture(target);

    /// <inheritdoc />
    public void EndCapture() => this.host.EndCapture();

    /// <inheritdoc />
    public Rectangle? TryGetStableCrop(PreviewCaptureTarget target) =>
        this.host.TryGetStableCrop(target);

    /// <inheritdoc />
    public void Dispose() => this.host.Dispose();
}
