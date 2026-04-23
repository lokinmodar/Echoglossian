// <copyright file="CharacterCanonicalPayloadHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Character;
using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers canonical exact-text reuse for Character-family GameWindow
///     payloads backed by the shared <c>addon:Character</c> structured row.
/// </summary>
public class CharacterCanonicalPayloadHelperTests
{
    /// <summary>
    ///     Ensures already-translated live text can be canonicalized back to
    ///     the original source text before DB-first lookup continues.
    /// </summary>
    [Fact]
    public void TryCanonicalizePayload_RewritesTranslatedTextBackToOriginal()
    {
        var payload = new StringArrayStructuredPayload();
        payload.Slots[0] = new StringArrayStructuredSlot
        {
            SemanticKey = "stringarray:0",
            OriginalText = "Storm Captain",
            TranslatedText = "Capitão da Tempestade",
        };
        payload.TextNodes["1:0"] = new StringArrayStructuredSlot
        {
            SemanticKey = "textnode:1:0",
            OriginalText = "Social Relations",
            TranslatedText = "Relações Sociais",
        };

        var originalLookup = new Dictionary<string, string>(StringComparer.Ordinal);
        var translatedLookup = new Dictionary<string, string>(StringComparer.Ordinal);
        var knownTexts = new HashSet<string>(StringComparer.Ordinal);
        CharacterCanonicalPayloadHelper.AppendLookupEntries(
            payload.Slots.Values,
            originalLookup,
            translatedLookup,
            knownTexts);
        CharacterCanonicalPayloadHelper.AppendLookupEntries(
            payload.TextNodes.Values,
            originalLookup,
            translatedLookup,
            knownTexts);

        var livePayload = new DbFirstGameWindowPayload(
            AtkValues: new SortedDictionary<int, string>(),
            StringArrayValues: new SortedDictionary<int, string>
            {
                [4] = "Capitão da Tempestade",
            },
            TextNodes: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["10:0"] = "Relações Sociais",
            });

        var resolved = CharacterCanonicalPayloadHelper.TryCanonicalizePayload(
            livePayload,
            originalLookup,
            out var canonicalPayload);

