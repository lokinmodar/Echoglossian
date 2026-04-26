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
            return cached;
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
            return cached;
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
            return cached;
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
}
