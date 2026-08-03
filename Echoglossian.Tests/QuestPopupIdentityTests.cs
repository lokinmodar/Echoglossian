// <copyright file="QuestPopupIdentityTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

using Echoglossian.NativeUI.AddonHandlers.Quest;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards the explicit quest-popup identity helpers used by
///     JournalAccept and JournalResult.
/// </summary>
public sealed class QuestPopupIdentityTests
{
    /// <summary>
    ///     Ensures the dedicated quest-popup identity type exposes separate
    ///     readers for JournalAccept and JournalResult.
    /// </summary>
    [Fact]
    public void QuestPopupIdentity_DefinesSeparateJournalPopupReaders()
    {
        var identityType = typeof(JournalAcceptHandler).Assembly.GetType(
            "Echoglossian.NativeUI.AddonHandlers.Quest.QuestPopupIdentity");

        Assert.NotNull(identityType);
        Assert.NotNull(identityType!.GetMethod(
            "TryReadJournalAcceptQuestId",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
        Assert.NotNull(identityType.GetMethod(
            "TryReadJournalResultQuestId",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
    }
}
