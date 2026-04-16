// <copyright file="StringArrayStructuredPayloadBuilderTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers typed-schema construction of canonical
///     <see cref="StringArrayStructuredPayload" /> instances.
/// </summary>
public class StringArrayStructuredPayloadBuilderTests
{
    /// <summary>
    ///     Ensures the payload builder keeps only schema-owned slots and
    ///     preserves the schema metadata for those slots.
    /// </summary>
    [Fact]
    public void Build_UsesTypedSchemaDescriptions()
    {
        var slotTexts = new Dictionary<int, string?>
        {
            [0] = "Character",
            [1] = "Profile",
            [2] = string.Empty,
            [50] = "Ignored",
        };

        var payload = StringArrayStructuredPayloadBuilder.Build(
            new FakeCharacterSchema(),
            "Character:Profile",
            slotTexts);

        Assert.Equal("Character", payload.Type);
        Assert.Equal("Character:Profile", payload.ContextKey);
        Assert.Equal(3, payload.SchemaVersion);
        Assert.Equal(3, payload.Slots.Count);
        Assert.Equal("header:title", payload.Slots[0].SemanticKey);
        Assert.False(payload.Slots[0].IsTranslatable);
        Assert.Equal("tab:profile", payload.Slots[1].SemanticKey);
        Assert.True(payload.Slots[1].IsVisible);
        Assert.True(payload.Slots[1].IsTranslatable);
        Assert.Equal("tab:empty", payload.Slots[2].SemanticKey);
        Assert.False(payload.Slots.ContainsKey(50));
    }

    /// <summary>
    ///     Provides a tiny typed schema for builder tests.
    /// </summary>
    private sealed class FakeCharacterSchema : IStringArrayStructuredSchema
    {
        /// <inheritdoc />
        public string Type => "Character";

        /// <inheritdoc />
        public int SchemaVersion => 3;

        /// <inheritdoc />
        public bool TryDescribeSlot(
            int slotIndex,
            string? slotText,
            out StringArrayStructuredSlotDescription description)
        {
            description = slotIndex switch
            {
                0 => new StringArrayStructuredSlotDescription(
                    "header:title",
                    IsVisible: true,
                    IsTranslatable: false),
                1 => new StringArrayStructuredSlotDescription("tab:profile"),
                2 => new StringArrayStructuredSlotDescription("tab:empty"),
                _ => new StringArrayStructuredSlotDescription(string.Empty),
            };

            return slotIndex <= 2;
        }
    }
}
