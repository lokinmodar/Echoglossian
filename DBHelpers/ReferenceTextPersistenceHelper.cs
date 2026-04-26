// <copyright file="ReferenceTextPersistenceHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.Helpers;

namespace Echoglossian;

/// <summary>
///     Persists canonical reference-text rows using the shared DB-first lookup
///     contract.
/// </summary>
public static class ReferenceTextPersistenceHelper
{
    /// <summary>
    ///     Creates one canonical reference-text row from structured payloads.
    /// </summary>
    /// <typeparam name="TRow">The concrete row type.</typeparam>
    /// <param name="originalLang">The language of the original payload.</param>
    /// <param name="translationLang">The target translation language.</param>
    /// <param name="translationEngine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The game version associated with the payload.</param>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="translatedPayload">The translated canonical payload, if available.</param>
    /// <returns>The canonical DB row.</returns>
    public static TRow CreateCanonicalRow<TRow>(
        string originalLang,
        string translationLang,
        int? translationEngine,
        string? gameVersion,
        ReferenceTextCanonicalPayload originalPayload,
        ReferenceTextCanonicalPayload? translatedPayload = null)
        where TRow : ReferenceTextRowBase, new()
    {
        ArgumentNullException.ThrowIfNull(originalPayload);

        return new TRow
        {
            ReferenceId = originalPayload.ReferenceId,
            OriginalName = originalPayload.Name,
            OriginalDescription = NormalizeOptionalText(
                originalPayload.Description),
            OriginalLang = originalLang,
            TranslatedName = NormalizeOptionalText(
                translatedPayload?.TranslatedName),
            TranslatedDescription = NormalizeOptionalText(
                translatedPayload?.TranslatedDescription),
            TranslationLang = translationLang,
            TranslationEngine = translationEngine,
            GameVersion = gameVersion,
            SourceContentHash = originalPayload.ComputeSourceContentHash(),
            CanonicalPayloadAsText = (translatedPayload ?? originalPayload)
                .Serialize(),
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
        };
    }

    /// <summary>
    ///     Inserts or updates one canonical reference-text row.
    /// </summary>
    /// <typeparam name="TRow">The concrete row type.</typeparam>
    /// <param name="configDirectory">The plugin config directory containing SQLite.</param>
    /// <param name="row">The row to persist.</param>
    /// <param name="setSelector">Selects the matching DbSet.</param>
    /// <param name="onPersisted">Optional callback invoked after the DB write succeeds.</param>
    /// <returns>A status message describing the result.</returns>
    public static string InsertReferenceText<TRow>(
        string configDirectory,
        TRow row,
        Func<EchoglossianDbContext, DbSet<TRow>> setSelector,
        Action<TRow>? onPersisted = null)
        where TRow : ReferenceTextRowBase
    {
        using var context = new EchoglossianDbContext(configDirectory);

        try
        {
            if (!IsValidForPersistence(row))
            {
                return "Invalid data.";
            }

            var set = setSelector(context);
            var hasRequestedGameVersion =
                GameVersionLookupHelper.HasRequestedVersion(row.GameVersion);
            var existing = set.FirstOrDefault(existing =>
                existing.ReferenceId == row.ReferenceId &&
                existing.TranslationLang == row.TranslationLang &&
                existing.TranslationEngine == row.TranslationEngine &&
                ((!hasRequestedGameVersion && existing.GameVersion == null) ||
                 (hasRequestedGameVersion &&
                  (existing.GameVersion == null ||
                   existing.GameVersion == row.GameVersion))) &&
                existing.SourceContentHash == row.SourceContentHash);
            if (existing != null)
            {
                MergeValues(existing, row);
                existing.UpdatedDate = DateTime.UtcNow;

                set.Update(existing);
                context.SaveChanges();
                onPersisted?.Invoke(existing);

                return "Record updated.";
            }

            row.CreatedDate = DateTime.UtcNow;
            row.UpdatedDate = DateTime.UtcNow;

            set.Add(row);
            context.SaveChanges();
            onPersisted?.Invoke(row);

            return "New record inserted.";
        }
        catch (Exception ex)
        {
            return $"Error inserting {typeof(TRow).Name}: {ex.Message}";
        }
    }

    /// <summary>
    ///     Finds one canonical reference-text row using the same lookup scope
    ///     as persistence.
    /// </summary>
    /// <typeparam name="TRow">The concrete row type.</typeparam>
    /// <param name="configDirectory">The plugin config directory containing SQLite.</param>
    /// <param name="probe">The probe row that defines the lookup scope.</param>
    /// <param name="setSelector">Selects the matching DbSet.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public static TRow? FindReferenceText<TRow>(
        string configDirectory,
        TRow probe,
        Func<EchoglossianDbContext, DbSet<TRow>> setSelector)
        where TRow : ReferenceTextRowBase
    {
        using var context = new EchoglossianDbContext(configDirectory);

        try
        {
            if (!IsValidForPersistence(probe))
            {
                return null;
            }

            var set = setSelector(context);
            var hasRequestedGameVersion =
                GameVersionLookupHelper.HasRequestedVersion(probe.GameVersion);

            return set
                .AsNoTracking()
                .FirstOrDefault(existing =>
                    existing.ReferenceId == probe.ReferenceId &&
                    existing.TranslationLang == probe.TranslationLang &&
                    existing.TranslationEngine == probe.TranslationEngine &&
                    ((!hasRequestedGameVersion &&
                      existing.GameVersion == null) ||
                     (hasRequestedGameVersion &&
                      (existing.GameVersion == null ||
                       existing.GameVersion == probe.GameVersion))) &&
                    existing.SourceContentHash == probe.SourceContentHash);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Determines whether the row contains enough identity to be persisted
    ///     safely.
    /// </summary>
    /// <typeparam name="TRow">The concrete row type.</typeparam>
    /// <param name="row">The row to validate.</param>
    /// <returns>True when the row has a safe persistence identity.</returns>
    private static bool IsValidForPersistence<TRow>(TRow row)
        where TRow : ReferenceTextRowBase
    {
        return row != null &&
               row.ReferenceId > 0 &&
               !string.IsNullOrWhiteSpace(row.TranslationLang) &&
               !string.IsNullOrWhiteSpace(row.SourceContentHash);
    }

    /// <summary>
    ///     Merges a newer payload into an existing row without discarding
    ///     already-translated fields.
    /// </summary>
    /// <typeparam name="TRow">The concrete row type.</typeparam>
    /// <param name="target">The existing stored row.</param>
    /// <param name="source">The incoming row.</param>
    private static void MergeValues<TRow>(TRow target, TRow source)
        where TRow : ReferenceTextRowBase
    {
        target.OriginalName = FirstNonEmpty(source.OriginalName, target.OriginalName);
        target.OriginalDescription = FirstNonEmpty(
            source.OriginalDescription,
            target.OriginalDescription);
        target.OriginalLang = FirstNonEmpty(source.OriginalLang, target.OriginalLang);
        target.TranslatedName = FirstNonEmpty(
            source.TranslatedName,
            target.TranslatedName);
        target.TranslatedDescription = FirstNonEmpty(
            source.TranslatedDescription,
            target.TranslatedDescription);
        target.CanonicalPayloadAsText = FirstNonEmpty(
            source.CanonicalPayloadAsText,
            target.CanonicalPayloadAsText);
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

    /// <summary>
    ///     Normalizes one optional text field so empty strings are persisted as
    ///     <see langword="null" />.
    /// </summary>
    /// <param name="text">The candidate text.</param>
    /// <returns>The normalized value.</returns>
    private static string? NormalizeOptionalText(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
