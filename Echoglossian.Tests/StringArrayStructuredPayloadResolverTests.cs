// <copyright file="StringArrayStructuredPayloadResolverTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers resolution of canonical structured payloads from persisted
///     <see cref="StringArrayDatas" /> rows.
/// </summary>
public class StringArrayStructuredPayloadResolverTests
{
    /// <summary>
    ///     Ensures the resolver prefers the persisted structured payload when it
    ///     exists.
    /// </summary>
    [Fact]
    public void TryResolvePayloads_UsesStructuredPayloadWhenPresent()
    {
        var originalPayload = CreatePayload(
            type: "Character",
            contextKey: "Character:Profile",
            translatedText: null);
        var translatedPayload = CreatePayload(
            type: "Character",
            contextKey: "Character:Profile",
            translatedText: "Perfil");
        var row = StringArrayDataPersistenceHelper.CreateCanonicalRow(
            type: "Character",
            originalLang: "en",
            translationLang: "pt",
            translationEngine: 0,
            gameVersion: "7.3",
            originalPayload: originalPayload,
            translatedPayload: translatedPayload);

        var resolved = StringArrayStructuredPayloadResolver.TryResolvePayloads(
            row,
            out var resolvedOriginal,
            out var resolvedTranslated);

        Assert.True(resolved);
        Assert.NotNull(resolvedOriginal);
        Assert.NotNull(resolvedTranslated);
        Assert.Equal("Profile", resolvedOriginal!.Slots[0].OriginalText);
        Assert.Equal("Perfil", resolvedTranslated!.Slots[0].TranslatedText);
        Assert.Equal("name", resolvedTranslated.Slots[0].SemanticKey);
    }

    /// <summary>
    ///     Ensures the resolver can project a usable payload from legacy flat
    ///     slot maps when no structured payload exists yet.
    /// </summary>
    [Fact]
    public void TryResolvePayloads_FallsBackToLegacySlotMaps()
    {
        var row = new StringArrayDatas(
            type: "Hud",
            size: 2,
            rawData: null,
            formattedRawData: null,
            originalLang: "en",
            originalStrings: "{\"0\":\"Duty List\",\"1\":\"Current Objective\"}",
            translationLang: "pt",
            translatedStrings: "{\"0\":\"Lista de Missões\",\"1\":\"Objetivo Atual\"}",
            translatedStringsWithPayloads: null,
            translationEngine: 0,
            gameVersion: "7.3",
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow)
        {
            ContextKey = "Hud:DutyList",
            SchemaVersion = 1,
        };

        var resolved = StringArrayStructuredPayloadResolver.TryResolvePayloads(
            row,
            out var originalPayload,
            out var translatedPayload);

        Assert.True(resolved);
        Assert.NotNull(originalPayload);
        Assert.NotNull(translatedPayload);
        Assert.Equal("slot:0", originalPayload!.Slots[0].SemanticKey);
        Assert.Equal("Duty List", originalPayload.Slots[0].OriginalText);
        Assert.Equal(
            "Lista de Missões",
            translatedPayload!.Slots[0].TranslatedText);
        Assert.Equal(
            "Objetivo Atual",
            translatedPayload.Slots[1].TranslatedText);
    }

    /// <summary>
    ///     Ensures sparse translated structured payloads keep the original slot
    ///     semantics and can still be completed by translated flat slot maps.
    /// </summary>
    [Fact]
    public void ResolveTranslatedPayload_PreservesOriginalSlotMetadata()
    {
        var originalPayload = new StringArrayStructuredPayload
        {
            Type = "RecommendList",
            ContextKey = "RecommendList:Current",
            SchemaVersion = 1,
        };
        originalPayload.Slots[20] = new StringArrayStructuredSlot
        {
            SemanticKey = "hover:0",
            OriginalText = "Speak with Momodi.",
            IsVisible = true,
            IsTranslatable = true,
        };
        originalPayload.Slots[21] = new StringArrayStructuredSlot
        {
            SemanticKey = "hover:1",
            OriginalText = "Speak with Baderon.",
            IsVisible = false,
            IsTranslatable = true,
        };

        var sparseTranslated = new StringArrayStructuredPayload
        {
            Type = "RecommendList",
            ContextKey = "RecommendList:Current",
            SchemaVersion = 1,
        };
        sparseTranslated.Slots[20] = new StringArrayStructuredSlot
        {
            SemanticKey = "hover:0",
            OriginalText = "Speak with Momodi.",
            TranslatedText = "Fale com Momodi.",
            IsVisible = true,
            IsTranslatable = true,
        };

        var row = StringArrayDataPersistenceHelper.CreateCanonicalRow(
            type: "RecommendList",
            originalLang: "en",
            translationLang: "pt",
            translationEngine: 0,
            gameVersion: "7.3",
            originalPayload: originalPayload,
            translatedPayload: sparseTranslated);
        row.TranslatedStrings = "{\"21\":\"Fale com Baderon.\"}";

        var translatedPayload = StringArrayStructuredPayloadResolver
            .ResolveTranslatedPayload(row, originalPayload);

        Assert.Equal("hover:0", translatedPayload.Slots[20].SemanticKey);
        Assert.Equal("hover:1", translatedPayload.Slots[21].SemanticKey);
        Assert.True(translatedPayload.Slots[20].IsVisible);
        Assert.False(translatedPayload.Slots[21].IsVisible);
        Assert.Equal(
            "Fale com Momodi.",
            translatedPayload.Slots[20].TranslatedText);
        Assert.Equal(
            "Fale com Baderon.",
            translatedPayload.Slots[21].TranslatedText);
    }

    /// <summary>
    ///     Creates a minimal canonical payload for test coverage.
    /// </summary>
    /// <param name="type">The payload type.</param>
    /// <param name="contextKey">The context key.</param>
    /// <param name="translatedText">The translated text to store in the slot.</param>
    /// <returns>The canonical payload.</returns>
    private static StringArrayStructuredPayload CreatePayload(
        string type,
        string contextKey,
        string? translatedText)
    {
        var payload = new StringArrayStructuredPayload
        {
            Type = type,
            ContextKey = contextKey,
            SchemaVersion = 1,
        };

        payload.Slots[0] = new StringArrayStructuredSlot
        {
            SemanticKey = "name",
            OriginalText = "Profile",
            TranslatedText = translatedText,
            IsVisible = true,
            IsTranslatable = true,
        };

        return payload;
    }
}
