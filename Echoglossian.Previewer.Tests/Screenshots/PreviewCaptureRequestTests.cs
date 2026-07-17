// <copyright file="PreviewCaptureRequestTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

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
    /// Ensures window target names are deterministic manifest values.
    /// </summary>
    [Fact]
    public void ManifestEntry_ContainsWindowTargetName()
    {
        var entry = new
        {
            CaptureTarget = PreviewCaptureTarget.DbManagerWindow.ToString(),
        };

        Assert.Equal("DbManagerWindow", entry.CaptureTarget);
    }
}
