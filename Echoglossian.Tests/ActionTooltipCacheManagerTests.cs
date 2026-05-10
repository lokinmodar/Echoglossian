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
///     <c>ActionMenu</c> window bootstrap path.
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

    /// <summary>
    ///     Ensures a same-hash translation from an older concrete game
    ///     version can be recovered for promotion into the current version.
    /// </summary>
    [Fact]
    public void TryFindHistoricalCanonicalMatch_ReturnsPreviousVersionSameHashRow()
    {
        ActionTooltipCacheManager.Clear();

        try
        {
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 1,
                ActionId = 15998,
                ActionName = "Technical Step",
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = "7.3",
                SourceContentHash = "hash-exact",
            });
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 2,
                ActionId = 15998,
                ActionName = "Technical Step",
                TranslatedActionName = "Passo Técnico",
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = "7.2",
                SourceContentHash = "hash-exact",
            });

            var found = ActionTooltipCacheManager.TryFindHistoricalCanonicalMatch(
                15998,
                "pt-BR",
                0,
                "7.3",
                "hash-exact");

            Assert.NotNull(found);
            Assert.Equal("7.2", found.GameVersion);
            Assert.Equal("Passo Técnico", found.TranslatedActionName);
        }
        finally
        {
            ActionTooltipCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures an untranslated current-version placeholder does not hide a
    ///     reusable translated row with the same source hash.
    /// </summary>
    [Fact]
    public void TryFindCanonicalMatch_PrefersTranslatedReusableRowOverCurrentPlaceholder()
    {
        ActionTooltipCacheManager.Clear();

        try
        {
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 1,
                ActionId = 15998,
                ActionName = "Technical Step",
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = "7.3",
                SourceContentHash = "hash-exact",
            });
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 2,
                ActionId = 15998,
                ActionName = "Technical Step",
                TranslatedActionName = "Passo Técnico",
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = null,
                SourceContentHash = "hash-exact",
            });

            var found = ActionTooltipCacheManager.TryFindCanonicalMatch(
                15998,
                "pt-BR",
                0,
                "7.3",
                "hash-exact");

            Assert.NotNull(found);
            Assert.Equal("Passo Técnico", found.TranslatedActionName);
        }
        finally
        {
            ActionTooltipCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures reverse lookup resolves a canonical original text from an
    ///     exact translated action name.
    /// </summary>
    [Fact]
    public void TryFindOriginalText_ResolvesTranslatedActionName()
    {
        ActionTooltipCacheManager.Clear();

        try
        {
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 1,
                ActionId = 16013,
                ActionName = "Flourish",
                TranslatedActionName = "Florescer",
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = "7.3",
                SourceContentHash = "hash-flourish",
            });

            var found = ActionTooltipCacheManager.TryFindOriginalText(
                "pt-BR",
                0,
                "7.3",
                "Florescer",
                out var originalText);

            Assert.True(found);
            Assert.Equal("Flourish", originalText);
        }
        finally
        {
            ActionTooltipCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures ambiguous reverse translations are excluded instead of
    ///     choosing an arbitrary original text.
    /// </summary>
    [Fact]
    public void TryFindOriginalText_ReturnsFalseForAmbiguousReverseLookup()
    {
        ActionTooltipCacheManager.Clear();

        try
        {
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 1,
                ActionId = 16013,
                ActionName = "Flourish",
                TranslatedActionName = "Florescer",
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = "7.3",
                SourceContentHash = "hash-flourish",
            });
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 2,
                ActionId = 16014,
                ActionName = "Bloom",
                TranslatedActionName = "Florescer",
                TranslationLang = "pt-BR",
                TranslationEngine = 0,
                GameVersion = "7.3",
                SourceContentHash = "hash-bloom",
            });

            var found = ActionTooltipCacheManager.TryFindOriginalText(
                "pt-BR",
                0,
                "7.3",
                "Florescer",
                out var originalText);

            Assert.False(found);
            Assert.Equal(string.Empty, originalText);
        }
        finally
        {
            ActionTooltipCacheManager.Clear();
        }
    }
}
