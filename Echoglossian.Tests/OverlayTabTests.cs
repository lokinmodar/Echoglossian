// <copyright file="OverlayTabTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Tabs;
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
}
