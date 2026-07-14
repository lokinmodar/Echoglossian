// <copyright file="GameWindowCacheManagerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers cache behavior for <see cref="GameWindowCacheManager" />.
/// </summary>
public class GameWindowCacheManagerTests
{
    /// <summary>
    ///     Ensures ActionMenu cache updates preserve multiple distinct payload
    ///     variants from the same lookup scope so live page changes do not
    ///     overwrite one another.
    /// </summary>
    [Fact]
    public void GetCandidates_ActionMenuReturnsAllDistinctScopedCandidates()
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
                classJobId: 38)
            {
                Id = 1,
            });
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
                classJobId: 38)
            {
                Id = 2,
            });

            var candidates = GameWindowCacheManager.GetCandidates(
                "ActionMenu",
                new TranslationReuseScope("en", "pt-BR", 0, true),
                "7.3",
                classJobId: 38);

            Assert.Equal(2, candidates.Count);
            Assert.Contains(
                candidates,
                row => row.OriginalWindowStrings ==
                       "{\"atkValues\":{\"17\":\"Peloton\"}}");
            Assert.Contains(
                candidates,
                row => row.OriginalWindowStrings ==
                       "{\"atkValues\":{\"17\":\"Cascade\"}}");
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
    ///     Ensures a strict engine-zero scope cannot reuse a legacy row whose
    ///     translation engine is unknown.
    /// </summary>
    [Fact]
    public void TryFindMatch_StrictEngineZeroRejectsNullEngineRow()
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
                translationEngine: null,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow));

            var match = GameWindowCacheManager.TryFindMatch(
                "CharacterProfile",
                new TranslationReuseScope("en", "pt-BR", 0, true),
                "7.3",
                originalJson);
            var candidates = GameWindowCacheManager.GetCandidates(
                "CharacterProfile",
                new TranslationReuseScope("en", "pt-BR", 0, true),
                "7.3");

            Assert.Null(match);
            Assert.Empty(candidates);
        }
        finally
        {
            GameWindowCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures ActionMenu retains distinct rows per normalized source
    ///     scope without collapsing different payload variants from that same
    ///     source.
    /// </summary>
    [Fact]
    public void Update_ActionMenuRetainsDistinctSourceRows()
    {
        GameWindowCacheManager.Clear();

        try
        {
            GameWindowCacheManager.Update(CreateActionMenuRow(
                1,
                "en",
                "Peloton",
                "Pelotao",
                DateTime.UtcNow.AddSeconds(-2)));
            GameWindowCacheManager.Update(CreateActionMenuRow(
                2,
                "de",
                "Peloton",
                "Peloton DE",
                DateTime.UtcNow.AddSeconds(-1)));
            GameWindowCacheManager.Update(CreateActionMenuRow(
                3,
                "English",
                "Cascade",
                "Cascata",
                DateTime.UtcNow));

            var german = Assert.Single(GameWindowCacheManager.GetCandidates(
                "ActionMenu",
                new TranslationReuseScope("German", "pt-BR", 0, true),
                "7.3",
                classJobId: 38));
            var english = GameWindowCacheManager.GetCandidates(
                "ActionMenu",
                new TranslationReuseScope("en", "pt-BR", 0, true),
                "7.3",
                classJobId: 38);

            Assert.Equal(2, english.Count);
            Assert.Contains(
                english,
                row => row.OriginalWindowStringsLang == "en" &&
                       row.OriginalWindowStrings!.Contains(
                           "Peloton",
                           StringComparison.Ordinal));
            Assert.Contains(
                english,
                row => row.OriginalWindowStringsLang == "English" &&
                       row.OriginalWindowStrings!.Contains(
                           "Cascade",
                           StringComparison.Ordinal));
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
    /// <param name="id">The cache row identifier.</param>
    /// <param name="sourceLanguage">The original payload language.</param>
    /// <param name="originalText">The original visible text.</param>
    /// <param name="translatedText">The translated visible text.</param>
    /// <param name="updatedAt">The row update timestamp.</param>
    /// <returns>The cache row.</returns>
    private static GameWindow CreateActionMenuRow(
        int id,
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
            classJobId: 38)
        {
            Id = id,
        };
    }
}
