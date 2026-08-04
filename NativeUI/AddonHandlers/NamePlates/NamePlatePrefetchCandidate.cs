// <copyright file="NamePlatePrefetchCandidate.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game.Gui.NamePlate;

namespace Echoglossian.NativeUI.AddonHandlers.NamePlates;

/// <summary>
///     Captures the stable data needed to translate one nameplate outside the
///     one-frame <see cref="INamePlateUpdateHandler" /> callback.
/// </summary>
/// <param name="Kind">The Dalamud nameplate kind.</param>
/// <param name="OriginalText">The original visible nameplate text.</param>
internal readonly record struct NamePlatePrefetchCandidate(
    NamePlateKind Kind,
    string OriginalText);
