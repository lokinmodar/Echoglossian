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
    /// Ensures expected image-save failures remove partial output and report the failure.
    /// </summary>
    [Fact]
    public void HandleInteractiveScreenshotFailure_IOException_DeletesPartialOutputAndReportsFailure()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var outputPath = Path.Combine(tempDirectory.FullName, "partial.png");
            File.WriteAllText(outputPath, "partial");
            string? reportedMessage = null;

            Program.HandleInteractiveScreenshotFailure(
                PreviewCaptureTarget.ConfigWindow,
                outputPath,
                new IOException("save failed"),
                message => reportedMessage = message);

            Assert.False(File.Exists(outputPath));
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
    /// Ensures graphics readback failures are handled as expected capture failures.
    /// </summary>
    [Fact]
    public void IsExpectedInteractiveScreenshotFailure_VeldridException_ReturnsTrue()
    {
        Assert.True(Program.IsExpectedInteractiveScreenshotFailure(
            new Veldrid.VeldridException("readback failed")));
    }
}
