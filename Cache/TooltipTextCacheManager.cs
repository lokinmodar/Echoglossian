// <copyright file="TooltipTextCacheManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian.Cache;

/// <summary>
///     Manages an in-memory cache of dedicated <see cref="TooltipText" />
///     rows so the Tooltip addon does not need SQLite lookups on its hot path.
/// </summary>
public static class TooltipTextCacheManager
{
    private static readonly ReaderWriterLockSlim CacheLock =
        new(LockRecursionPolicy.NoRecursion);
    private static readonly Dictionary<string, List<TooltipText>> Cache =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, TooltipText> ExactCache =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, List<TooltipText>> ScopeCache =
        new(StringComparer.Ordinal);
    private static bool isPreloaded;
    private static long revision;

    /// <summary>
    ///     Gets a value indicating whether the cache was preloaded from the
    ///     current session database.
    /// </summary>
    public static bool IsPreloaded
    {
        get
        {
            CacheLock.EnterReadLock();
            try
            {
                return isPreloaded;
            }
            finally
            {
                CacheLock.ExitReadLock();
            }
        }
    }

    /// <summary>
    ///     Gets the monotonically increasing cache revision.
    /// </summary>
    public static long Revision => Interlocked.Read(ref revision);

    /// <summary>
    ///     Loads all dedicated Tooltip addon rows into memory.
    /// </summary>
    /// <param name="configDir">The plugin configuration directory.</param>
    public static void Preload(string configDir)
    {
        try
        {
            using var context = new EchoglossianDbContext(configDir);
            var allRows = context.TooltipTexts
                .AsNoTracking()
                .Where(row =>
                    !string.IsNullOrWhiteSpace(row.AddonName) &&
                    !string.IsNullOrWhiteSpace(row.OriginalTextsAsText) &&
                    !string.IsNullOrWhiteSpace(row.GameVersion) &&
                    !string.IsNullOrWhiteSpace(row.SourceContentHash) &&
                    !string.IsNullOrWhiteSpace(row.TranslatedTextsAsText))
                .OrderBy(row => row.Id)
                .ToList();

            CacheLock.EnterWriteLock();
            try
            {
                Cache.Clear();
                ExactCache.Clear();
                ScopeCache.Clear();
                foreach (var row in allRows)
                {
                    IndexRecord(row);
                }

                isPreloaded = true;
                Interlocked.Increment(ref revision);
            }
            finally
            {
                CacheLock.ExitWriteLock();
            }

            PluginRuntimeLog.Debug(
                "TooltipTextCacheManager",
                $"Loaded {allRows.Count} TooltipText rows into {Cache.Count} addon buckets.");
        }
        catch (Exception ex)
        {
            CacheLock.EnterWriteLock();
            try
            {
                isPreloaded = false;
            }
            finally
            {
                CacheLock.ExitWriteLock();
            }

            PluginRuntimeLog.Error(
                "TooltipTextCacheManager",
                $"Failed to preload cache: {ex}");
        }
    }

