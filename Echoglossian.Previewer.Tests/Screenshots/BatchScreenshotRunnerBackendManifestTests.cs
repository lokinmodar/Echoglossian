// <copyright file="BatchScreenshotRunnerBackendManifestTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.PluginWindows;
using Echoglossian.Previewer.Screenshots;
using Echoglossian.Previewer.UI;

using Xunit;

namespace Echoglossian.Previewer.Tests.Screenshots;

/// <summary>
///     Covers plugin-window backend details in screenshot manifests.
/// </summary>
public sealed class BatchScreenshotRunnerBackendManifestTests
{
    /// <summary>
    ///     Ensures plugin-window captures record both the requested and effective backend.
    /// </summary>
    [Fact]
    public void SerializeManifest_plugin_window_capture_records_requested_and_effective_backend()
    {
        var manifest = BatchScreenshotRunner.CreateManifestForTests(
            captureTarget: PreviewCaptureTarget.ConfigWindow,
            requestedBackend: PluginWindowPreviewBackendMode.Auto,
            effectiveBackend: PluginWindowPreviewBackendMode.Standalone,
            fallbackReason: "DalaMock initialization failed");

        Assert.Equal("Auto", manifest.RequestedPluginWindowBackend);
        Assert.Equal("Standalone", manifest.EffectivePluginWindowBackend);
        Assert.Equal("DalaMock initialization failed", manifest.PluginWindowBackendFallbackReason);
    }
}
