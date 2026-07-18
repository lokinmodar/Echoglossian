// <copyright file="PreviewCommandLineBackendModeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Hosting;
using Echoglossian.Previewer.PluginWindows;

using Xunit;

namespace Echoglossian.Previewer.Tests.PluginWindows;

/// <summary>
///     Covers plugin-window backend command-line parsing.
/// </summary>
public sealed class PreviewCommandLineBackendModeTests
{
    /// <summary>
    ///     Ensures known plugin-window backend values map to their matching modes.
    /// </summary>
    /// <param name="rawValue">The command-line backend value.</param>
    /// <param name="expectedMode">The expected parsed backend mode.</param>
    [Theory]
    [InlineData("auto", PluginWindowPreviewBackendMode.Auto)]
    [InlineData("standalone", PluginWindowPreviewBackendMode.Standalone)]
    [InlineData("dalamock", PluginWindowPreviewBackendMode.DalaMockHosted)]
    public void Parse_plugin_window_backend_mode_maps_known_values(
        string rawValue,
        object expectedMode)
    {
        var commandLine = PreviewCommandLine.Parse(
            ["--plugin-window-backend", rawValue]);

        Assert.Equal(
            (PluginWindowPreviewBackendMode)expectedMode,
            commandLine.PluginWindowBackendMode);
    }

    /// <summary>
    ///     Ensures unknown plugin-window backend values are rejected.
    /// </summary>
    [Fact]
    public void Parse_plugin_window_backend_mode_rejects_unknown_values()
    {
        Action act = () => PreviewCommandLine.Parse(
            ["--plugin-window-backend", "bogus"]);

        var exception = Assert.Throws<ArgumentException>(act);

        Assert.Contains(
            "plugin window backend",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }
}
