// <copyright file="BatchScreenshotRunnerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Configuration;
using Echoglossian.Previewer.Scenarios;
using Echoglossian.Previewer.Screenshots;
using Echoglossian.UIOverlays.TranslationOverlay;

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
}
