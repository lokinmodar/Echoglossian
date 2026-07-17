// <copyright file="PreviewShellBackendStatusTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.PluginWindows;

using Xunit;

namespace Echoglossian.Previewer.Tests.PluginWindows;

/// <summary>
///     Covers plugin-window backend status state.
/// </summary>
public sealed class PreviewShellBackendStatusTests
{
    /// <summary>
    ///     Ensures matching requested and effective modes do not report a fallback.
    /// </summary>
    [Fact]
    public void Backend_status_reports_no_fallback_when_requested_and_effective_match()
    {
        var status = new PluginWindowBackendStatus(
            PluginWindowPreviewBackendMode.Standalone,
            PluginWindowPreviewBackendMode.Standalone,
            HostedRequested: false,
            HostedAvailable: true,
            FallbackReason: null);

        Assert.Null(status.FallbackReason);
        Assert.Equal(PluginWindowPreviewBackendMode.Standalone, status.EffectiveMode);
    }

    /// <summary>
    ///     Ensures auto-mode fallback details remain visible in backend status state.
    /// </summary>
    [Fact]
    public void Backend_status_retains_fallback_reason_when_auto_downgrades()
    {
        var status = new PluginWindowBackendStatus(
            PluginWindowPreviewBackendMode.Auto,
            PluginWindowPreviewBackendMode.Standalone,
            HostedRequested: true,
            HostedAvailable: false,
            FallbackReason: "DalaMock initialization failed");

        Assert.True(status.HostedRequested);
        Assert.False(status.HostedAvailable);
        Assert.Equal("DalaMock initialization failed", status.FallbackReason);
    }
}
