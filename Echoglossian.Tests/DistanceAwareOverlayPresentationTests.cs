// <copyright file="DistanceAwareOverlayPresentationTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TranslationOverlay;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers distance-aware overlay presentation decisions.
/// </summary>
public sealed class DistanceAwareOverlayPresentationTests
{
    /// <summary>
    /// Ensures nearby overlays remain visible at full size and opacity.
    /// </summary>
    [Fact]
    public void Resolve_within_full_scale_distance_returns_visible_full_presentation()
    {
        var state = DistanceAwareOverlayPresentation.Resolve(
            distanceToCamera: 6f,
            fullScaleDistance: 8f,
            fadeStartDistance: 16f,
            maxDistance: 28f,
            minScale: 0.60f);

        Assert.True(state.IsVisible);
        Assert.Equal(1f, state.Scale);
        Assert.Equal(1f, state.Alpha);
    }

    /// <summary>
    /// Ensures overlays between the full-scale and fade distances scale without fading.
    /// </summary>
    [Fact]
    public void Resolve_between_full_scale_and_fade_start_scales_without_fading()
    {
        var state = DistanceAwareOverlayPresentation.Resolve(
            distanceToCamera: 12f,
            fullScaleDistance: 8f,
            fadeStartDistance: 16f,
            maxDistance: 28f,
            minScale: 0.60f);

        Assert.True(state.IsVisible);
        Assert.Equal(0.92f, state.Scale, 2);
        Assert.Equal(1f, state.Alpha);
    }

    /// <summary>
    /// Ensures distant visible overlays both scale and fade.
    /// </summary>
    [Fact]
    public void Resolve_between_fade_start_and_max_distance_scales_and_fades()
    {
        var state = DistanceAwareOverlayPresentation.Resolve(
            distanceToCamera: 22f,
            fullScaleDistance: 8f,
            fadeStartDistance: 16f,
            maxDistance: 28f,
            minScale: 0.60f);

        Assert.True(state.IsVisible);
        Assert.Equal(0.72f, state.Scale, 2);
        Assert.Equal(0.50f, state.Alpha, 2);
    }

    /// <summary>
    /// Ensures overlays at or beyond the maximum distance are hidden at minimum scale.
    /// </summary>
    [Fact]
    public void Resolve_at_or_beyond_max_distance_hides_overlay()
    {
        var state = DistanceAwareOverlayPresentation.Resolve(
            distanceToCamera: 28f,
            fullScaleDistance: 8f,
            fadeStartDistance: 16f,
            maxDistance: 28f,
            minScale: 0.60f);

        Assert.False(state.IsVisible);
        Assert.Equal(0.60f, state.Scale, 2);
        Assert.Equal(0f, state.Alpha);
    }
}
