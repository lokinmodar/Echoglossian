// <copyright file="TranslationFailureCacheManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian.Cache;

/// <summary>
///     Manages an in-memory cache of exact translation failures so the plugin
///     does not keep re-sending identical known-failing text requests.
/// </summary>
public static class TranslationFailureCacheManager
{
    private static readonly Dictionary<string, List<TranslationFailure>> Cache =
        new(StringComparer.Ordinal);

    /// <summary>
    ///     Loads all persisted translation-failure rows into memory.
    /// </summary>
    /// <param name="configDir">The plugin configuration directory.</param>
    public static void Preload(string configDir)
    {
        try
        {
            using var context = new EchoglossianDbContext(configDir);
            var allRows = context.Set<TranslationFailure>()
                .AsNoTracking()
                .Where(row => !string.IsNullOrWhiteSpace(row.SourceTextHash))
                .AsEnumerable()
                .Where(row =>
                    TranslationPersistenceGuard.IsPersistentFailureReason(
                        row.FailureReason))
                .ToList();

            Cache.Clear();
            foreach (var row in allRows)
            {
                IndexRecord(row);
            }
        }
        catch (Exception ex)
        {
            Cache.Clear();
            PluginRuntimeLog.Error(
                $"[TranslationFailureCacheManager] Failed to preload cache: {ex}");
        }
    }

    /// <summary>
    ///     Updates or inserts one cached translation-failure row.
    /// </summary>
    /// <param name="newRecord">The row to cache.</param>
    public static void Update(TranslationFailure newRecord)
    {
        if (newRecord == null ||
            string.IsNullOrWhiteSpace(newRecord.SourceText) ||
            string.IsNullOrWhiteSpace(newRecord.SourceTextHash))
        {
            return;
        }

        var bucket = GetBucket(
            newRecord.SourceTextHash,
            newRecord.SourceLanguage,
            newRecord.TargetLanguage,
            newRecord.TranslationEngine);
        var existing = bucket.FirstOrDefault(row =>
            string.Equals(
                row.SourceText,
                newRecord.SourceText,
                StringComparison.Ordinal));
        if (existing != null)
        {
            bucket.Remove(existing);
        }

        bucket.Add(newRecord);
    }

    /// <summary>
    ///     Determines whether one exact translation request is already known to
    ///     fail for the given source/target language pair and engine.
    /// </summary>
    /// <param name="sourceText">The exact sanitized source text.</param>
    /// <param name="sourceLanguage">The source language code.</param>
    /// <param name="targetLanguage">The target language code.</param>
    /// <param name="translationEngine">The translation engine identifier.</param>
    /// <returns>
    ///     <see langword="true" /> when the exact request is already cached as
    ///     a known failure; otherwise <see langword="false" />.
    /// </returns>
    public static bool Contains(
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        int translationEngine)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return false;
        }

        var hash = TranslationFailureKey.ComputeSourceTextHash(sourceText);
        var lookupKey = TranslationFailureKey.BuildLookupKey(
            hash,
            sourceLanguage,
            targetLanguage,
            translationEngine);
        if (!Cache.TryGetValue(lookupKey, out var rows) || rows.Count == 0)
        {
            return false;
        }

        return rows.Any(row =>
            TranslationPersistenceGuard.IsPersistentFailureReason(
                row.FailureReason) &&
            string.Equals(
                row.SourceText,
                sourceText,
                StringComparison.Ordinal));
    }

    /// <summary>
    ///     Clears the in-memory cache.
    /// </summary>
    public static void Clear()
    {
        Cache.Clear();
        PluginRuntimeLog.Debug("[TranslationFailureCacheManager] Cleared translation-failure cache.");
    }

    private static void IndexRecord(TranslationFailure record)
    {
        GetBucket(
            record.SourceTextHash ?? string.Empty,
            record.SourceLanguage,
            record.TargetLanguage,
            record.TranslationEngine).Add(record);
    }

    private static List<TranslationFailure> GetBucket(
        string sourceTextHash,
        string? sourceLanguage,
        string? targetLanguage,
        int translationEngine)
    {
        var key = TranslationFailureKey.BuildLookupKey(
            sourceTextHash,
            sourceLanguage,
            targetLanguage,
            translationEngine);
        if (!Cache.TryGetValue(key, out var rows))
        {
            rows = [];
            Cache[key] = rows;
        }

        return rows;
    }
}