        Assert.True(resolved);
        Assert.Equal("Storm Captain", canonicalPayload.StringArrayValues[4]);
        Assert.Equal("Social Relations", canonicalPayload.TextNodes["10:0"]);
    }

    /// <summary>
    ///     Ensures canonical original text can be translated back to the target
    ///     text using the same shared lookup.
    /// </summary>
    [Fact]
    public void TryTranslatePayload_RewritesOriginalTextToTranslatedText()
    {
        var payload = new StringArrayStructuredPayload();
        payload.Slots[0] = new StringArrayStructuredSlot
        {
            SemanticKey = "stringarray:0",
            OriginalText = "Storm Captain",
            TranslatedText = "Capitão da Tempestade",
        };
        payload.TextNodes["1:0"] = new StringArrayStructuredSlot
        {
            SemanticKey = "textnode:1:0",
            OriginalText = "Social Relations",
            TranslatedText = "Relações Sociais",
        };

        var originalLookup = new Dictionary<string, string>(StringComparer.Ordinal);
        var translatedLookup = new Dictionary<string, string>(StringComparer.Ordinal);
        var knownTexts = new HashSet<string>(StringComparer.Ordinal);
        CharacterCanonicalPayloadHelper.AppendLookupEntries(
            payload.Slots.Values,
            originalLookup,
            translatedLookup,
            knownTexts);
        CharacterCanonicalPayloadHelper.AppendLookupEntries(
            payload.TextNodes.Values,
            originalLookup,
            translatedLookup,
            knownTexts);

        var originalPayload = new DbFirstGameWindowPayload(
            AtkValues: new SortedDictionary<int, string>(),
            StringArrayValues: new SortedDictionary<int, string>
            {
                [4] = "Storm Captain",
            },
            TextNodes: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["10:0"] = "Social Relations",
            });

        var resolved = CharacterCanonicalPayloadHelper.TryTranslatePayload(
            originalPayload,
            translatedLookup,
            out var translatedPayload);

        Assert.True(resolved);
        Assert.Equal(
            "Capitão da Tempestade",
            translatedPayload.StringArrayValues[4]);
        Assert.Equal("Relações Sociais", translatedPayload.TextNodes["10:0"]);
    }

    /// <summary>
    ///     Ensures addon-local GameWindow pairs can contribute canonical
    ///     lookups for Character-family windows whose text is not present in
    ///     the shared <c>addon:Character</c> string-array row.
    /// </summary>
    [Fact]
    public void AppendLookupEntries_FromGameWindowPairs_RecoversAddonLocalOriginals()
    {
        var originalLookup = new Dictionary<string, string>(StringComparer.Ordinal);
        var translatedLookup = new Dictionary<string, string>(StringComparer.Ordinal);
        var knownTexts = new HashSet<string>(StringComparer.Ordinal);

        var originalPayload = new DbFirstGameWindowPayload(
            AtkValues: new SortedDictionary<int, string>(),
            StringArrayValues: new SortedDictionary<int, string>(),
            TextNodes: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["17:0"] = "Healer",
                ["10:0"] = "Societal Relations",
            });
        var translatedPayload = new DbFirstGameWindowPayload(
            AtkValues: new SortedDictionary<int, string>(),
            StringArrayValues: new SortedDictionary<int, string>(),
            TextNodes: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["17:0"] = "Curador",
                ["10:0"] = "Relações Sociais",
            });

        CharacterCanonicalPayloadHelper.AppendLookupEntries(
            originalPayload.TextNodes,
            translatedPayload.TextNodes,
            originalLookup,
            translatedLookup,
            knownTexts,
            requireDifference: true);

        var livePayload = new DbFirstGameWindowPayload(
            AtkValues: new SortedDictionary<int, string>(),
            StringArrayValues: new SortedDictionary<int, string>(),
            TextNodes: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["17:0"] = "Curador",
                ["10:0"] = "Relações Sociais",
            });

        var resolved = CharacterCanonicalPayloadHelper.TryCanonicalizePayload(
            livePayload,
            originalLookup,
            out var canonicalPayload);

        Assert.True(resolved);
        Assert.Equal("Healer", canonicalPayload.TextNodes["17:0"]);
        Assert.Equal(
            "Societal Relations",
            canonicalPayload.TextNodes["10:0"]);
    }

    /// <summary>
    ///     Ensures repeated visible text is collapsed to the first stable key so
    ///     recycled Character-family rows do not persist duplicate labels such
    ///     as repeated expansion names.
    /// </summary>
    [Fact]
    public void CollapseDuplicateTextValues_KeepsFirstOccurrenceOfEachValue()
    {
        var sourceValues = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["10:0"] = "Societal Relations",
            ["2:0"] = "Heavensward",
            ["2:1"] = "Heavensward",
            ["3:5"] = "Player Commendation",
            ["3:6"] = "Player Commendation",
            ["8:0"] = "Daily Quest Allowance",
        };

        var collapsedValues =
            CharacterCanonicalPayloadHelper.CollapseDuplicateTextValues(
                sourceValues);

        Assert.Equal(4, collapsedValues.Count);
        Assert.Equal("Heavensward", collapsedValues["2:0"]);
        Assert.False(collapsedValues.ContainsKey("2:1"));
        Assert.Equal("Player Commendation", collapsedValues["3:5"]);
        Assert.False(collapsedValues.ContainsKey("3:6"));
    }

    /// <summary>
    ///     Ensures dynamic Character-family lists can build one value-to-value
    ///     mapping that ignores unstable node ordinals.
    /// </summary>
    [Fact]
    public void BuildValueMap_MapsSourceValuesToTargetValues()
    {
        var sourceValues = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["3:0"] = "Tank",
            ["3:1"] = "Healer",
            ["3:2"] = "Gladiator",
        };
        var targetValues = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["3:0"] = "Tanque",
            ["3:1"] = "Curador",
            ["3:2"] = "Gladiador",
        };

        var valueMap = CharacterCanonicalPayloadHelper.BuildValueMap(
            sourceValues,
            targetValues);

        Assert.Equal("Tanque", valueMap["Tank"]);
        Assert.Equal("Curador", valueMap["Healer"]);
        Assert.Equal("Gladiador", valueMap["Gladiator"]);
    }

    /// <summary>
    ///     Ensures value-based apply can rewrite visible text nodes using text
    ///     sourced from ATK values and string-array values, not only from
    ///     explicit text-node payload entries.
    /// </summary>
    [Fact]
    public void BuildValueMap_FromPayloads_IncludesAtkAndStringArrayTexts()
    {
        var sourcePayload = new DbFirstGameWindowPayload(
            AtkValues: new SortedDictionary<int, string>
            {
                [4] = "Strength",
            },
            StringArrayValues: new SortedDictionary<int, string>
            {
                [11] = "Critical Hit",
            },
            TextNodes: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["1:0"] = "Attributes",
            });
        var targetPayload = new DbFirstGameWindowPayload(
            AtkValues: new SortedDictionary<int, string>
            {
                [4] = "Força",
            },
            StringArrayValues: new SortedDictionary<int, string>
            {
                [11] = "Acerto Crítico",
            },
            TextNodes: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["1:0"] = "Atributos",
            });

        var valueMap = CharacterCanonicalPayloadHelper.BuildValueMap(
            sourcePayload,
            targetPayload);

        Assert.Equal("Força", valueMap["Strength"]);
        Assert.Equal("Acerto Crítico", valueMap["Critical Hit"]);
        Assert.Equal("Atributos", valueMap["Attributes"]);
    }

    /// <summary>
    ///     Ensures canonical fallback can still translate one visible text not
    ///     present in the active payload pair when the shared Character lookup
    ///     already knows the exact original/translated pair.
    /// </summary>
    [Fact]
    public void TryResolveCanonicalFallbackTarget_MapsMissingCharacterRootText()
    {
        var directValueMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Strength"] = "Força",
            ["Attributes"] = "Atributos",
        };
        var originalLookup = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Strength"] = "Strength",
            ["Força"] = "Strength",
            ["Attributes"] = "Attributes",
            ["Atributos"] = "Attributes",
            ["Gear Set"] = "Gear Set",
            ["Conjunto de Equipamentos"] = "Gear Set",
        };
        var translatedLookup = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Strength"] = "Força",
            ["Força"] = "Força",
            ["Attributes"] = "Atributos",
            ["Atributos"] = "Atributos",
            ["Gear Set"] = "Conjunto de Equipamentos",
            ["Conjunto de Equipamentos"] = "Conjunto de Equipamentos",
        };

        var resolved =
            CharacterCanonicalPayloadHelper.TryResolveCanonicalFallbackTarget(
                "Gear Set",
                directValueMap,
                originalLookup,
                translatedLookup,
                out var targetText);

        Assert.True(resolved);
        Assert.Equal("Conjunto de Equipamentos", targetText);
    }

    /// <summary>
    ///     Ensures canonical fallback can still restore one translated visible
    ///     text back to its original form when the active payload pair is
    ///     missing that exact label.
    /// </summary>
    [Fact]
    public void TryResolveCanonicalFallbackTarget_RestoresMissingCharacterRootText()
    {
        var directValueMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Força"] = "Strength",
            ["Atributos"] = "Attributes",
        };
        var originalLookup = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Strength"] = "Strength",
            ["Força"] = "Strength",
            ["Attributes"] = "Attributes",
            ["Atributos"] = "Attributes",
            ["Gear Set"] = "Gear Set",
            ["Conjunto de Equipamentos"] = "Gear Set",
        };
        var translatedLookup = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Strength"] = "Força",
            ["Força"] = "Força",
            ["Attributes"] = "Atributos",
            ["Atributos"] = "Atributos",
            ["Gear Set"] = "Conjunto de Equipamentos",
            ["Conjunto de Equipamentos"] = "Conjunto de Equipamentos",
        };

        var resolved =
            CharacterCanonicalPayloadHelper.TryResolveCanonicalFallbackTarget(
                "Conjunto de Equipamentos",
                directValueMap,
                originalLookup,
                translatedLookup,
                out var targetText);

        Assert.True(resolved);
        Assert.Equal("Gear Set", targetText);
    }

    /// <summary>
    ///     Ensures unseen-text counting deduplicates repeated values and
    ///     ignores already known text.
    /// </summary>
    [Fact]
    public void CountUnseenTextValues_CountsDistinctUnknownValuesOnly()
    {
        var sourceValues = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["3:0"] = "Carpenter",
            ["3:1"] = "Blacksmith",
            ["3:2"] = "Carpenter",
            ["6:0"] = "DoH/DoL",
        };
        var knownTexts = new HashSet<string>(StringComparer.Ordinal)
        {
            "DoH/DoL",
            "Tank",
        };

        var unseenCount = CharacterCanonicalPayloadHelper.CountUnseenTextValues(
            sourceValues,
            knownTexts);

        Assert.Equal(2, unseenCount);
    }

    /// <summary>
    ///     Ensures prefix-scoped counting ignores duplicates, blank values,
    ///     and unrelated keys when deciding whether one CharacterClass payload
    ///     is rich enough to persist.
    /// </summary>
    [Fact]
    public void CountDistinctTextValuesWithKeyPrefix_CountsDistinctMatchesOnly()
    {
        var sourceValues = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["3:0"] = "Samurai",
            ["3:1"] = "Ninja",
            ["3:2"] = "Samurai",
            ["6:0"] = "DoW/DoM",
            ["3:3"] = string.Empty,
        };

        var matchCount =
            CharacterCanonicalPayloadHelper.CountDistinctTextValuesWithKeyPrefix(
                sourceValues,
                "3:");

        Assert.Equal(2, matchCount);
    }
}
