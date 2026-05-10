// <copyright file="ItemTooltipCacheManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian.Cache;

/// <summary>
///     Manages an in-memory cache of canonical <see cref="ItemTooltip" /> rows.
/// </summary>
public static class ItemTooltipCacheManager
{
    private static readonly Dictionary<uint, List<ItemTooltip>> Cache = [];

    /// <summary>
    ///     Loads all canonical item-tooltip rows into memory.
    /// </summary>
    /// <param name="configDir">The plugin configuration directory.</param>
    public static void Preload(string configDir)
    {
        try
        {
            using var context = new EchoglossianDbContext(configDir);
            var allRows = context.ItemTooltip
                .AsNoTracking()
                .Where(row => row.ItemId > 0)
                .ToList();

            Cache.Clear();
            foreach (var row in allRows)
            {
                if (!Cache.TryGetValue(row.ItemId, out var rows))
                {
                    rows = [];
                    Cache[row.ItemId] = rows;
                }

                rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(
                "ItemTooltipCacheManager",
                $"Failed to preload cache: {ex}");
        }
    }

    /// <summary>
    ///     Updates or inserts one cached item-tooltip row.
    /// </summary>
    /// <param name="newRecord">The row to cache.</param>
    public static void Update(ItemTooltip newRecord)
    {
        if (newRecord == null || newRecord.ItemId == 0)
        {
            return;
        }

        if (!Cache.TryGetValue(newRecord.ItemId, out var rows))
        {
            rows = [];
            Cache[newRecord.ItemId] = rows;
        }

        var existing = rows.FirstOrDefault(row =>
            row.ItemId == newRecord.ItemId &&
            row.TranslationLang == newRecord.TranslationLang &&
            row.TranslationEngine == newRecord.TranslationEngine &&
            GameVersionLookupHelper.MatchesStoredVersion(
                row.GameVersion,
                newRecord.GameVersion) &&
            row.SourceContentHash == newRecord.SourceContentHash);
        if (existing != null)
        {
            rows.Remove(existing);
        }

        rows.Add(newRecord);
    }

    /// <summary>
    ///     Tries to find one canonical item-tooltip row in memory.
    /// </summary>
    /// <param name="itemId">The item row identifier.</param>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The game version.</param>
    /// <param name="sourceContentHash">The stable source-content hash.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public static ItemTooltip? TryFindCanonicalMatch(
        uint itemId,
        string lang,
        int engine,
        string? gameVersion,
        string sourceContentHash)
    {
        if (itemId == 0 ||
            string.IsNullOrWhiteSpace(lang) ||
            string.IsNullOrWhiteSpace(sourceContentHash))
        {
            return null;
        }

        if (!Cache.TryGetValue(itemId, out var rows) || rows.Count == 0)
        {
            return null;
        }

        return rows
            .Where(row =>
                RuntimeLanguageHelper.LanguagesMatch(
                    row.TranslationLang,
                    lang) &&
                row.TranslationEngine == engine &&
                GameVersionLookupHelper.MatchesStoredVersion(
                    row.GameVersion,
                    gameVersion) &&
                row.SourceContentHash == sourceContentHash)
            .OrderByDescending(row => ComputeCanonicalMatchScore(
                row,
                gameVersion))
            .ThenByDescending(row => row.UpdatedDate)
            .FirstOrDefault();
    }

    /// <summary>
    ///     Tries to find one historical version-specific canonical
    ///     item-tooltip row whose source hash still matches the current
    ///     payload.
    /// </summary>
    /// <param name="itemId">The item row identifier.</param>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="requestedGameVersion">The current game version.</param>
    /// <param name="sourceContentHash">The stable source-content hash.</param>
    /// <returns>The best matching historical row, or <see langword="null" />.</returns>
    public static ItemTooltip? TryFindHistoricalCanonicalMatch(
        uint itemId,
        string lang,
        int engine,
        string? requestedGameVersion,
        string sourceContentHash)
    {
        if (itemId == 0 ||
            string.IsNullOrWhiteSpace(lang) ||
            string.IsNullOrWhiteSpace(requestedGameVersion) ||
            string.IsNullOrWhiteSpace(sourceContentHash))
        {
            return null;
        }

        if (!Cache.TryGetValue(itemId, out var rows) || rows.Count == 0)
        {
            return null;
        }

        return rows
            .Where(row =>
                RuntimeLanguageHelper.LanguagesMatch(
                    row.TranslationLang,
                    lang) &&
                row.TranslationEngine == engine &&
                row.SourceContentHash == sourceContentHash &&
                !string.IsNullOrWhiteSpace(row.GameVersion) &&
                !string.Equals(
                    row.GameVersion,
                    requestedGameVersion,
                    StringComparison.Ordinal))
            .OrderByDescending(static row => HasAnyTranslatedContent(row))
            .ThenByDescending(row => row.UpdatedDate)
            .FirstOrDefault();
    }

