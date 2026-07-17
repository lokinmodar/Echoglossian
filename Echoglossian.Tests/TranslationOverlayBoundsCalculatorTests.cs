// <copyright file="TranslationOverlayBoundsCalculatorTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TranslationOverlay;

using System.Numerics;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers pure overlay-bounds calculations used before the ImGui layout pass.
/// </summary>
public sealed class TranslationOverlayBoundsCalculatorTests
{
    /// <summary>
    /// Ensures MiniTalk anchors to the visual bubble bounds when they are wider
    /// than the raw text node.
    /// </summary>
    [Fact]
    public void CalculateMiniTalkBounds_UsesVisualBubbleWhenAvailable()
    {
        var result = TranslationOverlayBoundsCalculator.CalculateMiniTalkBounds(
            new Vector2(640f, 480f),
            new Vector2(72f, 28f),
            1f,
            1.05f,
            new Vector2(612f, 468f),
            new Vector2(188f, 70f));

        Assert.Equal(612f, result.Position.X, 3);
        Assert.Equal(468f, result.Position.Y, 3);
        Assert.Equal(188f, result.Dimensions.X, 3);
        Assert.Equal(70f, result.Dimensions.Y, 3);
    }

    /// <summary>
    /// Ensures MiniTalk falls back to padded text bounds when no larger visual
    /// bubble node is available.
    /// </summary>
    [Fact]
    public void CalculateMiniTalkBounds_FallsBackToTextBoundsWhenVisualNodeIsTooSmall()
    {
        var result = TranslationOverlayBoundsCalculator.CalculateMiniTalkBounds(
            new Vector2(640f, 480f),
            new Vector2(100f, 24f),
            1f,
            1.05f,
            new Vector2(642f, 481f),
            new Vector2(80f, 20f));

        Assert.Equal(640f, result.Position.X, 3);
        Assert.Equal(480f, result.Position.Y, 3);
        Assert.Equal(105f, result.Dimensions.X, 3);
        Assert.Equal(25.2f, result.Dimensions.Y, 3);
    }

    /// <summary>
    /// Ensures generic text-bounded overlays preserve the existing padding-based
    /// sizing behavior used by toast surfaces.
    /// </summary>
    [Fact]
    public void CalculateTextBounds_AppliesScaleAndPadding()
    {
        var result = TranslationOverlayBoundsCalculator.CalculateTextBounds(
            new Vector2(300f, 140f),
            new Vector2(200f, 30f),
            1.5f,
            1.05f);

        Assert.Equal(300f, result.Position.X, 3);
        Assert.Equal(140f, result.Position.Y, 3);
        Assert.Equal(315f, result.Dimensions.X, 3);
        Assert.Equal(47.25f, result.Dimensions.Y, 3);
    }
}
