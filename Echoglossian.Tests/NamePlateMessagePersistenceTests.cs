// <copyright file="NamePlateMessagePersistenceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game.Gui.NamePlate;

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers persistence and in-memory reuse behavior for world-object
///     nameplate translations.
/// </summary>
public class NamePlateMessagePersistenceTests
{
    /// <summary>
    ///     Ensures nameplate translation starts disabled and uses the standard
    ///     native display mode when explicitly enabled by the user.
    /// </summary>
    [Fact]
    public void Config_Defaults_DisableNamePlateTranslation()
    {
        var config = new Config();

        Assert.False(config.TranslateNamePlates);
        Assert.Equal(
            JournalTranslationDisplayMode.NativeUiTranslation,
            config.NamePlateTranslationDisplayMode);
        Assert.Equal(1f, config.NamePlateFontScale);
    }

    /// <summary>
    ///     Ensures the nameplate cache only reuses exact kind/source/language
    ///     matches for the active engine when strict reuse is configured.
    /// </summary>
    [Fact]
    public void Cache_TryFindMatch_RequiresExactNamePlateScope()
    {
        NamePlateCacheManager.Clear();
        try
        {
            var row = CreateNamePlateMessage(
                NamePlateKind.EventObject,
                "Treasure Coffer",
                "Cofre do Tesouro",
                translationEngine: 3);
            NamePlateCacheManager.Update(row);

            var strictScope = new TranslationReuseScope(
                "en",
                "pt-BR",
                3,
                RequireMatchingEngine: true);

            var match = NamePlateCacheManager.TryFindMatch(
                NamePlateKind.EventObject,
                "Treasure Coffer",
                strictScope);
            var wrongKind = NamePlateCacheManager.TryFindMatch(
                NamePlateKind.GatheringPoint,
                "Treasure Coffer",
                strictScope);
            var wrongEngine = NamePlateCacheManager.TryFindMatch(
                NamePlateKind.EventObject,
                "Treasure Coffer",
                strictScope with { TranslationEngine = 4 });

            Assert.Same(row, match);
            Assert.Null(wrongKind);
            Assert.Null(wrongEngine);
        }
        finally
        {
            NamePlateCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures migrations create the dedicated nameplate table and the
    ///     context can read persisted rows through its DbSet.
    /// </summary>
    [Fact]
    public void Migration_CreatesNamePlateMessageTable()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                context.NamePlateMessages.Add(
                    CreateNamePlateMessage(
                        NamePlateKind.GatheringPoint,
                        "Lush Vegetation",
                        "Vegetacao Luxuriante",
                        translationEngine: 2));
                context.SaveChanges();
            }

            using (var verification = new EchoglossianDbContext(configDir))
            {
                var row = Assert.Single(verification.NamePlateMessages);

                Assert.Equal("Lush Vegetation", row.OriginalNamePlateText);
                Assert.Equal("Vegetacao Luxuriante", row.TranslatedNamePlateText);
                Assert.Equal((int)NamePlateKind.GatheringPoint, row.NamePlateKind);
            }
        }
        finally
        {
            if (Directory.Exists(configDir))
            {
                SqliteConnection.ClearAllPools();
                Directory.Delete(configDir, recursive: true);
            }
        }
    }

    private static NamePlateMessage CreateNamePlateMessage(
        NamePlateKind kind,
        string originalText,
        string translatedText,
        int translationEngine)
    {
        return new NamePlateMessage(
            (int)kind,
            originalText,
            "en",
            translatedText,
            "pt-BR",
            translationEngine,
            DateTime.Now,
            DateTime.Now);
    }
}
