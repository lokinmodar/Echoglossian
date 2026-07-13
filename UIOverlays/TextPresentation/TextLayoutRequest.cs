// <copyright file="TextLayoutRequest.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TextPresentation;

/// <summary>
/// Describes one translated text presentation request for an ImGui-owned
/// surface.
/// </summary>
/// <param name="Text">The logical text to present.</param>
/// <param name="LanguageId">The selected target language identifier.</param>
/// <param name="LanguageCode">The selected target language code.</param>
/// <param name="MaxWidth">The maximum content width available to the backend.</param>
/// <param name="FontScale">The relative font scale requested by the surface.</param>
/// <param name="ShouldUseGeneralFont">
/// Whether the general/original-text font path should be used.
/// </param>
/// <param name="TextColor">The foreground text color.</param>
/// <param name="BackgroundColor">The preferred background color.</param>
/// <param name="SurfaceId">The surface requesting presentation.</param>
/// <param name="CenterAligned">
/// Whether the caller normally centers the content in the available region.
/// </param>
internal sealed record TextLayoutRequest(
    string Text,
    int LanguageId,
    string LanguageCode,
    float MaxWidth,
    float FontScale,
    bool ShouldUseGeneralFont,
    Vector4 TextColor,
    Vector4 BackgroundColor,
    TranslationOverlaySurfaceId SurfaceId,
    bool CenterAligned);
