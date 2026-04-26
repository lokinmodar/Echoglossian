// <copyright file="MainCommandTextPersistenceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers persistence-sensitive MainCommand sheet behavior so the
///     sheet-backed canonical lookup stays stable while `_MainCommand` remains
///     on its working `GameWindow` runtime.
/// </summary>
public class MainCommandTextPersistenceTests
{
    /// <summary>
    ///     Ensures negative sheet sentinels do not overflow persisted
    ///     <c>MainCommand.SortID</c> metadata.
    /// </summary>
    [Fact]
    public void NormalizeMainCommandSortId_ReturnsNullForNegativeSentinel()
    {
        Assert.Null(Echoglossian.NormalizeMainCommandSortId(-1));
        Assert.Equal((uint)0, Echoglossian.NormalizeMainCommandSortId(0));
        Assert.Equal((uint)40, Echoglossian.NormalizeMainCommandSortId(40));
    }

    /// <summary>
    ///     Ensures the runtime only persists positive <c>MainCommand.Unknown0</c>
    ///     values, matching the client-side usage contract.
    /// </summary>
    [Fact]
    public void NormalizeMainCommandUnknown0_ReturnsNullForZeroSentinel()
    {
        Assert.Null(Echoglossian.NormalizeMainCommandUnknown0(0));
        Assert.Equal((uint)9, Echoglossian.NormalizeMainCommandUnknown0(9));
    }

