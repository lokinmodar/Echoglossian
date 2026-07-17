// <copyright file="PreviewCaptureRequestTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Hosting;
using Echoglossian.Previewer.Scenarios;
using Echoglossian.Previewer.Screenshots;
using Echoglossian.Previewer.UI;

using Xunit;

namespace Echoglossian.Previewer.Tests.Screenshots;

/// <summary>
/// Covers explicit preview screenshot capture targets.
/// </summary>
public sealed class PreviewCaptureRequestTests
{
    /// <summary>
    /// Ensures screenshot requests capture the complete frame by default.
    /// </summary>
    [Fact]
    public void ScreenshotRequest_DefaultsToFullFrameTarget()
    {
        var request = new ScreenshotRequest(
            ScreenshotMode.Full,
            PreviewScenarioCatalog.Defaults[0],
            PreviewScenarioCatalog.ViewportPresets[1],
            "artifacts");

        Assert.Equal(PreviewCaptureTarget.FullFrame, request.CaptureTarget);
    }

    /// <summary>
    /// Ensures the CLI parses a deterministic plugin-window export target.
    /// </summary>
    [Fact]
    public void Parse_ConfigWindowCaptureTarget_ReturnsTypedTarget()
    {
        var commandLine = PreviewCommandLine.Parse(
            ["--screenshot", "full", "--capture-target", "config-window"]);

        Assert.Equal(PreviewCaptureTarget.ConfigWindow, commandLine.CaptureTarget);
    }

    /// <summary>
    /// Ensures plugin-window targets cannot silently alter surface or batch semantics.
    /// </summary>
    [Theory]
    [InlineData("surface")]
    [InlineData("batch")]
    public void Parse_PluginWindowTargetWithoutFullMode_ThrowsArgumentException(
        string screenshotMode)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => PreviewCommandLine.Parse(
                ["--screenshot", screenshotMode, "--capture-target", "config-window"]));

        Assert.Contains("full", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
