// <copyright file="PreviewHostContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

namespace Echoglossian.Previewer.Tests.Hosting;

/// <summary>
/// Guards the preview host frame-loop synchronization contract.
/// </summary>
public sealed class PreviewHostContractTests
{
    /// <summary>
    /// Ensures the interactive frame loop does not force a GPU idle wait on
    /// every presented frame.
    /// </summary>
    [Fact]
    public void RunFrame_InteractiveLoopDoesNotForceGpuIdle()
    {
        var source = File.ReadAllText(Path.Combine(
            this.RepositoryRoot,
            "Echoglossian.Previewer",
            "Hosting",
            "PreviewHost.cs"));
        var runFrameBody = ExtractMethodBody(
            source,
            "internal void RunFrame(Action draw, Action? beforePresent)");

        Assert.DoesNotContain("WaitForIdle();", runFrameBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures screenshot readback paths still synchronize explicitly before
    /// mapping GPU resources.
    /// </summary>
    [Fact]
    public void CapturePaths_KeepExplicitReadbackSynchronization()
    {
        var hostSource = File.ReadAllText(Path.Combine(
            this.RepositoryRoot,
            "Echoglossian.Previewer",
            "Hosting",
            "PreviewHost.cs"));
        var captureFrameBody = ExtractMethodBody(
            hostSource,
            "internal void CaptureFramePng(");
        var screenshotSource = File.ReadAllText(Path.Combine(
            this.RepositoryRoot,
            "Echoglossian.Previewer",
            "Screenshots",
            "VeldridScreenshotCapture.cs"));

        Assert.Contains("WaitForIdle();", captureFrameBody, StringComparison.Ordinal);
        Assert.Contains(
            "graphicsDevice.WaitForIdle();",
            screenshotSource,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets the repository root discovered from the test output directory.
    /// </summary>
    private string RepositoryRoot => FindRepositoryRoot();

    /// <summary>
    /// Extracts the body for one source method.
    /// </summary>
    /// <param name="source">The full source file.</param>
    /// <param name="signature">The method signature text.</param>
    /// <returns>The method body text including braces.</returns>
    private static string ExtractMethodBody(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Could not find method signature: {signature}");

        var bodyStart = source.IndexOf('{', signatureIndex);
        Assert.True(bodyStart >= 0, $"Could not find body start for: {signature}");

        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(bodyStart, (index - bodyStart) + 1);
                }
            }
        }

        throw new InvalidOperationException($"Could not find body end for: {signature}");
    }

    /// <summary>
    /// Finds the repository root by walking upward from the test output
    /// directory until the solution file is found.
    /// </summary>
    /// <returns>The absolute repository-root path.</returns>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Echoglossian.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the Echoglossian repository root.");
    }
}
