// <copyright file="TranslationReuseScopeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the source-language, target-language, and engine contract used
///     by persisted translation reuse.
/// </summary>
public class TranslationReuseScopeTests
{
    /// <summary>
    ///     Ensures legacy source names match their equivalent persisted code.
    /// </summary>
    /// <param name="storedSource">The legacy source name stored in a row.</param>
    /// <param name="requestedSource">The requested canonical source code.</param>
    [Theory]
    [InlineData("English", "en")]
    [InlineData("Deutsch", "de")]
    [InlineData("Japanese", "ja")]
    [InlineData("French", "fr")]
    public void Matches_LegacyStoredSourceName_AcceptsEquivalentCode(
        string storedSource,
        string requestedSource)
    {
        var scope = new TranslationReuseScope(requestedSource, "iw", 4, false);

        Assert.True(scope.Matches(storedSource, "iw", 9));
    }

    /// <summary>
    ///     Ensures source and target mismatches are never reusable.
    /// </summary>
    [Fact]
    public void Matches_DifferentSourceOrTarget_ReturnsFalse()
    {
        var scope = new TranslationReuseScope("ja", "iw", 4, false);

        Assert.False(scope.Matches("en", "iw", 4));
        Assert.False(scope.Matches("ja", "fa", 4));
    }

    /// <summary>
    ///     Ensures retranslation only reuses rows made by the active engine.
    /// </summary>
    [Fact]
    public void Matches_RetranslationEnabled_RequiresActiveEngine()
    {
        var scope = new TranslationReuseScope("en", "iw", 4, true);

        Assert.False(scope.Matches("en", "iw", 7));
    }

    /// <summary>
    ///     Ensures extended raw client values retain their distinct persisted
    ///     source identities while exposing provider-supported language codes.
    /// </summary>
    /// <param name="rawClientLanguage">The raw client language value.</param>
    /// <param name="expectedPersistenceCode">The expected stored identity.</param>
    /// <param name="expectedProviderCode">The expected provider input code.</param>
    [Theory]
    [InlineData(4, "chs", "zh-CN")]
    [InlineData(5, "cht", "zh-CN")]
    [InlineData(6, "ko", "ko")]
    [InlineData(7, "tc", "zh-TW")]
    public void TryResolveSourceLanguage_ExtendedClientValue_ReturnsDistinctIdentity(
        int rawClientLanguage,
        string expectedPersistenceCode,
        string expectedProviderCode)
    {
        var resolved = RuntimeLanguageHelper.TryResolveSourceLanguage(
            (ClientLanguage)rawClientLanguage,
            out var sourceLanguage);

        Assert.True(resolved);
        Assert.Equal(expectedPersistenceCode, sourceLanguage.PersistenceCode);
        Assert.Equal(expectedProviderCode, sourceLanguage.ProviderCode);
    }

    /// <summary>
    ///     Ensures client values without a known source identity fail closed.
    /// </summary>
    [Fact]
    public void TryResolveSourceLanguage_UnknownClientValue_ReturnsFalse()
    {
        var resolved = RuntimeLanguageHelper.TryResolveSourceLanguage(
            (ClientLanguage)99,
            out _);

        Assert.False(resolved);
    }
}
