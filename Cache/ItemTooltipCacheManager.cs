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
            PluginLog.Error(
                $"[ItemTooltipCacheManager] Failed to preload cache: {ex}");
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
            row.GameVersion == newRecord.GameVersion &&
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

        return rows.FirstOrDefault(row =>
            row.TranslationLang == lang &&
            row.TranslationEngine == engine &&
            row.GameVersion == gameVersion &&
            row.SourceContentHash == sourceContentHash);
    }

    /// <summary>
    ///     Clears the in-memory cache.
    /// </summary>
    public static void Clear()
    {
        Cache.Clear();
    }
}
