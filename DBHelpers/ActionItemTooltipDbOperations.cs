// <copyright file="ActionItemTooltipDbOperations.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian;

/// <summary>
///     Provides DB and cache operations for canonical action/item/trait tooltip rows.
/// </summary>
public partial class Echoglossian
{
    /// <summary>
    ///     Finds one canonical action-tooltip row using cache-first lookup.
    /// </summary>
    /// <param name="probe">The probe row that defines the lookup scope.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public ActionTooltip? FindActionTooltip(ActionTooltip probe)
    {
        if (probe == null ||
            probe.ActionId == 0 ||
            string.IsNullOrWhiteSpace(probe.TranslationLang) ||
            string.IsNullOrWhiteSpace(probe.SourceContentHash))
        {
            return null;
        }

        var cached = ActionTooltipCacheManager.TryFindCanonicalMatch(
            probe.ActionId,
            probe.TranslationLang,
            probe.TranslationEngine ?? this.configuration.ChosenTransEngine,
            probe.GameVersion,
            probe.SourceContentHash);
        if (cached != null)
        {
            if (!HasTranslatedActionTooltipContent(cached))
            {
                var promotedCached = this.TryPromoteHistoricalActionTooltip(probe);
                if (promotedCached != null)
                {
                    return promotedCached;
                }
            }

            return cached;
        }

        var historical = this.TryPromoteHistoricalActionTooltip(probe);
        if (historical != null)
        {
            return historical;
        }

        var row = ActionTooltipPersistenceHelper.FindActionTooltip(
            ConfigDirectory,
            probe);
        if (row != null)
        {
            ActionTooltipCacheManager.Update(row);
        }

        return row;
    }

    /// <summary>
    ///     Inserts or updates one canonical action-tooltip row and refreshes cache state.
    /// </summary>
    /// <param name="row">The row to persist.</param>
    /// <returns>A status message describing the result.</returns>
    public string InsertActionTooltip(ActionTooltip row)
    {
        return ActionTooltipPersistenceHelper.InsertActionTooltip(
            ConfigDirectory,
            row,
            ActionTooltipCacheManager.Update);
    }

    /// <summary>
    ///     Finds one canonical trait row using cache-first lookup.
    /// </summary>
    /// <param name="probe">The probe row that defines the lookup scope.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public Trait? FindTrait(Trait probe)
    {
        if (probe == null ||
            probe.TraitId == 0 ||
            string.IsNullOrWhiteSpace(probe.TranslationLang) ||
            string.IsNullOrWhiteSpace(probe.SourceContentHash))
        {
            return null;
        }

        var cached = TraitCacheManager.TryFindCanonicalMatch(
            probe.TraitId,
            probe.TranslationLang,
            probe.TranslationEngine ?? this.configuration.ChosenTransEngine,
            probe.GameVersion,
            probe.SourceContentHash);
        if (cached != null)
        {
            if (!HasTranslatedTraitContent(cached))
            {
                var promotedCached = this.TryPromoteHistoricalTrait(probe);
                if (promotedCached != null)
                {
                    return promotedCached;
                }
            }

            return cached;
        }

        var historical = this.TryPromoteHistoricalTrait(probe);
        if (historical != null)
        {
            return historical;
        }

        var row = TraitPersistenceHelper.FindTrait(
            ConfigDirectory,
            probe);
        if (row != null)
        {
            TraitCacheManager.Update(row);
        }

        return row;
    }

    /// <summary>
    ///     Inserts or updates one canonical trait row and refreshes cache state.
    /// </summary>
    /// <param name="row">The row to persist.</param>
    /// <returns>A status message describing the result.</returns>
    public string InsertTrait(Trait row)
    {
        return TraitPersistenceHelper.InsertTrait(
            ConfigDirectory,
            row,
            TraitCacheManager.Update);
    }

    /// <summary>
    ///     Finds one canonical item-tooltip row using cache-first lookup.
    /// </summary>
    /// <param name="probe">The probe row that defines the lookup scope.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public ItemTooltip? FindItemTooltip(ItemTooltip probe)
    {
        if (probe == null ||
            probe.ItemId == 0 ||
            string.IsNullOrWhiteSpace(probe.TranslationLang) ||
            string.IsNullOrWhiteSpace(probe.SourceContentHash))
        {
            return null;
        }

        var cached = ItemTooltipCacheManager.TryFindCanonicalMatch(
            probe.ItemId,
            probe.TranslationLang,
            probe.TranslationEngine ?? this.configuration.ChosenTransEngine,
            probe.GameVersion,
            probe.SourceContentHash);
        if (cached != null)
        {
            if (!HasTranslatedItemTooltipContent(cached))
            {
                var promotedCached = this.TryPromoteHistoricalItemTooltip(probe);
                if (promotedCached != null)
                {
                    return promotedCached;
                }
            }

            return cached;
        }

        var historical = this.TryPromoteHistoricalItemTooltip(probe);
        if (historical != null)
        {
            return historical;
        }

        var row = ItemTooltipPersistenceHelper.FindItemTooltip(
            ConfigDirectory,
            probe);
        if (row != null)
        {
            ItemTooltipCacheManager.Update(row);
        }

        return row;
    }

