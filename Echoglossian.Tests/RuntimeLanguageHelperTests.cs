// <copyright file="RuntimeLanguageHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers language normalization used by DB-first recovery heuristics so
///     runtime comparisons do not hardcode one target language.
/// </summary>
public class RuntimeLanguageHelperTests
{
    /// <summary>
    ///     Ensures the current-client resolver fails closed for an unknown
    ///     runtime client-language value.
    /// </summary>
    [Fact]
    public void TryResolveCurrentSourceLanguage_UnknownClientValue_ReturnsFalse()
    {
        var originalClientState = global::Echoglossian.Echoglossian.ClientStateInterface;

        try
        {
            global::Echoglossian.Echoglossian.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState((ClientLanguage)99);

            var resolved = RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
                out _);

            Assert.False(resolved);
        }
        finally
        {
            global::Echoglossian.Echoglossian.ClientStateInterface = originalClientState;
        }
    }

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

    /// <summary>
    ///     Ensures Simplified and Traditional Chinese aliases normalize to the
    ///     existing plugin-facing target codes.
    /// </summary>
    [Theory]
    [InlineData("zh-Hans", "zh-CN")]
    [InlineData("zh-Hant", "zh-TW")]
    public void NormalizeLanguage_NormalizesChineseScriptAliases(
        string rawLanguage,
        string expectedCode)
    {
        var normalizedLanguage =
            RuntimeLanguageHelper.NormalizeLanguage(rawLanguage);

        Assert.Equal(expectedCode, normalizedLanguage);
    }
}
