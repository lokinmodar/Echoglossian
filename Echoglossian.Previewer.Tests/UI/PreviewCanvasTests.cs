// <copyright file="PreviewCanvasTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.UI;

using System.Numerics;

using Xunit;

namespace Echoglossian.Previewer.Tests.UI;

/// <summary>
/// Covers deterministic preview canvas layout calculations.
/// </summary>
public sealed class PreviewCanvasTests
{
    /// <summary>
    /// Ensures the logical viewport is uniformly scaled and centered in the host space.
    /// </summary>
    [Fact]
    public void CalculateScaledViewport_UniformlyScalesAndCentersLogicalViewport()
    {
        var layout = PreviewCanvas.CalculateScaledViewport(
            availableWidth: 1000f,
            availableHeight: 700f,
            logicalWidth: 1920,
            logicalHeight: 1080);

        Assert.Equal(1000f, layout.Size.X, precision: 3);
        Assert.Equal(562.5f, layout.Size.Y, precision: 3);
        Assert.Equal(0f, layout.Offset.X, precision: 3);
        Assert.Equal(68.75f, layout.Offset.Y, precision: 3);
        Assert.Equal(1000f / 1920f, layout.Scale, precision: 6);
    }

    /// <summary>
    /// Ensures invalid logical viewport dimensions fail fast.
    /// </summary>
    /// <param name="logicalWidth">The invalid logical width.</param>
    /// <param name="logicalHeight">The invalid logical height.</param>
    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, 0)]
    [InlineData(-1, 1080)]
    [InlineData(1920, -1)]
    public void CalculateScaledViewport_InvalidLogicalDimensions_Throws(
        int logicalWidth,
        int logicalHeight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PreviewCanvas.CalculateScaledViewport(
                availableWidth: 1000f,
                availableHeight: 700f,
                logicalWidth,
                logicalHeight));
    }
}
