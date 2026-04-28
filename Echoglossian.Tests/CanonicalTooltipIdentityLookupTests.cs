// <copyright file="CanonicalTooltipIdentityLookupTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers identity-based fallback lookup for canonical action, item, and
///     trait tooltip rows.
/// </summary>
public class CanonicalTooltipIdentityLookupTests
{
    /// <summary>
    ///     Ensures action fallback prefers the row scoped to the requested
    ///     class/job when multiple translated rows share the same action id.
    /// </summary>
    [Fact]
    public void TryFindIdentityMatch_ActionTooltip_PrefersMatchingClassJob()
    {
        ActionTooltipCacheManager.Clear();

        try
        {
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 1,
                ActionId = 16005,
                ActionName = "Saber Dance",
                ActionDescription = "Deals damage.",
                TranslatedActionName = "Saber Dance - Other",
                TranslatedActionDescription = "Causa dano.",
                TranslationLang = "pt",
                TranslationEngine = 0,
                GameVersion = "2026.03.17.0000.0000",
                ClassJobId = 31,
                ClassJobCategoryId = 86,
                SourceContentHash = "hash-other",
            });
            ActionTooltipCacheManager.Update(new ActionTooltip
            {
                Id = 2,
                ActionId = 16005,
                ActionName = "Saber Dance",
                ActionDescription = "Deals damage.",
                TranslatedActionName = "Dança de Sabre",
                TranslatedActionDescription = "Causa dano.",
                TranslationLang = "pt",
                TranslationEngine = 0,
                GameVersion = "2026.03.17.0000.0000",
                ClassJobId = 38,
                ClassJobCategoryId = 86,
                SourceContentHash = "hash-dnc",
            });

            var found = ActionTooltipCacheManager.TryFindIdentityMatch(
                16005,
                "pt-BR",
                0,
                "2026.03.17.0000.0000",
                38,
                86);

