// <copyright file="DalaMockHostedPluginWindowPreviewBackend.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Mock.Hosting;
using Echoglossian.Previewer.UI;

using System.Drawing;

namespace Echoglossian.Previewer.PluginWindows;

/// <summary>
///     Hosts the production plugin in DalaMock while rendering preview windows in the preview host.
/// </summary>
internal sealed class DalaMockHostedPluginWindowPreviewBackend : IPluginWindowPreviewBackend
{
    private readonly HostedPreviewPluginSession session;
    private readonly StandalonePluginWindowPreviewBackend fallbackRenderer;
    private bool disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DalaMockHostedPluginWindowPreviewBackend" /> class.
    /// </summary>
    /// <param name="session">The started DalaMock-hosted production plugin session.</param>
    /// <param name="fallbackRenderer">The preview-owned renderer for plugin window output.</param>
    internal DalaMockHostedPluginWindowPreviewBackend(
        HostedPreviewPluginSession session,
        StandalonePluginWindowPreviewBackend fallbackRenderer)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.fallbackRenderer = fallbackRenderer ??
            throw new ArgumentNullException(nameof(fallbackRenderer));
        this.Status = new PluginWindowBackendStatus(
            PluginWindowPreviewBackendMode.DalaMockHosted,
            PluginWindowPreviewBackendMode.DalaMockHosted,
            HostedRequested: true,
            HostedAvailable: true,
            FallbackReason: null);
    }

    /// <inheritdoc />
    public PluginWindowBackendStatus Status { get; }

    /// <inheritdoc />
    public bool DbManagerAvailable => this.fallbackRenderer.DbManagerAvailable;

    /// <inheritdoc />
    public bool CaptureFailed => this.fallbackRenderer.CaptureFailed;

    /// <inheritdoc />
    public void Draw(PreviewWorkbenchState state) => this.fallbackRenderer.Draw(state);

    /// <inheritdoc />
    public void BeginCapture(PreviewCaptureTarget target) =>
        this.fallbackRenderer.BeginCapture(target);

    /// <inheritdoc />
    public void EndCapture() => this.fallbackRenderer.EndCapture();

    /// <inheritdoc />
    public Rectangle? TryGetStableCrop(PreviewCaptureTarget target) =>
        this.fallbackRenderer.TryGetStableCrop(target);

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.fallbackRenderer.Dispose();
        this.session.Dispose();
    }
}
