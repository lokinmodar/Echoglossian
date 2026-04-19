// <copyright file="StringArrayDataPersistenceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers persistence-sensitive <see cref="StringArrayDatas" /> behavior so
///     the canonical DB-first contract can evolve additively without losing the
///     structured payload fields needed by future schema-driven runtimes.
/// </summary>
public class StringArrayDataPersistenceTests
{
    /// <summary>
    ///     Ensures the canonical structured payload fields round-trip through
    ///     the EF migration schema.
    /// </summary>
    [Fact]
    public void StringArrayDatas_PersistsCanonicalPayloadFields()
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

            var row = new StringArrayDatas(
                type: "Hud",
                size: 3,
                rawData: [0x01, 0x02, 0x03],
                formattedRawData: null,
                originalLang: "en",
                originalStrings: "{\"0\":\"Duty List\"}",
                translationLang: "pt",
                translatedStrings: "{\"0\":\"Lista de Missões\"}",
                translatedStringsWithPayloads: null,
                translationEngine: 0,
                gameVersion: "7.3",
                createdAt: DateTime.UtcNow,
                updatedAt: DateTime.UtcNow)
            {
                ContextKey = "Hud:DutyList",
                SchemaVersion = 1,
                SourceContentHash = "hash-123",
                OriginalStructuredPayload = "{\"slots\":{\"0\":\"Duty List\"}}",
                TranslatedStructuredPayload = "{\"slots\":{\"0\":\"Lista de Missões\"}}",
            };

            using (var context = new EchoglossianDbContext(configDir))
            {
                context.StringArrayDatas.Add(row);
                context.SaveChanges();
            }

            using var validationContext = new EchoglossianDbContext(configDir);
            var saved = Assert.Single(validationContext.StringArrayDatas);

            Assert.Equal("Hud:DutyList", saved.ContextKey);
            Assert.Equal(1, saved.SchemaVersion);
            Assert.Equal("hash-123", saved.SourceContentHash);
            Assert.Equal(
                "{\"slots\":{\"0\":\"Duty List\"}}",
                saved.OriginalStructuredPayload);
            Assert.Equal(
                "{\"slots\":{\"0\":\"Lista de Missões\"}}",
                saved.TranslatedStructuredPayload);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures different canonical contexts for the same string-array type
    ///     are preserved as distinct rows.
    /// </summary>
    [Fact]
    public void InsertStringArrayData_PreservesDistinctCanonicalContexts()
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

            var first = CreateCanonicalRow(
                type: "Character",
                contextKey: "Character:Profile",
                sourceHash: "hash-a",
                translatedPayload: "{\"slots\":{\"0\":\"Perfil\"}}");
            var second = CreateCanonicalRow(
                type: "Character",
                contextKey: "Character:Status",
                sourceHash: "hash-b",
                translatedPayload: "{\"slots\":{\"0\":\"Status\"}}");

            StringArrayDataPersistenceHelper.InsertStringArrayData(configDir, first);
            StringArrayDataPersistenceHelper.InsertStringArrayData(configDir, second);

            using var validationContext = new EchoglossianDbContext(configDir);
            var rows = validationContext.StringArrayDatas
                .Where(row =>
                    row.Type == "Character" &&
                    row.TranslationLang == "pt" &&
                    row.TranslationEngine == 0 &&
                    row.GameVersion == "7.3")
                .OrderBy(row => row.ContextKey)
                .ToList();

            Assert.Equal(2, rows.Count);
            Assert.Equal("Character:Profile", rows[0].ContextKey);
            Assert.Equal("Character:Status", rows[1].ContextKey);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures an exact canonical match updates the existing row instead of
    ///     inserting a duplicate.
    /// </summary>
    [Fact]
    public void InsertStringArrayData_UpdatesExactCanonicalMatch()
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

            var original = CreateCanonicalRow(
                type: "MainCommand",
                contextKey: "MainCommand:Root",
                sourceHash: "hash-root",
                translatedPayload: "{\"slots\":{\"0\":\"Menu Principal\"}}");
            var updated = CreateCanonicalRow(
                type: "MainCommand",
                contextKey: "MainCommand:Root",
                sourceHash: "hash-root",
                translatedPayload: "{\"slots\":{\"0\":\"Comando Principal\"}}");

            StringArrayDataPersistenceHelper.InsertStringArrayData(configDir, original);
            StringArrayDataPersistenceHelper.InsertStringArrayData(configDir, updated);

            using var validationContext = new EchoglossianDbContext(configDir);
            var row = Assert.Single(validationContext.StringArrayDatas);

            Assert.Equal("MainCommand:Root", row.ContextKey);
            Assert.Equal(
                "{\"slots\":{\"0\":\"Comando Principal\"}}",
                row.TranslatedStructuredPayload);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures a version-agnostic canonical row is reused when the runtime
    ///     later supplies a concrete game version for the same payload.
    /// </summary>
    [Fact]
    public void InsertStringArrayData_ReusesVersionAgnosticCanonicalMatch()
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

            var original = CreateCanonicalRow(
                type: "MainCommand",
                contextKey: "MainCommand:Root",
                sourceHash: "hash-root",
                translatedPayload: "{\"slots\":{\"0\":\"Menu Principal\"}}");
            original.GameVersion = null;

            var updated = CreateCanonicalRow(
                type: "MainCommand",
                contextKey: "MainCommand:Root",
                sourceHash: "hash-root",
                translatedPayload: "{\"slots\":{\"0\":\"Comando Principal\"}}");

            StringArrayDataPersistenceHelper.InsertStringArrayData(configDir, original);
            StringArrayDataPersistenceHelper.InsertStringArrayData(configDir, updated);

            using var validationContext = new EchoglossianDbContext(configDir);
            var row = Assert.Single(validationContext.StringArrayDatas);

            Assert.Null(row.GameVersion);
            Assert.Equal(
                "{\"slots\":{\"0\":\"Comando Principal\"}}",
                row.TranslatedStructuredPayload);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Creates a canonical string-array row for persistence tests.
    /// </summary>
    /// <param name="type">The string-array type.</param>
    /// <param name="contextKey">The canonical context key.</param>
    /// <param name="sourceHash">The stable source-content hash.</param>
    /// <param name="translatedPayload">The translated structured payload.</param>
    /// <returns>The canonical row.</returns>
    private static StringArrayDatas CreateCanonicalRow(
        string type,
        string contextKey,
        string sourceHash,
        string translatedPayload)
    {
        return new StringArrayDatas(
            type: type,
            size: 1,
            rawData: [0x01, 0x02],
            formattedRawData: "0102",
            originalLang: "en",
            originalStrings: "{\"0\":\"Profile\"}",
            translationLang: "pt",
            translatedStrings: "{\"0\":\"Perfil\"}",
            translatedStringsWithPayloads: null,
            translationEngine: 0,
            gameVersion: "7.3",
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow)
        {
            ContextKey = contextKey,
            SchemaVersion = 1,
            SourceContentHash = sourceHash,
            OriginalStructuredPayload = "{\"slots\":{\"0\":\"Profile\"}}",
            TranslatedStructuredPayload = translatedPayload,
        };
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
