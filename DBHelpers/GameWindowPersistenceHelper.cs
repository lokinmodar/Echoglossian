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
    private const string ActionMenuWindowName = "ActionMenu";

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
            if (gameWindow is null || string.IsNullOrWhiteSpace(gameWindow.WindowAddonName))
            {
                return "Invalid data.";
            }

            var existing = TryFindExistingRow(context, gameWindow);

            if (existing != null)
            {
                if (IsActionMenuScopedUpsert(gameWindow))
                {
                    existing.OriginalWindowStrings = gameWindow.OriginalWindowStrings;
                }

                existing.OriginalWindowStringsLang = gameWindow.OriginalWindowStringsLang;
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
        return IsActionMenuScopedUpsert(gameWindow)
            ? TryFindExistingActionMenuScopeRow(context, gameWindow)
            : TryFindExistingExactPayloadRow(context, gameWindow);
    }

    /// <summary>
    ///     Determines whether one <see cref="GameWindow" /> should use the
    ///     ActionMenu scoped upsert semantics.
    /// </summary>
    /// <param name="gameWindow">The candidate row.</param>
    /// <returns>
    ///     <see langword="true" /> when the row belongs to
    ///     <c>ActionMenu</c>; otherwise <see langword="false" />.
    /// </returns>
    private static bool IsActionMenuScopedUpsert(GameWindow gameWindow)
    {
        return string.Equals(
            gameWindow.WindowAddonName,
            ActionMenuWindowName,
            StringComparison.Ordinal);
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
                    g.TranslationLang,
                    gameWindow.TranslationLang) &&
                g.ClassJobId == gameWindow.ClassJobId &&
                g.TranslationEngine == gameWindow.TranslationEngine &&
                GameVersionLookupHelper.MatchesStoredVersion(
                    g.GameVersion,
                    gameWindow.GameVersion) &&
                g.OriginalWindowStrings == gameWindow.OriginalWindowStrings);
    }

    /// <summary>
    ///     Tries to find one existing ActionMenu row for the same effective
    ///     lookup scope, regardless of the current payload shape.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="gameWindow">The candidate ActionMenu row.</param>
    /// <returns>The matching scoped row, or <see langword="null" />.</returns>
    private static GameWindow? TryFindExistingActionMenuScopeRow(
        EchoglossianDbContext context,
        GameWindow gameWindow)
    {
        return context.GameWindow
            .AsEnumerable()
            .Where(g =>
                string.Equals(
                    g.WindowAddonName,
                    ActionMenuWindowName,
                    StringComparison.Ordinal) &&
                RuntimeLanguageHelper.LanguagesMatch(
                    g.TranslationLang,
                    gameWindow.TranslationLang) &&
                g.ClassJobId == gameWindow.ClassJobId &&
                g.TranslationEngine == gameWindow.TranslationEngine &&
                GameVersionLookupHelper.MatchesStoredVersion(
                    g.GameVersion,
                    gameWindow.GameVersion))
            .OrderByDescending(static g => g.UpdatedDate ?? g.CreatedDate ?? DateTime.MinValue)
            .ThenByDescending(static g => g.Id)
            .FirstOrDefault();
    }

}
