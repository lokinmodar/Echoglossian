// <copyright file="StringArrayDataCacheManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian.Cache;

/// <summary>
///     Manages an in-memory cache of canonical <see cref="StringArrayDatas" />
///     rows so DB-first string-array runtimes do not need to query SQLite on
///     every lifecycle event.
/// </summary>
public static class StringArrayDataCacheManager
{
    private static readonly Dictionary<string, List<StringArrayDatas>> Cache =
        new(StringComparer.Ordinal);

    /// <summary>
    ///     Loads all canonical <see cref="StringArrayDatas" /> rows into memory.
    /// </summary>
    /// <param name="configDir">The plugin configuration directory.</param>
    public static void Preload(string configDir)
    {
        try
        {
            using var context = new EchoglossianDbContext(configDir);
            var allRows = context.StringArrayDatas
                .AsNoTracking()
                .Where(row => !string.IsNullOrWhiteSpace(row.Type))
                .ToList();

            Cache.Clear();
            foreach (var row in allRows)
            {
                var typeKey = row.Type!;
                if (!Cache.TryGetValue(typeKey, out var rows))
                {
                    rows = [];
                    Cache[typeKey] = rows;
                }

                rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(
                $"[StringArrayDataCacheManager] Failed to preload cache: {ex}");
        }
    }

    /// <summary>
    ///     Updates or inserts one cached canonical row.
    /// </summary>
    /// <param name="newRecord">The row to cache.</param>
    public static void Update(StringArrayDatas newRecord)
    {
        if (newRecord == null || string.IsNullOrWhiteSpace(newRecord.Type))
        {
            return;
        }

        var typeKey = newRecord.Type;
        if (!Cache.TryGetValue(typeKey, out var rows))
        {
            rows = [];
            Cache[typeKey] = rows;
        }

        var existing = rows.FirstOrDefault(row =>
            row.ContextKey == newRecord.ContextKey &&
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
    ///     Attempts to resolve one canonical row from the in-memory cache.
    /// </summary>
    /// <param name="type">The logical payload type.</param>
    /// <param name="contextKey">The semantic surface context.</param>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation engine id.</param>
    /// <param name="gameVersion">The game version.</param>
    /// <param name="sourceContentHash">The stable source payload hash.</param>
    /// <returns>The matching cached row, or <see langword="null" />.</returns>
    public static StringArrayDatas? TryFindCanonicalMatch(
        string type,
        string contextKey,
        string lang,
        int engine,
        string? gameVersion,
        string sourceContentHash)
    {
        if (string.IsNullOrWhiteSpace(type) ||
            string.IsNullOrWhiteSpace(contextKey) ||
            string.IsNullOrWhiteSpace(lang) ||
            string.IsNullOrWhiteSpace(sourceContentHash))
        {
            return null;
        }

        if (!Cache.TryGetValue(type, out var rows) || rows.Count == 0)
        {
            return null;
        }

        return rows.FirstOrDefault(row =>
            row.ContextKey == contextKey &&
            row.TranslationLang == lang &&
            row.TranslationEngine == engine &&
            GameVersionLookupHelper.MatchesStoredVersion(
                row.GameVersion,
                gameVersion) &&
            row.SourceContentHash == sourceContentHash);
    }

    /// <summary>
    ///     Returns cached candidates for one canonical lookup scope so runtimes
    ///     can recover original payloads from already-translated live UI.
    /// </summary>
    /// <param name="type">The logical payload type.</param>
    /// <param name="contextKey">The semantic surface context.</param>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation engine id.</param>
    /// <param name="gameVersion">The game version.</param>
    /// <returns>The matching cached rows.</returns>
    public static IReadOnlyList<StringArrayDatas> GetCandidates(
        string type,
        string contextKey,
        string lang,
        int engine,
        string? gameVersion)
    {
        if (string.IsNullOrWhiteSpace(type) ||
            string.IsNullOrWhiteSpace(contextKey) ||
            string.IsNullOrWhiteSpace(lang))
        {
            return [];
        }

        if (!Cache.TryGetValue(type, out var rows) || rows.Count == 0)
        {
            return [];
        }

        return rows
            .Where(row =>
                row.ContextKey == contextKey &&
                row.TranslationLang == lang &&
                row.TranslationEngine == engine &&
                GameVersionLookupHelper.MatchesStoredVersion(
                    row.GameVersion,
                    gameVersion))
            .ToList();
    }

    /// <summary>
    ///     Clears the in-memory cache.
    /// </summary>
    public static void Clear()
    {
        Cache.Clear();
        PluginRuntimeLog.Debug("[StringArrayDataCacheManager] Cleared StringArrayData cache.");
    }
}


