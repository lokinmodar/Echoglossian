// <copyright file="TooltipTextCacheManagerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the dedicated cache used by the Tooltip addon persistence path.
/// </summary>
public sealed class TooltipTextCacheManagerTests
{
    /// <summary>
    ///     Ensures the dedicated Tooltip cache exposes the same two hot-path
    ///     lookup shapes the runtime needs: exact match and candidate
    ///     enumeration.
    /// </summary>
    [Fact]
    public void TooltipTextCacheManager_DefinesLookupApis()
    {
        var cacheManagerType = ResolveTooltipTextCacheManagerType();

        Assert.NotNull(cacheManagerType);
        Assert.NotNull(cacheManagerType!.GetMethod(
            "TryFindMatch",
            BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(cacheManagerType.GetMethod(
            "GetCandidates",
            BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(cacheManagerType.GetMethod(
            "Update",
            BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(cacheManagerType.GetMethod(
            "Clear",
            BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(cacheManagerType.GetMethod(
            "Preload",
            BindingFlags.Public | BindingFlags.Static));
    }

    /// <summary>
    ///     Ensures preload loads rows with translated payloads from SQLite.
    /// </summary>
    [Fact]
    public void TooltipTextCacheManager_PreloadLoadsTranslatedTooltipRowsFromSqlite()
    {
        var configDir = CreateTempConfigDirectory();

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                context.TooltipTexts.Add(new TooltipText
                {
                    Id = 1,
                    AddonName = "Tooltip",
                    OriginalTextsAsText = "[\"Travel\"]",
                    OriginalLang = "en",
                    TranslatedTextsAsText = "[\"Viaje\"]",
                    TranslationLang = "pt-BR",
                    TranslationEngine = 0,
                    GameVersion = "7.3",
                    SourceContentHash = "tooltip-travel-hash",
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow,
                });
                context.SaveChanges();
            }

            TooltipTextCacheManager.Clear();
            TooltipTextCacheManager.Preload(configDir);

            var match = TooltipTextCacheManager.TryFindMatch(
                "Tooltip",
                new TranslationReuseScope("en", "pt-BR", 0, true),
                "7.3",
                "[\"Travel\"]",
                "tooltip-travel-hash");

            Assert.NotNull(match);
        }
        finally
        {
            TooltipTextCacheManager.Clear();
        }
    }

    private static string CreateTempConfigDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "EchoglossianTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    ///     Ensures exact Tooltip cache lookup remains source-scoped so rows
    ///     from a different original client language are not reused.
    /// </summary>
    [Fact]
    public void TooltipTextCacheManager_TryFindMatchRequiresMatchingSourceLanguage()
    {
        var cacheManagerType = ResolveTooltipTextCacheManagerType();
        Assert.NotNull(cacheManagerType);

        var clearMethod = cacheManagerType!.GetMethod(
            "Clear",
            BindingFlags.Public | BindingFlags.Static);
        var updateMethod = cacheManagerType.GetMethod(
            "Update",
            BindingFlags.Public | BindingFlags.Static);
        var tryFindMatchMethod = cacheManagerType.GetMethod(
            "TryFindMatch",
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(clearMethod);
        Assert.NotNull(updateMethod);
        Assert.NotNull(tryFindMatchMethod);

        clearMethod!.Invoke(null, null);

        try
        {
            updateMethod!.Invoke(null, [new TooltipText
            {
                Id = 1,
                AddonName = "Tooltip",
                OriginalTextsAsText = "[\"Travel\"]",
                OriginalLang = "en",
                TranslatedTextsAsText = "[\"Viagem\"]",
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = "7.3",
                SourceContentHash = "tooltip-travel-hash",
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow,
            }]);

            var matching = tryFindMatchMethod!.Invoke(
                null,
                [
                    "Tooltip",
                    new TranslationReuseScope("English", "pt-BR", 0, true),
                    "7.3",
                    "[\"Travel\"]",
                    "tooltip-travel-hash",
                ]);
            var mismatching = tryFindMatchMethod.Invoke(
                null,
                [
                    "Tooltip",
                    new TranslationReuseScope("de", "pt-BR", 0, true),
                    "7.3",
                    "[\"Travel\"]",
                    "tooltip-travel-hash",
                ]);

            Assert.NotNull(matching);
            Assert.Null(mismatching);
        }
        finally
        {
            clearMethod.Invoke(null, null);
        }
    }

    private static Type? ResolveTooltipTextCacheManagerType()
    {
        return typeof(TooltipText).Assembly.GetType(
            "Echoglossian.Cache.TooltipTextCacheManager");
    }
}
