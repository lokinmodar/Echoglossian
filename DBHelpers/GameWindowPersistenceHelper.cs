// <copyright file="GameWindowPersistenceHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.Helpers;
using System.Security.Cryptography;
using System.Text;

namespace Echoglossian;

/// <summary>
///     Persists <see cref="GameWindow" /> rows without requiring the full plugin runtime.
/// </summary>
public static class GameWindowPersistenceHelper
{
    private static readonly DiagnosticTelemetryHelper ActionMenuPersistenceTelemetry = new(
        "ActionMenuPersist",
        TimeSpan.FromMilliseconds(250));

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

            var existing = context.GameWindow
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

            if (existing != null)
            {
                existing.OriginalWindowStringsLang = gameWindow.OriginalWindowStringsLang;
                existing.TranslatedWindowStrings = gameWindow.TranslatedWindowStrings;
                existing.UpdatedDate = DateTime.UtcNow;

                context.GameWindow.Update(existing);
                context.SaveChanges();

                LogActionMenuPersistence(
                    "update",
                    existing,
                    existing.Id);
                onPersisted?.Invoke(existing);

                return "Record updated.";
            }

            gameWindow.CreatedDate = DateTime.UtcNow;
            gameWindow.UpdatedDate = DateTime.UtcNow;

            context.GameWindow.Add(gameWindow);
            context.SaveChanges();

            LogActionMenuPersistence(
                "insert",
                gameWindow,
                gameWindow.Id);
            onPersisted?.Invoke(gameWindow);

            return "New record inserted.";
        }
        catch (Exception ex)
        {
            return $"Error inserting GameWindow: {ex.Message}";
        }
    }

    /// <summary>
    ///     Emits one focused persistence diagnostic for ActionMenu rows so the
    ///     live log can distinguish inserts from updates and correlate them
    ///     with payload hashes.
    /// </summary>
    /// <param name="operation">The persistence operation label.</param>
    /// <param name="row">The persisted row.</param>
    /// <param name="persistedId">The database identifier of the row.</param>
    private static void LogActionMenuPersistence(
        string operation,
        GameWindow row,
        long persistedId)
    {
        if (row == null ||
            !string.Equals(
                row.WindowAddonName,
                "ActionMenu",
                StringComparison.Ordinal))
        {
            return;
        }

        ActionMenuPersistenceTelemetry.Information(
            operation,
            $"operation={operation} id={persistedId} classJobId={row.ClassJobId?.ToString() ?? "<none>"} translationLang={row.TranslationLang ?? "<none>"} engine={row.TranslationEngine} gameVersion={row.GameVersion ?? "<none>"} originalHash={ComputeDiagnosticHash(row.OriginalWindowStrings)} translatedHash={ComputeDiagnosticHash(row.TranslatedWindowStrings)} originalLen={row.OriginalWindowStrings?.Length ?? 0} translatedLen={row.TranslatedWindowStrings?.Length ?? 0}",
            signature: $"{operation}|{persistedId}|{ComputeDiagnosticHash(row.OriginalWindowStrings)}|{ComputeDiagnosticHash(row.TranslatedWindowStrings)}",
            cooldown: TimeSpan.FromMilliseconds(1));
    }

    /// <summary>
    ///     Computes one short diagnostic hash for correlating persisted JSON
    ///     payloads with the ActionMenu runtime logs.
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <returns>The short uppercase hexadecimal hash.</returns>
    private static string ComputeDiagnosticHash(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "EMPTY";
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes.AsSpan(0, 6));
    }
}
