// <copyright file="StringArrayDataPersistenceHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.Helpers;

using Microsoft.EntityFrameworkCore;

namespace Echoglossian;

/// <summary>
///     Persists <see cref="StringArrayDatas" /> rows using the canonical
///     DB-first lookup contract.
/// </summary>
public static class StringArrayDataPersistenceHelper
{
    /// <summary>
    ///     Creates a canonical <see cref="StringArrayDatas" /> row from
    ///     structured payloads.
    /// </summary>
    /// <param name="type">The logical string-array type or family.</param>
    /// <param name="originalLang">The language of the original payload.</param>
    /// <param name="translationLang">The target translation language.</param>
    /// <param name="translationEngine">The translation engine identifier.</param>
    /// <param name="gameVersion">The game version associated with the payload.</param>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="translatedPayload">The translated canonical payload, if available.</param>
    /// <returns>The canonical DB row.</returns>
    public static StringArrayDatas CreateCanonicalRow(
        string type,
        string originalLang,
        string translationLang,
        int? translationEngine,
        string? gameVersion,
        StringArrayStructuredPayload originalPayload,
        StringArrayStructuredPayload? translatedPayload = null)
    {
        ArgumentNullException.ThrowIfNull(originalPayload);

        return new StringArrayDatas(
            type: type,
            size: originalPayload.Slots.Count,
            rawData: null,
            formattedRawData: null,
            originalLang: originalLang,
            originalStrings: JsonConvert.SerializeObject(
                originalPayload.Slots.ToDictionary(
                    pair => pair.Key.ToString(CultureInfo.InvariantCulture),
                    pair => pair.Value.OriginalText)),
            translationLang: translationLang,
            translatedStrings: translatedPayload == null
                ? null
                : JsonConvert.SerializeObject(
                    translatedPayload.Slots
                        .Where(pair => !string.IsNullOrWhiteSpace(pair.Value.TranslatedText))
                        .ToDictionary(
                            pair => pair.Key.ToString(CultureInfo.InvariantCulture),
                            pair => pair.Value.TranslatedText)),
            translatedStringsWithPayloads: null,
            translationEngine: translationEngine,
            gameVersion: gameVersion,
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow)
        {
            ContextKey = originalPayload.ContextKey,
            SchemaVersion = originalPayload.SchemaVersion,
            SourceContentHash = originalPayload.ComputeSourceContentHash(),
            OriginalStructuredPayload = originalPayload.Serialize(),
            TranslatedStructuredPayload = translatedPayload?.Serialize(),
        };
    }

    /// <summary>
    ///     Inserts or updates a <see cref="StringArrayDatas" /> row using the
    ///     canonical lookup scope when available, and a legacy formatted-raw-data
    ///     fallback during transition.
    /// </summary>
    /// <param name="configDirectory">The plugin config directory containing the SQLite database.</param>
    /// <param name="stringArrayData">The string-array payload to persist.</param>
    /// <returns>A status message describing the result.</returns>
    public static string InsertStringArrayData(
        string configDirectory,
        StringArrayDatas stringArrayData)
    {
        using var context = new EchoglossianDbContext(configDirectory);

        try
        {
            if (!IsValidForPersistence(stringArrayData))
            {
                return "Invalid data.";
            }

            var existing = BuildLookupQuery(
                    context.StringArrayDatas,
                    stringArrayData)
                .FirstOrDefault();
            if (existing != null)
            {
                MergeValues(existing, stringArrayData);
                existing.UpdatedAt = DateTime.UtcNow;

                context.StringArrayDatas.Update(existing);
                context.SaveChanges();

                return "Record updated.";
            }

            stringArrayData.CreatedAt = DateTime.UtcNow;
            stringArrayData.UpdatedAt = DateTime.UtcNow;

            context.StringArrayDatas.Add(stringArrayData);
            context.SaveChanges();

            return "New record inserted.";
        }
        catch (Exception ex)
        {
            return $"Error inserting StringArrayData: {ex.Message}";
        }
    }

    /// <summary>
    ///     Finds a <see cref="StringArrayDatas" /> row using the same canonical
    ///     lookup contract used by persistence.
    /// </summary>
    /// <param name="configDirectory">The plugin config directory containing the SQLite database.</param>
    /// <param name="probe">The probe payload that defines the lookup scope.</param>
    /// <returns>The matching row, or <see langword="null" /> when not found.</returns>
    public static StringArrayDatas? FindStringArrayData(
        string configDirectory,
        StringArrayDatas probe)
    {
        if (probe == null)
        {
            return null;
        }

        if (!HasCanonicalScope(probe))
        {
            using var context = new EchoglossianDbContext(configDirectory);
            try
            {
                return IsValidForPersistence(probe)
                    ? BuildLookupQuery(
                            context.StringArrayDatas.AsNoTracking(),
                            probe)
                        .FirstOrDefault()
                    : null;
            }
            catch
            {
                return null;
            }
        }

        return FindStringArrayData(
            configDirectory,
            probe,
            new TranslationReuseScope(
                probe.OriginalLang ?? string.Empty,
                probe.TranslationLang ?? string.Empty,
                probe.TranslationEngine,
                true));
    }

