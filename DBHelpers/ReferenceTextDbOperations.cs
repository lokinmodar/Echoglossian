// <copyright file="ReferenceTextDbOperations.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian;

/// <summary>
///     Provides shared DB and cache operations for canonical reference-text
///     rows.
/// </summary>
public partial class Echoglossian
{
    /// <summary>
    ///     Finds one canonical reference-text row using cache-first lookup.
    /// </summary>
    /// <typeparam name="TRow">The concrete row type.</typeparam>
    /// <param name="probe">The probe row that defines the lookup scope.</param>
    /// <param name="cacheStore">The specific in-memory cache to consult first.</param>
    /// <param name="setSelector">Selects the matching DbSet.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    private TRow? FindReferenceText<TRow>(
        TRow probe,
        ReferenceTextCacheStore<TRow> cacheStore,
        Func<EchoglossianDbContext, DbSet<TRow>> setSelector)
        where TRow : ReferenceTextRowBase
    {
        if (probe == null ||
            probe.ReferenceId == 0 ||
            string.IsNullOrWhiteSpace(probe.TranslationLang) ||
            string.IsNullOrWhiteSpace(probe.SourceContentHash))
        {
            return null;
        }

        var cached = cacheStore.TryFindCanonicalMatch(
            probe.ReferenceId,
            probe.TranslationLang,
            probe.TranslationEngine ?? this.configuration.ChosenTransEngine,
            probe.GameVersion,
            probe.SourceContentHash);
        if (cached != null)
        {
            return cached;
        }

        var row = ReferenceTextPersistenceHelper.FindReferenceText(
            ConfigDirectory,
            probe,
            setSelector);
        if (row != null)
        {
            cacheStore.Update(row);
        }

        return row;
    }

    /// <summary>
    ///     Inserts or updates one canonical reference-text row and refreshes
    ///     cache state.
    /// </summary>
    /// <typeparam name="TRow">The concrete row type.</typeparam>
    /// <param name="row">The row to persist.</param>
    /// <param name="cacheStore">The specific in-memory cache to refresh.</param>
    /// <param name="setSelector">Selects the matching DbSet.</param>
    /// <returns>A status message describing the result.</returns>
    private string InsertReferenceText<TRow>(
        TRow row,
        ReferenceTextCacheStore<TRow> cacheStore,
        Func<EchoglossianDbContext, DbSet<TRow>> setSelector)
        where TRow : ReferenceTextRowBase
    {
        return ReferenceTextPersistenceHelper.InsertReferenceText(
            ConfigDirectory,
            row,
            setSelector,
            cacheStore.Update);
    }
}
