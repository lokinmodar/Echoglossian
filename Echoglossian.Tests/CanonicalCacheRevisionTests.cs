// <copyright file="CanonicalCacheRevisionTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.AddonHandlers.Character;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the invalidation signals used by runtime-local canonical lookup
///     snapshots.
/// </summary>
public class CanonicalCacheRevisionTests
{
    /// <summary>
    ///     Ensures a canonical structured payload update invalidates runtime
    ///     snapshots that depend on the StringArrayData cache.
    /// </summary>
    [Fact]
    public void Update_StringArrayDataAdvancesRevision()
    {
        StringArrayDataCacheManager.Clear();
        var before = StringArrayDataCacheManager.Revision;

        try
        {
            StringArrayDataCacheManager.Update(new StringArrayDatas(
                type: "Character",
                size: 1,
                rawData: null,
                formattedRawData: null,
                originalLang: "en",
                originalStrings: "{\"0\":\"Profile\"}",
                translationLang: "pt-BR",
                translatedStrings: "{\"0\":\"Perfil\"}",
                translatedStringsWithPayloads: null,
                translationEngine: 0,
                gameVersion: "7.3",
                createdAt: DateTime.UtcNow,
                updatedAt: DateTime.UtcNow)
            {
                ContextKey = "addon:Character",
                SourceContentHash = "character-profile",
            });

            Assert.True(StringArrayDataCacheManager.Revision > before);
        }
        finally
        {
            StringArrayDataCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures a legacy Character-family payload update invalidates
    ///     runtime snapshots that still consult the GameWindow cache.
    /// </summary>
    [Fact]
    public void Update_GameWindowAdvancesRevision()
    {
        GameWindowCacheManager.Clear();
        var before = GameWindowCacheManager.Revision;

        try
        {
            GameWindowCacheManager.Update(new GameWindow(
                windowAddonName: "Character",
                originalWindowStrings: "{\"textNodes\":{\"0\":\"Profile\"}}",
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"textNodes\":{\"0\":\"Perfil\"}}",
                translationLang: "pt-BR",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow,
                classJobId: null));

            Assert.True(GameWindowCacheManager.Revision > before);
        }
        finally
        {
            GameWindowCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures a Character lookup snapshot remains reusable only while its
    ///     source scope, game version, and both canonical cache revisions are
    ///     unchanged.
    /// </summary>
    [Fact]
    public void CharacterLookupSnapshotCache_RejectsChangedCanonicalRevision()
    {
        var cache = new CharacterCanonicalLookupSnapshotCache();
        var scope = new TranslationReuseScope("en", "pt-BR", 0, false);
        cache.Store(scope, "7.3", 3L, 5L);

        Assert.True(cache.IsCurrent(scope, "7.3", 3L, 5L));
        Assert.False(cache.IsCurrent(scope, "7.3", 4L, 5L));
        Assert.False(cache.IsCurrent(scope, "7.3", 3L, 6L));
    }

}
