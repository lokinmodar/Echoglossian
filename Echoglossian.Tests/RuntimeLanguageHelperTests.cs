// <copyright file="RuntimeLanguageHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers language normalization used by DB-first recovery heuristics so
///     runtime comparisons do not hardcode one target language.
/// </summary>
public class RuntimeLanguageHelperTests
{
    /// <summary>
    ///     Ensures native game language names normalize to their expected
    ///     comparison codes.
    /// </summary>
    /// <param name="rawLanguage">The raw language value.</param>
    /// <param name="expectedCode">The expected normalized code.</param>
    [Theory]
    [InlineData("English", "en")]
    [InlineData("Deutsch", "de")]
    [InlineData("French", "fr")]
    [InlineData("Français", "fr")]
    [InlineData("Japanese", "ja")]
    [InlineData("日本語", "ja")]
    public void NormalizeLanguage_NormalizesRuntimeNames(
        string rawLanguage,
        string expectedCode)
    {
        var normalizedLanguage =
            RuntimeLanguageHelper.NormalizeLanguage(rawLanguage);

        Assert.Equal(expectedCode, normalizedLanguage);
    }

    /// <summary>
    ///     Ensures language matching accepts human-readable source language
    ///     names and normalized target codes.
    /// </summary>
    [Fact]
    public void LanguagesMatch_ReturnsTrue_ForEquivalentNameAndCode()
    {
        var result = RuntimeLanguageHelper.LanguagesMatch("English", "en");

        Assert.True(result);
    }

    /// <summary>
    ///     Ensures regional target codes still match their normalized alias.
    /// </summary>
    [Fact]
    public void LanguagesMatch_ReturnsTrue_ForNormalizedRegionalCodes()
    {
        var result = RuntimeLanguageHelper.LanguagesMatch("pt", "pt-BR");

        Assert.True(result);
    }
}
