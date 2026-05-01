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
                "pt-BR",
                0,
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
                "pt-BR",
                0,
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
}
