// <copyright file="TranslationOverlayBoundsCalculator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TranslationOverlay;

/// <summary>
/// Calculates overlay anchor bounds from native node geometry without relying on
/// a live ImGui context.
/// </summary>
internal static class TranslationOverlayBoundsCalculator
{
    /// <summary>
    /// Calculates padded bounds from a text node.
    /// </summary>
    /// <param name="textPosition">The screen position of the text node.</param>
    /// <param name="textSize">The native width and height of the text node.</param>
    /// <param name="scale">The owning addon's current scale.</param>
    /// <param name="paddingScale">
    /// The multiplier applied to the text-node size so overlays keep a small
    /// amount of breathing room.
    /// </param>
    /// <returns>The resolved overlay bounds.</returns>
    public static TranslationOverlayBounds CalculateTextBounds(
        Vector2 textPosition,
        Vector2 textSize,
        float scale,
        float paddingScale)
    {
        return new TranslationOverlayBounds(
            textPosition,
            new Vector2(
                Math.Max(1f, textSize.X * scale * paddingScale),
                Math.Max(1f, textSize.Y * scale * paddingScale)));
    }

    /// <summary>
    /// Calculates MiniTalk overlay bounds, preferring the surrounding bubble
    /// geometry whenever it is large enough to represent the visible balloon.
    /// </summary>
    /// <param name="textPosition">The screen position of the text node.</param>
    /// <param name="textSize">The native width and height of the text node.</param>
    /// <param name="scale">The owning addon's current scale.</param>
    /// <param name="paddingScale">
    /// The multiplier applied to fallback text-node bounds.
    /// </param>
    /// <param name="visualNodePosition">
    /// The screen position of the resolved bubble container or background.
    /// </param>
    /// <param name="visualNodeSize">
    /// The native width and height of the resolved bubble container or
    /// background.
    /// </param>
    /// <returns>The resolved overlay bounds.</returns>
    public static TranslationOverlayBounds CalculateMiniTalkBounds(
        Vector2 textPosition,
        Vector2 textSize,
        float scale,
        float paddingScale,
        Vector2? visualNodePosition,
        Vector2? visualNodeSize)
    {
        var fallbackBounds = CalculateTextBounds(
            textPosition,
            textSize,
            scale,
            paddingScale);

        if (!visualNodePosition.HasValue ||
            !visualNodeSize.HasValue ||
            visualNodeSize.Value.X <= 0f ||
            visualNodeSize.Value.Y <= 0f)
        {
            return fallbackBounds;
        }

        var visualDimensions = new Vector2(
            Math.Max(1f, visualNodeSize.Value.X * scale),
            Math.Max(1f, visualNodeSize.Value.Y * scale));
        if (visualDimensions.X + 0.5f < fallbackBounds.Dimensions.X &&
            visualDimensions.Y + 0.5f < fallbackBounds.Dimensions.Y)
        {
            return fallbackBounds;
        }

        return new TranslationOverlayBounds(
            visualNodePosition.Value,
            visualDimensions);
    }
}

/// <summary>
/// Represents the screen bounds used to anchor one overlay.
/// </summary>
/// <param name="Position">The upper-left screen position.</param>
/// <param name="Dimensions">The width and height of the overlay anchor.</param>
internal sealed record TranslationOverlayBounds(
    Vector2 Position,
    Vector2 Dimensions);
