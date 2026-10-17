// <copyright file="TranslationToggleHudInteractionTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Tests;

using System.Numerics;

using global::Echoglossian.PluginUI;

using Xunit;

/// <summary>
/// Covers translation HUD drag, click, and viewport-clamp behavior.
/// </summary>
public sealed class TranslationToggleHudInteractionTests
{
    /// <summary>
    /// Verifies pointer movement below the threshold remains a click.
    /// </summary>
    [Fact]
    public void Update_BelowDragThreshold_ReturnsClick()
    {
        var interaction = new TranslationToggleHudInteraction();
        interaction.Begin(new Vector2(10f, 10f), new Vector2(100f, 100f));

        var result = interaction.Update(
            new Vector2(13f, 12f),
            released: true,
            dragThreshold: 5f);

        Assert.Equal(TranslationToggleHudInteractionAction.Click, result.Action);
        Assert.Equal(new Vector2(100f, 100f), result.Position);
    }

    /// <summary>
    /// Verifies pointer movement at the threshold suppresses click activation.
    /// </summary>
    [Fact]
    public void Update_AtDragThreshold_ReturnsDragCompleted()
    {
        var interaction = new TranslationToggleHudInteraction();
        interaction.Begin(new Vector2(10f, 10f), new Vector2(100f, 100f));

        var result = interaction.Update(
            new Vector2(15f, 10f),
            released: true,
            dragThreshold: 5f);

        Assert.Equal(
            TranslationToggleHudInteractionAction.DragCompleted,
            result.Action);
        Assert.Equal(new Vector2(105f, 100f), result.Position);
    }

    /// <summary>
    /// Verifies positions are clamped inside a non-zero viewport origin.
    /// </summary>
    [Theory]
    [InlineData(-100f, -100f, 20f, 30f)]
    [InlineData(2000f, 1200f, 778f, 588f)]
    public void ClampToViewport_OutsideWorkArea_ClampsInside(
        float x,
        float y,
        float expectedX,
        float expectedY)
    {
        var result = TranslationToggleHudInteraction.ClampToViewport(
            new Vector2(x, y),
            new Vector2(42f, 42f),
            new Vector2(20f, 30f),
            new Vector2(800f, 600f));

        Assert.Equal(new Vector2(expectedX, expectedY), result);
    }
}
