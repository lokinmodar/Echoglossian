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
}
