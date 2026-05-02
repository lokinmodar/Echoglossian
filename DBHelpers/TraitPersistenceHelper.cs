// <copyright file="TraitPersistenceHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.Helpers;

namespace Echoglossian;

/// <summary>
///     Persists canonical <see cref="Trait" /> rows.
/// </summary>
public static class TraitPersistenceHelper
{
    /// <summary>
    ///     Creates one canonical trait row from source payloads.
    /// </summary>
    /// <param name="originalLang">The language of the original payload.</param>
    /// <param name="translationLang">The target translation language.</param>
    /// <param name="translationEngine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The game version.</param>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="translatedPayload">The translated canonical payload, if any.</param>
    /// <returns>The canonical DB row.</returns>
    public static Trait CreateCanonicalRow(
        string originalLang,
        string translationLang,
        int? translationEngine,
        string? gameVersion,
        TraitCanonicalPayload originalPayload,
        TraitCanonicalPayload? translatedPayload = null)
    {
        ArgumentNullException.ThrowIfNull(originalPayload);

        var normalizedOriginalPayload = NormalizePayload(originalPayload);
        var normalizedTranslatedPayload = translatedPayload != null
            ? NormalizePayload(translatedPayload)
            : null;

        return new Trait
        {
            TraitId = normalizedOriginalPayload.TraitId,
            IconId = normalizedOriginalPayload.IconId,
            ClassJobId = normalizedOriginalPayload.ClassJobId,
            ClassJobCategoryId = normalizedOriginalPayload.ClassJobCategoryId,
            TraitName = normalizedOriginalPayload.Name,
            TraitDescription = normalizedOriginalPayload.Description,
            OriginalTooltipText = normalizedOriginalPayload.BuildOriginalTooltipText(),
            OriginalLang = originalLang,
            TranslatedTraitName = normalizedTranslatedPayload?.TranslatedName,
            TranslatedTraitDescription =
                normalizedTranslatedPayload?.TranslatedDescription,
            TranslatedTooltipText =
                normalizedTranslatedPayload?.BuildTranslatedTooltipText(),
            TranslationLang = translationLang,
            TranslationEngine = translationEngine,
            GameVersion = gameVersion,
            SourceContentHash = normalizedOriginalPayload.ComputeSourceContentHash(),
            CanonicalPayloadAsText =
                (normalizedTranslatedPayload ?? normalizedOriginalPayload)
                .Serialize(),
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
        };
    }

    /// <summary>
    ///     Inserts or updates one canonical trait row.
    /// </summary>
    /// <param name="configDirectory">The plugin config directory containing SQLite.</param>
    /// <param name="trait">The row to persist.</param>
    /// <param name="onPersisted">Optional callback invoked after the DB write succeeds.</param>
    /// <returns>A status message describing the result.</returns>
    public static string InsertTrait(
        string configDirectory,
        Trait trait,
        Action<Trait>? onPersisted = null)
    {
        using var context = new EchoglossianDbContext(configDirectory);

        try
        {
            if (!IsValidForPersistence(trait))
            {
                return "Invalid data.";
            }

            var hasRequestedGameVersion =
                GameVersionLookupHelper.HasRequestedVersion(trait.GameVersion);

            var existing = context.Traits.FirstOrDefault(row =>
                row.TraitId == trait.TraitId &&
                row.TranslationLang == trait.TranslationLang &&
                row.TranslationEngine == trait.TranslationEngine &&
                ((!hasRequestedGameVersion && row.GameVersion == null) ||
                 (hasRequestedGameVersion &&
                  (row.GameVersion == null ||
                   row.GameVersion == trait.GameVersion))) &&
                row.SourceContentHash == trait.SourceContentHash);
            if (existing != null)
            {
                MergeValues(existing, trait);
                existing.UpdatedDate = DateTime.UtcNow;

                context.Traits.Update(existing);
                context.SaveChanges();
                onPersisted?.Invoke(existing);

                return "Record updated.";
            }

            trait.CreatedDate = DateTime.UtcNow;
            trait.UpdatedDate = DateTime.UtcNow;

            context.Traits.Add(trait);
            context.SaveChanges();
            onPersisted?.Invoke(trait);

            return "New record inserted.";
        }
        catch (Exception ex)
        {
            return $"Error inserting Trait: {ex.Message}";
        }
    }

