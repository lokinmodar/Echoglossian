// <copyright file="ActionTooltipCacheManagerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers cache-first action-tooltip text lookup behavior used by the
///     <c>Actions</c> window bootstrap path.
/// </summary>
public class ActionTooltipCacheManagerTests
{
    /// <summary>
    ///     Ensures an exact-version action-tooltip translation wins when both
    ///     exact and version-agnostic rows exist for the same original text.
    /// </summary>
    [Fact]
    public void TryFindTranslatedText_PrefersExactVersionMatch()
    {
        ActionTooltipCacheManager.Clear();

        try
        {
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 1,
                ActionId = 15998,
                ActionName = "Technical Step",
                TranslatedActionName = "Passo Técnico (fallback)",
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = null,
                SourceContentHash = "hash-fallback",
            });
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 2,
                ActionId = 15998,
                ActionName = "Technical Step",
                TranslatedActionName = "Passo Técnico",
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = "7.3",
                SourceContentHash = "hash-exact",
            });

            var found = ActionTooltipCacheManager.TryFindTranslatedText(
                "pt-BR",
                0,
                "7.3",
                "Technical Step",
                out var translatedText);

            Assert.True(found);
            Assert.Equal("Passo Técnico", translatedText);
        }
        finally
        {
            ActionTooltipCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures a version-agnostic action-tooltip translation is reused
    ///     when no exact-version row exists.
    /// </summary>
    [Fact]
    public void TryFindTranslatedText_FallsBackToVersionAgnosticRow()
    {
        ActionTooltipCacheManager.Clear();

        try
        {
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 1,
                ActionId = 15998,
                ActionName = "Technical Step",
                TranslatedActionName = "Passo Técnico",
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = null,
                SourceContentHash = "hash-fallback",
            });

            var found = ActionTooltipCacheManager.TryFindTranslatedText(
                "pt-BR",
                0,
                "7.3",
                "Technical Step",
                out var translatedText);

            Assert.True(found);
            Assert.Equal("Passo Técnico", translatedText);
        }
        finally
        {
            ActionTooltipCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures description text is also indexed for exact-lookup reuse.
    /// </summary>
    [Fact]
    public void TryFindTranslatedText_ResolvesDescriptions()
    {
        ActionTooltipCacheManager.Clear();

        try
        {
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 1,
                ActionId = 15998,
                ActionDescription = "Begin dancing, granting yourself Technical Finish.",
                TranslatedActionDescription = "Comece a dançar, concedendo Technical Finish.",
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = "7.3",
                SourceContentHash = "hash-description",
            });

            var found = ActionTooltipCacheManager.TryFindTranslatedText(
                "pt-BR",
                0,
                "7.3",
                "Begin dancing, granting yourself Technical Finish.",
                out var translatedText);

            Assert.True(found);
            Assert.Equal(
                "Comece a dançar, concedendo Technical Finish.",
                translatedText);
        }
        finally
        {
            ActionTooltipCacheManager.Clear();
        }
    }
}
