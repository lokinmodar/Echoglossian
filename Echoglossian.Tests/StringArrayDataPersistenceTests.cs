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
