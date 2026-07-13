// <copyright file="CharacterStatusSubWindowHandlerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Character;
using Echoglossian.NativeUI.AddonHandlers.Common;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers language-agnostic canonical fallback trust rules for CharacterStatus.
/// </summary>
public class CharacterStatusSubWindowHandlerTests
{
    /// <summary>
    ///     Ensures the CharacterStatus canonical fallback accepts translated
    ///     payloads in non-Portuguese target languages as long as enough stable
    ///     section titles changed relative to the original payload.
    /// </summary>
    [Fact]
    public void HasExpectedTranslatedSectionCoverage_NonPortugueseTranslatedPayload_ReturnsTrue()
    {
        var originalPayload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [1] = "Attributes",
                [2] = "Offensive Properties",
                [3] = "Defensive Properties",
                [4] = "Strength",
            },
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal));
        var translatedPayload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [1] = "מאפיינים",
                [2] = "מאפייני התקפה",
                [3] = "מאפייני הגנה",
                [4] = "כוח",
            },
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal));

        var hasCoverage =
            CharacterStatusSubWindowHandler.HasExpectedTranslatedSectionCoverage(
                originalPayload,
                translatedPayload);

        Assert.True(hasCoverage);
    }

    /// <summary>
    ///     Ensures unchanged payloads are not treated as trusted translated
    ///     canonical fallbacks.
    /// </summary>
    [Fact]
    public void HasExpectedTranslatedSectionCoverage_UnchangedPayload_ReturnsFalse()
    {
        var payload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [1] = "Attributes",
                [2] = "Offensive Properties",
                [3] = "Defensive Properties",
            },
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal));

        var hasCoverage =
            CharacterStatusSubWindowHandler.HasExpectedTranslatedSectionCoverage(
                payload,
                payload);

        Assert.False(hasCoverage);
    }
}
