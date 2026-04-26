// <copyright file="GameWindowPersistenceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers persistence-sensitive <see cref="GameWindow" /> behavior so
///     DB-first addon runtimes can preserve multiple payload variants for the
///     same addon without overwriting prior rows.
/// </summary>
public class GameWindowPersistenceTests
{
    /// <summary>
    ///     Ensures different original payloads for the same addon are inserted
    ///     as separate rows instead of overwriting one another.
    /// </summary>
    [Fact]
    public void InsertGameWindow_PreservesDistinctVariants_ForSameAddonLookupScope()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var first = new GameWindow(
                windowAddonName: "Character",
                originalWindowStrings: "{\"atkValues\":{\"1\":\"Profile A\"}}",
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"atkValues\":{\"1\":\"Perfil A\"}}",
                translationLang: "pt",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow);
            var second = new GameWindow(
                windowAddonName: "Character",
                originalWindowStrings: "{\"atkValues\":{\"1\":\"Profile B\"}}",
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"atkValues\":{\"1\":\"Perfil B\"}}",
                translationLang: "pt",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow);

            GameWindowPersistenceHelper.InsertGameWindow(configDir, first);
            GameWindowPersistenceHelper.InsertGameWindow(configDir, second);

            using var validationContext = new EchoglossianDbContext(configDir);
            var rows = validationContext.GameWindow
                .Where(window =>
                    window.WindowAddonName == "Character" &&
                    window.TranslationLang == "pt" &&
                    window.TranslationEngine == 0 &&
                    window.GameVersion == "7.3")
                .OrderBy(window => window.OriginalWindowStrings)
                .ToList();

            Assert.Equal(2, rows.Count);
            Assert.Equal(
                "{\"atkValues\":{\"1\":\"Profile A\"}}",
                rows[0].OriginalWindowStrings);
            Assert.Equal(
                "{\"atkValues\":{\"1\":\"Profile B\"}}",
                rows[1].OriginalWindowStrings);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures the exact payload row is updated in place when the addon,
    ///     engine, language, version, and original payload all match.
    /// </summary>
    [Fact]
    public void InsertGameWindow_UpdatesExactPayloadMatch_InPlace()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var originalJson = "{\"atkValues\":{\"1\":\"Main Command\"}}";
            GameWindowPersistenceHelper.InsertGameWindow(configDir, new GameWindow(
                windowAddonName: "_MainCommand",
                originalWindowStrings: originalJson,
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"atkValues\":{\"1\":\"Comando Principal\"}}",
                translationLang: "pt",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow));
            GameWindowPersistenceHelper.InsertGameWindow(configDir, new GameWindow(
                windowAddonName: "_MainCommand",
                originalWindowStrings: originalJson,
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"atkValues\":{\"1\":\"Comandos\"}}",
                translationLang: "pt",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow));

            using var validationContext = new EchoglossianDbContext(configDir);
            var rows = validationContext.GameWindow
                .Where(window =>
                    window.WindowAddonName == "_MainCommand" &&
                    window.OriginalWindowStrings == originalJson)
                .ToList();

            var row = Assert.Single(rows);
            Assert.Equal(
                "{\"atkValues\":{\"1\":\"Comandos\"}}",
                row.TranslatedWindowStrings);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures normalized language aliases reuse the same row instead of
    ///     inserting duplicates for the same effective translation target.
    /// </summary>
    [Fact]
    public void InsertGameWindow_TreatsNormalizedLanguageAliasesAsOneRow()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var originalJson = "{\"atkValues\":{\"1\":\"Main Command\"}}";
            GameWindowPersistenceHelper.InsertGameWindow(configDir, new GameWindow(
                windowAddonName: "_MainCommand",
                originalWindowStrings: originalJson,
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"atkValues\":{\"1\":\"Comando Principal\"}}",
                translationLang: "pt",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow));
            GameWindowPersistenceHelper.InsertGameWindow(configDir, new GameWindow(
                windowAddonName: "_MainCommand",
                originalWindowStrings: originalJson,
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"atkValues\":{\"1\":\"Comandos\"}}",
                translationLang: "pt-BR",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow));

            using var validationContext = new EchoglossianDbContext(configDir);
            var rows = validationContext.GameWindow
                .Where(window =>
                    window.WindowAddonName == "_MainCommand" &&
                    window.OriginalWindowStrings == originalJson)
                .ToList();

            var row = Assert.Single(rows);
            Assert.Equal(
                "{\"atkValues\":{\"1\":\"Comandos\"}}",
                row.TranslatedWindowStrings);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures a version-agnostic row is reused when the runtime now
    ///     provides a concrete game version for the same logical payload.
    /// </summary>
    [Fact]
    public void InsertGameWindow_ReusesVersionAgnosticRow()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var originalJson = "{\"atkValues\":{\"1\":\"Main Command\"}}";
            GameWindowPersistenceHelper.InsertGameWindow(configDir, new GameWindow(
                windowAddonName: "_MainCommand",
                originalWindowStrings: originalJson,
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"atkValues\":{\"1\":\"Comando Principal\"}}",
                translationLang: "pt-BR",
                translationEngine: 0,
                gameVersion: null,
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow));
            GameWindowPersistenceHelper.InsertGameWindow(configDir, new GameWindow(
                windowAddonName: "_MainCommand",
                originalWindowStrings: originalJson,
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"atkValues\":{\"1\":\"Comandos\"}}",
                translationLang: "pt-BR",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow));

