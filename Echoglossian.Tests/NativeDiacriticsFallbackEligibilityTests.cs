// <copyright file="NativeDiacriticsFallbackEligibilityTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers language metadata for the optional native replacement
///     diacritics-removal fallback.
/// </summary>
public class NativeDiacriticsFallbackEligibilityTests
{
    /// <summary>
    ///     Ensures the current curated language ids are modeled in language
    ///     metadata instead of hardcoded UI conditionals.
    /// </summary>
    [Fact]
    public void CuratedLanguages_EnableNativeReplacementDiacriticsFallbackMetadata()
    {
        var languages = Echoglossian.CreateLanguagesDictionary();
        var eligibleLanguageIds = new[]
        {
            24,
            25,
            44,
            60,
            61,
            80,
            83,
            87,
            91,
            104,
            105,
            109,
            110,
        };

        foreach (var languageId in eligibleLanguageIds)
        {
            Assert.True(
                languages[languageId].SupportsNativeReplacementDiacriticsFallback,
                $"Language id {languageId} should expose the native diacritics fallback.");
        }
    }

    /// <summary>
    ///     Ensures candidate accented languages are not implicitly opted in by
    ///     heuristics before deliberate validation.
    /// </summary>
    [Fact]
    public void CandidateAccentedLanguages_RemainIneligibleUntilCurated()
    {
        var languages = Echoglossian.CreateLanguagesDictionary();
        var candidateLanguageIds = new[]
        {
            6, // Azerbaijani
            12, // Bosnian
            29, // Esperanto
            46, // Igbo
            68, // Maltese
            113, // Welsh
            117, // Yoruba
            125, // Azerbaijani (Latin)
        };

        foreach (var languageId in candidateLanguageIds)
        {
            Assert.False(
                languages[languageId].SupportsNativeReplacementDiacriticsFallback,
                $"Language id {languageId} should remain opt-in only after validation.");
        }
    }
}
