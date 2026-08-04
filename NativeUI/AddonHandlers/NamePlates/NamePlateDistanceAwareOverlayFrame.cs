// <copyright file="NamePlateDistanceAwareOverlayFrame.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.NamePlates;

/// <summary>
///     Represents live projected NamePlate overlay presentation for one frame.
/// </summary>
/// <param name="ScreenPosition">The projected screen-space anchor.</param>
/// <param name="DistanceToCamera">The current distance from the camera.</param>
/// <param name="ScaleMultiplier">The distance-aware render scale.</param>
/// <param name="AlphaMultiplier">The distance-aware render opacity.</param>
internal readonly record struct NamePlateDistanceAwareOverlayFrame(
    Vector2 ScreenPosition,
    float DistanceToCamera,
    float ScaleMultiplier,
    float AlphaMultiplier);
