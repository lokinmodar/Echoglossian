// <copyright file="TooltipTextCacheManagerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

using Echoglossian.EFCoreSqlite.Models;
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
