// <copyright file="RuntimeLanguageHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game;
using Echoglossian.LanguagesHandling;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers language normalization used by DB-first recovery heuristics so
///     runtime comparisons do not hardcode one target language.
/// </summary>
public class RuntimeLanguageHelperTests
{
    /// <summary>
    ///     Ensures a lookup carrying an effective surface engine does not
    ///     rebuild its reuse scope from the global default engine.
    /// </summary>
    [Fact]
    public void TranslationReuseScopeTryCreate_ExplicitEngineOverridesConfig()
    {
        var originalClientState = global::Echoglossian.Echoglossian.ClientStateInterface;
        var originalLanguages = global::Echoglossian.Echoglossian.LangDict;

        try
        {
            global::Echoglossian.Echoglossian.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState(ClientLanguage.English);
            global::Echoglossian.Echoglossian.LangDict = new Dictionary<int, LanguageInfo>
            {
                [28] = new LanguageInfo(
                    "pt-BR",
                    "Portuguese",
                    string.Empty,
                    string.Empty,
                    []),
            };
            var config = new Config
            {
                Lang = 28,
                ChosenTransEngine = 4,
                TranslateAlreadyTranslatedTexts = true,
            };

            var created = TranslationReuseScope.TryCreate(
                config,
                translationEngine: 7,
                out var scope);

            Assert.True(created);
            Assert.Equal(7, scope.TranslationEngine);
        }
        finally
        {
            global::Echoglossian.Echoglossian.LangDict = originalLanguages;
            global::Echoglossian.Echoglossian.ClientStateInterface = originalClientState;
        }
    }

    /// <summary>
    ///     Ensures an unknown current client language cannot create a
    ///     persistence reuse scope or provider source identity.
    /// </summary>
    [Fact]
    public void TranslationReuseScopeTryCreate_UnknownClientValue_ReturnsFalse()
    {
        var originalClientState = global::Echoglossian.Echoglossian.ClientStateInterface;

        try
        {
            global::Echoglossian.Echoglossian.ClientStateInterface =
                TranslationReuseScopeTests.CreateClientState((ClientLanguage)99);
            var config = new Config
            {
                Lang = 42,
                ChosenTransEngine = 4,
                TranslateAlreadyTranslatedTexts = true,
            };

            var created = TranslationReuseScope.TryCreate(config, out var scope);

            Assert.False(created);
            Assert.Equal(default, scope);
        }
        finally
        {
            global::Echoglossian.Echoglossian.ClientStateInterface = originalClientState;
        }
    }

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
    ///     Ensures aliases accepted for legacy source rows remain within one
    ///     canonical identity and do not bridge extended client identities.
    /// </summary>
    /// <param name="left">The first stored or requested language value.</param>
    /// <param name="right">The second stored or requested language value.</param>
    /// <param name="expectedMatch">Whether the values belong to one identity.</param>
    [Theory]
    [InlineData("English", "en", true)]
    [InlineData("Deutsch", "de", true)]
    [InlineData("Japanese", "ja", true)]
    [InlineData("French", "fr", true)]
    [InlineData("chs", "cht", false)]
    [InlineData("chs", "tc", false)]
    [InlineData("cht", "tc", false)]
    [InlineData("zh-Hans", "chs", false)]
    [InlineData("zh-Hans", "cht", false)]
    [InlineData("zh-Hant", "tc", false)]
    public void LanguagesMatch_LegacyAliases_DoNotBridgeExtendedIdentities(
        string left,
        string right,
        bool expectedMatch)
    {
        var result = RuntimeLanguageHelper.LanguagesMatch(left, right);

        Assert.Equal(expectedMatch, result);
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