    /// <summary>
    ///     Tries to find the best translated item-tooltip row by stable
    ///     identity when the stricter canonical hash does not match.
    /// </summary>
    /// <param name="itemId">The item row identifier.</param>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="classJobCategoryId">
    ///     The preferred class-job-category identifier.
    /// </param>
    /// <returns>The best translated row, or <see langword="null" />.</returns>
    public static ItemTooltip? TryFindIdentityMatch(
        uint itemId,
        string lang,
        int engine,
        string? gameVersion,
        uint classJobCategoryId)
    {
        if (itemId == 0 || string.IsNullOrWhiteSpace(lang))
        {
            return null;
        }

        if (!Cache.TryGetValue(itemId, out var rows) || rows.Count == 0)
        {
            return null;
        }

        return rows
            .Where(row =>
                RuntimeLanguageHelper.LanguagesMatch(
                    row.TranslationLang,
                    lang) &&
                row.TranslationEngine == engine &&
                GameVersionLookupHelper.MatchesStoredVersion(
                    row.GameVersion,
                    gameVersion))
            .OrderByDescending(row => ComputeIdentityMatchScore(
                row,
                gameVersion,
                classJobCategoryId))
            .ThenByDescending(row => row.UpdatedDate)
            .FirstOrDefault();
    }

    /// <summary>
    ///     Clears the in-memory cache.
    /// </summary>
    public static void Clear()
    {
        Cache.Clear();
        PluginRuntimeLog.Debug(
            "ItemTooltipCacheManager",
            "Cleared item-tooltip cache.");
    }

    /// <summary>
    ///     Computes one ordering score for tolerant identity-based item
    ///     tooltip lookup.
    /// </summary>
    /// <param name="row">The candidate row.</param>
    /// <param name="gameVersion">The requested game version.</param>
    /// <param name="classJobCategoryId">
    ///     The preferred class-job-category identifier.
    /// </param>
    /// <returns>The ordering score.</returns>
    private static int ComputeIdentityMatchScore(
        ItemTooltip row,
        string? gameVersion,
        uint classJobCategoryId)
    {
        var score = 0;

        if (HasCompleteTranslation(row))
        {
            score += 10_000;
        }

        if (!string.IsNullOrWhiteSpace(gameVersion) &&
            string.Equals(
                row.GameVersion,
                gameVersion,
                StringComparison.Ordinal))
        {
            score += 1_000;
        }

        if (row.GameVersion == null)
        {
            score += 100;
        }

        if (classJobCategoryId != 0 &&
            row.ClassJobCategoryId == classJobCategoryId)
        {
            score += 25;
        }

        return score;
    }

    /// <summary>
    ///     Computes one ordering score for exact source-hash matches so fully
    ///     translated reusable rows beat current-version placeholders.
    /// </summary>
    /// <param name="row">The candidate row.</param>
    /// <param name="gameVersion">The requested game version.</param>
    /// <returns>The ordering score.</returns>
    private static int ComputeCanonicalMatchScore(
        ItemTooltip row,
        string? gameVersion)
    {
        var score = 0;

        if (HasCompleteTranslation(row))
        {
            score += 10_000;
        }
        else if (HasAnyTranslatedContent(row))
        {
            score += 1_000;
        }

        if (!string.IsNullOrWhiteSpace(gameVersion) &&
            string.Equals(
                row.GameVersion,
                gameVersion,
                StringComparison.Ordinal))
        {
            score += 100;
        }

        if (string.IsNullOrWhiteSpace(row.GameVersion))
        {
            score += 10;
        }

        return score;
    }

    /// <summary>
    ///     Gets whether the row carries any translated canonical payload that
    ///     can be promoted to a newer game version.
    /// </summary>
    /// <param name="row">The candidate row.</param>
    /// <returns>True when the row contains translated content.</returns>
    private static bool HasAnyTranslatedContent(ItemTooltip row)
    {
        return !string.IsNullOrWhiteSpace(row.TranslatedItemName) ||
               !string.IsNullOrWhiteSpace(row.TranslatedItemDescription) ||
               !string.IsNullOrWhiteSpace(row.TranslatedTooltipText);
    }

    /// <summary>
    ///     Gets whether one item-tooltip row contains every translated field
    ///     required by the live tooltip runtime.
    /// </summary>
    /// <param name="row">The candidate row.</param>
    /// <returns>
    ///     <see langword="true" /> when the row contains a translated name and
    ///     any required translated description; otherwise
    ///     <see langword="false" />.
    /// </returns>
    private static bool HasCompleteTranslation(ItemTooltip row)
    {
        return !string.IsNullOrWhiteSpace(row.TranslatedItemName) &&
               (string.IsNullOrWhiteSpace(row.ItemDescription) ||
                !string.IsNullOrWhiteSpace(
                    row.TranslatedItemDescription));
    }
}