    /// <summary>
    ///     Inserts or updates one canonical item-tooltip row and refreshes cache state.
    /// </summary>
    /// <param name="row">The row to persist.</param>
    /// <returns>A status message describing the result.</returns>
    public string InsertItemTooltip(ItemTooltip row)
    {
        return ItemTooltipPersistenceHelper.InsertItemTooltip(
            ConfigDirectory,
            row,
            ItemTooltipCacheManager.Update);
    }

    /// <summary>
    ///     Promotes one translated historical action-tooltip row to the
    ///     current game version when the original payload hash still matches.
    /// </summary>
    /// <param name="probe">The current-version probe row.</param>
    /// <returns>The promoted row, or <see langword="null" />.</returns>
    private ActionTooltip? TryPromoteHistoricalActionTooltip(ActionTooltip probe)
    {
        if (!GameVersionLookupHelper.HasRequestedVersion(probe.GameVersion))
        {
            return null;
        }

        var translationLang = probe.TranslationLang!;
        var sourceContentHash = probe.SourceContentHash!;
        var historical = ActionTooltipCacheManager.TryFindHistoricalCanonicalMatch(
            probe.ActionId,
            translationLang,
            probe.TranslationEngine ?? this.configuration.ChosenTransEngine,
            probe.GameVersion,
            sourceContentHash);
        if (historical == null || !HasTranslatedActionTooltipContent(historical))
        {
            return null;
        }

        var promoted = CloneActionTooltipForGameVersion(historical, probe.GameVersion);
        this.InsertActionTooltip(promoted);
        return promoted;
    }

    /// <summary>
    ///     Promotes one translated historical trait row to the current game
    ///     version when the original payload hash still matches.
    /// </summary>
    /// <param name="probe">The current-version probe row.</param>
    /// <returns>The promoted row, or <see langword="null" />.</returns>
    private Trait? TryPromoteHistoricalTrait(Trait probe)
    {
        if (!GameVersionLookupHelper.HasRequestedVersion(probe.GameVersion))
        {
            return null;
        }

        var translationLang = probe.TranslationLang!;
        var sourceContentHash = probe.SourceContentHash!;
        var historical = TraitCacheManager.TryFindHistoricalCanonicalMatch(
            probe.TraitId,
            translationLang,
            probe.TranslationEngine ?? this.configuration.ChosenTransEngine,
            probe.GameVersion,
            sourceContentHash);
        if (historical == null || !HasTranslatedTraitContent(historical))
        {
            return null;
        }

        var promoted = CloneTraitForGameVersion(historical, probe.GameVersion);
        this.InsertTrait(promoted);
        return promoted;
    }

    /// <summary>
    ///     Promotes one translated historical item-tooltip row to the current
    ///     game version when the original payload hash still matches.
    /// </summary>
    /// <param name="probe">The current-version probe row.</param>
    /// <returns>The promoted row, or <see langword="null" />.</returns>
    private ItemTooltip? TryPromoteHistoricalItemTooltip(ItemTooltip probe)
    {
        if (!GameVersionLookupHelper.HasRequestedVersion(probe.GameVersion))
        {
            return null;
        }

        var translationLang = probe.TranslationLang!;
        var sourceContentHash = probe.SourceContentHash!;
        var historical = ItemTooltipCacheManager.TryFindHistoricalCanonicalMatch(
            probe.ItemId,
            translationLang,
            probe.TranslationEngine ?? this.configuration.ChosenTransEngine,
            probe.GameVersion,
            sourceContentHash);
        if (historical == null || !HasTranslatedItemTooltipContent(historical))
        {
            return null;
        }

        var promoted = CloneItemTooltipForGameVersion(historical, probe.GameVersion);
        this.InsertItemTooltip(promoted);
        return promoted;
    }

    /// <summary>
    ///     Clones one canonical action-tooltip row for a newer game version.
    /// </summary>
    /// <param name="source">The historical row.</param>
    /// <param name="gameVersion">The requested game version.</param>
    /// <returns>The cloned row.</returns>
    private static ActionTooltip CloneActionTooltipForGameVersion(
        ActionTooltip source,
        string? gameVersion)
    {
        return new ActionTooltip
        {
            ActionId = source.ActionId,
            IconId = source.IconId,
            ActionCategoryId = source.ActionCategoryId,
            ClassJobId = source.ClassJobId,
            ClassJobCategoryId = source.ClassJobCategoryId,
            ActionName = source.ActionName,
            ActionDescription = source.ActionDescription,
            OriginalTooltipText = source.OriginalTooltipText,
            OriginalLang = source.OriginalLang,
            TranslatedActionName = source.TranslatedActionName,
            TranslatedActionDescription = source.TranslatedActionDescription,
            TranslatedTooltipText = source.TranslatedTooltipText,
            TranslationLang = source.TranslationLang,
            TranslationEngine = source.TranslationEngine,
            GameVersion = gameVersion,
            SourceContentHash = source.SourceContentHash,
            CanonicalPayloadAsText = source.CanonicalPayloadAsText,
        };
    }

