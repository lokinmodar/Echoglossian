// <copyright file="BatchScreenshotRunnerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Configuration;
using Echoglossian.Previewer.Screenshots;

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
}