            Assert.NotNull(found);
            Assert.Equal("Dança de Sabre", found.TranslatedActionName);
        }
        finally
        {
            ActionTooltipCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures item fallback reuses one translated row by id, language,
    ///     engine, and version without depending on the canonical hash.
    /// </summary>
    [Fact]
    public void TryFindIdentityMatch_ItemTooltip_ReusesTranslatedRow()
    {
        ItemTooltipCacheManager.Clear();

        try
        {
            ItemTooltipCacheManager.Update(new ItemTooltip
            {
                Id = 1,
                ItemId = 23167,
                ItemName = "Super-Potion",
                ItemDescription =
                    "This concentrated concoction instantly restores a significant amount of HP.",
                TranslatedItemName = "Super-Poção",
                TranslatedItemDescription =
                    "Essa mistura concentrada restaura instantaneamente uma quantidade significativa de HP.",
                TranslationLang = "pt",
                TranslationEngine = 0,
                GameVersion = "2026.03.17.0000.0000",
                ClassJobCategoryId = 0,
                SourceContentHash = "hash-super-potion",
            });

            var found = ItemTooltipCacheManager.TryFindIdentityMatch(
                23167,
                "pt-BR",
                0,
                "2026.03.17.0000.0000",
                0);

            Assert.NotNull(found);
            Assert.Equal("Super-Poção", found.TranslatedItemName);
            Assert.False(string.IsNullOrWhiteSpace(found.TranslatedItemDescription));
        }
        finally
        {
            ItemTooltipCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures trait fallback prefers the row scoped to the requested
    ///     class/job when the same trait id exists for multiple jobs.
    /// </summary>
    [Fact]
    public void TryFindIdentityMatch_Trait_PrefersMatchingClassJob()
    {
        TraitCacheManager.Clear();

        try
        {
            TraitCacheManager.Update(new Trait
            {
                Id = 1,
                TraitId = 642,
                TraitName = "Enhanced Second Wind",
                TraitDescription = "Increases the healing potency of Second Wind to 800.",
                TranslatedTraitName = "Vento Revigorado Aprimorado",
                TranslatedTraitDescription =
                    "Aumenta a potência de cura do Second Wind para 800.",
                TranslationLang = "pt",
                TranslationEngine = 0,
                GameVersion = "2026.03.17.0000.0000",
                ClassJobId = 31,
                ClassJobCategoryId = 35,
                SourceContentHash = "hash-reaper",
            });
            TraitCacheManager.Update(new Trait
            {
                Id = 2,
                TraitId = 642,
                TraitName = "Enhanced Second Wind",
                TraitDescription = "Increases the healing potency of Second Wind to 800.",
                TranslatedTraitName = "Uma segunda onda de energia aprimorada",
                TranslatedTraitDescription =
                    "Aumenta o poder de cura do Segundo Fôlego para 800.",
                TranslationLang = "pt",
                TranslationEngine = 0,
                GameVersion = "2026.03.17.0000.0000",
                ClassJobId = 38,
                ClassJobCategoryId = 35,
                SourceContentHash = "hash-dancer",
            });

            var found = TraitCacheManager.TryFindIdentityMatch(
                642,
                "pt-BR",
                0,
                "2026.03.17.0000.0000",
                38,
                35);

            Assert.NotNull(found);
            Assert.Equal(
                "Uma segunda onda de energia aprimorada",
                found.TranslatedTraitName);
        }
        finally
        {
            TraitCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures action-detail fallback can resolve one translated
    ///     EventAction payload by stable identity from the dedicated
    ///     reference-text cache.
    /// </summary>
    [Fact]
    public void TryFindTranslatedActionIdentityPayload_EventAction_ReusesDedicatedRow()
    {
        ReferenceTextCacheRegistry.EventActionTexts.Clear();

        try
        {
            var payload = new ReferenceTextCanonicalPayload
            {
                ReferenceId = 44,
                Name = "Duty Action",
                Description = null,
                TranslatedName = "Acao da Missao",
                TranslatedDescription = null,
            };

            ReferenceTextCacheRegistry.EventActionTexts.Update(new EventActionText
            {
                Id = 1,
                ReferenceId = 44,
                OriginalName = payload.Name,
                OriginalLang = "English",
                TranslatedName = payload.TranslatedName,
                TranslationLang = "pt",
                TranslationEngine = 0,
                GameVersion = "2026.04.27.0000.0000",
                SourceContentHash = payload.ComputeSourceContentHash(),
                CanonicalPayloadAsText = payload.Serialize(),
            });

            var found = ReferenceTextCacheRegistry.TryFindTranslatedActionIdentityPayload(
                44,
                "pt-BR",
                0,
                "2026.04.27.0000.0000",
                out var resolvedPayload);

            Assert.True(found);
            Assert.Equal("Acao da Missao", resolvedPayload.TranslatedName);
        }
        finally
        {
            ReferenceTextCacheRegistry.EventActionTexts.Clear();
        }
    }

    /// <summary>
    ///     Ensures item-detail fallback can resolve one translated
    ///     <c>EventItem</c> payload by stable identity from the dedicated
    ///     reference-text cache.
    /// </summary>
    [Fact]
    public void TryFindTranslatedItemIdentityPayload_EventItem_ReusesDedicatedRow()
    {
        ReferenceTextCacheRegistry.EventItemTexts.Clear();

        try
        {
            var payload = new ReferenceTextCanonicalPayload
            {
                ReferenceId = 2000001,
                ActionId = 77,
                IconId = 1234,
                Name = "Aether Compass",
                Description = null,
                TranslatedName = "Bussola Eterea",
                TranslatedDescription = null,
            };

            ReferenceTextCacheRegistry.EventItemTexts.Update(new EventItemText
            {
                Id = 1,
                ReferenceId = 2000001,
                OriginalName = payload.Name,
                OriginalLang = "English",
                TranslatedName = payload.TranslatedName,
                TranslationLang = "pt",
                TranslationEngine = 0,
                GameVersion = "2026.04.27.0000.0000",
                SourceContentHash = payload.ComputeSourceContentHash(),
                CanonicalPayloadAsText = payload.Serialize(),
            });

            var found = ReferenceTextCacheRegistry.TryFindTranslatedItemIdentityPayload(
                2000001,
                "pt-BR",
                0,
                "2026.04.27.0000.0000",
                out var resolvedPayload);

            Assert.True(found);
            Assert.Equal("Bussola Eterea", resolvedPayload.TranslatedName);
        }
        finally
        {
            ReferenceTextCacheRegistry.EventItemTexts.Clear();
        }
    }

    /// <summary>
    ///     Ensures item-detail fallback can resolve one translated
    ///     <c>DeepDungeonItem</c> payload by stable identity from the
    ///     dedicated reference-text cache.
    /// </summary>
    [Fact]
    public void TryFindTranslatedItemIdentityPayload_DeepDungeonItem_ReusesDedicatedRow()
    {
        ReferenceTextCacheRegistry.DeepDungeonItemTexts.Clear();

        try
        {
            var payload = new ReferenceTextCanonicalPayload
            {
                ReferenceId = 5,
                ActionId = 91,
                IconId = 4567,
                Name = "Pomander of Strength",
                Description = "Increases damage dealt.",
                TranslatedName = "Pomander de Forca",
                TranslatedDescription = "Aumenta o dano causado.",
            };

            ReferenceTextCacheRegistry.DeepDungeonItemTexts.Update(
                new DeepDungeonItemText
                {
                    Id = 1,
                    ReferenceId = 5,
                    OriginalName = payload.Name,
                    OriginalDescription = payload.Description,
                    OriginalLang = "English",
                    TranslatedName = payload.TranslatedName,
                    TranslatedDescription = payload.TranslatedDescription,
                    TranslationLang = "pt",
                    TranslationEngine = 0,
                    GameVersion = "2026.04.27.0000.0000",
                    SourceContentHash = payload.ComputeSourceContentHash(),
                    CanonicalPayloadAsText = payload.Serialize(),
                });

            var found = ReferenceTextCacheRegistry.TryFindTranslatedItemIdentityPayload(
                5,
                "pt-BR",
                0,
                "2026.04.27.0000.0000",
                out var resolvedPayload);

            Assert.True(found);
            Assert.Equal("Pomander de Forca", resolvedPayload.TranslatedName);
            Assert.Equal(
                "Aumenta o dano causado.",
                resolvedPayload.TranslatedDescription);
        }
        finally
        {
            ReferenceTextCacheRegistry.DeepDungeonItemTexts.Clear();
        }
    }

    /// <summary>
    ///     Ensures structured action-detail payload conversion prefers the
    ///     linked action id when the reference-text family supplies one.
    /// </summary>
    [Fact]
    public void CreateActionTooltipPayloadFromReferencePayload_PrefersLinkedActionId()
    {
        var referencePayload = new ReferenceTextCanonicalPayload
        {
            ReferenceId = 20,
            ActionId = 1695,
            IconId = 116,
            Name = "Dig",
            Description =
                "Locate a buried treasure coffer within a 30-yalm radius and extract it from the ground.",
        };

        var payload = Echoglossian.CreateActionTooltipPayloadFromReferencePayload(
            referencePayload,
            currentClassJobId: 1);

        Assert.Equal((uint)1695, payload.ActionId);
        Assert.Equal((uint)116, payload.IconId);
        Assert.Equal("Dig", payload.Name);
    }

    /// <summary>
    ///     Ensures structured action-detail payload conversion falls back to
    ///     the reference id when no linked action id exists.
    /// </summary>
    [Fact]
    public void CreateActionTooltipPayloadFromReferencePayload_FallsBackToReferenceId()
    {
        var referencePayload = new ReferenceTextCanonicalPayload
        {
            ReferenceId = 44,
            ActionId = null,
            IconId = 912,
            Name = "Duty Action",
            Description = string.Empty,
        };

        var payload = Echoglossian.CreateActionTooltipPayloadFromReferencePayload(
            referencePayload,
            currentClassJobId: 1);

        Assert.Equal((uint)44, payload.ActionId);
        Assert.Equal((uint)912, payload.IconId);
        Assert.Equal("Duty Action", payload.Name);
    }
}
