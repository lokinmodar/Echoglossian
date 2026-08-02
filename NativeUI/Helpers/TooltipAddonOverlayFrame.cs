// <copyright file="TooltipAddonOverlayFrame.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
/// Captures one Tooltip addon overlay anchor snapshot.
/// </summary>
/// <param name="Position">The anchored overlay position.</param>
/// <param name="Size">The anchored overlay size.</param>
/// <param name="NativeScale">The native UI scale resolved for the tooltip.</param>
/// <param name="NativeVisible">Whether the native tooltip is still visible.</param>
internal readonly record struct TooltipAddonOverlayFrame(
    Vector2 Position,
    Vector2 Size,
    float NativeScale,
    bool NativeVisible);
