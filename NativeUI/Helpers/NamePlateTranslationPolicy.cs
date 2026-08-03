// <copyright file="NamePlateTranslationPolicy.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game.Gui.NamePlate;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Provides translation eligibility rules for world-object nameplates.
/// </summary>
internal static class NamePlateTranslationPolicy
{
    /// <summary>
    ///     Determines whether a nameplate kind should be translated.
    /// </summary>
    /// <param name="kind">The Dalamud nameplate kind.</param>
    /// <returns><c>true</c> when the nameplate kind is eligible.</returns>
    internal static bool ShouldTranslateKind(NamePlateKind kind)
    {
        return kind is not NamePlateKind.PlayerCharacter
            and not NamePlateKind.EventNpcCompanion
            and not NamePlateKind.Retainer
            and not NamePlateKind.BattleNpcEnemy
            and not NamePlateKind.BattleNpcFriendly
            and not NamePlateKind.BattleNpcSubkind6;
    }
}
