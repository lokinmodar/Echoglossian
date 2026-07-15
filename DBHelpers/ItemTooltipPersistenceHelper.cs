// <copyright file="ItemTooltipPersistenceHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.Helpers;

namespace Echoglossian;

/// <summary>
///     Persists canonical <see cref="ItemTooltip" /> rows.
/// </summary>
public static class ItemTooltipPersistenceHelper
{
    /// <summary>
    ///     Creates one canonical item-tooltip row from source payloads.
    /// </summary>
    /// <param name="originalLang">The language of the original payload.</param>
    /// <param name="translationLang">The target translation language.</param>
    /// <param name="translationEngine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The game version.</param>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="translatedPayload">The translated canonical payload, if any.</param>
    /// <returns>The canonical DB row.</returns>
    public static ItemTooltip CreateCanonicalRow(
        string originalLang,
        string translationLang,
        int? translationEngine,
        string? gameVersion,
        ItemTooltipCanonicalPayload originalPayload,
        ItemTooltipCanonicalPayload? translatedPayload = null)
    {
        ArgumentNullException.ThrowIfNull(originalPayload);

        var normalizedOriginalPayload = NormalizePayload(originalPayload);
        var normalizedTranslatedPayload = translatedPayload != null
            ? NormalizePayload(translatedPayload)
            : null;

        return new ItemTooltip
        {
            ItemId = normalizedOriginalPayload.ItemId,
            IconId = normalizedOriginalPayload.IconId,
            ItemActionId = normalizedOriginalPayload.ItemActionId,
            ItemUiCategoryId = normalizedOriginalPayload.ItemUiCategoryId,
            ClassJobCategoryId = normalizedOriginalPayload.ClassJobCategoryId,
            ItemName = normalizedOriginalPayload.Name,
            ItemDescription = normalizedOriginalPayload.Description,
            OriginalTooltipText = normalizedOriginalPayload.BuildOriginalTooltipText(),
            OriginalLang = originalLang,
            TranslatedItemName = normalizedTranslatedPayload?.TranslatedName,
            TranslatedItemDescription =
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
    ///     Inserts or updates one canonical item-tooltip row.
    /// </summary>
    /// <param name="configDirectory">The plugin config directory containing SQLite.</param>
    /// <param name="itemTooltip">The row to persist.</param>
    /// <param name="onPersisted">Optional callback invoked after the DB write succeeds.</param>
    /// <returns>A status message describing the result.</returns>
    public static string InsertItemTooltip(
        string configDirectory,
        ItemTooltip itemTooltip,
        Action<ItemTooltip>? onPersisted = null)
    {
        using var context = new EchoglossianDbContext(configDirectory);

        try
        {
            if (!IsValidForPersistence(itemTooltip))
            {
                return "Invalid data.";
            }

            var hasRequestedGameVersion =
                GameVersionLookupHelper.HasRequestedVersion(
                    itemTooltip.GameVersion);

            var existing = context.ItemTooltip
                .Where(row =>
                    row.ItemId == itemTooltip.ItemId &&
                    row.TranslationEngine == itemTooltip.TranslationEngine &&
                    ((!hasRequestedGameVersion && row.GameVersion == null) ||
                     (hasRequestedGameVersion &&
                      (row.GameVersion == null ||
                       row.GameVersion == itemTooltip.GameVersion))) &&
                    row.SourceContentHash == itemTooltip.SourceContentHash)
                .AsEnumerable()
                .Where(row => RuntimeLanguageHelper.LanguagesMatch(
                    row.OriginalLang,
                    itemTooltip.OriginalLang) &&
                    RuntimeLanguageHelper.LanguagesMatch(
                        row.TranslationLang,
                        itemTooltip.TranslationLang))
                .OrderByDescending(GetTranslationCompletenessScore)
                .ThenByDescending(row => row.UpdatedDate)
                .FirstOrDefault();
            if (existing != null)
            {
                MergeValues(existing, itemTooltip);
                existing.UpdatedDate = DateTime.UtcNow;

                context.ItemTooltip.Update(existing);
                context.SaveChanges();
                onPersisted?.Invoke(existing);

                return "Record updated.";
            }

            itemTooltip.CreatedDate = DateTime.UtcNow;
            itemTooltip.UpdatedDate = DateTime.UtcNow;

            context.ItemTooltip.Add(itemTooltip);
            context.SaveChanges();
            onPersisted?.Invoke(itemTooltip);

            return "New record inserted.";
        }
        catch (Exception ex)
        {
            return $"Error inserting ItemTooltip: {ex.Message}";
        }
    }

    /// <summary>
    ///     Finds one canonical item-tooltip row using the same lookup scope as persistence.
    /// </summary>
    /// <param name="configDirectory">The plugin config directory containing SQLite.</param>
    /// <param name="probe">The probe row that defines the lookup scope.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public static ItemTooltip? FindItemTooltip(
        string configDirectory,
        ItemTooltip probe)
    {
        if (probe == null)
        {
            return null;
        }

        return FindItemTooltip(
            configDirectory,
            probe,
            new TranslationReuseScope(
                probe.OriginalLang ?? string.Empty,
                probe.TranslationLang ?? string.Empty,
                probe.TranslationEngine,
                true));
    }

    /// <summary>
    ///     Finds one canonical item-tooltip row using an explicit translation
    ///     reuse scope.
    /// </summary>
    /// <param name="configDirectory">The plugin config directory containing SQLite.</param>
    /// <param name="probe">The probe row that defines the content and version identity.</param>
    /// <param name="scope">The source, target, and engine reuse policy.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public static ItemTooltip? FindItemTooltip(
        string configDirectory,
        ItemTooltip probe,
        TranslationReuseScope scope)
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

