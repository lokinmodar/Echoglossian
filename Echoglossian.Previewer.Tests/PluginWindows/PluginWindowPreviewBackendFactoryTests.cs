// <copyright file="PluginWindowPreviewBackendFactoryTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.PluginWindows;
using Echoglossian.Previewer.UI;

using System.Drawing;

using Xunit;

namespace Echoglossian.Previewer.Tests.PluginWindows;

/// <summary>
///     Covers selection and fallback behavior for plugin-window preview backends.
/// </summary>
public sealed class PluginWindowPreviewBackendFactoryTests
{
    /// <summary>
    ///     Ensures automatic backend selection remains usable when hosted boot fails.
    /// </summary>
    /// <returns>A task that completes after backend selection.</returns>
    [Fact]
    public async Task CreateAsync_auto_falls_back_to_standalone_when_hosted_boot_fails()
    {
        var result = await PluginWindowPreviewBackendFactory.CreateForTestsAsync(
            PluginWindowPreviewBackendMode.Auto,
            static () => throw new InvalidOperationException("synthetic hosted failure"));

        Assert.Equal(PluginWindowPreviewBackendMode.Standalone, result.Status.EffectiveMode);
        Assert.Contains("synthetic hosted failure", result.Status.FallbackReason);
        Assert.Equal(result.Status, result.Backend.Status);
    }

