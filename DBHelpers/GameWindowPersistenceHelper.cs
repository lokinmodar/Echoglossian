// <copyright file="GameWindowPersistenceHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Echoglossian;

/// <summary>
///     Persists <see cref="GameWindow" /> rows without requiring the full plugin runtime.
/// </summary>
public static class GameWindowPersistenceHelper
{
    /// <summary>
    ///     Inserts or updates a <see cref="GameWindow" /> row using the DB-first
    ///     lookup semantics for addon, optional class/job scope, language,
    ///     engine, version, and original payload.
    /// </summary>
    /// <param name="configDirectory">The plugin config directory containing the SQLite database.</param>
    /// <param name="gameWindow">The game window payload to persist.</param>
    /// <param name="onPersisted">
    ///     Optional callback invoked with the updated entity after the DB write succeeds.
    /// </param>
    /// <returns>A status message describing the result.</returns>
    public static async Task<string> InsertGameWindowAsync(
        string configDirectory,
        GameWindow gameWindow,
        Action<GameWindow>? onPersisted = null)
    {
        await using var context = await new EchoglossianDbContextRuntimeFactory(configDirectory).CreateDbContextAsync().ConfigureAwait(false);

        try
        {
            if (gameWindow is null ||
                string.IsNullOrWhiteSpace(gameWindow.WindowAddonName) ||
                string.IsNullOrWhiteSpace(gameWindow.OriginalWindowStringsLang))
            {
                return "Invalid data.";
            }

            var result = await UpsertAsync(context, gameWindow, CancellationToken.None).ConfigureAwait(false);
            var inserted = context.Entry(result.Row).State == EntityState.Added;
            if (result.Changed)
            {
                await context.SaveChangesAsync().ConfigureAwait(false);
            }

            onPersisted?.Invoke(Clone(result.Row));
            return inserted ? "New record inserted." : "Record updated.";
        }
        catch (Exception ex)
        {
            return $"Error inserting GameWindow: {ex.Message}";
        }
    }

    /// <summary>
    ///     Tries to find one existing row that should be updated instead of
    ///     inserting a new row.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="gameWindow">The candidate row.</param>
    /// <param name="cancellationToken">The worker cancellation token.</param>
    /// <returns>The tracked persisted values and whether a mutation is needed.</returns>
    internal static async Task<(GameWindow Row, bool Changed)> UpsertAsync(
        EchoglossianDbContext context,
        GameWindow gameWindow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gameWindow);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameWindow.WindowAddonName);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameWindow.OriginalWindowStringsLang);
        var candidates = await context.GameWindow.Where(row => row.WindowAddonName == gameWindow.WindowAddonName &&
            row.ClassJobId == gameWindow.ClassJobId && row.TranslationEngine == gameWindow.TranslationEngine &&
            row.OriginalWindowStrings == gameWindow.OriginalWindowStrings).ToListAsync(cancellationToken).ConfigureAwait(false);
        var existing = candidates.FirstOrDefault(row => MatchesIdentity(row, gameWindow));
        // A batch commits once, so subsequent operations must also see its pending inserts.
        existing ??= context.GameWindow.Local.FirstOrDefault(row =>
            context.Entry(row).State == EntityState.Added && MatchesIdentity(row, gameWindow));
        if (existing == null)
        {
            var inserted = Clone(gameWindow);
            inserted.Id = 0;
            inserted.RowVersion = null;
            inserted.CreatedDate = DateTime.UtcNow;
            inserted.UpdatedDate = inserted.CreatedDate;
            context.GameWindow.Add(inserted);
            return (inserted, true);
        }

        if (existing.OriginalWindowStringsLang == gameWindow.OriginalWindowStringsLang &&
            existing.OriginalWindowStrings == gameWindow.OriginalWindowStrings &&
            existing.TranslatedWindowStrings == gameWindow.TranslatedWindowStrings)
        {
            return (existing, false);
        }

        existing.OriginalWindowStringsLang = gameWindow.OriginalWindowStringsLang;
        existing.OriginalWindowStrings = gameWindow.OriginalWindowStrings;
        existing.TranslatedWindowStrings = gameWindow.TranslatedWindowStrings;
        existing.UpdatedDate = DateTime.UtcNow;
        return (existing, true);
    }

    /// <summary>Copies managed persisted values without sharing mutable cache state.</summary>
    /// <param name="row">The row to copy.</param>
    /// <returns>A detached row.</returns>
    internal static GameWindow Clone(GameWindow row) => new(row.WindowAddonName, row.OriginalWindowStrings,
        row.OriginalWindowStringsLang, row.TranslatedWindowStrings, row.TranslationLang, row.TranslationEngine,
        row.GameVersion, row.CreatedDate, row.UpdatedDate, row.ClassJobId)
    {
        Id = row.Id,
        RowVersion = row.RowVersion?.ToArray(),
    };

    /// <summary>Matches both committed rows and pending inserts using the stored lookup semantics.</summary>
    /// <param name="row">The existing candidate.</param>
    /// <param name="requested">The requested persisted identity.</param>
    /// <returns>Whether the candidate can be reused for this request.</returns>
    private static bool MatchesIdentity(GameWindow row, GameWindow requested)
    {
        return row.WindowAddonName == requested.WindowAddonName &&
            row.ClassJobId == requested.ClassJobId &&
            row.TranslationEngine == requested.TranslationEngine &&
            row.OriginalWindowStrings == requested.OriginalWindowStrings &&
            RuntimeLanguageHelper.LanguagesMatch(row.OriginalWindowStringsLang, requested.OriginalWindowStringsLang) &&
            RuntimeLanguageHelper.LanguagesMatch(row.TranslationLang, requested.TranslationLang) &&
            GameVersionLookupHelper.MatchesStoredVersion(row.GameVersion, requested.GameVersion);
    }
}