    /// <summary>
    ///     Ensures one exact MainCommand canonical match updates in place while
    ///     preserving sheet metadata.
    /// </summary>
    [Fact]
    public void InsertMainCommandText_UpdatesExactCanonicalMatchAndPreservesMetadata()
    {
        var configDir = CreateTempConfigDir();

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var originalPayload = new ReferenceTextCanonicalPayload
            {
                ReferenceId = 12,
                IconId = 1234,
                CategoryId = 7,
                MainCommandCategoryId = 3,
                Unknown0 = 9,
                SortId = 40,
                Name = "Actions & Traits",
                Description = "Open the actions and traits window.",
            };
            var firstTranslatedPayload = new ReferenceTextCanonicalPayload
            {
                ReferenceId = 12,
                IconId = 1234,
                CategoryId = 7,
                MainCommandCategoryId = 3,
                Unknown0 = 9,
                SortId = 40,
                Name = "Actions & Traits",
                Description = "Open the actions and traits window.",
                TranslatedName = "Acoes e Caracteristicas",
            };
            var updatedTranslatedPayload = new ReferenceTextCanonicalPayload
            {
                ReferenceId = 12,
                IconId = 1234,
                CategoryId = 7,
                MainCommandCategoryId = 3,
                Unknown0 = 9,
                SortId = 40,
                Name = "Actions & Traits",
                Description = "Open the actions and traits window.",
                TranslatedName = "Acoes e Caracteristicas",
                TranslatedDescription =
                    "Abre a janela de acoes e caracteristicas.",
            };

            ReferenceTextPersistenceHelper.InsertReferenceText(
                configDir,
                CreateCanonicalMainCommandTextRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    originalPayload,
                    firstTranslatedPayload),
                static context => context.MainCommandTexts);
            ReferenceTextPersistenceHelper.InsertReferenceText(
                configDir,
                CreateCanonicalMainCommandTextRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    originalPayload,
                    updatedTranslatedPayload),
                static context => context.MainCommandTexts);

            using var validationContext = new EchoglossianDbContext(configDir);
            var row = Assert.Single(validationContext.MainCommandTexts);

            Assert.Equal((uint)12, row.MainCommandId);
            Assert.Equal((uint)1234, row.IconId);
            Assert.Equal((uint)7, row.CategoryId);
            Assert.Equal((uint)3, row.MainCommandCategoryId);
            Assert.Equal((uint)9, row.Unknown0);
            Assert.Equal((uint)40, row.SortId);
            Assert.Equal(
                "Acoes e Caracteristicas",
                row.TranslatedName);
            Assert.Equal(
                "Abre a janela de acoes e caracteristicas.",
                row.TranslatedDescription);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures distinct MainCommand metadata variants for the same row id
    ///     are preserved when the source hash differs.
    /// </summary>
    [Fact]
    public void InsertMainCommandText_PreservesDistinctMetadataSensitiveHashes()
    {
        var configDir = CreateTempConfigDir();

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var firstPayload = new ReferenceTextCanonicalPayload
            {
                ReferenceId = 12,
                IconId = 1234,
                CategoryId = 7,
                MainCommandCategoryId = 3,
                Unknown0 = 9,
                SortId = 40,
                Name = "Actions & Traits",
                Description = "Open the actions and traits window.",
            };
            var secondPayload = new ReferenceTextCanonicalPayload
            {
                ReferenceId = 12,
                IconId = 1234,
                CategoryId = 8,
                MainCommandCategoryId = 3,
                Unknown0 = 9,
                SortId = 40,
                Name = "Actions & Traits",
                Description = "Open the actions and traits window.",
            };

            ReferenceTextPersistenceHelper.InsertReferenceText(
                configDir,
                CreateCanonicalMainCommandTextRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    firstPayload),
                static context => context.MainCommandTexts);
            ReferenceTextPersistenceHelper.InsertReferenceText(
                configDir,
                CreateCanonicalMainCommandTextRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    secondPayload),
                static context => context.MainCommandTexts);

            using var validationContext = new EchoglossianDbContext(configDir);
            var rows = validationContext.MainCommandTexts
                .Where(row => row.ReferenceId == 12)
                .ToList();

            Assert.Equal(2, rows.Count);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures the specific MainCommand cache resolves exact translated and
    ///     canonical original text lookups.
    /// </summary>
    [Fact]
    public void MainCommandCacheStore_ResolvesForwardAndReverseLookups()
    {
        var cache = new ReferenceTextCacheStore<MainCommandText>(
            "MainCommandTextTestCache");
        var row = CreateCanonicalMainCommandTextRow(
            "en",
            "pt",
            0,
            "7.3",
            new ReferenceTextCanonicalPayload
            {
                ReferenceId = 12,
                IconId = 1234,
                CategoryId = 7,
                MainCommandCategoryId = 3,
                Unknown0 = 9,
                SortId = 40,
                Name = "Actions & Traits",
                Description = "Open the actions and traits window.",
            },
            new ReferenceTextCanonicalPayload
            {
                ReferenceId = 12,
                IconId = 1234,
                CategoryId = 7,
                MainCommandCategoryId = 3,
                Unknown0 = 9,
                SortId = 40,
                Name = "Actions & Traits",
                Description = "Open the actions and traits window.",
                TranslatedName = "Acoes e Caracteristicas",
                TranslatedDescription =
                    "Abre a janela de acoes e caracteristicas.",
            });

        cache.Update(row);

        Assert.True(
            cache.TryFindTranslatedText(
                "pt",
                0,
                "7.3",
                "Actions & Traits",
                out var translatedText));
        Assert.Equal("Acoes e Caracteristicas", translatedText);

        Assert.True(
            cache.TryFindOriginalText(
                "pt",
                0,
                "7.3",
                "Acoes e Caracteristicas",
                out var originalText));
        Assert.Equal("Actions & Traits", originalText);

        Assert.True(
            cache.TryFindTranslatedText(
                "pt-BR",
                0,
                "7.3",
                "Actions & Traits",
                out var translatedRegionalText));
        Assert.Equal("Acoes e Caracteristicas", translatedRegionalText);

        Assert.True(
            cache.TryFindOriginalText(
                "pt-BR",
                0,
                "7.3",
                "Acoes e Caracteristicas",
                out var originalRegionalText));
        Assert.Equal("Actions & Traits", originalRegionalText);
    }

    /// <summary>
    ///     Creates one canonical MainCommand row using the runtime's
    ///     metadata-sensitive hash semantics.
    /// </summary>
    /// <param name="originalLang">The language of the original payload.</param>
    /// <param name="translationLang">The target translation language.</param>
    /// <param name="translationEngine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The game version associated with the payload.</param>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="translatedPayload">The translated canonical payload, if any.</param>
    /// <returns>The canonical MainCommand row.</returns>
    private static MainCommandText CreateCanonicalMainCommandTextRow(
        string originalLang,
        string translationLang,
        int? translationEngine,
        string? gameVersion,
        ReferenceTextCanonicalPayload originalPayload,
        ReferenceTextCanonicalPayload? translatedPayload = null)
    {
        var row = ReferenceTextPersistenceHelper.CreateCanonicalRow<MainCommandText>(
            originalLang,
            translationLang,
            translationEngine,
            gameVersion,
            originalPayload,
            translatedPayload);
        row.IconId = originalPayload.IconId;
        row.CategoryId = originalPayload.CategoryId;
        row.MainCommandCategoryId = originalPayload.MainCommandCategoryId;
        row.Unknown0 = originalPayload.Unknown0;
        row.SortId = originalPayload.SortId;
        row.SourceContentHash = ComputeMainCommandSourceContentHash(
            originalPayload);
        return row;
    }

    /// <summary>
    ///     Computes the metadata-sensitive MainCommand source hash.
    /// </summary>
    /// <param name="payload">The canonical source payload.</param>
    /// <returns>The stable source hash.</returns>
    private static string ComputeMainCommandSourceContentHash(
        ReferenceTextCanonicalPayload payload)
    {
        var builder = new StringBuilder();
        builder.Append(payload.SchemaVersion)
            .Append('|')
            .Append(payload.ReferenceId)
            .Append('|')
            .Append(payload.IconId?.ToString() ?? string.Empty)
            .Append('|')
            .Append(payload.CategoryId?.ToString() ?? string.Empty)
            .Append('|')
            .Append(payload.MainCommandCategoryId?.ToString() ?? string.Empty)
            .Append('|')
            .Append(payload.Unknown0?.ToString() ?? string.Empty)
            .Append('|')
            .Append(payload.SortId?.ToString() ?? string.Empty)
            .Append('|')
            .Append(payload.Name)
            .Append('|')
            .Append(payload.Description ?? string.Empty);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    ///     Creates one temporary plugin config directory for SQLite tests.
    /// </summary>
    /// <returns>The temp directory path.</returns>
    private static string CreateTempConfigDir()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "EchoglossianTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    ///     Deletes one temporary test directory when it still exists.
    /// </summary>
    /// <param name="directory">The directory to delete.</param>
    private static void TryDeleteDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
