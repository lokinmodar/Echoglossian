// <copyright file="StringArrayStructuredPayloadTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the canonical structured payload used by future DB-first
///     StringArrayData surfaces.
/// </summary>
public class StringArrayStructuredPayloadTests
{
    /// <summary>
    ///     Ensures the source-content hash depends only on original source
    ///     semantics, not on translated text.
    /// </summary>
    [Fact]
    public void ComputeSourceContentHash_IgnoresTranslatedText()
    {
        var first = CreatePayload("Perfil");
        var second = CreatePayload("Profile translated differently");

        var firstHash = first.ComputeSourceContentHash();
        var secondHash = second.ComputeSourceContentHash();

        Assert.Equal(firstHash, secondHash);
    }

    /// <summary>
    ///     Ensures the payload round-trips through JSON without losing slot
    ///     metadata.
    /// </summary>
    [Fact]
    public void SerializeAndDeserialize_RoundTripsPayload()
    {
        var payload = CreatePayload("Perfil");

        var serialized = payload.Serialize();
        var deserialized = StringArrayStructuredPayload.Deserialize(serialized);

        Assert.NotNull(deserialized);
        Assert.Equal("Character", deserialized!.Type);
        Assert.Equal("Character:Profile", deserialized.ContextKey);
        Assert.Equal("name", deserialized.Slots[0].SemanticKey);
        Assert.Equal("Profile", deserialized.Slots[0].OriginalText);
        Assert.Equal("Perfil", deserialized.Slots[0].TranslatedText);
    }

    /// <summary>
    ///     Creates a minimal canonical payload for test coverage.
    /// </summary>
    /// <param name="translatedText">The translated text to store in the slot.</param>
    /// <returns>The canonical payload.</returns>
    private static StringArrayStructuredPayload CreatePayload(
        string translatedText)
    {
        var payload = new StringArrayStructuredPayload
        {
            Type = "Character",
            ContextKey = "Character:Profile",
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
