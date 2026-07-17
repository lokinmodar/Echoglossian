// <copyright file="TranslationOverlayRenderResult.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TranslationOverlay;

/// <summary>
/// Reports the exact bounds and text backend used by a render pass.
/// </summary>
/// <param name="WasDrawn">Whether a window was rendered.</param>
/// <param name="Position">The rendered window position.</param>
/// <param name="Size">The rendered window size.</param>
/// <param name="PresentationMode">The text backend used for the render.</param>
internal sealed record TranslationOverlayRenderResult(
    bool WasDrawn,
    Vector2 Position,
    Vector2 Size,
    TextPresentationBackendKind PresentationMode);
