// <copyright file="ActionItemTooltipPersistenceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;
using System.Runtime.CompilerServices;

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite;
using Echoglossian.NativeUI.Helpers;

using Microsoft.EntityFrameworkCore;

using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
///     Covers persistence-sensitive action/item tooltip behavior so canonical
///     tooltip rows can evolve without losing DB-first lookup semantics.
/// </summary>
public class ActionItemTooltipPersistenceTests
{
    /// <summary>
    ///     Ensures an untranslated regional target alias cannot replace a
    ///     complete canonical action translation already cached for the same
    ///     effective target language.
    /// </summary>
    [Fact]
    public void ActionTooltipCache_UpdateIncompleteTargetAlias_PreservesCompleteTranslation()
    {
        ActionTooltipCacheManager.Clear();

        try
        {
            var originalPayload = new ActionTooltipCanonicalPayload
            {
                ActionId = 15989,
                IconId = 1,
                Name = "Cascade",
                Description = "Delivers an attack.",
            };
            var translatedPayload = new ActionTooltipCanonicalPayload
            {
                ActionId = 15989,
                IconId = 1,
                Name = "Cascade",
                Description = "Delivers an attack.",
                TranslatedName = "Cascata",
                TranslatedDescription = "Executa um ataque.",
            };

            ActionTooltipCacheManager.Update(
                ActionTooltipPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    originalPayload,
                    translatedPayload));
            ActionTooltipCacheManager.Update(
                ActionTooltipPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt-BR",
                    0,
                    "7.3",
                    originalPayload));

            var found = ActionTooltipCacheManager.TryFindTranslatedText(
                new TranslationReuseScope("en", "pt-BR", 0, true),
                "7.3",
                "Cascade",
                out var translatedText);

