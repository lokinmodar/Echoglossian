// <copyright file="NamePlateTranslationPolicyTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game.Gui.NamePlate;

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the translation policy for world-object nameplates.
/// </summary>
public class NamePlateTranslationPolicyTests
{
    /// <summary>
    ///     Ensures player, NPC, retainer, battle NPC, and subkind-6 battle NPC
    ///     nameplates remain excluded.
    /// </summary>
    /// <param name="kind">The nameplate kind to evaluate.</param>
    [Theory]
    [InlineData(NamePlateKind.PlayerCharacter)]
    [InlineData(NamePlateKind.EventNpcCompanion)]
    [InlineData(NamePlateKind.Retainer)]
    [InlineData(NamePlateKind.BattleNpcEnemy)]
    [InlineData(NamePlateKind.BattleNpcFriendly)]
    [InlineData(NamePlateKind.BattleNpcSubkind6)]
    public void ShouldTranslateKind_ExcludedKinds_ReturnsFalse(
        NamePlateKind kind)
    {
        Assert.False(NamePlateTranslationPolicy.ShouldTranslateKind(kind));
    }

    /// <summary>
    ///     Ensures interactible world-object nameplates are eligible.
    /// </summary>
    /// <param name="kind">The nameplate kind to evaluate.</param>
    [Theory]
    [InlineData(NamePlateKind.EventObject)]
    [InlineData(NamePlateKind.Treasure)]
    [InlineData(NamePlateKind.GatheringPoint)]
    [InlineData(NamePlateKind.Other)]
    public void ShouldTranslateKind_WorldObjectKinds_ReturnsTrue(
        NamePlateKind kind)
    {
        Assert.True(NamePlateTranslationPolicy.ShouldTranslateKind(kind));
    }
}
