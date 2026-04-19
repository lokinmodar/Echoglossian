// <copyright file="ActionItemTooltipPersistenceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.NativeUI.Helpers;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers persistence-sensitive action/item tooltip behavior so canonical
///     tooltip rows can evolve without losing DB-first lookup semantics.
/// </summary>
public class ActionItemTooltipPersistenceTests
{
    /// <summary>
    ///     Ensures distinct action variants for the same action id are preserved
    ///     when their source payload hash differs.
    /// </summary>
    [Fact]
    public void InsertActionTooltip_PreservesDistinctSourceHashes()
    {
        var configDir = CreateTempConfigDir();

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var firstPayload = new ActionTooltipCanonicalPayload
            {
                ActionId = 15998,
                IconId = 1,
                Name = "Technical Step",
                Description = "Original description A",
            };
            var secondPayload = new ActionTooltipCanonicalPayload
            {
                ActionId = 15998,
                IconId = 1,
                Name = "Technical Step",
                Description = "Original description B",
            };

            ActionTooltipPersistenceHelper.InsertActionTooltip(
                configDir,
                ActionTooltipPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    firstPayload));
            ActionTooltipPersistenceHelper.InsertActionTooltip(
                configDir,
                ActionTooltipPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    secondPayload));

            using var validationContext = new EchoglossianDbContext(configDir);
            var rows = validationContext.ActionTooltip
                .Where(row => row.ActionId == 15998)
                .ToList();

            Assert.Equal(2, rows.Count);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures an exact action canonical match updates in place.
    /// </summary>
    [Fact]
    public void InsertActionTooltip_UpdatesExactCanonicalMatch()
    {
        var configDir = CreateTempConfigDir();

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var originalPayload = new ActionTooltipCanonicalPayload
            {
                ActionId = 15998,
                IconId = 1,
                Name = "Technical Step",
                Description = "Begin dancing.",
            };
            var translatedPayload = new ActionTooltipCanonicalPayload
            {
                ActionId = 15998,
                IconId = 1,
                Name = "Technical Step",
                Description = "Begin dancing.",
                TranslatedName = "Passo Técnico",
            };
            var updatedPayload = new ActionTooltipCanonicalPayload
            {
                ActionId = 15998,
                IconId = 1,
                Name = "Technical Step",
                Description = "Begin dancing.",
                TranslatedName = "Passo Técnico",
                TranslatedDescription = "Comece a dançar.",
            };

            ActionTooltipPersistenceHelper.InsertActionTooltip(
                configDir,
                ActionTooltipPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    originalPayload,
                    translatedPayload));
            ActionTooltipPersistenceHelper.InsertActionTooltip(
                configDir,
                ActionTooltipPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    originalPayload,
                    updatedPayload));

            using var validationContext = new EchoglossianDbContext(configDir);
            var row = Assert.Single(validationContext.ActionTooltip);

            Assert.Equal("Passo Técnico", row.TranslatedActionName);
            Assert.Equal("Comece a dançar.", row.TranslatedActionDescription);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures a version-agnostic action-tooltip row is reused when a
    ///     later write includes the current game version.
    /// </summary>
    [Fact]
    public void InsertActionTooltip_ReusesVersionAgnosticCanonicalMatch()
    {
        var configDir = CreateTempConfigDir();

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var originalPayload = new ActionTooltipCanonicalPayload
            {
                ActionId = 15998,
                IconId = 1,
                Name = "Technical Step",
                Description = "Begin dancing.",
            };
            var translatedPayload = new ActionTooltipCanonicalPayload
            {
                ActionId = 15998,
                IconId = 1,
                Name = "Technical Step",
                Description = "Begin dancing.",
                TranslatedName = "Passo Técnico",
            };
            var updatedPayload = new ActionTooltipCanonicalPayload
            {
                ActionId = 15998,
                IconId = 1,
                Name = "Technical Step",
                Description = "Begin dancing.",
                TranslatedName = "Passo Técnico",
                TranslatedDescription = "Comece a dançar.",
            };

            ActionTooltipPersistenceHelper.InsertActionTooltip(
                configDir,
                ActionTooltipPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    null,
                    originalPayload,
                    translatedPayload));
            ActionTooltipPersistenceHelper.InsertActionTooltip(
                configDir,
                ActionTooltipPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    originalPayload,
                    updatedPayload));

            using var validationContext = new EchoglossianDbContext(configDir);
            var row = Assert.Single(validationContext.ActionTooltip);

            Assert.Null(row.GameVersion);
            Assert.Equal("Comece a dançar.", row.TranslatedActionDescription);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures distinct item variants for the same item id are preserved
    ///     when their source payload hash differs.
    /// </summary>
    [Fact]
    public void InsertItemTooltip_PreservesDistinctSourceHashes()
    {
        var configDir = CreateTempConfigDir();

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var firstPayload = new ItemTooltipCanonicalPayload
            {
                ItemId = 4868,
                IconId = 1,
                Name = "Gysahl Greens",
                Description = "Original description A",
            };
            var secondPayload = new ItemTooltipCanonicalPayload
            {
                ItemId = 4868,
                IconId = 1,
                Name = "Gysahl Greens",
                Description = "Original description B",
            };

            ItemTooltipPersistenceHelper.InsertItemTooltip(
                configDir,
                ItemTooltipPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    firstPayload));
            ItemTooltipPersistenceHelper.InsertItemTooltip(
                configDir,
                ItemTooltipPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    secondPayload));

            using var validationContext = new EchoglossianDbContext(configDir);
            var rows = validationContext.ItemTooltip
                .Where(row => row.ItemId == 4868)
                .ToList();

            Assert.Equal(2, rows.Count);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures an exact item canonical match updates in place.
    /// </summary>
    [Fact]
    public void InsertItemTooltip_UpdatesExactCanonicalMatch()
    {
        var configDir = CreateTempConfigDir();

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var originalPayload = new ItemTooltipCanonicalPayload
            {
                ItemId = 4868,
                IconId = 1,
                Name = "Gysahl Greens",
                Description = "A leafy vegetable.",
            };
            var translatedPayload = new ItemTooltipCanonicalPayload
            {
                ItemId = 4868,
                IconId = 1,
                Name = "Gysahl Greens",
                Description = "A leafy vegetable.",
                TranslatedName = "Verduras de Gysahl",
            };
            var updatedPayload = new ItemTooltipCanonicalPayload
            {
                ItemId = 4868,
                IconId = 1,
                Name = "Gysahl Greens",
                Description = "A leafy vegetable.",
                TranslatedName = "Verduras de Gysahl",
                TranslatedDescription = "Um vegetal folhoso.",
            };

            ItemTooltipPersistenceHelper.InsertItemTooltip(
                configDir,
                ItemTooltipPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    originalPayload,
                    translatedPayload));
            ItemTooltipPersistenceHelper.InsertItemTooltip(
                configDir,
                ItemTooltipPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    originalPayload,
                    updatedPayload));

            using var validationContext = new EchoglossianDbContext(configDir);
            var row = Assert.Single(validationContext.ItemTooltip);

            Assert.Equal("Verduras de Gysahl", row.TranslatedItemName);
            Assert.Equal("Um vegetal folhoso.", row.TranslatedItemDescription);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures a version-agnostic item-tooltip row is reused when a later
    ///     write includes the current game version.
    /// </summary>
    [Fact]
    public void InsertItemTooltip_ReusesVersionAgnosticCanonicalMatch()
    {
        var configDir = CreateTempConfigDir();

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var originalPayload = new ItemTooltipCanonicalPayload
            {
                ItemId = 4868,
                IconId = 1,
                Name = "Gysahl Greens",
                Description = "A leafy vegetable.",
            };
            var translatedPayload = new ItemTooltipCanonicalPayload
            {
                ItemId = 4868,
                IconId = 1,
                Name = "Gysahl Greens",
                Description = "A leafy vegetable.",
                TranslatedName = "Verduras de Gysahl",
            };
            var updatedPayload = new ItemTooltipCanonicalPayload
            {
                ItemId = 4868,
                IconId = 1,
                Name = "Gysahl Greens",
                Description = "A leafy vegetable.",
                TranslatedName = "Verduras de Gysahl",
                TranslatedDescription = "Um vegetal folhoso.",
            };

            ItemTooltipPersistenceHelper.InsertItemTooltip(
                configDir,
                ItemTooltipPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    null,
                    originalPayload,
                    translatedPayload));
            ItemTooltipPersistenceHelper.InsertItemTooltip(
                configDir,
                ItemTooltipPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    originalPayload,
                    updatedPayload));

            using var validationContext = new EchoglossianDbContext(configDir);
            var row = Assert.Single(validationContext.ItemTooltip);

            Assert.Null(row.GameVersion);
            Assert.Equal("Um vegetal folhoso.", row.TranslatedItemDescription);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Creates a temporary config directory for persistence tests.
    /// </summary>
    /// <returns>The created directory path.</returns>
    private static string CreateTempConfigDir()
    {
        var configDir = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        return configDir;
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
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
