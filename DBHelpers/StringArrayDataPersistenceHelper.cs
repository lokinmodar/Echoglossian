// <copyright file="StringArrayDataPersistenceHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

using Microsoft.EntityFrameworkCore;

namespace Echoglossian;

/// <summary>
///     Persists <see cref="StringArrayDatas" /> rows using the canonical
///     DB-first lookup contract.
/// </summary>
public static class StringArrayDataPersistenceHelper
{
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

            var existing = BuildLookupQuery(context, stringArrayData)
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
        using var context = new EchoglossianDbContext(configDirectory);

        try
        {
            if (!IsValidForPersistence(probe))
            {
                return null;
            }

            return BuildLookupQuery(context, probe)
                .AsNoTracking()
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
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

        return HasCanonicalScope(row) ||
               !string.IsNullOrWhiteSpace(row.FormattedRawData);
    }

    /// <summary>
    ///     Builds the lookup query for one row.
    /// </summary>
    /// <param name="context">The active DB context.</param>
    /// <param name="row">The row that defines the lookup scope.</param>
    /// <returns>The lookup query.</returns>
    private static IQueryable<StringArrayDatas> BuildLookupQuery(
        EchoglossianDbContext context,
        StringArrayDatas row)
    {
        if (HasCanonicalScope(row))
        {
            return context.StringArrayDatas.Where(existing =>
                existing.Type == row.Type &&
                existing.ContextKey == row.ContextKey &&
                existing.TranslationLang == row.TranslationLang &&
                existing.TranslationEngine == row.TranslationEngine &&
                existing.GameVersion == row.GameVersion &&
                existing.SourceContentHash == row.SourceContentHash);
        }

        return context.StringArrayDatas.Where(existing =>
            existing.Type == row.Type &&
            existing.TranslationLang == row.TranslationLang &&
            existing.TranslationEngine == row.TranslationEngine &&
            existing.GameVersion == row.GameVersion &&
            existing.FormattedRawData == row.FormattedRawData);
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
        target.GameVersion = FirstNonEmpty(source.GameVersion, target.GameVersion);
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
