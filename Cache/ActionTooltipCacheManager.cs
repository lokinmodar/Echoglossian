// <copyright file="ActionTooltipCacheManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian.Cache;

/// <summary>
///     Manages an in-memory cache of canonical <see cref="ActionTooltip" /> rows.
/// </summary>
public static class ActionTooltipCacheManager
{
    private static readonly Dictionary<uint, List<ActionTooltip>> Cache = [];

    /// <summary>
    ///     Loads all canonical action-tooltip rows into memory.
    /// </summary>
    /// <param name="configDir">The plugin configuration directory.</param>
    public static void Preload(string configDir)
    {
        try
        {
            using var context = new EchoglossianDbContext(configDir);
            var allRows = context.ActionTooltip
                .AsNoTracking()
                .Where(row => row.ActionId > 0)
                .ToList();

            Cache.Clear();
            foreach (var row in allRows)
            {
                if (!Cache.TryGetValue(row.ActionId, out var rows))
                {
                    rows = [];
                    Cache[row.ActionId] = rows;
                }

                rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error(
                $"[ActionTooltipCacheManager] Failed to preload cache: {ex}");
        }
    }

    /// <summary>
    ///     Updates or inserts one cached action-tooltip row.
    /// </summary>
    /// <param name="newRecord">The row to cache.</param>
    public static void Update(ActionTooltip newRecord)
    {
        if (newRecord == null || newRecord.ActionId == 0)
        {
            return;
        }

        if (!Cache.TryGetValue(newRecord.ActionId, out var rows))
        {
            rows = [];
            Cache[newRecord.ActionId] = rows;
        }

        var existing = rows.FirstOrDefault(row =>
            row.ActionId == newRecord.ActionId &&
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
    ///     Tries to find one canonical action-tooltip row in memory.
    /// </summary>
    /// <param name="actionId">The action row identifier.</param>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The game version.</param>
    /// <param name="sourceContentHash">The stable source-content hash.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public static ActionTooltip? TryFindCanonicalMatch(
        uint actionId,
        string lang,
        int engine,
        string? gameVersion,
        string sourceContentHash)
    {
        if (actionId == 0 ||
            string.IsNullOrWhiteSpace(lang) ||
            string.IsNullOrWhiteSpace(sourceContentHash))
        {
            return null;
        }

        if (!Cache.TryGetValue(actionId, out var rows) || rows.Count == 0)
        {
            return null;
        }

        return rows.FirstOrDefault(row =>
            row.TranslationLang == lang &&
            row.TranslationEngine == engine &&
            GameVersionLookupHelper.MatchesStoredVersion(
                row.GameVersion,
                gameVersion) &&
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
