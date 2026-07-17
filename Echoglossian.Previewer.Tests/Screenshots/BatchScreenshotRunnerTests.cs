// <copyright file="BatchScreenshotRunnerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Configuration;
using Echoglossian.Previewer.Scenarios;
using Echoglossian.Previewer.Screenshots;
using Echoglossian.Previewer.UI;
using Echoglossian.UIOverlays.TranslationOverlay;

using System.Text.Json;

using Xunit;

namespace Echoglossian.Previewer.Tests.Screenshots;

/// <summary>
/// Covers screenshot manifest metadata redaction.
/// </summary>
public sealed class BatchScreenshotRunnerTests
{
    /// <summary>
    /// Ensures loaded preview configs are recorded by file name only.
    /// </summary>
    [Fact]
    public void GetManifestConfigSourceLabel_LoadedConfig_UsesFileNameOnly()
    {
        var configuration = new PreviewConfiguration(
            new Config(),
            @"C:\Users\lokin\AppData\Roaming\XIVLauncher\pluginConfigs\Echoglossian.json",
            loaded: true,
            []);

        var label = BatchScreenshotRunner.GetManifestConfigSourceLabel(configuration);

        Assert.Equal("Echoglossian.json", label);
        Assert.DoesNotContain("lokin", label, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"\", label, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures default-only preview sessions do not leak missing source paths.
    /// </summary>
    [Fact]
    public void GetManifestConfigSourceLabel_UnloadedConfig_UsesDefaultsLabel()
    {
        var configuration = new PreviewConfiguration(
            new Config(),
            @"C:\Users\lokin\AppData\Roaming\XIVLauncher\pluginConfigs\Missing.json",
            loaded: false,
            []);

        var label = BatchScreenshotRunner.GetManifestConfigSourceLabel(configuration);

        Assert.Equal("defaults", label);
    }

    /// <summary>
    /// Ensures screenshot batches fail fast when requests target mixed output directories.
    /// </summary>
    [Fact]
    public void ResolveSharedOutputDirectory_MixedDirectories_ThrowsArgumentException()
    {
        var requests = new[]
        {
            CreateRequest(@"C:\captures\a"),
            CreateRequest(@"C:\captures\b"),
        };

        var exception = Assert.Throws<ArgumentException>(
            () => BatchScreenshotRunner.ResolveSharedOutputDirectory(requests));

        Assert.Contains("same output directory", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ensures screenshot manifest entries do not expose absolute png paths.
    /// </summary>
    [Fact]
    public void GetManifestPngPathLabel_AbsolutePath_UsesFileNameOnly()
    {
        var label = BatchScreenshotRunner.GetManifestPngPathLabel(
            @"C:\Users\lokin\Desktop\captures\full-talk-1920x1080.png");

        Assert.Equal("full-talk-1920x1080.png", label);
        Assert.DoesNotContain("lokin", label, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"\", label, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the manifest record name reflects that config source values are
    /// redacted labels rather than absolute paths.
    /// </summary>
    [Fact]
    public void ScreenshotManifest_RecordUsesConfigSourceLabelName()
    {
        var source = File.ReadAllText(Path.Combine(
            this.RepositoryRoot,
            "Echoglossian.Previewer",
            "Screenshots",
            "BatchScreenshotRunner.cs"));

        Assert.Contains("string ConfigSourceLabel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("string ConfigSourcePath", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the real manifest serializer writes plugin-window targets as stable JSON strings.
    /// </summary>
    [Fact]
    public void SerializeManifest_PluginWindowTarget_WritesCaptureTargetString()
    {
        var manifest = new BatchScreenshotRunner.ScreenshotManifest(
            "defaults",
            [],
            20,
            [
                new BatchScreenshotRunner.ScreenshotManifestEntry(
                    "talk",
                    "Talk",
                    1920,
                    1080,
                    "Full",
                    PreviewCaptureTarget.DbManagerWindow,
                    "PlainImGui",
                    "full-talk-dbmanagerwindow-1920x1080.png"),
            ]);

        var json = BatchScreenshotRunner.SerializeManifest(manifest);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(
            "DbManagerWindow",
            document.RootElement
                .GetProperty("Entries")[0]
                .GetProperty("CaptureTarget")
                .GetString());
    }

    /// <summary>
    /// Ensures plugin-window captures rely on stable window bounds rather than overlay draw success.
    /// </summary>
    [Theory]
    [InlineData((int)PreviewCaptureTarget.ConfigWindow)]
    [InlineData((int)PreviewCaptureTarget.DbManagerWindow)]
    [InlineData((int)PreviewCaptureTarget.TranslatorMetricsWindow)]
    public void IsCaptureReady_PluginWindowWithStableBounds_DoesNotRequireOverlayDraw(
        int targetValue)
    {
        var ready = BatchScreenshotRunner.IsCaptureReady(
            (PreviewCaptureTarget)targetValue,
            overlayWasDrawn: false,
            hasStableWindowBounds: true);

        Assert.True(ready);
    }

    /// <summary>
    /// Ensures overlay captures continue to require an overlay draw.
    /// </summary>
    [Fact]
    public void IsCaptureReady_OverlaySurfaceWithoutDraw_IsNotReady()
    {
        var ready = BatchScreenshotRunner.IsCaptureReady(
            PreviewCaptureTarget.OverlaySurface,
            overlayWasDrawn: false,
            hasStableWindowBounds: true);

        Assert.False(ready);
    }

    /// <summary>
    /// Gets the repository root discovered from the test output directory.
    /// </summary>
    private string RepositoryRoot => FindRepositoryRoot();

    private static ScreenshotRequest CreateRequest(string outputDirectory)
    {
        return new ScreenshotRequest(
            ScreenshotMode.Full,
            new PreviewScenario(
                "talk",
                "Talk",
                TranslationOverlaySurfaceId.Talk,
                new PreviewAddonBounds(0f, 0f, 400f, 180f),
                "Translated text",
                "Speaker",
                Visible: true,
                ShowsSimulatedAddonBounds: false),
            new PreviewViewportPreset("1080p", 1920, 1080),
            outputDirectory);
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