    /// <summary>
    ///     Clones one canonical trait row for a newer game version.
    /// </summary>
    /// <param name="source">The historical row.</param>
    /// <param name="gameVersion">The requested game version.</param>
    /// <returns>The cloned row.</returns>
    private static Trait CloneTraitForGameVersion(
        Trait source,
        string? gameVersion)
    {
        return new Trait
        {
            TraitId = source.TraitId,
            IconId = source.IconId,
            ClassJobId = source.ClassJobId,
            ClassJobCategoryId = source.ClassJobCategoryId,
            TraitName = source.TraitName,
            TraitDescription = source.TraitDescription,
            OriginalTooltipText = source.OriginalTooltipText,
            OriginalLang = source.OriginalLang,
            TranslatedTraitName = source.TranslatedTraitName,
            TranslatedTraitDescription = source.TranslatedTraitDescription,
            TranslatedTooltipText = source.TranslatedTooltipText,
            TranslationLang = source.TranslationLang,
            TranslationEngine = source.TranslationEngine,
            GameVersion = gameVersion,
            SourceContentHash = source.SourceContentHash,
            CanonicalPayloadAsText = source.CanonicalPayloadAsText,
        };
    }

    /// <summary>
    ///     Clones one canonical item-tooltip row for a newer game version.
    /// </summary>
    /// <param name="source">The historical row.</param>
    /// <param name="gameVersion">The requested game version.</param>
    /// <returns>The cloned row.</returns>
    private static ItemTooltip CloneItemTooltipForGameVersion(
        ItemTooltip source,
        string? gameVersion)
    {
        return new ItemTooltip
        {
            ItemId = source.ItemId,
            IconId = source.IconId,
            ItemActionId = source.ItemActionId,
            ItemUiCategoryId = source.ItemUiCategoryId,
            ClassJobCategoryId = source.ClassJobCategoryId,
            ItemName = source.ItemName,
            ItemDescription = source.ItemDescription,
            OriginalTooltipText = source.OriginalTooltipText,
            OriginalLang = source.OriginalLang,
            TranslatedItemName = source.TranslatedItemName,
            TranslatedItemDescription = source.TranslatedItemDescription,
            TranslatedTooltipText = source.TranslatedTooltipText,
            TranslationLang = source.TranslationLang,
            TranslationEngine = source.TranslationEngine,
            GameVersion = gameVersion,
            SourceContentHash = source.SourceContentHash,
            CanonicalPayloadAsText = source.CanonicalPayloadAsText,
        };
    }

    /// <summary>
    ///     Gets whether one action-tooltip row already contains translated
    ///     canonical content.
    /// </summary>
    /// <param name="row">The candidate row.</param>
    /// <returns>True when the row contains translated content.</returns>
    private static bool HasTranslatedActionTooltipContent(ActionTooltip row)
    {
        return !string.IsNullOrWhiteSpace(row.TranslatedActionName) ||
               !string.IsNullOrWhiteSpace(row.TranslatedActionDescription) ||
               !string.IsNullOrWhiteSpace(row.TranslatedTooltipText);
    }

    /// <summary>
    ///     Gets whether one trait row already contains translated canonical
    ///     content.
    /// </summary>
    /// <param name="row">The candidate row.</param>
    /// <returns>True when the row contains translated content.</returns>
    private static bool HasTranslatedTraitContent(Trait row)
    {
        return !string.IsNullOrWhiteSpace(row.TranslatedTraitName) ||
               !string.IsNullOrWhiteSpace(row.TranslatedTraitDescription) ||
               !string.IsNullOrWhiteSpace(row.TranslatedTooltipText);
    }

    /// <summary>
    ///     Gets whether one item-tooltip row already contains translated
    ///     canonical content.
    /// </summary>
    /// <param name="row">The candidate row.</param>
    /// <returns>True when the row contains translated content.</returns>
    private static bool HasTranslatedItemTooltipContent(ItemTooltip row)
    {
        return !string.IsNullOrWhiteSpace(row.TranslatedItemName) ||
               !string.IsNullOrWhiteSpace(row.TranslatedItemDescription) ||
               !string.IsNullOrWhiteSpace(row.TranslatedTooltipText);
    }
}