    /// <summary>
    ///     Finds one canonical trait row using the same lookup scope as persistence.
    /// </summary>
    /// <param name="configDirectory">The plugin config directory containing SQLite.</param>
    /// <param name="probe">The probe row that defines the lookup scope.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public static Trait? FindTrait(
        string configDirectory,
        Trait probe)
    {
        using var context = new EchoglossianDbContext(configDirectory);

        try
        {
            if (!IsValidForPersistence(probe))
            {
                return null;
            }

            var hasRequestedGameVersion =
                GameVersionLookupHelper.HasRequestedVersion(probe.GameVersion);

            return context.Traits
                .AsNoTracking()
                .FirstOrDefault(row =>
                    row.TraitId == probe.TraitId &&
                    row.TranslationLang == probe.TranslationLang &&
                    row.TranslationEngine == probe.TranslationEngine &&
                    ((!hasRequestedGameVersion && row.GameVersion == null) ||
                     (hasRequestedGameVersion &&
                      (row.GameVersion == null ||
                       row.GameVersion == probe.GameVersion))) &&
                    row.SourceContentHash == probe.SourceContentHash);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Determines whether a row contains enough identity to be persisted safely.
    /// </summary>
    /// <param name="row">The row to validate.</param>
    /// <returns>True when the row has a safe persistence identity.</returns>
    private static bool IsValidForPersistence(Trait row)
    {
        return row != null &&
               row.TraitId > 0 &&
               !string.IsNullOrWhiteSpace(row.TranslationLang) &&
               !string.IsNullOrWhiteSpace(row.SourceContentHash);
    }

    /// <summary>
    ///     Merges a newer payload into an existing row without discarding already-translated fields.
    /// </summary>
    /// <param name="target">The existing stored row.</param>
    /// <param name="source">The incoming row.</param>
    private static void MergeValues(Trait target, Trait source)
    {
        target.IconId = source.IconId != 0 ? source.IconId : target.IconId;
        target.ClassJobId = source.ClassJobId != 0
            ? source.ClassJobId
            : target.ClassJobId;
        target.ClassJobCategoryId = source.ClassJobCategoryId != 0
            ? source.ClassJobCategoryId
            : target.ClassJobCategoryId;
        target.TraitName = FirstNonEmpty(source.TraitName, target.TraitName);
        target.TraitDescription = FirstNonEmpty(
            source.TraitDescription,
            target.TraitDescription);
        target.OriginalTooltipText = FirstNonEmpty(
            source.OriginalTooltipText,
            target.OriginalTooltipText);
        target.OriginalLang = FirstNonEmpty(source.OriginalLang, target.OriginalLang);
        target.TranslatedTraitName = FirstNonEmpty(
            source.TranslatedTraitName,
            target.TranslatedTraitName);
        target.TranslatedTraitDescription = FirstNonEmpty(
            source.TranslatedTraitDescription,
            target.TranslatedTraitDescription);
        target.TranslatedTooltipText = FirstNonEmpty(
            source.TranslatedTooltipText,
            target.TranslatedTooltipText);
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
    ///     Normalizes canonical trait payload identity before persistence so
    ///     unresolved sheet sentinels do not leak into the DB or source hash.
    /// </summary>
    /// <param name="payload">The payload to normalize.</param>
    /// <returns>The normalized payload copy.</returns>
    private static TraitCanonicalPayload NormalizePayload(
        TraitCanonicalPayload payload)
    {
        return new TraitCanonicalPayload
        {
            SchemaVersion = payload.SchemaVersion,
            TraitId = payload.TraitId,
            IconId = payload.IconId,
            ClassJobId = SheetRowIdNormalizationHelper.NormalizeOrZero(
                payload.ClassJobId),
            ClassJobCategoryId = SheetRowIdNormalizationHelper.NormalizeOrZero(
                payload.ClassJobCategoryId),
            Name = payload.Name,
            Description = payload.Description,
            TranslatedName = payload.TranslatedName,
            TranslatedDescription = payload.TranslatedDescription,
        };
    }
}
