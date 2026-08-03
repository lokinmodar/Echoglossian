// <copyright file="NamePlateOverlayGeometryTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.NamePlates;

using System.Numerics;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers nameplate overlay geometry that can be validated without a live
///     Dalamud runtime.
/// </summary>
public sealed class NamePlateOverlayGeometryTests
{
    /// <summary>
    ///     Ensures the projected nameplate point is treated as the overlay
    ///     center, not as its top-left corner.
    /// </summary>
    [Fact]
    public void ResolveCenteredNamePlateOverlayBounds_CentersProjectedPoint()
    {
        var projectedPoint = new Vector2(640f, 360f);

        var (position, size) =
            NamePlateTranslationRuntime.ResolveCenteredNamePlateOverlayBounds(
                projectedPoint,
                new Vector2(1920f, 1080f));

        Assert.Equal(307.2f, size.X, precision: 3);
        Assert.Equal(24f, size.Y);
        Assert.Equal(projectedPoint.X, position.X + (size.X * 0.5f), precision: 3);
        Assert.Equal(projectedPoint.Y, position.Y + (size.Y * 0.5f), precision: 3);
    }

    /// <summary>
    ///     Ensures the world anchor remains close to the object text target
    ///     instead of applying a large radius multiplier.
    /// </summary>
    /// <param name="hitboxRadius">The object's hitbox radius.</param>
    /// <param name="expectedOffset">The expected vertical anchor offset.</param>
    [Theory]
    [InlineData(0f, 0.75f)]
    [InlineData(1f, 1.25f)]
    [InlineData(5f, 5.25f)]
    public void ResolveNamePlateWorldAnchor_UsesCloseStableVerticalOffset(
        float hitboxRadius,
        float expectedOffset)
    {
        var objectPosition = new Vector3(10f, 20f, 30f);

        var actual = NamePlateTranslationRuntime.ResolveNamePlateWorldAnchor(
            objectPosition,
            hitboxRadius);

        Assert.Equal(objectPosition.X, actual.X);
        Assert.Equal(objectPosition.Y + expectedOffset, actual.Y, precision: 3);
        Assert.Equal(objectPosition.Z, actual.Z);
    }
}