    /// <summary>
    ///     Updates or inserts one cached Tooltip row.
    /// </summary>
    /// <param name="newRecord">The row to cache.</param>
    public static void Update(TooltipText newRecord)
    {
        if (newRecord == null ||
            string.IsNullOrWhiteSpace(newRecord.AddonName) ||
            string.IsNullOrWhiteSpace(newRecord.OriginalTextsAsText) ||
            string.IsNullOrWhiteSpace(newRecord.GameVersion) ||
            string.IsNullOrWhiteSpace(newRecord.SourceContentHash) ||
            !HasSavedTranslation(newRecord))
        {
            return;
        }

        CacheLock.EnterWriteLock();
        try
        {
            var existing = TryFindExistingCacheRow(newRecord);
            if (existing != null)
            {
                RemoveIndexedRecord(existing);
            }

            IndexRecord(newRecord);
            Interlocked.Increment(ref revision);
        }
        finally
        {
            CacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    ///     Tries to find one exact dedicated Tooltip row in memory.
    /// </summary>
    /// <param name="addonName">The addon name.</param>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The requested game version.</param>
    /// <param name="originalTextsAsText">The serialized original payload.</param>
    /// <param name="sourceContentHash">The stable source-content hash.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public static TooltipText? TryFindMatch(
        string addonName,
        TranslationReuseScope scope,
        string? gameVersion,
        string originalTextsAsText,
        string sourceContentHash)
    {
        if (string.IsNullOrWhiteSpace(addonName) ||
            string.IsNullOrWhiteSpace(scope.SourceLanguageCode) ||
            string.IsNullOrWhiteSpace(scope.TargetLanguageCode) ||
            string.IsNullOrWhiteSpace(gameVersion) ||
            string.IsNullOrWhiteSpace(originalTextsAsText) ||
            string.IsNullOrWhiteSpace(sourceContentHash))
        {
            return null;
        }

        CacheLock.EnterReadLock();
        try
        {
            if (scope.RequireMatchingEngine)
            {
                return ExactCache.TryGetValue(
                    BuildExactKey(
                        addonName,
                        scope.SourceLanguageCode,
                        scope.TargetLanguageCode,
                        scope.TranslationEngine,
                        gameVersion,
                        originalTextsAsText,
                        sourceContentHash),
                    out var exactMatch) &&
                    scope.Matches(
                        exactMatch.OriginalLang,
                        exactMatch.TranslationLang,
                        exactMatch.TranslationEngine)
                    ? exactMatch
                    : null;
            }

            return GetScopedRows(addonName, scope, gameVersion)?
                .FirstOrDefault(row =>
                    row.SourceContentHash == sourceContentHash &&
                    string.Equals(
                        row.OriginalTextsAsText,
                        originalTextsAsText,
                        StringComparison.Ordinal));
        }
        finally
        {
            CacheLock.ExitReadLock();
        }
    }

    /// <summary>
    ///     Returns cached candidate Tooltip rows for one exact lookup scope so
    ///     the runtime can recover canonical originals without touching
    ///     SQLite.
    /// </summary>
    /// <param name="addonName">The addon name.</param>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The requested game version.</param>
    /// <returns>The matching cached rows.</returns>
    public static IReadOnlyList<TooltipText> GetCandidates(
        string addonName,
        TranslationReuseScope scope,
        string? gameVersion)
    {
        if (string.IsNullOrWhiteSpace(addonName) ||
            string.IsNullOrWhiteSpace(scope.SourceLanguageCode) ||
            string.IsNullOrWhiteSpace(scope.TargetLanguageCode) ||
            string.IsNullOrWhiteSpace(gameVersion))
        {
            return [];
        }

        CacheLock.EnterReadLock();
        try
        {
            var scopedRows = GetScopedRows(addonName, scope, gameVersion);
            return scopedRows == null
                ? []
                : scopedRows
                    .OrderByDescending(static row => row.UpdatedDate ?? row.CreatedDate ?? DateTime.MinValue)
                    .ThenByDescending(static row => row.Id)
                    .ToList();
        }
        finally
        {
            CacheLock.ExitReadLock();
        }
    }

    /// <summary>
    ///     Clears the in-memory cache.
    /// </summary>
    public static void Clear()
    {
        CacheLock.EnterWriteLock();
        try
        {
            Cache.Clear();
            ExactCache.Clear();
            ScopeCache.Clear();
            isPreloaded = false;
            Interlocked.Increment(ref revision);
        }
        finally
        {
            CacheLock.ExitWriteLock();
        }

        PluginRuntimeLog.Debug(
            "TooltipTextCacheManager",
            "Cleared TooltipText cache.");
    }

    /// <summary>
    ///     Gets rows for one exact stored version and source-target-engine
    ///     scope.
    /// </summary>
    /// <param name="addonName">The addon name.</param>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="version">The exact stored game version.</param>
    /// <returns>The matching rows, or <see langword="null" />.</returns>
    private static List<TooltipText>? GetScopedRows(
        string addonName,
        TranslationReuseScope scope,
        string? version)
    {
        if (scope.RequireMatchingEngine)
        {
            if (!ScopeCache.TryGetValue(
                    BuildScopeKey(
                        addonName,
                        scope.SourceLanguageCode,
                        scope.TargetLanguageCode,
                        scope.TranslationEngine,
                        version),
                    out var strictRows))
            {
                return null;
            }

            var matchingStrictRows = strictRows
                .Where(row => scope.Matches(
                    row.OriginalLang,
                    row.TranslationLang,
                    row.TranslationEngine))
                .ToList();
            return matchingStrictRows.Count == 0 ? null : matchingStrictRows;
        }

        if (!Cache.TryGetValue(addonName, out var addonRows))
        {
            return null;
        }

        var matchingRows = addonRows
            .Where(row =>
                scope.Matches(
                    row.OriginalLang,
                    row.TranslationLang,
                    row.TranslationEngine) &&
                string.Equals(
                    row.GameVersion,
                    version,
                    StringComparison.Ordinal))
            .ToList();
        return matchingRows.Count == 0 ? null : matchingRows;
    }

    /// <summary>
    ///     Tries to find the cached row that should be replaced by the
    ///     supplied record.
    /// </summary>
    /// <param name="newRecord">The incoming row.</param>
    /// <returns>The cached row to replace, or <see langword="null" />.</returns>
    private static TooltipText? TryFindExistingCacheRow(TooltipText newRecord)
    {
        if (!Cache.TryGetValue(newRecord.AddonName!, out var rows))
        {
            return null;
        }

        return rows.FirstOrDefault(row =>
            RuntimeLanguageHelper.LanguagesMatch(
                row.OriginalLang,
                newRecord.OriginalLang) &&
            RuntimeLanguageHelper.LanguagesMatch(
                row.TranslationLang,
                newRecord.TranslationLang) &&
            row.TranslationEngine == newRecord.TranslationEngine &&
            string.Equals(
                row.GameVersion,
                newRecord.GameVersion,
                StringComparison.Ordinal) &&
            string.Equals(
                row.SourceContentHash,
                newRecord.SourceContentHash,
                StringComparison.Ordinal) &&
            string.Equals(
                row.OriginalTextsAsText,
                newRecord.OriginalTextsAsText,
                StringComparison.Ordinal));
    }

    /// <summary>
    ///     Gets the addon bucket for one Tooltip surface.
    /// </summary>
    /// <param name="addonName">The addon name.</param>
    /// <returns>The existing or newly created addon bucket.</returns>
    private static List<TooltipText> GetAddonBucket(string addonName)
    {
        if (!Cache.TryGetValue(addonName, out var rows))
        {
            rows = [];
            Cache[addonName] = rows;
        }

        return rows;
    }

    /// <summary>
    ///     Gets the scope bucket for one strict source-target-engine-version
    ///     Tooltip lookup scope.
    /// </summary>
    /// <param name="addonName">The addon name.</param>
    /// <param name="sourceLanguage">The source language.</param>
    /// <param name="targetLanguage">The target language.</param>
    /// <param name="engine">The translation engine.</param>
    /// <param name="version">The game version.</param>
    /// <returns>The existing or newly created scope bucket.</returns>
    private static List<TooltipText> GetScopeBucket(
        string addonName,
        string? sourceLanguage,
        string? targetLanguage,
        int? engine,
        string? version)
    {
        var scopeKey = BuildScopeKey(
            addonName,
            sourceLanguage,
            targetLanguage,
            engine,
            version);
        if (!ScopeCache.TryGetValue(scopeKey, out var rows))
        {
            rows = [];
            ScopeCache[scopeKey] = rows;
        }

        return rows;
    }

    /// <summary>
    ///     Adds one row to every in-memory index.
    /// </summary>
    /// <param name="record">The row to index.</param>
    private static void IndexRecord(TooltipText record)
    {
        var addonName = record.AddonName!;
        GetAddonBucket(addonName).Add(record);

        var normalizedSourceLanguage = RuntimeLanguageHelper.NormalizeLanguage(
            record.OriginalLang);
        var normalizedTargetLanguage = RuntimeLanguageHelper.NormalizeLanguage(
            record.TranslationLang);
        GetScopeBucket(
            addonName,
            normalizedSourceLanguage,
            normalizedTargetLanguage,
            record.TranslationEngine,
            record.GameVersion).Add(record);

        ExactCache[BuildExactKey(
            addonName,
            normalizedSourceLanguage,
            normalizedTargetLanguage,
            record.TranslationEngine,
            record.GameVersion,
            record.OriginalTextsAsText ?? string.Empty,
            record.SourceContentHash ?? string.Empty)] = record;
    }

    /// <summary>
    ///     Removes one row from every in-memory index.
    /// </summary>
    /// <param name="record">The row to remove.</param>
    private static void RemoveIndexedRecord(TooltipText record)
    {
        var addonName = record.AddonName!;
        GetAddonBucket(addonName).Remove(record);

        var normalizedSourceLanguage = RuntimeLanguageHelper.NormalizeLanguage(
            record.OriginalLang);
        var normalizedTargetLanguage = RuntimeLanguageHelper.NormalizeLanguage(
            record.TranslationLang);
        var scopeBucket = GetScopeBucket(
            addonName,
            normalizedSourceLanguage,
            normalizedTargetLanguage,
            record.TranslationEngine,
            record.GameVersion);
        scopeBucket.Remove(record);
        if (scopeBucket.Count == 0)
        {
            ScopeCache.Remove(
                BuildScopeKey(
                    addonName,
                    normalizedSourceLanguage,
                    normalizedTargetLanguage,
                    record.TranslationEngine,
                    record.GameVersion));
        }

        ExactCache.Remove(
            BuildExactKey(
                addonName,
                normalizedSourceLanguage,
                normalizedTargetLanguage,
                record.TranslationEngine,
                record.GameVersion,
                record.OriginalTextsAsText ?? string.Empty,
                record.SourceContentHash ?? string.Empty));
    }

    /// <summary>
    ///     Builds the stable scope key used by strict engine-aware lookups.
    /// </summary>
    /// <param name="addonName">The addon name.</param>
    /// <param name="sourceLanguage">The source language.</param>
    /// <param name="targetLanguage">The target language.</param>
    /// <param name="engine">The translation engine.</param>
    /// <param name="version">The game version.</param>
    /// <returns>The stable scope key.</returns>
    private static string BuildScopeKey(
        string addonName,
        string? sourceLanguage,
        string? targetLanguage,
        int? engine,
        string? version)
    {
        var normalizedSourceLanguage = RuntimeLanguageHelper.NormalizeLanguage(
            sourceLanguage);
        var normalizedTargetLanguage = RuntimeLanguageHelper.NormalizeLanguage(
            targetLanguage);
        var engineIdentity = engine?.ToString() ?? "null";
        return string.Join(
            '|',
            addonName,
            normalizedSourceLanguage,
            normalizedTargetLanguage,
            engineIdentity,
            version ?? string.Empty);
    }

    /// <summary>
    ///     Builds the stable exact-match key for one Tooltip payload.
    /// </summary>
    /// <param name="addonName">The addon name.</param>
    /// <param name="sourceLanguage">The source language.</param>
    /// <param name="targetLanguage">The target language.</param>
    /// <param name="engine">The translation engine.</param>
    /// <param name="version">The game version.</param>
    /// <param name="originalTextsAsText">The serialized original payload.</param>
    /// <param name="sourceContentHash">The source-content hash.</param>
    /// <returns>The stable exact-match key.</returns>
    private static string BuildExactKey(
        string addonName,
        string? sourceLanguage,
        string? targetLanguage,
        int? engine,
        string? version,
        string originalTextsAsText,
        string sourceContentHash)
    {
        return string.Join(
            '|',
            BuildScopeKey(
                addonName,
                sourceLanguage,
                targetLanguage,
                engine,
                version),
            sourceContentHash,
            originalTextsAsText);
    }

    /// <summary>
    ///     Determines whether one row contains a usable translated Tooltip
    ///     payload.
    /// </summary>
    /// <param name="row">The row to inspect.</param>
    /// <returns><c>true</c> when translated Tooltip text is present.</returns>
    private static bool HasSavedTranslation(TooltipText row)
    {
        return row != null &&
               !string.IsNullOrWhiteSpace(row.TranslatedTextsAsText);
    }
}
