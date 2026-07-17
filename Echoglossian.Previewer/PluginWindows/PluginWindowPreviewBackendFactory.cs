// <copyright file="PluginWindowPreviewBackendFactory.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

namespace Echoglossian.Previewer.PluginWindows;

/// <summary>
///     Selects a plugin-window preview backend while preserving explicit hosted-mode failures.
/// </summary>
internal static class PluginWindowPreviewBackendFactory
{
    /// <summary>
    ///     Creates the requested plugin-window preview backend.
    /// </summary>
    /// <param name="requestedMode">The mode requested by the previewer operator.</param>
    /// <param name="createHostedBackend">Starts the DalaMock-hosted backend.</param>
    /// <param name="createStandaloneBackend">Creates the standalone backend.</param>
    /// <returns>The selected backend and its visible effective status.</returns>
    internal static async Task<(IPluginWindowPreviewBackend Backend, PluginWindowBackendStatus Status)>
        CreateAsync(
            PluginWindowPreviewBackendMode requestedMode,
            Func<Task<DalaMockHostedPluginWindowPreviewBackend>> createHostedBackend,
            Func<IPluginWindowPreviewBackend> createStandaloneBackend)
    {
        ArgumentNullException.ThrowIfNull(createHostedBackend);
        ArgumentNullException.ThrowIfNull(createStandaloneBackend);

        if (requestedMode == PluginWindowPreviewBackendMode.Standalone)
        {
            var backend = createStandaloneBackend();
            return (backend, backend.Status);
        }

        try
        {
            var hostedBackend = await createHostedBackend();
            return (hostedBackend, hostedBackend.Status);
        }
        catch (Exception exception) when (requestedMode == PluginWindowPreviewBackendMode.Auto)
        {
            var standaloneBackend = createStandaloneBackend();
            var fallbackStatus = new PluginWindowBackendStatus(
                PluginWindowPreviewBackendMode.Auto,
                PluginWindowPreviewBackendMode.Standalone,
                HostedRequested: true,
                HostedAvailable: false,
                FallbackReason: CreateFallbackReason(exception));
            return (new StatusOverrideBackend(standaloneBackend, fallbackStatus), fallbackStatus);
        }
    }

    /// <summary>
    ///     Creates a backend selection result from a hosted-startup test seam.
    /// </summary>
    /// <param name="requestedMode">The mode requested by the test.</param>
    /// <param name="startHostedBackend">Starts the hosted backend or throws its failure.</param>
    /// <returns>The selected backend and its visible effective status.</returns>
    internal static Task<(IPluginWindowPreviewBackend Backend, PluginWindowBackendStatus Status)>
        CreateForTestsAsync(
            PluginWindowPreviewBackendMode requestedMode,
            Func<Task> startHostedBackend)
    {
        ArgumentNullException.ThrowIfNull(startHostedBackend);
        return CreateAsync(
            requestedMode,
            async () =>
            {
                await startHostedBackend();
                throw new InvalidOperationException(
                    "The hosted test seam must throw before creating a backend.");
            },
            static () => StandalonePluginWindowPreviewBackend.CreateForTests(
                dbManagerAvailable: true));
    }

    private static string CreateFallbackReason(Exception exception)
    {
        if (exception is ReflectionTypeLoadException typeLoadException &&
            typeLoadException.LoaderExceptions.Length > 0)
        {
            return $"{exception.Message} {string.Join(" ", typeLoadException.LoaderExceptions.Select(loaderException => loaderException?.Message))}";
        }

        return exception.Message;
    }

    private sealed class StatusOverrideBackend : IPluginWindowPreviewBackend
    {
        private readonly IPluginWindowPreviewBackend inner;

        internal StatusOverrideBackend(IPluginWindowPreviewBackend inner, PluginWindowBackendStatus status)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.Status = status ?? throw new ArgumentNullException(nameof(status));
        }

        public PluginWindowBackendStatus Status { get; }

        public bool DbManagerAvailable => this.inner.DbManagerAvailable;

        public bool CaptureFailed => this.inner.CaptureFailed;

        public void Draw(UI.PreviewWorkbenchState state) => this.inner.Draw(state);

        public void BeginCapture(UI.PreviewCaptureTarget target) => this.inner.BeginCapture(target);

        public void EndCapture() => this.inner.EndCapture();

        public System.Drawing.Rectangle? TryGetStableCrop(UI.PreviewCaptureTarget target) => this.inner.TryGetStableCrop(target);

        public void Dispose() => this.inner.Dispose();
    }
}