    /// <summary>
    ///     Finds a <see cref="StringArrayDatas" /> row using an explicit
    ///     translation reuse scope.
    /// </summary>
    /// <param name="configDirectory">The plugin config directory containing the SQLite database.</param>
    /// <param name="probe">The probe payload that defines content and version identity.</param>
    /// <param name="scope">The source, target, and engine reuse policy.</param>
    /// <returns>The matching row, or <see langword="null" /> when not found.</returns>
    public static StringArrayDatas? FindStringArrayData(
        string configDirectory,
        StringArrayDatas probe,
        TranslationReuseScope scope)
    {
        using var context = new EchoglossianDbContext(configDirectory);

        try
        {
            if (!IsValidForPersistence(probe))
            {
                return null;
            }

            return BuildReuseLookupQuery(
                    context.StringArrayDatas.AsNoTracking(),
                    probe,
                    scope)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Builds a policy-aware lookup query for one row.
    /// </summary>
    /// <param name="rows">The source query.</param>
    /// <param name="row">The row that defines content and version identity.</param>
    /// <param name="scope">The source, target, and engine reuse policy.</param>
    /// <returns>The policy-compatible lookup query.</returns>
    private static IEnumerable<StringArrayDatas> BuildReuseLookupQuery(
        IQueryable<StringArrayDatas> rows,
        StringArrayDatas row,
        TranslationReuseScope scope)
    {
        var hasRequestedGameVersion =
            GameVersionLookupHelper.HasRequestedVersion(row.GameVersion);
        var matchingRows = HasCanonicalScope(row)
            ? rows.Where(existing =>
                existing.Type == row.Type &&
                existing.ContextKey == row.ContextKey &&
                ((!hasRequestedGameVersion && existing.GameVersion == null) ||
                 (hasRequestedGameVersion &&
                  (existing.GameVersion == null ||
                   existing.GameVersion == row.GameVersion))) &&
                existing.SourceContentHash == row.SourceContentHash)
            : rows.Where(existing =>
                existing.Type == row.Type &&
                ((!hasRequestedGameVersion && existing.GameVersion == null) ||
                 (hasRequestedGameVersion &&
                  (existing.GameVersion == null ||
                   existing.GameVersion == row.GameVersion))) &&
                existing.FormattedRawData == row.FormattedRawData);

        return matchingRows
            .AsEnumerable()
            .Where(existing => scope.Matches(
                existing.OriginalLang,
                existing.TranslationLang,
                existing.TranslationEngine));
    }

    /// <summary>
    ///     Determines whether the payload contains enough identity to be
    ///     persisted safely.
    /// </summary>
    /// <param name="row">The row to validate.</param>
    /// <returns>True when the row has a safe persistence identity.</returns>
    private static bool IsValidForPersistence(StringArrayDatas row)
    {
        if (row == null ||
            string.IsNullOrWhiteSpace(row.Type) ||
            string.IsNullOrWhiteSpace(row.TranslationLang))
        {
            return false;
        }

        return HasCanonicalScope(row)
            ? !string.IsNullOrWhiteSpace(row.OriginalLang)
            : !string.IsNullOrWhiteSpace(row.FormattedRawData);
    }

    /// <summary>
    ///     Builds the lookup query for one row.
    /// </summary>
    /// <param name="rows">The source query.</param>
    /// <param name="row">The row that defines the lookup scope.</param>
    /// <returns>The lookup query.</returns>
    private static IEnumerable<StringArrayDatas> BuildLookupQuery(
        IQueryable<StringArrayDatas> rows,
        StringArrayDatas row)
    {
        var hasRequestedGameVersion =
            GameVersionLookupHelper.HasRequestedVersion(row.GameVersion);

        if (HasCanonicalScope(row))
        {
            return rows
                .Where(existing =>
                    existing.Type == row.Type &&
                    existing.ContextKey == row.ContextKey &&
                    existing.TranslationLang == row.TranslationLang &&
                    existing.TranslationEngine == row.TranslationEngine &&
                    ((!hasRequestedGameVersion && existing.GameVersion == null) ||
                     (hasRequestedGameVersion &&
                      (existing.GameVersion == null ||
                       existing.GameVersion == row.GameVersion))) &&
                    existing.SourceContentHash == row.SourceContentHash)
                .AsEnumerable()
                .Where(existing => RuntimeLanguageHelper.LanguagesMatch(
                    existing.OriginalLang,
                    row.OriginalLang));
        }

        return rows
            .Where(existing =>
                existing.Type == row.Type &&
                existing.TranslationLang == row.TranslationLang &&
                existing.TranslationEngine == row.TranslationEngine &&
                ((!hasRequestedGameVersion && existing.GameVersion == null) ||
                 (hasRequestedGameVersion &&
                  (existing.GameVersion == null ||
                   existing.GameVersion == row.GameVersion))) &&
                existing.FormattedRawData == row.FormattedRawData)
            .AsEnumerable()
            .Where(existing => LegacySourceLanguagesMatch(
                existing.OriginalLang,
                row.OriginalLang));
    }

    /// <summary>
    ///     Compares legacy source identities while preserving matching
    ///     source-less legacy rows.
    /// </summary>
    /// <param name="storedSource">The stored legacy source identity.</param>
    /// <param name="requestedSource">The incoming legacy source identity.</param>
    /// <returns>Whether the legacy source identities match.</returns>
    private static bool LegacySourceLanguagesMatch(
        string? storedSource,
        string? requestedSource)
    {
        if (string.IsNullOrWhiteSpace(storedSource) ||
            string.IsNullOrWhiteSpace(requestedSource))
        {
            return string.IsNullOrWhiteSpace(storedSource) &&
                   string.IsNullOrWhiteSpace(requestedSource);
        }

        return RuntimeLanguageHelper.LanguagesMatch(
            storedSource,
            requestedSource);
    }

    /// <summary>
    ///     Determines whether the row has the canonical lookup fields populated.
    /// </summary>
    /// <param name="row">The row to inspect.</param>
    /// <returns>True when the canonical lookup scope is available.</returns>
    private static bool HasCanonicalScope(StringArrayDatas row)
    {
        return !string.IsNullOrWhiteSpace(row.ContextKey) &&
               !string.IsNullOrWhiteSpace(row.SourceContentHash);
    }

    /// <summary>
    ///     Merges a newer payload into an existing row without discarding
    ///     already-populated translated data when the new payload is sparse.
    /// </summary>
    /// <param name="target">The row already stored in the database.</param>
    /// <param name="source">The newer payload.</param>
    private static void MergeValues(
        StringArrayDatas target,
        StringArrayDatas source)
    {
        target.Type = FirstNonEmpty(source.Type, target.Type);
        target.Size = source.Size > 0 ? source.Size : target.Size;
        target.RawData = source.RawData is { Length: > 0 }
            ? source.RawData
            : target.RawData;
        target.FormattedRawData = FirstNonEmpty(
            source.FormattedRawData,
            target.FormattedRawData);
        target.OriginalLang = FirstNonEmpty(source.OriginalLang, target.OriginalLang);
        target.OriginalStrings = FirstNonEmpty(
            source.OriginalStrings,
            target.OriginalStrings);
        target.TranslationLang = FirstNonEmpty(
            source.TranslationLang,
            target.TranslationLang);
        target.TranslatedStrings = FirstNonEmpty(
            source.TranslatedStrings,
            target.TranslatedStrings);
        target.TranslatedStringsWithPayloads = FirstNonEmpty(
            source.TranslatedStringsWithPayloads,
            target.TranslatedStringsWithPayloads);
        target.TranslationEngine = source.TranslationEngine ?? target.TranslationEngine;
        target.ContextKey = FirstNonEmpty(source.ContextKey, target.ContextKey);
        target.SchemaVersion = source.SchemaVersion ?? target.SchemaVersion;
        target.SourceContentHash = FirstNonEmpty(
            source.SourceContentHash,
            target.SourceContentHash);
        target.OriginalStructuredPayload = FirstNonEmpty(
            source.OriginalStructuredPayload,
            target.OriginalStructuredPayload);
        target.TranslatedStructuredPayload = FirstNonEmpty(
            source.TranslatedStructuredPayload,
            target.TranslatedStructuredPayload);
    }

    /// <summary>
    ///     Returns the first non-empty string, preferring the incoming value.
    /// </summary>
    /// <param name="preferred">The candidate incoming value.</param>
    /// <param name="fallback">The existing value.</param>
    /// <returns>The chosen string.</returns>
    private static string? FirstNonEmpty(string? preferred, string? fallback)
    {
        return string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
    }
}
