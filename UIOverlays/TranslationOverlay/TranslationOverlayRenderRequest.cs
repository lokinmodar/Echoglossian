// <copyright file="TranslationOverlayRenderRequest.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TranslationOverlay;

/// <summary>
/// Describes one translation overlay render pass.
/// </summary>
/// <param name="Overlay">The visible overlay state.</param>
/// <param name="WindowConfig">The surface-specific window configuration.</param>
/// <param name="ViewportPosition">The upper-left viewport position.</param>
/// <param name="ViewportSize">The viewport dimensions.</param>
/// <param name="AddonPosition">The source addon position.</param>
/// <param name="AddonSize">The source addon dimensions.</param>
/// <param name="IsPreview">Whether the request comes from the standalone previewer.</param>
internal sealed record TranslationOverlayRenderRequest(
    TranslationOverlay Overlay,
    TranslationWindowConfig WindowConfig,
    Vector2 ViewportPosition,
    Vector2 ViewportSize,
    Vector2 AddonPosition,
    Vector2 AddonSize,
    bool IsPreview);
