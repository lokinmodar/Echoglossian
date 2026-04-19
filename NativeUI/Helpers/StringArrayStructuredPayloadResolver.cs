// <copyright file="StringArrayStructuredPayloadResolver.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;

using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Resolves canonical <see cref="StringArrayStructuredPayload" /> instances
///     from persisted <see cref="StringArrayDatas" /> rows.
/// </summary>
public static class StringArrayStructuredPayloadResolver
{
    /// <summary>
    ///     Resolves the original and translated payloads for one persisted row.
    /// </summary>
    /// <param name="row">The persisted row.</param>
    /// <param name="originalPayload">The resolved original payload.</param>
    /// <param name="translatedPayload">The resolved translated payload.</param>
    /// <returns>
    ///     <see langword="true" /> when the original payload could be
    ///     resolved; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryResolvePayloads(
        StringArrayDatas row,
        out StringArrayStructuredPayload? originalPayload,
        out StringArrayStructuredPayload? translatedPayload)
    {
        ArgumentNullException.ThrowIfNull(row);

        originalPayload = ResolveOriginalPayload(row);
        if (originalPayload == null)
        {
            translatedPayload = null;
            return false;
        }

        translatedPayload = ResolveTranslatedPayload(row, originalPayload);
        return true;
    }

    /// <summary>
    ///     Resolves the original structured payload for one persisted row.
    /// </summary>
    /// <param name="row">The persisted row.</param>
    /// <returns>The resolved original payload, or <see langword="null" />.</returns>
    public static StringArrayStructuredPayload? ResolveOriginalPayload(
        StringArrayDatas row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var structuredPayload = StringArrayStructuredPayload.Deserialize(
            row.OriginalStructuredPayload);
        if (structuredPayload != null)
        {
            return ClonePayload(structuredPayload);
        }

        var originalStrings = ParseSlotMap(row.OriginalStrings);
        if (originalStrings.Count == 0)
        {
            return null;
        }

        var payload = new StringArrayStructuredPayload
        {
            Type = row.Type ?? string.Empty,
            ContextKey = row.ContextKey ?? string.Empty,
            SchemaVersion = row.SchemaVersion ?? 1,
        };

        foreach (var pair in originalStrings)
        {
            payload.Slots[pair.Key] = new StringArrayStructuredSlot
            {
                SemanticKey = BuildFallbackSemanticKey(pair.Key),
                OriginalText = pair.Value ?? string.Empty,
                IsVisible = true,
                IsTranslatable = !string.IsNullOrWhiteSpace(pair.Value),
            };
        }

        return payload;
    }

    /// <summary>
    ///     Resolves the translated structured payload for one persisted row,
    ///     preserving the original slot semantics even when the translated
    ///     payload is sparse.
    /// </summary>
    /// <param name="row">The persisted row.</param>
    /// <param name="originalPayload">The resolved original payload.</param>
    /// <returns>The translated payload.</returns>
    public static StringArrayStructuredPayload ResolveTranslatedPayload(
        StringArrayDatas row,
        StringArrayStructuredPayload originalPayload)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(originalPayload);

        var translatedPayload = ClonePayload(originalPayload);
        var structuredTranslated = StringArrayStructuredPayload.Deserialize(
            row.TranslatedStructuredPayload);
        if (structuredTranslated != null)
        {
            foreach (var pair in structuredTranslated.Slots)
            {
                if (!translatedPayload.Slots.TryGetValue(
                        pair.Key,
                        out var existingSlot))
                {
                    translatedPayload.Slots[pair.Key] = CloneSlot(pair.Value);
                    continue;
                }

                existingSlot.TranslatedText =
                    string.IsNullOrWhiteSpace(pair.Value.TranslatedText)
                        ? existingSlot.TranslatedText
                        : pair.Value.TranslatedText;
                existingSlot.IsVisible = pair.Value.IsVisible;
                existingSlot.IsTranslatable = pair.Value.IsTranslatable;
                existingSlot.SemanticKey =
                    string.IsNullOrWhiteSpace(pair.Value.SemanticKey)
                        ? existingSlot.SemanticKey
                        : pair.Value.SemanticKey;
            }

            foreach (var pair in structuredTranslated.TextNodes)
            {
                if (!translatedPayload.TextNodes.TryGetValue(
                        pair.Key,
                        out var existingTextNode))
                {
                    translatedPayload.TextNodes[pair.Key] = CloneSlot(pair.Value);
                    continue;
                }

                existingTextNode.TranslatedText =
                    string.IsNullOrWhiteSpace(pair.Value.TranslatedText)
                        ? existingTextNode.TranslatedText
                        : pair.Value.TranslatedText;
                existingTextNode.IsVisible = pair.Value.IsVisible;
                existingTextNode.IsTranslatable = pair.Value.IsTranslatable;
                existingTextNode.SemanticKey =
                    string.IsNullOrWhiteSpace(pair.Value.SemanticKey)
                        ? existingTextNode.SemanticKey
                        : pair.Value.SemanticKey;
            }
        }

        var translatedStrings = ParseSlotMap(row.TranslatedStrings);
        foreach (var pair in translatedStrings)
        {
            if (!translatedPayload.Slots.TryGetValue(pair.Key, out var slot))
            {
                translatedPayload.Slots[pair.Key] = new StringArrayStructuredSlot
                {
                    SemanticKey = BuildFallbackSemanticKey(pair.Key),
                    OriginalText = string.Empty,
                    TranslatedText = pair.Value,
                    IsVisible = true,
                    IsTranslatable = !string.IsNullOrWhiteSpace(pair.Value),
                };
                continue;
            }

            slot.TranslatedText = string.IsNullOrWhiteSpace(pair.Value)
                ? slot.TranslatedText
                : pair.Value;
        }

        return translatedPayload;
    }

    /// <summary>
    ///     Parses a serialized slot map keyed by array index.
    /// </summary>
    /// <param name="serializedMap">The serialized slot map.</param>
    /// <returns>The parsed slot map.</returns>
    private static SortedDictionary<int, string?> ParseSlotMap(
        string? serializedMap)
    {
        var result = new SortedDictionary<int, string?>();
        if (string.IsNullOrWhiteSpace(serializedMap))
        {
            return result;
        }

        Dictionary<string, string?>? rawMap;
        try
        {
            rawMap = JsonConvert.DeserializeObject<Dictionary<string, string?>>(
                serializedMap);
        }
        catch
        {
            return result;
        }

        if (rawMap == null)
        {
            return result;
        }

        foreach (var pair in rawMap)
        {
            if (!int.TryParse(
                    pair.Key,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var index))
            {
                continue;
            }

            result[index] = pair.Value;
        }

        return result;
    }

    /// <summary>
    ///     Builds one fallback semantic key for legacy slot maps.
    /// </summary>
    /// <param name="slotIndex">The source slot index.</param>
    /// <returns>The fallback semantic key.</returns>
    private static string BuildFallbackSemanticKey(int slotIndex)
    {
        return $"slot:{slotIndex.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    ///     Clones one payload instance.
    /// </summary>
    /// <param name="payload">The payload to clone.</param>
    /// <returns>The cloned payload.</returns>
    private static StringArrayStructuredPayload ClonePayload(
        StringArrayStructuredPayload payload)
    {
        var clone = new StringArrayStructuredPayload
        {
            Type = payload.Type,
            ContextKey = payload.ContextKey,
            SchemaVersion = payload.SchemaVersion,
        };

        foreach (var pair in payload.Slots)
        {
            clone.Slots[pair.Key] = CloneSlot(pair.Value);
        }

        foreach (var pair in payload.TextNodes)
        {
            clone.TextNodes[pair.Key] = CloneSlot(pair.Value);
        }

        return clone;
    }

    /// <summary>
    ///     Clones one structured slot instance.
    /// </summary>
    /// <param name="slot">The slot to clone.</param>
    /// <returns>The cloned slot.</returns>
    private static StringArrayStructuredSlot CloneSlot(
        StringArrayStructuredSlot slot)
    {
        return new StringArrayStructuredSlot
        {
            SemanticKey = slot.SemanticKey,
            OriginalText = slot.OriginalText,
            TranslatedText = slot.TranslatedText,
            IsVisible = slot.IsVisible,
            IsTranslatable = slot.IsTranslatable,
        };
    }
}
