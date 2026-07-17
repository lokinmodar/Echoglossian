// <copyright file="ScreenshotFileNameTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Scenarios;
using Echoglossian.Previewer.Screenshots;
using Echoglossian.Previewer.UI;

using Xunit;

namespace Echoglossian.Previewer.Tests.Screenshots;

/// <summary>
/// Covers deterministic screenshot file naming.
/// </summary>
public sealed class ScreenshotFileNameTests
{
    /// <summary>
    /// Ensures screenshot names are stable for a scenario, viewport, and mode.
    /// </summary>
    [Fact]
    public void CreatePngName_StableInputs_ReturnsDeterministicName()
    {
        var viewport = new PreviewViewportPreset("1920x1080", 1920, 1080);

        var name = ScreenshotFileName.CreatePngName(
            ScreenshotMode.Surface,
            "talk",
            viewport);

        Assert.Equal("surface-talk-1920x1080.png", name);
    }

    /// <summary>
    /// Ensures file names avoid characters rejected by Windows paths.
    /// </summary>
    [Fact]
    public void CreatePngName_UnsafeScenarioKey_ReplacesWindowsUnsafeCharacters()
    {
        var viewport = new PreviewViewportPreset("custom/size", 1280, 720);

        var name = ScreenshotFileName.CreatePngName(
            ScreenshotMode.Full,
            "quest:toast?wide*rtl",
            viewport);

        Assert.Equal("full-quest-toast-wide-rtl-1280x720.png", name);
    }

    /// <summary>
    /// Ensures distinct plugin-window targets produce distinct deterministic names.
    /// </summary>
    [Fact]
    public void CreatePngName_WindowTargets_IncludeCaptureTarget()
    {
        var viewport = new PreviewViewportPreset("1920x1080", 1920, 1080);

        var configName = ScreenshotFileName.CreatePngName(
            ScreenshotMode.Full,
            "talk",
            viewport,
            PreviewCaptureTarget.ConfigWindow);
        var dbManagerName = ScreenshotFileName.CreatePngName(
            ScreenshotMode.Full,
            "talk",
            viewport,
            PreviewCaptureTarget.DbManagerWindow);

        Assert.Equal("full-talk-configwindow-1920x1080.png", configName);
        Assert.Equal("full-talk-dbmanagerwindow-1920x1080.png", dbManagerName);
        Assert.NotEqual(configName, dbManagerName);
    }

    /// <summary>
    /// Ensures the default output root is timestamped below previewer artifacts.
    /// </summary>
    [Fact]
    public void CreateDefaultOutputDirectory_UsesTimestampedPreviewerArtifactRoot()
    {
        var timestamp = new DateTimeOffset(2026, 7, 15, 21, 8, 9, TimeSpan.Zero);

        var directory = ScreenshotFileName.CreateDefaultOutputDirectory(timestamp);

        Assert.Equal(
            Path.Combine(
                "artifacts",
                "previewer",
                "screenshots",
                "20260715-210809"),
            directory);
    }
}
