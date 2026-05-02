// <copyright file="ActionTooltipPersistenceHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.Helpers;

namespace Echoglossian;

/// <summary>
///     Persists canonical <see cref="ActionTooltip" /> rows.
/// </summary>
public static class ActionTooltipPersistenceHelper
{
    /// <summary>
    ///     Creates one canonical action-tooltip row from source payloads.
    /// </summary>
    /// <param name="originalLang">The language of the original payload.</param>
    /// <param name="translationLang">The target translation language.</param>
    /// <param name="translationEngine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The game version.</param>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="translatedPayload">The translated canonical payload, if any.</param>
    /// <returns>The canonical DB row.</returns>
    public static ActionTooltip CreateCanonicalRow(
        string originalLang,
        string translationLang,
        int? translationEngine,
        string? gameVersion,
        ActionTooltipCanonicalPayload originalPayload,
        ActionTooltipCanonicalPayload? translatedPayload = null)
    {
        ArgumentNullException.ThrowIfNull(originalPayload);

        var normalizedOriginalPayload = NormalizePayload(originalPayload);
        var normalizedTranslatedPayload = translatedPayload != null
            ? NormalizePayload(translatedPayload)
            : null;

        return new ActionTooltip
        {
            ActionId = normalizedOriginalPayload.ActionId,
            IconId = normalizedOriginalPayload.IconId,
            ActionCategoryId = normalizedOriginalPayload.ActionCategoryId,
            ClassJobId = normalizedOriginalPayload.ClassJobId,
            ClassJobCategoryId = normalizedOriginalPayload.ClassJobCategoryId,
            ActionName = normalizedOriginalPayload.Name,
            ActionDescription = normalizedOriginalPayload.Description,
            OriginalTooltipText = normalizedOriginalPayload.BuildOriginalTooltipText(),
            OriginalLang = originalLang,
            TranslatedActionName = normalizedTranslatedPayload?.TranslatedName,
            TranslatedActionDescription =
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
    ///     Inserts or updates one canonical action-tooltip row.
    /// </summary>
    /// <param name="configDirectory">The plugin config directory containing SQLite.</param>
    /// <param name="actionTooltip">The row to persist.</param>
    /// <param name="onPersisted">Optional callback invoked after the DB write succeeds.</param>
    /// <returns>A status message describing the result.</returns>
    public static string InsertActionTooltip(
        string configDirectory,
        ActionTooltip actionTooltip,
        Action<ActionTooltip>? onPersisted = null)
    {
        using var context = new EchoglossianDbContext(configDirectory);

        try
        {
            if (!IsValidForPersistence(actionTooltip))
            {
                return "Invalid data.";
            }

            var hasRequestedGameVersion =
                GameVersionLookupHelper.HasRequestedVersion(
                    actionTooltip.GameVersion);

            var existing = context.ActionTooltip.FirstOrDefault(row =>
                row.ActionId == actionTooltip.ActionId &&
                row.TranslationLang == actionTooltip.TranslationLang &&
                row.TranslationEngine == actionTooltip.TranslationEngine &&
                ((!hasRequestedGameVersion && row.GameVersion == null) ||
                 (hasRequestedGameVersion &&
                  (row.GameVersion == null ||
                   row.GameVersion == actionTooltip.GameVersion))) &&
                row.SourceContentHash == actionTooltip.SourceContentHash);
            if (existing != null)
            {
                MergeValues(existing, actionTooltip);
                existing.UpdatedDate = DateTime.UtcNow;

                context.ActionTooltip.Update(existing);
                context.SaveChanges();
                onPersisted?.Invoke(existing);

                return "Record updated.";
            }

            actionTooltip.CreatedDate = DateTime.UtcNow;
            actionTooltip.UpdatedDate = DateTime.UtcNow;

            context.ActionTooltip.Add(actionTooltip);
            context.SaveChanges();
            onPersisted?.Invoke(actionTooltip);

            return "New record inserted.";
        }
        catch (Exception ex)
        {
            return $"Error inserting ActionTooltip: {ex.Message}";
        }
    }

    /// <summary>
    ///     Finds one canonical action-tooltip row using the same lookup scope as persistence.
    /// </summary>
    /// <param name="configDirectory">The plugin config directory containing SQLite.</param>
    /// <param name="probe">The probe row that defines the lookup scope.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public static ActionTooltip? FindActionTooltip(
        string configDirectory,
        ActionTooltip probe)
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

            return context.ActionTooltip
                .AsNoTracking()
                .FirstOrDefault(row =>
                    row.ActionId == probe.ActionId &&
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
    private static bool IsValidForPersistence(ActionTooltip row)
    {
        return row != null &&
               row.ActionId > 0 &&
               !string.IsNullOrWhiteSpace(row.TranslationLang) &&
               !string.IsNullOrWhiteSpace(row.SourceContentHash);
    }

    /// <summary>
    ///     Merges a newer payload into an existing row without discarding already-translated fields.
    /// </summary>
    /// <param name="target">The existing stored row.</param>
    /// <param name="source">The incoming row.</param>
    private static void MergeValues(ActionTooltip target, ActionTooltip source)
    {
        target.IconId = source.IconId != 0 ? source.IconId : target.IconId;
        target.ActionCategoryId = source.ActionCategoryId != 0
            ? source.ActionCategoryId
            : target.ActionCategoryId;
        target.ClassJobId = source.ClassJobId != 0
            ? source.ClassJobId
            : target.ClassJobId;
        target.ClassJobCategoryId = source.ClassJobCategoryId != 0
            ? source.ClassJobCategoryId
            : target.ClassJobCategoryId;
        target.ActionName = FirstNonEmpty(source.ActionName, target.ActionName);
        target.ActionDescription = FirstNonEmpty(
            source.ActionDescription,
            target.ActionDescription);
        target.OriginalTooltipText = FirstNonEmpty(
            source.OriginalTooltipText,
            target.OriginalTooltipText);
        target.OriginalLang = FirstNonEmpty(source.OriginalLang, target.OriginalLang);
        target.TranslatedActionName = FirstNonEmpty(
            source.TranslatedActionName,
            target.TranslatedActionName);
        target.TranslatedActionDescription = FirstNonEmpty(
            source.TranslatedActionDescription,
            target.TranslatedActionDescription);
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
    ///     Normalizes canonical action payload identity before persistence so
    ///     unresolved sheet sentinels do not leak into the DB or source hash.
    /// </summary>
    /// <param name="payload">The payload to normalize.</param>
    /// <returns>The normalized payload copy.</returns>
    private static ActionTooltipCanonicalPayload NormalizePayload(
        ActionTooltipCanonicalPayload payload)
    {
        return new ActionTooltipCanonicalPayload
        {
            SchemaVersion = payload.SchemaVersion,
            ActionId = payload.ActionId,
            IconId = payload.IconId,
            ActionCategoryId = SheetRowIdNormalizationHelper.NormalizeOrZero(
                payload.ActionCategoryId),
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
