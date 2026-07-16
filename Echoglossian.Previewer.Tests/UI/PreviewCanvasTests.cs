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
}
