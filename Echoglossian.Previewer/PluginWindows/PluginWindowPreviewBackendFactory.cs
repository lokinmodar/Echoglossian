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
            Func<Task<IPluginWindowPreviewBackend>> createHostedBackend,
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
            if (requestedMode == PluginWindowPreviewBackendMode.Auto)
            {
                var backend = new AutoFallbackBackend(hostedBackend, createStandaloneBackend);
                return (backend, backend.Status);
            }

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

    private sealed class AutoFallbackBackend : IPluginWindowPreviewBackend
    {
        private readonly Func<IPluginWindowPreviewBackend> createStandaloneBackend;
        private IPluginWindowPreviewBackend activeBackend;
        private bool hostedActive = true;
        private PluginWindowBackendStatus status = new(
            PluginWindowPreviewBackendMode.Auto,
            PluginWindowPreviewBackendMode.DalaMockHosted,
            HostedRequested: true,
            HostedAvailable: true,
            FallbackReason: null);

        internal AutoFallbackBackend(
            IPluginWindowPreviewBackend hostedBackend,
            Func<IPluginWindowPreviewBackend> createStandaloneBackend)
        {
            this.activeBackend = hostedBackend ?? throw new ArgumentNullException(nameof(hostedBackend));
            this.createStandaloneBackend = createStandaloneBackend ??
                throw new ArgumentNullException(nameof(createStandaloneBackend));
        }

        public PluginWindowBackendStatus Status => this.status;

        public bool DbManagerAvailable => this.Execute(backend => backend.DbManagerAvailable);

        public bool CaptureFailed => this.Execute(backend => backend.CaptureFailed);

        public void Draw(UI.PreviewWorkbenchState state) => this.Execute(backend => backend.Draw(state));

        public void BeginCapture(UI.PreviewCaptureTarget target) => this.Execute(backend => backend.BeginCapture(target));

        public void EndCapture() => this.Execute(backend => backend.EndCapture());

        public System.Drawing.Rectangle? TryGetStableCrop(UI.PreviewCaptureTarget target) =>
            this.Execute(backend => backend.TryGetStableCrop(target));

        public void Dispose() => this.activeBackend.Dispose();

        private T Execute<T>(Func<IPluginWindowPreviewBackend, T> operation)
        {
            try
            {
                return operation(this.activeBackend);
            }
            catch (Exception exception) when (this.hostedActive)
            {
                this.FallBackToStandalone(exception);
                return operation(this.activeBackend);
            }
        }

        private void Execute(Action<IPluginWindowPreviewBackend> operation)
        {
            try
            {
                operation(this.activeBackend);
            }
            catch (Exception exception) when (this.hostedActive)
            {
                this.FallBackToStandalone(exception);
                operation(this.activeBackend);
            }
        }

        private void FallBackToStandalone(Exception exception)
        {
            var hostedBackend = this.activeBackend;
            var standaloneBackend = this.createStandaloneBackend();
            this.activeBackend = standaloneBackend;
            this.hostedActive = false;
            this.status = new PluginWindowBackendStatus(
                PluginWindowPreviewBackendMode.Auto,
                PluginWindowPreviewBackendMode.Standalone,
                HostedRequested: true,
                HostedAvailable: false,
                FallbackReason: CreateFallbackReason(exception));

            try
            {
                hostedBackend.Dispose();
            }
            catch (Exception disposeException)
            {
                this.status = this.status with
                {
                    FallbackReason = $"{this.status.FallbackReason} Hosted cleanup failed: {disposeException.Message}",
                };
            }
        }
    }
}
