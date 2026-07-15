// <copyright file="GameWindowPersistenceHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

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
    public static string InsertGameWindow(
        string configDirectory,
        GameWindow gameWindow,
        Action<GameWindow>? onPersisted = null)
    {
        using var context = new EchoglossianDbContext(configDirectory);

        try
        {
            if (gameWindow is null ||
                string.IsNullOrWhiteSpace(gameWindow.WindowAddonName) ||
                string.IsNullOrWhiteSpace(gameWindow.OriginalWindowStringsLang))
            {
                return "Invalid data.";
            }

            var existing = TryFindExistingRow(context, gameWindow);

            if (existing != null)
            {
                existing.OriginalWindowStringsLang = gameWindow.OriginalWindowStringsLang;
                existing.OriginalWindowStrings = gameWindow.OriginalWindowStrings;
                existing.TranslatedWindowStrings = gameWindow.TranslatedWindowStrings;
                existing.UpdatedDate = DateTime.UtcNow;

                context.GameWindow.Update(existing);
                context.SaveChanges();
                onPersisted?.Invoke(existing);

                return "Record updated.";
            }

            gameWindow.CreatedDate = DateTime.UtcNow;
            gameWindow.UpdatedDate = DateTime.UtcNow;

            context.GameWindow.Add(gameWindow);
            context.SaveChanges();
            onPersisted?.Invoke(gameWindow);

            return "New record inserted.";
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
    /// <returns>The matching row, or <see langword="null" />.</returns>
    private static GameWindow? TryFindExistingRow(
        EchoglossianDbContext context,
        GameWindow gameWindow)
    {
        return TryFindExistingExactPayloadRow(context, gameWindow);
    }

    /// <summary>
    ///     Tries to find one exact persisted payload row using the default
    ///     addon semantics.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="gameWindow">The candidate row.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    private static GameWindow? TryFindExistingExactPayloadRow(
        EchoglossianDbContext context,
        GameWindow gameWindow)
    {
        return context.GameWindow
            .AsEnumerable()
            .FirstOrDefault(g =>
                g.WindowAddonName == gameWindow.WindowAddonName &&
                RuntimeLanguageHelper.LanguagesMatch(
                    g.OriginalWindowStringsLang,
                    gameWindow.OriginalWindowStringsLang) &&
                RuntimeLanguageHelper.LanguagesMatch(
                    g.TranslationLang,
                    gameWindow.TranslationLang) &&
                g.ClassJobId == gameWindow.ClassJobId &&
                g.TranslationEngine == gameWindow.TranslationEngine &&
                GameVersionLookupHelper.MatchesStoredVersion(
                    g.GameVersion,
                    gameWindow.GameVersion) &&
                g.OriginalWindowStrings == gameWindow.OriginalWindowStrings);
    }

}
