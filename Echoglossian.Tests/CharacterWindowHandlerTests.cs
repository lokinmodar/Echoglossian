// <copyright file="CharacterWindowHandlerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Character;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers narrow root-header capture rules for the main Character window.
/// </summary>
public class CharacterWindowHandlerTests
{
    /// <summary>
    ///     Ensures stable root header labels remain explicitly capturable so
    ///     the window title and tab names can participate in Character DB-first
    ///     translation even when subwindow payloads own the denser body text.
    /// </summary>
    [Theory]
    [InlineData("Character", true)]
    [InlineData("Attributes", true)]
    [InlineData("Profile", true)]
    [InlineData("Classes/Jobs", true)]
    [InlineData("Reputation", true)]
    [InlineData("Gear Set", false)]
    [InlineData("Strength", false)]
    [InlineData("", false)]
    public void IsStableCharacterHeaderText_ReturnsExpectedResult(
        string visibleText,
        bool expected)
    {
        var actual = CharacterWindowHandler.IsStableCharacterHeaderText(
            visibleText);

        Assert.Equal(expected, actual);
    }
}
