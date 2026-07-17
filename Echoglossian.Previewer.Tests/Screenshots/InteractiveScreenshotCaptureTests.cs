// <copyright file="InteractiveScreenshotCaptureTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer;
using Echoglossian.Previewer.UI;

using Xunit;

namespace Echoglossian.Previewer.Tests.Screenshots;

/// <summary>
/// Covers interactive screenshot capture failure handling.
/// </summary>
public sealed class InteractiveScreenshotCaptureTests
{
    /// <summary>
    /// Ensures an expected pre-write capture failure preserves an earlier
    /// published screenshot at the requested output path.
    /// </summary>
    [Fact]
    public void CaptureInteractiveScreenshot_PreWriteFailure_PreservesExistingOutput()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var outputPath = Path.Combine(tempDirectory.FullName, "capture.png");
            File.WriteAllText(outputPath, "previous-success");

            Assert.Throws<IOException>(() => Program.CaptureInteractiveScreenshot(
                outputPath,
                _ => throw new IOException("readback failed")));

            Assert.Equal("previous-success", File.ReadAllText(outputPath));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Ensures expected image-save failures report the failure without deleting
    /// an earlier published screenshot.
    /// </summary>
    [Fact]
    public void HandleInteractiveScreenshotFailure_IOException_PreservesPublishedOutputAndReportsFailure()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var outputPath = Path.Combine(tempDirectory.FullName, "published.png");
            File.WriteAllText(outputPath, "previous-success");
            string? reportedMessage = null;

            Program.HandleInteractiveScreenshotFailure(
                PreviewCaptureTarget.ConfigWindow,
                outputPath,
                new IOException("save failed"),
                message => reportedMessage = message);

            Assert.Equal("previous-success", File.ReadAllText(outputPath));
            Assert.NotNull(reportedMessage);
            Assert.Contains("ConfigWindow", reportedMessage, StringComparison.Ordinal);
            Assert.Contains(outputPath, reportedMessage, StringComparison.Ordinal);
            Assert.Contains("save failed", reportedMessage, StringComparison.Ordinal);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Ensures the shared failure helper removes partial output and returns
    /// target and path context for both interactive and batch capture paths.
    /// </summary>
    [Fact]
    public void HandleScreenshotFailure_IOException_DeletesPartialOutputAndReturnsContext()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var outputPath = Path.Combine(tempDirectory.FullName, "partial.png");
            File.WriteAllText(outputPath, "partial");

            var message = Program.HandleScreenshotFailure(
                PreviewCaptureTarget.ConfigWindow,
                outputPath,
                new IOException("save failed"));

            Assert.False(File.Exists(outputPath));
            Assert.Contains("ConfigWindow", message, StringComparison.Ordinal);
            Assert.Contains(outputPath, message, StringComparison.Ordinal);
            Assert.Contains("save failed", message, StringComparison.Ordinal);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Ensures graphics readback failures are handled as expected capture failures.
    /// </summary>
    [Fact]
    public void IsExpectedInteractiveScreenshotFailure_VeldridException_ReturnsTrue()
    {
        Assert.True(Program.IsExpectedInteractiveScreenshotFailure(
            new Veldrid.VeldridException("readback failed")));
    }
}