            Assert.True(found);
            Assert.Equal("Cascata", translatedText);
        }
        finally
        {
            ActionTooltipCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures normalized target aliases update one canonical action row
    ///     without erasing its completed translation.
    /// </summary>
    [Fact]
    public void InsertActionTooltip_ReusesCompatibleTargetAlias()
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
                ActionId = 15989,
                IconId = 1,
                Name = "Cascade",
                Description = "Delivers an attack.",
            };
            var translatedPayload = new ActionTooltipCanonicalPayload
            {
                ActionId = 15989,
                IconId = 1,
                Name = "Cascade",
                Description = "Delivers an attack.",
                TranslatedName = "Cascata",
                TranslatedDescription = "Executa um ataque.",
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
                    "pt-BR",
                    0,
                    "7.3",
                    originalPayload));

            using var validationContext = new EchoglossianDbContext(configDir);
            var row = Assert.Single(validationContext.ActionTooltip);
            Assert.Equal("Cascata", row.TranslatedActionName);
            Assert.Equal(
                "Executa um ataque.",
                row.TranslatedActionDescription);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures an untranslated regional target alias cannot replace a
    ///     complete canonical item translation already cached for the same
    ///     effective target language.
    /// </summary>
    [Fact]
    public void ItemTooltipCache_UpdateIncompleteTargetAlias_PreservesCompleteTranslation()
    {
        ItemTooltipCacheManager.Clear();

        try
        {
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
                TranslatedDescription = "Um vegetal folhoso.",
            };
            var completeRow = ItemTooltipPersistenceHelper.CreateCanonicalRow(
                "en",
                "pt",
                0,
                "7.3",
                originalPayload,
                translatedPayload);

            ItemTooltipCacheManager.Update(completeRow);
            ItemTooltipCacheManager.Update(
                ItemTooltipPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt-BR",
                    0,
                    "7.3",
                    originalPayload));

            var row = ItemTooltipCacheManager.TryFindCanonicalMatch(
                originalPayload.ItemId,
                new TranslationReuseScope("en", "pt-BR", 0, true),
                "7.3",
                completeRow.SourceContentHash!);

            Assert.NotNull(row);
            Assert.Equal("Verduras de Gysahl", row.TranslatedItemName);
            Assert.Equal("Um vegetal folhoso.", row.TranslatedItemDescription);
        }
        finally
        {
            ItemTooltipCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures normalized target aliases update one canonical item row
    ///     without erasing its completed translation.
    /// </summary>
    [Fact]
    public void InsertItemTooltip_ReusesCompatibleTargetAlias()
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
                    "pt-BR",
                    0,
                    "7.3",
                    originalPayload));

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
    ///     Ensures an untranslated regional target alias cannot replace a
    ///     complete canonical trait translation already cached for the same
    ///     effective target language.
    /// </summary>
    [Fact]
    public void TraitCache_UpdateIncompleteTargetAlias_PreservesCompleteTranslation()
    {
        TraitCacheManager.Clear();

        try
        {
            var originalPayload = new TraitCanonicalPayload
            {
                TraitId = 201,
                IconId = 1,
                ClassJobId = 38,
                ClassJobCategoryId = 111,
                Name = "Enhanced Windmill",
                Description = "Upgrades Windmill.",
            };
            var translatedPayload = new TraitCanonicalPayload
            {
                TraitId = 201,
                IconId = 1,
                ClassJobId = 38,
                ClassJobCategoryId = 111,
                Name = "Enhanced Windmill",
                Description = "Upgrades Windmill.",
                TranslatedName = "Moinho Aprimorado",
                TranslatedDescription = "Aprimora o Moinho.",
            };
            var completeRow = TraitPersistenceHelper.CreateCanonicalRow(
                "en",
                "pt",
                0,
                "7.3",
                originalPayload,
                translatedPayload);

            TraitCacheManager.Update(completeRow);
            TraitCacheManager.Update(
                TraitPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt-BR",
                    0,
                    "7.3",
                    originalPayload));

            var row = TraitCacheManager.TryFindCanonicalMatch(
                originalPayload.TraitId,
                new TranslationReuseScope("en", "pt-BR", 0, true),
                "7.3",
                completeRow.SourceContentHash!);

            Assert.NotNull(row);
            Assert.Equal("Moinho Aprimorado", row.TranslatedTraitName);
            Assert.Equal("Aprimora o Moinho.", row.TranslatedTraitDescription);
        }
        finally
        {
            TraitCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures normalized target aliases update one canonical trait row
    ///     without erasing its completed translation.
    /// </summary>
    [Fact]
    public void InsertTrait_ReusesCompatibleTargetAlias()
    {
        var configDir = CreateTempConfigDir();

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var originalPayload = new TraitCanonicalPayload
            {
                TraitId = 201,
                IconId = 1,
                ClassJobId = 38,
                ClassJobCategoryId = 111,
                Name = "Enhanced Windmill",
                Description = "Upgrades Windmill.",
            };
            var translatedPayload = new TraitCanonicalPayload
            {
                TraitId = 201,
                IconId = 1,
                ClassJobId = 38,
                ClassJobCategoryId = 111,
                Name = "Enhanced Windmill",
                Description = "Upgrades Windmill.",
                TranslatedName = "Moinho Aprimorado",
                TranslatedDescription = "Aprimora o Moinho.",
            };

            TraitPersistenceHelper.InsertTrait(
                configDir,
                TraitPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    originalPayload,
                    translatedPayload));
            TraitPersistenceHelper.InsertTrait(
                configDir,
                TraitPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt-BR",
                    0,
                    "7.3",
                    originalPayload));

            using var validationContext = new EchoglossianDbContext(configDir);
            var row = Assert.Single(validationContext.Traits);
            Assert.Equal("Moinho Aprimorado", row.TranslatedTraitName);
            Assert.Equal("Aprimora o Moinho.", row.TranslatedTraitDescription);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

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
    ///     Ensures unresolved sheet sentinels do not leak into persisted action
    ///     tooltip identity, source hashes, or canonical payload JSON.
    /// </summary>
    [Fact]
    public void CreateCanonicalRow_ActionTooltip_NormalizesInvalidSheetIdentity()
    {
        var originalPayload = new ActionTooltipCanonicalPayload
        {
            ActionId = 7535,
            IconId = 806,
            ActionCategoryId = 4,
            ClassJobId = uint.MaxValue,
            ClassJobCategoryId = 113,
            Name = "Reprisal",
            Description = "Reduces damage dealt by nearby enemies by 10%.",
        };
        var normalizedPayload = new ActionTooltipCanonicalPayload
        {
            ActionId = 7535,
            IconId = 806,
            ActionCategoryId = 4,
            ClassJobId = 0,
            ClassJobCategoryId = 113,
            Name = "Reprisal",
            Description = "Reduces damage dealt by nearby enemies by 10%.",
        };

        var row = ActionTooltipPersistenceHelper.CreateCanonicalRow(
            "en",
            "pt",
            0,
            "7.3",
            originalPayload);
        var serializedPayload = Assert.IsType<ActionTooltipCanonicalPayload>(
            ActionTooltipCanonicalPayload.Deserialize(row.CanonicalPayloadAsText));

        Assert.Equal((uint)0, row.ClassJobId);
        Assert.Equal((uint)0, serializedPayload.ClassJobId);
        Assert.Equal(
            normalizedPayload.ComputeSourceContentHash(),
            row.SourceContentHash);
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
    ///     Ensures unresolved sheet sentinels do not leak into persisted item
    ///     tooltip identity, source hashes, or canonical payload JSON.
    /// </summary>
    [Fact]
    public void CreateCanonicalRow_ItemTooltip_NormalizesInvalidSheetIdentity()
    {
        var originalPayload = new ItemTooltipCanonicalPayload
        {
            ItemId = 2000001,
            IconId = 1234,
            ItemActionId = uint.MaxValue,
            ItemUiCategoryId = uint.MaxValue,
            ClassJobCategoryId = uint.MaxValue,
            Name = "Aether Compass",
            Description = string.Empty,
        };
        var normalizedPayload = new ItemTooltipCanonicalPayload
        {
            ItemId = 2000001,
            IconId = 1234,
            ItemActionId = 0,
            ItemUiCategoryId = 0,
            ClassJobCategoryId = 0,
            Name = "Aether Compass",
            Description = string.Empty,
        };

        var row = ItemTooltipPersistenceHelper.CreateCanonicalRow(
            "en",
            "pt",
            0,
            "7.3",
            originalPayload);
        var serializedPayload = Assert.IsType<ItemTooltipCanonicalPayload>(
            ItemTooltipCanonicalPayload.Deserialize(row.CanonicalPayloadAsText));

        Assert.Equal((uint)0, row.ItemActionId);
        Assert.Equal((uint)0, row.ItemUiCategoryId);
        Assert.Equal((uint)0, row.ClassJobCategoryId);
        Assert.Equal((uint)0, serializedPayload.ItemActionId);
        Assert.Equal((uint)0, serializedPayload.ItemUiCategoryId);
        Assert.Equal((uint)0, serializedPayload.ClassJobCategoryId);
        Assert.Equal(
            normalizedPayload.ComputeSourceContentHash(),
            row.SourceContentHash);
    }

    /// <summary>
    ///     Ensures distinct trait variants for the same trait id are preserved
    ///     when their source payload hash differs.
    /// </summary>
    [Fact]
    public void InsertTrait_PreservesDistinctSourceHashes()
    {
        var configDir = CreateTempConfigDir();

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var firstPayload = new TraitCanonicalPayload
            {
                TraitId = 201,
                IconId = 1,
                ClassJobId = 38,
                ClassJobCategoryId = 111,
                Name = "Enhanced Windmill",
                Description = "Original description A",
            };
            var secondPayload = new TraitCanonicalPayload
            {
                TraitId = 201,
                IconId = 1,
                ClassJobId = 38,
                ClassJobCategoryId = 111,
                Name = "Enhanced Windmill",
                Description = "Original description B",
            };

            TraitPersistenceHelper.InsertTrait(
                configDir,
                TraitPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    firstPayload));
            TraitPersistenceHelper.InsertTrait(
                configDir,
                TraitPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    secondPayload));

            using var validationContext = new EchoglossianDbContext(configDir);
            var rows = validationContext.Traits
                .Where(row => row.TraitId == 201)
                .ToList();

            Assert.Equal(2, rows.Count);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures an exact trait canonical match updates in place.
    /// </summary>
    [Fact]
    public void InsertTrait_UpdatesExactCanonicalMatch()
    {
        var configDir = CreateTempConfigDir();

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var originalPayload = new TraitCanonicalPayload
            {
                TraitId = 201,
                IconId = 1,
                ClassJobId = 38,
                ClassJobCategoryId = 111,
                Name = "Enhanced Windmill",
                Description = "Upgrades Windmill.",
            };
            var translatedPayload = new TraitCanonicalPayload
            {
                TraitId = 201,
                IconId = 1,
                ClassJobId = 38,
                ClassJobCategoryId = 111,
                Name = "Enhanced Windmill",
                Description = "Upgrades Windmill.",
                TranslatedName = "Moinho Aprimorado",
            };
            var updatedPayload = new TraitCanonicalPayload
            {
                TraitId = 201,
                IconId = 1,
                ClassJobId = 38,
                ClassJobCategoryId = 111,
                Name = "Enhanced Windmill",
                Description = "Upgrades Windmill.",
                TranslatedName = "Moinho Aprimorado",
                TranslatedDescription = "Aprimora o Moinho.",
            };

            TraitPersistenceHelper.InsertTrait(
                configDir,
                TraitPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    originalPayload,
                    translatedPayload));
            TraitPersistenceHelper.InsertTrait(
                configDir,
                TraitPersistenceHelper.CreateCanonicalRow(
                    "en",
                    "pt",
                    0,
                    "7.3",
                    originalPayload,
                    updatedPayload));

            using var validationContext = new EchoglossianDbContext(configDir);
            var row = Assert.Single(validationContext.Traits);

            Assert.Equal("Moinho Aprimorado", row.TranslatedTraitName);
            Assert.Equal("Aprimora o Moinho.", row.TranslatedTraitDescription);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures unresolved sheet sentinels do not leak into persisted trait
    ///     identity, source hashes, or canonical payload JSON.
    /// </summary>
    [Fact]
    public void CreateCanonicalRow_Trait_NormalizesInvalidSheetIdentity()
    {
        var originalPayload = new TraitCanonicalPayload
        {
            TraitId = 642,
            IconId = 1,
            ClassJobId = uint.MaxValue,
            ClassJobCategoryId = uint.MaxValue,
            Name = "Enhanced Second Wind",
            Description = "Increases the healing potency of Second Wind to 800.",
        };
        var normalizedPayload = new TraitCanonicalPayload
        {
            TraitId = 642,
            IconId = 1,
            ClassJobId = 0,
            ClassJobCategoryId = 0,
            Name = "Enhanced Second Wind",
            Description = "Increases the healing potency of Second Wind to 800.",
        };

        var row = TraitPersistenceHelper.CreateCanonicalRow(
            "en",
            "pt",
            0,
            "7.3",
            originalPayload);
        var serializedPayload = Assert.IsType<TraitCanonicalPayload>(
            TraitCanonicalPayload.Deserialize(row.CanonicalPayloadAsText));

        Assert.Equal((uint)0, row.ClassJobId);
        Assert.Equal((uint)0, row.ClassJobCategoryId);
        Assert.Equal((uint)0, serializedPayload.ClassJobId);
        Assert.Equal((uint)0, serializedPayload.ClassJobCategoryId);
        Assert.Equal(
            normalizedPayload.ComputeSourceContentHash(),
            row.SourceContentHash);
    }

    /// <summary>
    ///     Ensures canonical action, item, and trait rows with the same stable
    ///     identity remain separated by source language on write and read.
    /// </summary>
    [Fact]
    public void CanonicalTooltipPersistence_PreservesDistinctSourceLanguages()
    {
        var configDir = CreateTempConfigDir();

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var actionPayload = new ActionTooltipCanonicalPayload
            {
                ActionId = 15998,
                IconId = 1,
                Name = "Technical Step",
                Description = "Begin dancing.",
            };
            var englishAction = ActionTooltipPersistenceHelper.CreateCanonicalRow(
                "en", "pt", 0, "7.3", actionPayload);
            var germanAction = ActionTooltipPersistenceHelper.CreateCanonicalRow(
                "de", "pt", 0, "7.3", actionPayload);

            var itemPayload = new ItemTooltipCanonicalPayload
            {
                ItemId = 4868,
                IconId = 1,
                Name = "Gysahl Greens",
                Description = "A leafy vegetable.",
            };
            var englishItem = ItemTooltipPersistenceHelper.CreateCanonicalRow(
                "en", "pt", 0, "7.3", itemPayload);
            var germanItem = ItemTooltipPersistenceHelper.CreateCanonicalRow(
                "de", "pt", 0, "7.3", itemPayload);

            var traitPayload = new TraitCanonicalPayload
            {
                TraitId = 201,
                IconId = 1,
                ClassJobId = 38,
                ClassJobCategoryId = 111,
                Name = "Enhanced Windmill",
                Description = "Increases potency.",
            };
            var englishTrait = TraitPersistenceHelper.CreateCanonicalRow(
                "en", "pt", 0, "7.3", traitPayload);
            var germanTrait = TraitPersistenceHelper.CreateCanonicalRow(
                "de", "pt", 0, "7.3", traitPayload);

            ActionTooltipPersistenceHelper.InsertActionTooltip(configDir, englishAction);
            ActionTooltipPersistenceHelper.InsertActionTooltip(configDir, germanAction);
            ItemTooltipPersistenceHelper.InsertItemTooltip(configDir, englishItem);
            ItemTooltipPersistenceHelper.InsertItemTooltip(configDir, germanItem);
            TraitPersistenceHelper.InsertTrait(configDir, englishTrait);
            TraitPersistenceHelper.InsertTrait(configDir, germanTrait);

            using var validationContext = new EchoglossianDbContext(configDir);
            Assert.Equal(2, validationContext.ActionTooltip.Count());
            Assert.Equal(2, validationContext.ItemTooltip.Count());
            Assert.Equal(2, validationContext.Traits.Count());

            Assert.Equal(
                "en",
                ActionTooltipPersistenceHelper.FindActionTooltip(
                    configDir,
                    englishAction)?.OriginalLang);
            Assert.Equal(
                "de",
                ActionTooltipPersistenceHelper.FindActionTooltip(
                    configDir,
                    germanAction)?.OriginalLang);
            Assert.Equal(
                "en",
                ItemTooltipPersistenceHelper.FindItemTooltip(
                    configDir,
                    englishItem)?.OriginalLang);
            Assert.Equal(
                "de",
                ItemTooltipPersistenceHelper.FindItemTooltip(
                    configDir,
                    germanItem)?.OriginalLang);
            Assert.Equal(
                "en",
                TraitPersistenceHelper.FindTrait(
                    configDir,
                    englishTrait)?.OriginalLang);
            Assert.Equal(
                "de",
                TraitPersistenceHelper.FindTrait(
                    configDir,
                    germanTrait)?.OriginalLang);
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures canonical tooltip fallback reads honor engine-compatible and
    ///     strict reuse scopes while writes retain exact-engine history.
    /// </summary>
    [Fact]
    public void CanonicalTooltipPersistence_AppliesEngineReusePolicy()
    {
        var configDir = CreateTempConfigDir();

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var actionPayload = new ActionTooltipCanonicalPayload
            {
                ActionId = 15998,
                Name = "Technical Step",
                Description = "Begin dancing.",
            };
            var itemPayload = new ItemTooltipCanonicalPayload
            {
                ItemId = 4868,
                Name = "Gysahl Greens",
                Description = "A leafy vegetable.",
            };
            var traitPayload = new TraitCanonicalPayload
            {
                TraitId = 201,
                Name = "Enhanced Windmill",
                Description = "Increases potency.",
            };
            var storedAction = ActionTooltipPersistenceHelper.CreateCanonicalRow(
                "en", "pt", 7, "7.3", actionPayload);
            var storedItem = ItemTooltipPersistenceHelper.CreateCanonicalRow(
                "en", "pt", 7, "7.3", itemPayload);
            var storedTrait = TraitPersistenceHelper.CreateCanonicalRow(
                "en", "pt", 7, "7.3", traitPayload);
            var actionProbe = ActionTooltipPersistenceHelper.CreateCanonicalRow(
                "en", "pt", 0, "7.3", actionPayload);
            var itemProbe = ItemTooltipPersistenceHelper.CreateCanonicalRow(
                "en", "pt", 0, "7.3", itemPayload);
            var traitProbe = TraitPersistenceHelper.CreateCanonicalRow(
                "en", "pt", 0, "7.3", traitPayload);
            var compatibleScope = new TranslationReuseScope("en", "pt", 0, false);
            var strictScope = new TranslationReuseScope("en", "pt", 0, true);

            ActionTooltipPersistenceHelper.InsertActionTooltip(
                configDir,
                storedAction);
            ItemTooltipPersistenceHelper.InsertItemTooltip(configDir, storedItem);
            TraitPersistenceHelper.InsertTrait(configDir, storedTrait);

            Assert.Equal(
                7,
                ActionTooltipPersistenceHelper.FindActionTooltip(
                    configDir,
                    actionProbe,
                    compatibleScope)?.TranslationEngine);
            Assert.Null(ActionTooltipPersistenceHelper.FindActionTooltip(
                configDir,
                actionProbe,
                strictScope));
            Assert.Equal(
                7,
                ItemTooltipPersistenceHelper.FindItemTooltip(
                    configDir,
                    itemProbe,
                    compatibleScope)?.TranslationEngine);
            Assert.Null(ItemTooltipPersistenceHelper.FindItemTooltip(
                configDir,
                itemProbe,
                strictScope));
            Assert.Equal(
                7,
                TraitPersistenceHelper.FindTrait(
                    configDir,
                    traitProbe,
                    compatibleScope)?.TranslationEngine);
            Assert.Null(TraitPersistenceHelper.FindTrait(
                configDir,
                traitProbe,
                strictScope));

            ActionTooltipPersistenceHelper.InsertActionTooltip(configDir, actionProbe);
            ItemTooltipPersistenceHelper.InsertItemTooltip(configDir, itemProbe);
            TraitPersistenceHelper.InsertTrait(configDir, traitProbe);

            using var validationContext = new EchoglossianDbContext(configDir);
            Assert.Equal(2, validationContext.ActionTooltip.Count());
            Assert.Equal(2, validationContext.ItemTooltip.Count());
            Assert.Equal(2, validationContext.Traits.Count());
        }
        finally
        {
            TryDeleteDirectory(configDir);
        }
    }

    /// <summary>
    ///     Ensures live tooltip fallback reads pass their configured engine
    ///     reuse policy through to persistence.
    /// </summary>
    [Fact]
    public void LiveTooltipFallback_CompatiblePolicyReusesDifferentEngineRows()
    {
        var configDir = CreateTempConfigDir();
        var previousConfigDirectory = PluginEntry.ConfigDirectory;
        PluginEntry.ConfigDirectory = configDir + Path.DirectorySeparatorChar;
        ActionTooltipCacheManager.Clear();
        ItemTooltipCacheManager.Clear();
        TraitCacheManager.Clear();

        try
        {
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
            }

            var actionPayload = new ActionTooltipCanonicalPayload
            {
                ActionId = 15998,
                Name = "Technical Step",
                Description = "Begin dancing.",
            };
            var itemPayload = new ItemTooltipCanonicalPayload
            {
                ItemId = 4868,
                Name = "Gysahl Greens",
                Description = "A leafy vegetable.",
            };
            var traitPayload = new TraitCanonicalPayload
            {
                TraitId = 201,
                Name = "Enhanced Windmill",
                Description = "Increases potency.",
            };
            var storedAction = ActionTooltipPersistenceHelper.CreateCanonicalRow(
                "en", "pt", 7, "7.3", actionPayload);
            var storedItem = ItemTooltipPersistenceHelper.CreateCanonicalRow(
                "en", "pt", 7, "7.3", itemPayload);
            var storedTrait = TraitPersistenceHelper.CreateCanonicalRow(
                "en", "pt", 7, "7.3", traitPayload);
            var actionProbe = ActionTooltipPersistenceHelper.CreateCanonicalRow(
                "en", "pt", 0, "7.3", actionPayload);
            var itemProbe = ItemTooltipPersistenceHelper.CreateCanonicalRow(
                "en", "pt", 0, "7.3", itemPayload);
            var traitProbe = TraitPersistenceHelper.CreateCanonicalRow(
                "en", "pt", 0, "7.3", traitPayload);

            ActionTooltipPersistenceHelper.InsertActionTooltip(
                configDir,
                storedAction);
            ItemTooltipPersistenceHelper.InsertItemTooltip(configDir, storedItem);
            TraitPersistenceHelper.InsertTrait(configDir, storedTrait);

            var plugin = (PluginEntry)RuntimeHelpers.GetUninitializedObject(
                typeof(PluginEntry));
            var configurationField = typeof(PluginEntry).GetField(
                "configuration",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(configurationField);
            configurationField.SetValue(plugin, new Config
            {
                TranslateAlreadyTranslatedTexts = false,
            });

            Assert.Equal(7, plugin.FindActionTooltip(actionProbe)?.TranslationEngine);
            Assert.Equal(7, plugin.FindItemTooltip(itemProbe)?.TranslationEngine);
            Assert.Equal(7, plugin.FindTrait(traitProbe)?.TranslationEngine);
        }
        finally
        {
            ActionTooltipCacheManager.Clear();
            ItemTooltipCacheManager.Clear();
            TraitCacheManager.Clear();
            PluginEntry.ConfigDirectory = previousConfigDirectory;
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
