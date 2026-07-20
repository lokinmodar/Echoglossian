// <copyright file="OverlayTabTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Tabs;
using System.IO;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers UI identity helpers used by the overlay configuration tab.
/// </summary>
public sealed class OverlayTabTests
{
    /// <summary>
    ///     Ensures placement buckets use distinct ImGui IDs even when their
    ///     nested controls share the same visible labels.
    /// </summary>
    [Fact]
    public void BuildToastPlacementBucketId_ReturnsDistinctIdsForEachPlacement()
    {
        var ids = new string[]
        {
            OverlayTab.BuildToastPlacementBucketId("Top"),
            OverlayTab.BuildToastPlacementBucketId("Bottom"),
            OverlayTab.BuildToastPlacementBucketId("Left"),
            OverlayTab.BuildToastPlacementBucketId("Centre"),
            OverlayTab.BuildToastPlacementBucketId("Right"),
        };

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    ///     Ensures ActionDetail and ItemDetail controls are reachable from the
    ///     visible vertical overlay tab list.
    /// </summary>
    [Fact]
    public void OverlayTab_RendersDetailsAndHoverTab()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "PluginUI",
            "Tabs",
            "OverlayTab.cs"));

        Assert.Contains("Resources.TooltipTabTitle", source);
        Assert.Contains("changed |= TooltipTab.Draw(config);", source);
    }

    /// <summary>
    ///     Finds the repository root from the test output directory.
    /// </summary>
    /// <returns>The repository root directory.</returns>
    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }
}
