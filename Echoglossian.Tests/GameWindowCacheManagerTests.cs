// <copyright file="GameWindowCacheManagerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers ActionMenu-specific cache compaction semantics for
///     <see cref="GameWindowCacheManager" />.
/// </summary>
public class GameWindowCacheManagerTests
{
    /// <summary>
    ///     Ensures ActionMenu cache updates collapse multiple payload variants
    ///     from the same scope to one preferred candidate.
    /// </summary>
    [Fact]
    public void GetCandidates_ActionMenuReturnsSingleScopedCandidate()
    {
        GameWindowCacheManager.Clear();

        try
        {
            GameWindowCacheManager.Update(new GameWindow(
                windowAddonName: "ActionMenu",
                originalWindowStrings: "{\"atkValues\":{\"17\":\"Peloton\"}}",
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"atkValues\":{\"17\":\"Pelotão\"}}",
                translationLang: "pt-BR",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow.AddSeconds(-1),
                updatedDate: DateTime.UtcNow.AddSeconds(-1),
                classJobId: 38));
            GameWindowCacheManager.Update(new GameWindow(
                windowAddonName: "ActionMenu",
                originalWindowStrings: "{\"atkValues\":{\"17\":\"Cascade\"}}",
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"atkValues\":{\"17\":\"Cascata\"}}",
                translationLang: "pt-BR",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow,
                classJobId: 38));

            var candidates = GameWindowCacheManager.GetCandidates(
                "ActionMenu",
                new TranslationReuseScope("en", "pt-BR", 0, true),
                "7.3",
                classJobId: 38);

            var row = Assert.Single(candidates);
            Assert.Equal(
                "{\"atkValues\":{\"17\":\"Cascade\"}}",
                row.OriginalWindowStrings);
        }
        finally
        {
            GameWindowCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures Character-family cache lookups collapse multiple payload
    ///     variants from the same scope to the richest candidate so partial
    ///     snapshots do not outrank the most complete row.
    /// </summary>
    [Fact]
    public void GetCandidates_CharacterReturnsRichestScopedCandidate()
    {
        GameWindowCacheManager.Clear();

        try
        {
            GameWindowCacheManager.Update(new GameWindow(
                windowAddonName: "CharacterProfile",
                originalWindowStrings: "{\"textNodes\":{\"0\":\"Profile\"}}",
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"textNodes\":{\"0\":\"Perfil\"}}",
                translationLang: "pt-BR",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow.AddSeconds(-1),
                updatedDate: DateTime.UtcNow.AddSeconds(-1),
                classJobId: null));
            GameWindowCacheManager.Update(new GameWindow(
                windowAddonName: "CharacterProfile",
                originalWindowStrings: "{\"textNodes\":{\"0\":\"Profile\",\"1\":\"Classes/Jobs\",\"2\":\"Reputation\"}}",
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"textNodes\":{\"0\":\"Perfil\",\"1\":\"Classes/Jobs\",\"2\":\"Reputação\"}}",
                translationLang: "pt-BR",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow,
                classJobId: null));

            var candidates = GameWindowCacheManager.GetCandidates(
                "CharacterProfile",
                new TranslationReuseScope("en", "pt-BR", 0, true),
                "7.3");

            var row = Assert.Single(candidates);
            Assert.Contains(
                "\"2\":\"Reputation\"",
                row.OriginalWindowStrings);
        }
        finally
        {
            GameWindowCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures exact cache lookup requires matching source identity.
    /// </summary>
    [Fact]
    public void TryFindMatch_RequiresMatchingSourceLanguage()
    {
        GameWindowCacheManager.Clear();

        try
        {
            const string originalJson = "{\"textNodes\":{\"0\":\"Profile\"}}";
            GameWindowCacheManager.Update(new GameWindow(
                windowAddonName: "CharacterProfile",
                originalWindowStrings: originalJson,
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"textNodes\":{\"0\":\"Perfil\"}}",
                translationLang: "pt-BR",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow));

            var matching = GameWindowCacheManager.TryFindMatch(
                "CharacterProfile",
                new TranslationReuseScope("English", "pt-BR", 0, true),
                "7.3",
                originalJson);
            var mismatching = GameWindowCacheManager.TryFindMatch(
                "CharacterProfile",
                new TranslationReuseScope("de", "pt-BR", 0, true),
                "7.3",
                originalJson);

            Assert.NotNull(matching);
            Assert.Null(mismatching);
        }
        finally
        {
            GameWindowCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures ActionMenu replacement compacts only rows from the same
    ///     normalized source scope.
    /// </summary>
    [Fact]
    public void Update_ActionMenuRetainsDistinctSourceRows()
    {
        GameWindowCacheManager.Clear();

        try
        {
            GameWindowCacheManager.Update(CreateActionMenuRow(
                "en",
                "Peloton",
                "Pelotao",
                DateTime.UtcNow.AddSeconds(-2)));
            GameWindowCacheManager.Update(CreateActionMenuRow(
                "de",
                "Peloton",
                "Peloton DE",
                DateTime.UtcNow.AddSeconds(-1)));
            GameWindowCacheManager.Update(CreateActionMenuRow(
                "English",
                "Cascade",
                "Cascata",
                DateTime.UtcNow));

            var english = Assert.Single(GameWindowCacheManager.GetCandidates(
                "ActionMenu",
                new TranslationReuseScope("en", "pt-BR", 0, true),
                "7.3",
                classJobId: 38));
            var german = Assert.Single(GameWindowCacheManager.GetCandidates(
                "ActionMenu",
                new TranslationReuseScope("German", "pt-BR", 0, true),
                "7.3",
                classJobId: 38));

            Assert.Contains("Cascade", english.OriginalWindowStrings);
            Assert.Contains("Peloton", german.OriginalWindowStrings);
            Assert.Equal("de", german.OriginalWindowStringsLang);
        }
        finally
        {
            GameWindowCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Creates one ActionMenu row for source-scoped cache tests.
    /// </summary>
    /// <param name="sourceLanguage">The original payload language.</param>
    /// <param name="originalText">The original visible text.</param>
    /// <param name="translatedText">The translated visible text.</param>
    /// <param name="updatedAt">The row update timestamp.</param>
    /// <returns>The cache row.</returns>
    private static GameWindow CreateActionMenuRow(
        string sourceLanguage,
        string originalText,
        string translatedText,
        DateTime updatedAt)
    {
        return new GameWindow(
            windowAddonName: "ActionMenu",
            originalWindowStrings: $"{{\"atkValues\":{{\"17\":\"{originalText}\"}}}}",
            originalWindowStringsLang: sourceLanguage,
            translatedWindowStrings: $"{{\"atkValues\":{{\"17\":\"{translatedText}\"}}}}",
            translationLang: "pt-BR",
            translationEngine: 0,
            gameVersion: "7.3",
            createdDate: updatedAt,
            updatedDate: updatedAt,
            classJobId: 38);
    }
}