    /// <summary>
    ///     Ensures explicitly requested hosted mode surfaces boot failures.
    /// </summary>
    /// <returns>A task that completes after backend selection.</returns>
    [Fact]
    public async Task CreateAsync_dalamock_does_not_silently_fallback_when_hosted_boot_fails()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => PluginWindowPreviewBackendFactory.CreateForTestsAsync(
                PluginWindowPreviewBackendMode.DalaMockHosted,
                static () => throw new InvalidOperationException("synthetic hosted failure")));

        Assert.Contains("synthetic hosted failure", exception.Message);
    }

    /// <summary>
    ///     Ensures the hosted backend resolves the production plugin members that own all preview windows.
    /// </summary>
    [Fact]
    public void Hosted_backend_resolves_production_plugin_window_members_once()
    {
        DalaMockHostedPluginWindowPreviewBackend.ValidateHostedPluginWindowBridgeForTests();
    }

    /// <summary>
    ///     Ensures a successfully started automatic backend remains visibly hosted.
    /// </summary>
    /// <returns>A task that completes after backend selection.</returns>
    [Fact]
    public async Task CreateAsync_auto_reports_hosted_effective_mode_after_successful_startup()
    {
        var hosted = new TestBackend(PluginWindowPreviewBackendMode.DalaMockHosted);
        var result = await PluginWindowPreviewBackendFactory.CreateAsync(
            PluginWindowPreviewBackendMode.Auto,
            () => Task.FromResult<IPluginWindowPreviewBackend>(hosted),
            static () => new TestBackend(PluginWindowPreviewBackendMode.Standalone));
        using var backend = result.Backend;

        Assert.Equal(PluginWindowPreviewBackendMode.Auto, result.Backend.Status.RequestedMode);
        Assert.Equal(PluginWindowPreviewBackendMode.DalaMockHosted, result.Backend.Status.EffectiveMode);
    }

    /// <summary>
    ///     Ensures a hosted draw failure retries the operation against standalone.
    /// </summary>
    /// <returns>A task that completes after backend selection.</returns>
    [Fact]
    public async Task CreateAsync_auto_falls_back_when_hosted_draw_fails()
    {
        var hosted = new TestBackend(PluginWindowPreviewBackendMode.DalaMockHosted)
        {
            ThrowOnDraw = true,
        };
        var standalone = new TestBackend(PluginWindowPreviewBackendMode.Standalone);
        var result = await PluginWindowPreviewBackendFactory.CreateAsync(
            PluginWindowPreviewBackendMode.Auto,
            () => Task.FromResult<IPluginWindowPreviewBackend>(hosted),
            () => standalone);
        using var backend = result.Backend;

        backend.Draw(null!);

        Assert.Equal(1, hosted.DrawCount);
        Assert.Equal(1, standalone.DrawCount);
        Assert.Equal(PluginWindowPreviewBackendMode.Standalone, backend.Status.EffectiveMode);
        Assert.Contains("synthetic draw failure", backend.Status.FallbackReason);
    }

    /// <summary>
    ///     Ensures an explicit hosted selection does not hide a runtime hosted-backend failure.
    /// </summary>
    /// <returns>A task that completes after backend selection.</returns>
    [Fact]
    public async Task CreateAsync_dalamock_surfaces_hosted_draw_failures()
    {
        var hosted = new TestBackend(PluginWindowPreviewBackendMode.DalaMockHosted)
        {
            ThrowOnDraw = true,
        };
        var result = await PluginWindowPreviewBackendFactory.CreateAsync(
            PluginWindowPreviewBackendMode.DalaMockHosted,
            () => Task.FromResult<IPluginWindowPreviewBackend>(hosted),
            static () => new TestBackend(PluginWindowPreviewBackendMode.Standalone));
        using var backend = result.Backend;

        var exception = Assert.Throws<InvalidOperationException>(() => backend.Draw(null!));

        Assert.Contains("synthetic draw failure", exception.Message);
    }

    /// <summary>
    ///     Ensures capture startup retries against standalone when hosted capture fails at runtime.
    /// </summary>
    /// <returns>A task that completes after backend selection.</returns>
    [Fact]
    public async Task CreateAsync_auto_falls_back_when_hosted_capture_begins_failing()
    {
        var hosted = new TestBackend(PluginWindowPreviewBackendMode.DalaMockHosted)
        {
            ThrowOnBeginCapture = true,
        };
        var standalone = new TestBackend(PluginWindowPreviewBackendMode.Standalone);
        var result = await PluginWindowPreviewBackendFactory.CreateAsync(
            PluginWindowPreviewBackendMode.Auto,
            () => Task.FromResult<IPluginWindowPreviewBackend>(hosted),
            () => standalone);
        using var backend = result.Backend;

        backend.BeginCapture(PreviewCaptureTarget.ConfigWindow);

        Assert.Equal(1, hosted.BeginCaptureCount);
        Assert.Equal(1, standalone.BeginCaptureCount);
        Assert.Equal(PluginWindowPreviewBackendMode.Standalone, backend.Status.EffectiveMode);
        Assert.Contains("synthetic capture failure", backend.Status.FallbackReason);
    }

    /// <summary>
    ///     Ensures capture availability checks trigger the same visible automatic fallback.
    /// </summary>
    /// <returns>A task that completes after backend selection.</returns>
    [Fact]
    public async Task CreateAsync_auto_falls_back_when_hosted_availability_check_fails()
    {
        var hosted = new TestBackend(PluginWindowPreviewBackendMode.DalaMockHosted)
        {
            ThrowOnDbManagerAvailable = true,
        };
        var standalone = new TestBackend(PluginWindowPreviewBackendMode.Standalone);
        var result = await PluginWindowPreviewBackendFactory.CreateAsync(
            PluginWindowPreviewBackendMode.Auto,
            () => Task.FromResult<IPluginWindowPreviewBackend>(hosted),
            () => standalone);
        using var backend = result.Backend;

        Assert.True(backend.DbManagerAvailable);

        Assert.Equal(1, hosted.DbManagerAvailableCount);
        Assert.Equal(1, standalone.DbManagerAvailableCount);
        Assert.Equal(PluginWindowPreviewBackendMode.Standalone, backend.Status.EffectiveMode);
        Assert.Contains("synthetic availability failure", backend.Status.FallbackReason);
    }

    private sealed class TestBackend : IPluginWindowPreviewBackend
    {
        internal TestBackend(PluginWindowPreviewBackendMode mode)
        {
            this.Status = new PluginWindowBackendStatus(mode, mode, mode == PluginWindowPreviewBackendMode.DalaMockHosted, true, null);
        }

        public PluginWindowBackendStatus Status { get; }

        public bool ThrowOnDraw { get; init; }

        public bool ThrowOnBeginCapture { get; init; }

        public bool ThrowOnDbManagerAvailable { get; init; }

        internal int DrawCount { get; private set; }

        internal int BeginCaptureCount { get; private set; }

        internal int DbManagerAvailableCount { get; private set; }

        public bool DbManagerAvailable
        {
            get
            {
                this.DbManagerAvailableCount++;
                if (this.ThrowOnDbManagerAvailable)
                {
                    throw new InvalidOperationException("synthetic availability failure");
                }

                return true;
            }
        }

        public bool CaptureFailed => false;

        public void Draw(PreviewWorkbenchState state)
        {
            this.DrawCount++;
            if (this.ThrowOnDraw)
            {
                throw new InvalidOperationException("synthetic draw failure");
            }
        }

        public void BeginCapture(PreviewCaptureTarget target)
        {
            this.BeginCaptureCount++;
            if (this.ThrowOnBeginCapture)
            {
                throw new InvalidOperationException("synthetic capture failure");
            }
        }

        public void EndCapture()
        {
        }

        public Rectangle? TryGetStableCrop(PreviewCaptureTarget target) => null;

        public void Dispose()
        {
        }
    }
}
