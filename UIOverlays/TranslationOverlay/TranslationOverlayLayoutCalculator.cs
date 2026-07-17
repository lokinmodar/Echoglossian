// <copyright file="TranslationOverlayLayoutCalculator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TranslationOverlay;

/// <summary>
/// Describes the ImGui-independent inputs required to place one overlay.
/// </summary>
/// <param name="ViewportPosition">The upper-left viewport position.</param>
/// <param name="ViewportSize">The viewport dimensions.</param>
/// <param name="AddonPosition">The source addon position.</param>
/// <param name="AddonSize">The source addon dimensions.</param>
/// <param name="PreviousWindowSize">The window dimensions from the prior render.</param>
/// <param name="MeasuredTextSize">The measured body text dimensions.</param>
/// <param name="MeasuredTitleSize">The measured title dimensions.</param>
/// <param name="HorizontalPadding">The total horizontal window padding.</param>
/// <param name="WindowConfig">The surface-specific window configuration.</param>
internal sealed record TranslationOverlayLayoutRequest(
    Vector2 ViewportPosition,
    Vector2 ViewportSize,
    Vector2 AddonPosition,
    Vector2 AddonSize,
    Vector2 PreviousWindowSize,
    Vector2 MeasuredTextSize,
    Vector2 MeasuredTitleSize,
    float HorizontalPadding,
    TranslationWindowConfig WindowConfig);

/// <summary>
/// Reports the requested ImGui geometry for one overlay render pass.
/// </summary>
/// <param name="RequestedPosition">The clamped upper-left window position.</param>
/// <param name="RequestedSize">The requested window dimensions.</param>
/// <param name="ContentWrapWidth">The available width for wrapped content.</param>
internal sealed record TranslationOverlayLayoutResult(
    Vector2 RequestedPosition,
    Vector2 RequestedSize,
    float ContentWrapWidth);

/// <summary>
/// Calculates translation overlay geometry without reading active ImGui state.
/// </summary>
internal static class TranslationOverlayLayoutCalculator
{
    /// <summary>
    /// Calculates the requested position, width constraints, and content wrap
    /// width for one overlay render pass.
    /// </summary>
    /// <param name="request">The geometry and measured-content inputs.</param>
    /// <returns>The calculated overlay geometry.</returns>
    public static TranslationOverlayLayoutResult Calculate(
        TranslationOverlayLayoutRequest request)
    {
        var config = request.WindowConfig;
        var viewportWidth = Math.Max(1f, request.ViewportSize.X);
        var viewportHeight = Math.Max(1f, request.ViewportSize.Y);
        var baseWidth = request.AddonSize.X * config.WidthMultiplier;
        var defaultMaxWidth = Math.Max(320f, viewportWidth - 80f);
        var minWidth = config.MinWidthViewportFraction > 0f
            ? viewportWidth * config.MinWidthViewportFraction
            : 0f;
        var maxWidth = config.MaxWidthViewportFraction > 0f
            ? Math.Min(viewportWidth * config.MaxWidthViewportFraction, defaultMaxWidth)
            : defaultMaxWidth;
        var measuredContentWidth = Math.Max(
            request.MeasuredTextSize.X,
            config.ForceShowTitle ? request.MeasuredTitleSize.X : 0f) +
            request.HorizontalPadding;
        var desiredWidth = baseWidth;
        if (config.AutoSizeToTextWithMaxWidth)
        {
            desiredWidth = Math.Max(baseWidth, measuredContentWidth);
        }

        if (config.ExpandWidthToFitText)
        {
            var autoExpandedWidth = Math.Min(
                measuredContentWidth,
                baseWidth * config.MaxAutoExpandedWidthMultiplier);
            desiredWidth = Math.Max(baseWidth, autoExpandedWidth);
        }

        var width = Math.Clamp(desiredWidth, minWidth, maxWidth);
        var maxHeight = Math.Max(180f, viewportHeight - 80f);
        var measuredContentHeight = request.MeasuredTextSize.Y +
            (config.ForceShowTitle ? request.MeasuredTitleSize.Y : 0f);
        var requestedHeight = Math.Min(
            maxHeight,
            Math.Max(
                request.PreviousWindowSize.Y,
                measuredContentHeight));
        var requestedPosition = config.CenterOnAddon
            ? new Vector2(
                request.AddonPosition.X + (request.AddonSize.X * 0.5f) -
                (request.PreviousWindowSize.X * 0.5f),
                request.AddonPosition.Y + (request.AddonSize.Y * 0.5f) -
                (request.PreviousWindowSize.Y * 0.5f))
            : new Vector2(
                request.AddonPosition.X + (request.AddonSize.X * 0.5f) -
                (request.PreviousWindowSize.X * 0.5f),
                request.AddonPosition.Y - request.PreviousWindowSize.Y - 20f);
        requestedPosition += config.PosCorrection;

        var maximumPosition = request.ViewportPosition + new Vector2(
            Math.Max(0f, viewportWidth - width),
            Math.Max(0f, viewportHeight - Math.Min(requestedHeight, maxHeight)));
        requestedPosition = Vector2.Clamp(
            requestedPosition,
            request.ViewportPosition,
            maximumPosition);

        return new TranslationOverlayLayoutResult(
            requestedPosition,
            new Vector2(width, requestedHeight),
            Math.Max(64f, width - request.HorizontalPadding));
    }

}
