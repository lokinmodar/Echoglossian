// <copyright file="DistanceAwareOverlayPresentation.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TranslationOverlay;

/// <summary>
/// Represents the resolved visibility, scale, and opacity for a distance-aware overlay.
/// </summary>
internal readonly record struct DistanceAwareOverlayPresentationState(
    bool IsVisible,
    float Scale,
    float Alpha);

/// <summary>
/// Resolves distance-aware overlay presentation values from primitive inputs.
/// </summary>
internal static class DistanceAwareOverlayPresentation
{
    /// <summary>
    /// Resolves visibility, scale, and opacity for an overlay at a given camera distance.
    /// </summary>
    /// <param name="distanceToCamera">The overlay's distance to the camera.</param>
    /// <param name="fullScaleDistance">The distance through which the overlay remains full size.</param>
    /// <param name="fadeStartDistance">The distance at which the overlay begins fading.</param>
    /// <param name="maxDistance">The distance at which the overlay becomes hidden.</param>
    /// <param name="minScale">The minimum scale reached at the maximum distance.</param>
    /// <returns>The resolved distance-aware presentation state.</returns>
    internal static DistanceAwareOverlayPresentationState Resolve(
        float distanceToCamera,
        float fullScaleDistance,
        float fadeStartDistance,
        float maxDistance,
        float minScale)
    {
        var normalizedMinScale = Math.Clamp(minScale, 0.10f, 1.0f);
        var normalizedFullScaleDistance = Math.Max(0f, fullScaleDistance);
        var normalizedFadeStartDistance = Math.Max(
            normalizedFullScaleDistance,
            fadeStartDistance);
        var normalizedMaxDistance = Math.Max(
            normalizedFadeStartDistance + 0.01f,
            maxDistance);

        if (distanceToCamera >= normalizedMaxDistance)
        {
            return new DistanceAwareOverlayPresentationState(
                false,
                normalizedMinScale,
                0f);
        }

        if (distanceToCamera <= normalizedFullScaleDistance)
        {
            return new DistanceAwareOverlayPresentationState(
                true,
                1f,
                1f);
        }

        var scaleProgress =
            (distanceToCamera - normalizedFullScaleDistance) /
            (normalizedMaxDistance - normalizedFullScaleDistance);
        var scale = 1f - ((1f - normalizedMinScale) * scaleProgress);

        var alpha = 1f;
        if (distanceToCamera >= normalizedFadeStartDistance)
        {
            var fadeProgress =
                (distanceToCamera - normalizedFadeStartDistance) /
                (normalizedMaxDistance - normalizedFadeStartDistance);
            alpha = 1f - fadeProgress;
        }

        return new DistanceAwareOverlayPresentationState(
            true,
            Math.Clamp(scale, normalizedMinScale, 1f),
            Math.Clamp(alpha, 0f, 1f));
    }
}