            return context.ItemTooltip
                .AsNoTracking()
                .Where(row =>
                    row.ItemId == probe.ItemId &&
                    ((!hasRequestedGameVersion && row.GameVersion == null) ||
                     (hasRequestedGameVersion &&
                      (row.GameVersion == null ||
                       row.GameVersion == probe.GameVersion))) &&
                    row.SourceContentHash == probe.SourceContentHash)
                .AsEnumerable()
                .Where(row => scope.Matches(
                    row.OriginalLang,
                    row.TranslationLang,
                    row.TranslationEngine))
                .OrderByDescending(GetTranslationCompletenessScore)
                .ThenByDescending(row => row.UpdatedDate)
                .FirstOrDefault();
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
    private static bool IsValidForPersistence(ItemTooltip row)
    {
        return row != null &&
               row.ItemId > 0 &&
               !string.IsNullOrWhiteSpace(row.OriginalLang) &&
               !string.IsNullOrWhiteSpace(row.TranslationLang) &&
               !string.IsNullOrWhiteSpace(row.SourceContentHash);
    }

    /// <summary>
    ///     Merges a newer payload into an existing row without discarding already-translated fields.
    /// </summary>
    /// <param name="target">The existing stored row.</param>
    /// <param name="source">The incoming row.</param>
    private static void MergeValues(ItemTooltip target, ItemTooltip source)
    {
        target.IconId = source.IconId != 0 ? source.IconId : target.IconId;
        target.ItemActionId = source.ItemActionId != 0
            ? source.ItemActionId
            : target.ItemActionId;
        target.ItemUiCategoryId = source.ItemUiCategoryId != 0
            ? source.ItemUiCategoryId
            : target.ItemUiCategoryId;
        target.ClassJobCategoryId = source.ClassJobCategoryId != 0
            ? source.ClassJobCategoryId
            : target.ClassJobCategoryId;
        target.ItemName = FirstNonEmpty(source.ItemName, target.ItemName);
        target.ItemDescription = FirstNonEmpty(
            source.ItemDescription,
            target.ItemDescription);
        target.OriginalTooltipText = FirstNonEmpty(
            source.OriginalTooltipText,
            target.OriginalTooltipText);
        target.OriginalLang = FirstNonEmpty(source.OriginalLang, target.OriginalLang);
        target.TranslatedItemName = FirstNonEmpty(
            source.TranslatedItemName,
            target.TranslatedItemName);
        target.TranslatedItemDescription = FirstNonEmpty(
            source.TranslatedItemDescription,
            target.TranslatedItemDescription);
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
    ///     Computes how many translated item fields are populated so alias
    ///     reuse prefers an existing completed row over an incomplete row.
    /// </summary>
    /// <param name="row">The item row to score.</param>
    /// <returns>The number of populated translated fields.</returns>
    private static int GetTranslationCompletenessScore(ItemTooltip row)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(row.TranslatedItemName))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(row.TranslatedItemDescription))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(row.TranslatedTooltipText))
        {
            score++;
        }

        return score;
    }

    /// <summary>
    ///     Normalizes canonical item payload identity before persistence so
    ///     unresolved sheet sentinels do not leak into the DB or source hash.
    /// </summary>
    /// <param name="payload">The payload to normalize.</param>
    /// <returns>The normalized payload copy.</returns>
    private static ItemTooltipCanonicalPayload NormalizePayload(
        ItemTooltipCanonicalPayload payload)
    {
        return new ItemTooltipCanonicalPayload
        {
            SchemaVersion = payload.SchemaVersion,
            ItemId = payload.ItemId,
            IconId = payload.IconId,
            ItemActionId = SheetRowIdNormalizationHelper.NormalizeOrZero(
                payload.ItemActionId),
            ItemUiCategoryId = SheetRowIdNormalizationHelper.NormalizeOrZero(
                payload.ItemUiCategoryId),
            ClassJobCategoryId = SheetRowIdNormalizationHelper.NormalizeOrZero(
                payload.ClassJobCategoryId),
            Name = payload.Name,
            Description = payload.Description,
            TranslatedName = payload.TranslatedName,
            TranslatedDescription = payload.TranslatedDescription,
        };
    }
}
