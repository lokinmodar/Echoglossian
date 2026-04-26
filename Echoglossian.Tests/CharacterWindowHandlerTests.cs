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

    /// <summary>
    ///     Ensures the root Character header labels have local fallback
    ///     translations so the window title and tab labels remain translatable
    ///     even when the DB-first canonical rows omit them.
    /// </summary>
    [Theory]
    [InlineData("Character", "Personagem")]
    [InlineData("Attributes", "Atributos")]
    [InlineData("Profile", "Perfil")]
    [InlineData("Classes/Jobs", "Classes/Profissões")]
    [InlineData("Reputation", "Reputação")]
    public void TryGetStableHeaderFallbackTranslation_ReturnsExpectedValue(
        string originalText,
        string expectedTranslation)
    {
        var found = CharacterWindowHandler.TryGetStableHeaderFallbackTranslation(
            originalText,
            out var translatedText);

        Assert.True(found);
        Assert.Equal(expectedTranslation, translatedText);
    }

    /// <summary>
    ///     Ensures the root Character handler uses a bounded post-lifecycle
    ///     settling window instead of permanent pre-draw polling.
    /// </summary>
    [Fact]
    public void GetRootCharacterAppliedStateRefreshWindow_ReturnsExpectedDuration()
    {
        var refreshWindow =
            CharacterWindowHandler.GetRootCharacterAppliedStateRefreshWindow();

        Assert.Equal(TimeSpan.FromSeconds(1), refreshWindow);
    }
}
