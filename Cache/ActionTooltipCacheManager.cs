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
    private static readonly Dictionary<string, Dictionary<string, string>>
        TextLookupCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Dictionary<string, string>>
        ReverseTextLookupCache = new(StringComparer.Ordinal);

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

            TextLookupCache.Clear();
            ReverseTextLookupCache.Clear();
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
        TextLookupCache.Clear();
        ReverseTextLookupCache.Clear();
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
    ///     Tries to resolve one translated action text by exact original text
    ///     from the canonical action-tooltip cache.
    /// </summary>
    /// <param name="lang">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="originalText">The original text to translate.</param>
    /// <param name="translatedText">The resolved translated text.</param>
    /// <returns>
    ///     <see langword="true" /> when an exact translated text was found in
    ///     canonical action-tooltip storage; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryFindTranslatedText(
        string lang,
        int engine,
        string? gameVersion,
        string originalText,
        out string translatedText)
    {
        translatedText = string.Empty;

        if (string.IsNullOrWhiteSpace(lang) ||
            string.IsNullOrWhiteSpace(originalText))
        {
            return false;
        }

        if (TryFindTranslatedTextInScope(
                lang,
                engine,
                gameVersion,
                originalText,
                out translatedText))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            return TryFindTranslatedTextInScope(
                lang,
                engine,
                version: null,
                originalText,
                out translatedText);
        }

        return false;
    }

    /// <summary>
    ///     Tries to resolve one canonical original action text by exact
    ///     translated text from the cached action-tooltip rows.
    /// </summary>
    /// <param name="lang">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="translatedText">The translated text to reverse.</param>
    /// <param name="originalText">The resolved canonical original text.</param>
    /// <returns>
    ///     <see langword="true" /> when one exact canonical original text was
    ///     found; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryFindOriginalText(
        string lang,
        int engine,
        string? gameVersion,
        string translatedText,
        out string originalText)
    {
        originalText = string.Empty;

        if (string.IsNullOrWhiteSpace(lang) ||
            string.IsNullOrWhiteSpace(translatedText))
        {
            return false;
        }

        if (TryFindOriginalTextInScope(
                lang,
                engine,
                gameVersion,
                translatedText,
                out originalText))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            return TryFindOriginalTextInScope(
                lang,
                engine,
                version: null,
                translatedText,
                out originalText);
        }

        return false;
    }

    /// <summary>
    ///     Clears the in-memory cache.
    /// </summary>
    public static void Clear()
    {
        Cache.Clear();
        TextLookupCache.Clear();
        ReverseTextLookupCache.Clear();
        PluginLog.Debug("[ActionTooltipCacheManager] Cleared action-tooltip cache.");
    }

    /// <summary>
    ///     Tries to resolve one translated action text from one cached scope.
    /// </summary>
    /// <param name="lang">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="version">The exact stored game version scope.</param>
    /// <param name="originalText">The original text to translate.</param>
    /// <param name="translatedText">The translated text.</param>
    /// <returns>
    ///     <see langword="true" /> when a translated text was found in this
    ///     exact scope; otherwise <see langword="false" />.
    /// </returns>
    private static bool TryFindTranslatedTextInScope(
        string lang,
        int engine,
        string? version,
        string originalText,
        out string translatedText)
    {
        translatedText = string.Empty;

        var scopeKey = BuildTextLookupScopeKey(lang, engine, version);
        if (!TextLookupCache.TryGetValue(scopeKey, out var lookup))
        {
            lookup = BuildTextLookup(lang, engine, version);
            TextLookupCache[scopeKey] = lookup;
        }

        var found = lookup.TryGetValue(originalText, out var resolvedText);
        translatedText = resolvedText ?? string.Empty;
        return found;
    }

    /// <summary>
    ///     Tries to resolve one canonical original action text from one cached
    ///     reverse lookup scope.
    /// </summary>
    /// <param name="lang">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="version">The exact stored game version scope.</param>
    /// <param name="translatedText">The translated text to reverse.</param>
    /// <param name="originalText">The resolved original text.</param>
    /// <returns>
    ///     <see langword="true" /> when a canonical original text was found in
    ///     this exact scope; otherwise <see langword="false" />.
    /// </returns>
    private static bool TryFindOriginalTextInScope(
        string lang,
        int engine,
        string? version,
        string translatedText,
        out string originalText)
    {
        originalText = string.Empty;

        var scopeKey = BuildTextLookupScopeKey(lang, engine, version);
        if (!ReverseTextLookupCache.TryGetValue(scopeKey, out var lookup))
        {
            lookup = BuildReverseTextLookup(lang, engine, version);
            ReverseTextLookupCache[scopeKey] = lookup;
        }

        var found = lookup.TryGetValue(translatedText, out var resolvedText);
        originalText = resolvedText ?? string.Empty;
        return found;
    }

    /// <summary>
    ///     Builds one translated-text lookup for a single action-tooltip cache
    ///     scope.
    /// </summary>
    /// <param name="lang">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="version">The exact stored game version scope.</param>
    /// <returns>The translated-text lookup map.</returns>
    private static Dictionary<string, string> BuildTextLookup(
        string lang,
        int engine,
        string? version)
    {
        var lookup = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var row in Cache.Values.SelectMany(static rows => rows))
        {
            if (!RuntimeLanguageHelper.LanguagesMatch(
                    row.TranslationLang,
                    lang) ||
                row.TranslationEngine != engine ||
                !string.Equals(
                    row.GameVersion,
                    version,
                    StringComparison.Ordinal))
            {
                continue;
            }

            TryAddLookupValue(
                lookup,
                row.ActionName,
                row.TranslatedActionName);
            TryAddLookupValue(
                lookup,
                row.ActionDescription,
                row.TranslatedActionDescription);
            TryAddLookupValue(
                lookup,
                row.OriginalTooltipText,
                row.TranslatedTooltipText);
        }

        return lookup;
    }

    /// <summary>
    ///     Builds one reverse translated-text lookup for a single
    ///     action-tooltip cache scope.
    /// </summary>
    /// <param name="lang">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="version">The exact stored game version scope.</param>
    /// <returns>The reverse translated-text lookup map.</returns>
    private static Dictionary<string, string> BuildReverseTextLookup(
        string lang,
        int engine,
        string? version)
    {
        var lookup = new Dictionary<string, string>(StringComparer.Ordinal);
        var ambiguousKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in Cache.Values.SelectMany(static rows => rows))
        {
            if (!RuntimeLanguageHelper.LanguagesMatch(
                    row.TranslationLang,
                    lang) ||
                row.TranslationEngine != engine ||
                !string.Equals(
                    row.GameVersion,
                    version,
                    StringComparison.Ordinal))
            {
                continue;
            }

            TryAddReverseLookupValue(
                lookup,
                ambiguousKeys,
                row.ActionName,
                row.TranslatedActionName);
            TryAddReverseLookupValue(
                lookup,
                ambiguousKeys,
                row.ActionDescription,
                row.TranslatedActionDescription);
            TryAddReverseLookupValue(
                lookup,
                ambiguousKeys,
                row.OriginalTooltipText,
                row.TranslatedTooltipText);
        }

        return lookup;
    }

    /// <summary>
    ///     Adds one translated-text lookup entry when both texts are usable.
    /// </summary>
    /// <param name="lookup">The lookup map to update.</param>
    /// <param name="originalText">The original text.</param>
    /// <param name="translatedText">The translated text.</param>
    private static void TryAddLookupValue(
        IDictionary<string, string> lookup,
        string? originalText,
        string? translatedText)
    {
        if (string.IsNullOrWhiteSpace(originalText) ||
            string.IsNullOrWhiteSpace(translatedText) ||
            string.Equals(
                originalText,
                translatedText,
                StringComparison.Ordinal))
        {
            return;
        }

        lookup.TryAdd(originalText, translatedText);
    }

    /// <summary>
    ///     Adds one reverse translated-text lookup entry when the translated
    ///     text maps uniquely back to a single canonical original text.
    /// </summary>
    /// <param name="lookup">The reverse lookup map to update.</param>
    /// <param name="ambiguousKeys">
    ///     Tracks translated texts that map to multiple originals and must be
    ///     excluded from reverse recovery.
    /// </param>
    /// <param name="originalText">The canonical original text.</param>
    /// <param name="translatedText">The translated text.</param>
    private static void TryAddReverseLookupValue(
        IDictionary<string, string> lookup,
        ISet<string> ambiguousKeys,
        string? originalText,
        string? translatedText)
    {
        if (string.IsNullOrWhiteSpace(originalText) ||
            string.IsNullOrWhiteSpace(translatedText) ||
            string.Equals(
                originalText,
                translatedText,
                StringComparison.Ordinal) ||
            ambiguousKeys.Contains(translatedText))
        {
            return;
        }

        if (lookup.TryGetValue(translatedText, out var existingOriginal))
        {
            if (!string.Equals(
                    existingOriginal,
                    originalText,
                    StringComparison.Ordinal))
            {
                lookup.Remove(translatedText);
                ambiguousKeys.Add(translatedText);
            }

            return;
        }

        lookup[translatedText] = originalText;
    }

    /// <summary>
    ///     Builds one stable scope key for translated-text lookups.
    /// </summary>
    /// <param name="lang">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="version">The exact stored game version scope.</param>
    /// <returns>The stable scope key.</returns>
    private static string BuildTextLookupScopeKey(
        string lang,
        int engine,
        string? version)
    {
        return $"{lang}|{engine}|{version ?? string.Empty}";
    }
}