            using var validationContext = new EchoglossianDbContext(configDir);
            var row = Assert.Single(validationContext.GameWindow);

            Assert.Null(row.GameVersion);
            Assert.Equal(
                "{\"atkValues\":{\"1\":\"Comandos\"}}",
                row.TranslatedWindowStrings);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures the same payload can coexist for different class/job
    ///     scopes without one row overwriting the other.
    /// </summary>
    [Fact]
    public void InsertGameWindow_PreservesDistinctClassJobScopedVariants()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var originalJson = "{\"atkValues\":{\"17\":\"Peloton\"}}";
            GameWindowPersistenceHelper.InsertGameWindow(configDir, new GameWindow(
                windowAddonName: "ActionMenu",
                originalWindowStrings: originalJson,
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"atkValues\":{\"17\":\"Pelotão\"}}",
                translationLang: "pt-BR",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow,
                classJobId: 38));
            GameWindowPersistenceHelper.InsertGameWindow(configDir, new GameWindow(
                windowAddonName: "ActionMenu",
                originalWindowStrings: originalJson,
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"atkValues\":{\"17\":\"Pelotão\"}}",
                translationLang: "pt-BR",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow,
                classJobId: 19));

            using var validationContext = new EchoglossianDbContext(configDir);
            var rows = validationContext.GameWindow
                .Where(window =>
                    window.WindowAddonName == "ActionMenu" &&
                    window.OriginalWindowStrings == originalJson)
                .OrderBy(window => window.ClassJobId)
                .ToList();

            Assert.Equal(2, rows.Count);
            Assert.Equal<uint?>(19, rows[0].ClassJobId);
            Assert.Equal<uint?>(38, rows[1].ClassJobId);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures one class/job-scoped row is updated in place when the
    ///     payload and class/job id both match.
    /// </summary>
    [Fact]
    public void InsertGameWindow_UpdatesExactClassJobScopedMatch_InPlace()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var originalJson = "{\"atkValues\":{\"17\":\"Peloton\"}}";
            GameWindowPersistenceHelper.InsertGameWindow(configDir, new GameWindow(
                windowAddonName: "ActionMenu",
                originalWindowStrings: originalJson,
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"atkValues\":{\"17\":\"Pelotão\"}}",
                translationLang: "pt-BR",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow,
                classJobId: 38));
            GameWindowPersistenceHelper.InsertGameWindow(configDir, new GameWindow(
                windowAddonName: "ActionMenu",
                originalWindowStrings: originalJson,
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"atkValues\":{\"17\":\"Peloton\"}}",
                translationLang: "pt-BR",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow,
                classJobId: 38));

            using var validationContext = new EchoglossianDbContext(configDir);
            var rows = validationContext.GameWindow
                .Where(window =>
                    window.WindowAddonName == "ActionMenu" &&
                    window.OriginalWindowStrings == originalJson)
                .ToList();

            var row = Assert.Single(rows);
            Assert.Equal<uint?>(38, row.ClassJobId);
            Assert.Equal(
                "{\"atkValues\":{\"17\":\"Peloton\"}}",
                row.TranslatedWindowStrings);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures ActionMenu now updates a single scoped row even when the
    ///     visible payload changes across categories inside the same window.
    /// </summary>
    [Fact]
    public void InsertGameWindow_ActionMenuCollapsesDistinctPayloads_IntoScopedRow()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            GameWindowPersistenceHelper.InsertGameWindow(configDir, new GameWindow(
                windowAddonName: "ActionMenu",
                originalWindowStrings: "{\"atkValues\":{\"17\":\"Peloton\"}}",
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"atkValues\":{\"17\":\"Pelotão\"}}",
                translationLang: "pt-BR",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow,
                classJobId: 38));
            GameWindowPersistenceHelper.InsertGameWindow(configDir, new GameWindow(
                windowAddonName: "ActionMenu",
                originalWindowStrings: "{\"atkValues\":{\"17\":\"Cascade\"}}",
                originalWindowStringsLang: "en",
                translatedWindowStrings: "{\"atkValues\":{\"17\":\"Cascata\"}}",
                translationLang: "pt-BR",
                translationEngine: 0,
                gameVersion: "7.3",
                createdDate: DateTime.UtcNow,
                updatedDate: DateTime.UtcNow,
                classJobId: 38));

            using var validationContext = new EchoglossianDbContext(configDir);
            var rows = validationContext.GameWindow
                .Where(window =>
                    window.WindowAddonName == "ActionMenu" &&
                    window.ClassJobId == 38 &&
                    window.TranslationLang == "pt-BR" &&
                    window.TranslationEngine == 0 &&
                    window.GameVersion == "7.3")
                .ToList();

            var row = Assert.Single(rows);
            Assert.Equal(
                "{\"atkValues\":{\"17\":\"Cascade\"}}",
                row.OriginalWindowStrings);
            Assert.Equal(
                "{\"atkValues\":{\"17\":\"Cascata\"}}",
                row.TranslatedWindowStrings);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Deletes a temporary test directory when possible.
    /// </summary>
    /// <param name="path">The path to delete.</param>
    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup for transient SQLite file locks during tests.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort cleanup for transient SQLite file locks during tests.
        }
    }
}
