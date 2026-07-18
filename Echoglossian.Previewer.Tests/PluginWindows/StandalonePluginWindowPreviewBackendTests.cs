// <copyright file="StandalonePluginWindowPreviewBackendTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.PluginWindows;

using Xunit;

namespace Echoglossian.Previewer.Tests.PluginWindows;

/// <summary>
///     Covers the standalone plugin-window preview backend.
/// </summary>
public sealed class StandalonePluginWindowPreviewBackendTests
{
    /// <summary>
    ///     Ensures the standalone backend reports its active mode.
    /// </summary>
    [Fact]
    public void Standalone_backend_reports_standalone_status()
    {
        using var backend = StandalonePluginWindowPreviewBackend.CreateForTests(
            dbManagerAvailable: true);

        Assert.Equal(
            PluginWindowPreviewBackendMode.Standalone,
            backend.Status.RequestedMode);
        Assert.Equal(
            PluginWindowPreviewBackendMode.Standalone,
            backend.Status.EffectiveMode);
        Assert.True(backend.Status.HostedAvailable);
    }
}
