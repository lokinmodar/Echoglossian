// <copyright file="PluginWindowPreviewBackendFactoryTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.PluginWindows;

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
}
