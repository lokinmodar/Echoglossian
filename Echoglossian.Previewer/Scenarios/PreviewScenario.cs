// <copyright file="PreviewScenario.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TranslationOverlay;

namespace Echoglossian.Previewer.Scenarios;

/// <summary>
/// Describes one deterministic overlay preview scenario.
/// </summary>
/// <param name="Key">The stable scenario key.</param>
/// <param name="DisplayName">The UI display name.</param>
/// <param name="SurfaceId">The selected overlay surface.</param>
/// <param name="AddonBounds">The simulated source addon bounds.</param>
/// <param name="TranslatedText">The translated text shown by the overlay.</param>
/// <param name="Title">The optional speaker or surface title.</param>
/// <param name="Visible">Whether the overlay starts visible.</param>
/// <param name="ShowsSimulatedAddonBounds">Whether the guide starts enabled.</param>
internal sealed record PreviewScenario(
    string Key,
    string DisplayName,
    TranslationOverlaySurfaceId SurfaceId,
    PreviewAddonBounds AddonBounds,
    string TranslatedText,
    string? Title,
    bool Visible,
    bool ShowsSimulatedAddonBounds);

/// <summary>
/// Describes preview-only simulated addon bounds in logical viewport coordinates.
/// </summary>
/// <param name="X">The logical X coordinate.</param>
/// <param name="Y">The logical Y coordinate.</param>
/// <param name="Width">The logical width.</param>
/// <param name="Height">The logical height.</param>
internal sealed record PreviewAddonBounds(float X, float Y, float Width, float Height);
