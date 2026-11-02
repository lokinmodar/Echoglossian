// <copyright file="GameWindowCachePublicationTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers worker publication into the shared GameWindow cache.
/// </summary>
public sealed class GameWindowCachePublicationTests
{
    /// <summary>
    ///     Ensures reset rejects both stale preload and committed-row callbacks.
    /// </summary>
    [Fact]
    public void Clear_RejectsPublicationFromPreviousSession()
    {
        GameWindowCacheManager.Clear();
        var generation = GameWindowCacheManager.Generation;
        GameWindowCacheManager.Clear();

        try
        {
            GameWindowCacheManager.PublishPreloaded([CreateRow(1, "old")], generation);
            GameWindowCacheManager.Update(CreateRow(2, "late"), generation);

            Assert.False(GameWindowCacheManager.IsPreloaded);
            Assert.Empty(GameWindowCacheManager.GetCandidates(
                "ActionMenu", new TranslationReuseScope("en", "pt-BR", 0, true), "7.3"));
        }
        finally
        {
            GameWindowCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures a slower initial read cannot replace a newly committed value.
    /// </summary>
    [Fact]
    public void PublishPreloaded_PreservesNewerCommittedRows()
    {
        GameWindowCacheManager.Clear();

        try
        {
            var generation = GameWindowCacheManager.Generation;
            var committed = CreateRow(1, "new translation");
            GameWindowCacheManager.Update(committed, generation);
            GameWindowCacheManager.PublishPreloaded(
                [CreateRow(1, "old translation"), CreateRow(2, "other translation")], generation);

            Assert.True(GameWindowCacheManager.IsPreloaded);
            Assert.Same(committed, GameWindowCacheManager.TryFindMatch(
                "ActionMenu", new TranslationReuseScope("en", "pt-BR", 0, true), "7.3", "source-1"));
            Assert.Equal(2, GameWindowCacheManager.GetCandidates(
                "ActionMenu", new TranslationReuseScope("en", "pt-BR", 0, true), "7.3").Count);
        }
        finally
        {
            GameWindowCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures readers retain stable candidate snapshots during worker updates.
    /// </summary>
    [Fact]
    public async Task GetCandidates_RemainsSafeDuringConcurrentPublication()
    {
        GameWindowCacheManager.Clear();

        try
        {
            var generation = GameWindowCacheManager.Generation;
            var scope = new TranslationReuseScope("en", "pt-BR", 0, true);
            GameWindowCacheManager.Update(CreateRow(1, "initial"), generation);
            var snapshot = GameWindowCacheManager.GetCandidates("ActionMenu", scope, "7.3");

            await Task.WhenAll(
                Task.Run(() =>
                {
                    for (var i = 1; i <= 100; i++)
                    {
                        GameWindowCacheManager.Update(CreateRow(i, "updated"), generation);
                    }
                }),
                Task.Run(() =>
                {
                    for (var i = 1; i <= 100; i++)
                    {
                        var candidates = GameWindowCacheManager.GetCandidates("ActionMenu", scope, "7.3");
                        Assert.All(candidates, row => Assert.Equal("ActionMenu", row.WindowAddonName));
                        GameWindowCacheManager.TryFindMatch("ActionMenu", scope, "7.3", "source-1");
                    }
                }));

            Assert.Single(snapshot);
            Assert.Equal("initial", snapshot[0].TranslatedWindowStrings);
            Assert.Equal(100, GameWindowCacheManager.GetCandidates("ActionMenu", scope, "7.3").Count);
        }
        finally
        {
            GameWindowCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Creates one committed cache row with a distinct original payload.
    /// </summary>
    /// <param name="id">The persisted identifier.</param>
    /// <param name="translation">The translated payload.</param>
    /// <returns>The cache row.</returns>
    private static GameWindow CreateRow(int id, string translation)
    {
        return new GameWindow(
            "ActionMenu", $"source-{id}", "en", translation, "pt-BR", 0, "7.3", null, null)
        {
            Id = id,
        };
    }
}
