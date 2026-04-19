// <copyright file="DbFirstStructuredStringArrayHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;
using System.Text;

using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Provides canonical payload helpers for DB-first addon surfaces backed by
///     <c>StringArrayDatas</c> rows.
/// </summary>
public static class DbFirstStructuredStringArrayHelper
{
    private const int MaxChunkLength = 4000;

    /// <summary>
    ///     Builds one canonical structured payload from a mixed addon surface
    ///     containing ATK values and StringArrayData values.
    /// </summary>
    /// <param name="type">The logical payload type.</param>
    /// <param name="contextKey">The semantic surface context key.</param>
    /// <param name="atkValues">The captured ATK value strings.</param>
    /// <param name="stringArrayValues">The captured StringArrayData strings.</param>
    /// <returns>The canonical payload.</returns>
    public static StringArrayStructuredPayload BuildCanonicalPayload(
        string type,
        string contextKey,
        IReadOnlyDictionary<int, string> atkValues,
        IReadOnlyDictionary<int, string> stringArrayValues,
        IReadOnlyDictionary<string, string>? textNodes = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(contextKey);
        ArgumentNullException.ThrowIfNull(atkValues);
        ArgumentNullException.ThrowIfNull(stringArrayValues);

        var payload = new StringArrayStructuredPayload
        {
            Type = type,
            ContextKey = contextKey,
            SchemaVersion = 1,
        };

        foreach (var pair in atkValues.OrderBy(pair => pair.Key))
        {
            var slotKey = EncodeAtkSlot(pair.Key);
            payload.Slots[slotKey] = new StringArrayStructuredSlot
            {
                SemanticKey =
                    $"atk:{pair.Key.ToString(CultureInfo.InvariantCulture)}",
                OriginalText = pair.Value,
                IsVisible = true,
                IsTranslatable = !string.IsNullOrWhiteSpace(pair.Value),
            };
        }

        foreach (var pair in stringArrayValues.OrderBy(pair => pair.Key))
        {
            payload.Slots[pair.Key] = new StringArrayStructuredSlot
            {
                SemanticKey =
                    $"stringarray:{pair.Key.ToString(CultureInfo.InvariantCulture)}",
                OriginalText = pair.Value,
                IsVisible = true,
                IsTranslatable = !string.IsNullOrWhiteSpace(pair.Value),
            };
        }

        if (textNodes != null)
        {
            foreach (var pair in textNodes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                payload.TextNodes[pair.Key] = new StringArrayStructuredSlot
                {
                    SemanticKey = $"textnode:{pair.Key}",
                    OriginalText = pair.Value,
                    IsVisible = true,
                    IsTranslatable = !string.IsNullOrWhiteSpace(pair.Value),
                };
            }
        }

        return payload;
    }

    /// <summary>
    ///     Projects one translated structured payload back into live ATK and
    ///     StringArrayData maps.
    /// </summary>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="translatedPayload">The translated canonical payload.</param>
    /// <param name="projection">The projected live payload maps.</param>
    /// <returns>
    ///     <see langword="true" /> when all translatable slots are present in
    ///     the translated payload; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryProjectTranslatedPayload(
        StringArrayStructuredPayload originalPayload,
        StringArrayStructuredPayload translatedPayload,
        out DbFirstStructuredStringArrayProjection projection)
    {
        ArgumentNullException.ThrowIfNull(originalPayload);
        ArgumentNullException.ThrowIfNull(translatedPayload);

        var atkValues = new SortedDictionary<int, string>();
        var stringArrayValues = new SortedDictionary<int, string>();
        var textNodes = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in originalPayload.Slots)
        {
            if (!translatedPayload.Slots.TryGetValue(pair.Key, out var translatedSlot))
            {
                if (pair.Value.IsTranslatable)
                {
                    projection = DbFirstStructuredStringArrayProjection.Empty;
                    return false;
                }

                translatedSlot = pair.Value;
            }

            var finalText = pair.Value.IsTranslatable
                ? translatedSlot.TranslatedText
                : pair.Value.OriginalText;
            if (pair.Value.IsTranslatable &&
                string.IsNullOrWhiteSpace(finalText))
            {
                projection = DbFirstStructuredStringArrayProjection.Empty;
                return false;
            }

            if (TryDecodeAtkSlot(pair.Key, out var atkIndex))
            {
                atkValues[atkIndex] = finalText ?? string.Empty;
                continue;
            }

            stringArrayValues[pair.Key] = finalText ?? string.Empty;
        }

        foreach (var pair in originalPayload.TextNodes)
        {
            if (!translatedPayload.TextNodes.TryGetValue(
                    pair.Key,
                    out var translatedTextNode))
            {
                if (pair.Value.IsTranslatable)
                {
                    projection = DbFirstStructuredStringArrayProjection.Empty;
                    return false;
                }

                translatedTextNode = pair.Value;
            }

            var finalText = pair.Value.IsTranslatable
                ? translatedTextNode.TranslatedText
                : pair.Value.OriginalText;
            if (pair.Value.IsTranslatable &&
                string.IsNullOrWhiteSpace(finalText))
            {
                projection = DbFirstStructuredStringArrayProjection.Empty;
                return false;
            }

            textNodes[pair.Key] = finalText ?? string.Empty;
        }

        projection = new DbFirstStructuredStringArrayProjection(
            atkValues,
            stringArrayValues,
            textNodes);
        return true;
    }

    /// <summary>
    ///     Projects one original structured payload back into live ATK and
    ///     StringArrayData maps.
    /// </summary>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <returns>The projected original payload maps.</returns>
    public static DbFirstStructuredStringArrayProjection ProjectOriginalPayload(
        StringArrayStructuredPayload originalPayload)
    {
        ArgumentNullException.ThrowIfNull(originalPayload);

        var atkValues = new SortedDictionary<int, string>();
        var stringArrayValues = new SortedDictionary<int, string>();
        var textNodes = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in originalPayload.Slots)
        {
            var finalText = pair.Value.OriginalText ?? string.Empty;
            if (TryDecodeAtkSlot(pair.Key, out var atkIndex))
            {
                atkValues[atkIndex] = finalText;
                continue;
            }

            stringArrayValues[pair.Key] = finalText;
        }

        foreach (var pair in originalPayload.TextNodes)
        {
            textNodes[pair.Key] = pair.Value.OriginalText ?? string.Empty;
        }

        return new DbFirstStructuredStringArrayProjection(
            atkValues,
            stringArrayValues,
            textNodes);
    }

    /// <summary>
    ///     Translates one canonical payload while preserving slot semantics and
    ///     structure.
    /// </summary>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="translationService">The translation service.</param>
    /// <param name="sourceLanguage">The source language.</param>
    /// <param name="targetLanguage">The target language.</param>
    /// <returns>The translated canonical payload.</returns>
    public static async Task<StringArrayStructuredPayload> TranslatePayloadAsync(
        StringArrayStructuredPayload originalPayload,
        TranslationService translationService,
        string sourceLanguage,
        string targetLanguage)
    {
        ArgumentNullException.ThrowIfNull(originalPayload);
        ArgumentNullException.ThrowIfNull(translationService);

        var translatedPayload = StringArrayStructuredPayloadResolver
            .ResolveTranslatedPayload(
                StringArrayDataPersistenceHelper.CreateCanonicalRow(
                    originalPayload.Type,
                    sourceLanguage,
                    targetLanguage,
                    null,
                    null,
                    originalPayload),
                originalPayload);

        var slotTexts = originalPayload.Slots
            .Where(pair =>
                pair.Value.IsTranslatable &&
                !string.IsNullOrWhiteSpace(pair.Value.OriginalText))
            .ToList();
        var textNodeTexts = originalPayload.TextNodes
            .Where(pair =>
                pair.Value.IsTranslatable &&
                !string.IsNullOrWhiteSpace(pair.Value.OriginalText))
            .ToList();
        if (slotTexts.Count == 0 && textNodeTexts.Count == 0)
        {
            return translatedPayload;
        }

        var builder = new StringBuilder();
        var translatedMap = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in slotTexts)
        {
            var encodedKey = EncodeTranslationKey(pair.Key);
            var encodedEntry = $"{encodedKey}|{pair.Value.OriginalText}";

            if (builder.Length + encodedEntry.Length + 1 > MaxChunkLength)
            {
                await TranslateAndMergeChunkAsync(
                    translationService,
                    builder.ToString(),
                    translatedMap,
                    sourceLanguage,
                    targetLanguage);
                builder.Clear();
            }

            if (builder.Length > 0)
            {
                builder.Append('|');
            }

            builder.Append(encodedEntry);
        }

        foreach (var pair in textNodeTexts)
        {
            var encodedKey = EncodeTextNodeTranslationKey(pair.Key);
            var encodedEntry = $"{encodedKey}|{pair.Value.OriginalText}";

            if (builder.Length + encodedEntry.Length + 1 > MaxChunkLength)
            {
                await TranslateAndMergeChunkAsync(
                    translationService,
                    builder.ToString(),
                    translatedMap,
                    sourceLanguage,
                    targetLanguage);
                builder.Clear();
            }

            if (builder.Length > 0)
            {
                builder.Append('|');
            }

            builder.Append(encodedEntry);
        }

        if (builder.Length > 0)
        {
            await TranslateAndMergeChunkAsync(
                translationService,
                builder.ToString(),
                translatedMap,
                sourceLanguage,
                targetLanguage);
        }

        foreach (var pair in slotTexts)
        {
            var encodedKey = EncodeTranslationKey(pair.Key);
            if (!translatedMap.TryGetValue(encodedKey, out var translatedText) ||
                string.IsNullOrWhiteSpace(translatedText))
            {
                continue;
            }

            translatedPayload.Slots[pair.Key].TranslatedText = translatedText;
        }

        foreach (var pair in textNodeTexts)
        {
            var encodedKey = EncodeTextNodeTranslationKey(pair.Key);
            if (!translatedMap.TryGetValue(encodedKey, out var translatedText) ||
                string.IsNullOrWhiteSpace(translatedText))
            {
                continue;
            }

            translatedPayload.TextNodes[pair.Key].TranslatedText = translatedText;
        }

        return translatedPayload;
    }

    /// <summary>
    ///     Translates one canonical payload and persists it to the
    ///     <c>stringarraydatas</c> table.
    /// </summary>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="translationService">The translation service.</param>
    /// <param name="sourceLanguage">The source language.</param>
    /// <param name="targetLanguage">The target language.</param>
    /// <param name="translationEngine">The translation engine id.</param>
    /// <param name="gameVersion">The game version.</param>
    /// <param name="configDirectory">The plugin configuration directory.</param>
    /// <returns>The persisted row snapshot.</returns>
    public static async Task<StringArrayDatas?> TranslateAndPersistAsync(
        StringArrayStructuredPayload originalPayload,
        TranslationService translationService,
        string sourceLanguage,
        string targetLanguage,
        int? translationEngine,
        string? gameVersion,
        string configDirectory)
    {
        ArgumentNullException.ThrowIfNull(originalPayload);
        ArgumentNullException.ThrowIfNull(translationService);
        ArgumentNullException.ThrowIfNull(configDirectory);

        var translatedPayload = await TranslatePayloadAsync(
            originalPayload,
            translationService,
            sourceLanguage,
            targetLanguage);
        if (!HasCompleteTranslatedPayload(originalPayload, translatedPayload))
        {
            return null;
        }

        var row = StringArrayDataPersistenceHelper.CreateCanonicalRow(
            originalPayload.Type,
            sourceLanguage,
            targetLanguage,
            translationEngine,
            gameVersion,
            originalPayload,
            translatedPayload);

        StringArrayDataPersistenceHelper.InsertStringArrayData(
            configDirectory,
            row);

        return row;
    }

    /// <summary>
    ///     Encodes one ATK slot index into the canonical payload key space.
    /// </summary>
    /// <param name="atkIndex">The original ATK slot index.</param>
    /// <returns>The encoded canonical slot key.</returns>
    public static int EncodeAtkSlot(int atkIndex)
    {
        return -atkIndex - 1;
    }

    /// <summary>
    ///     Attempts to decode one canonical slot key back into an ATK index.
    /// </summary>
    /// <param name="slotKey">The canonical slot key.</param>
    /// <param name="atkIndex">The decoded ATK slot index.</param>
    /// <returns>
    ///     <see langword="true" /> when the slot key belongs to ATK values;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public static bool TryDecodeAtkSlot(int slotKey, out int atkIndex)
    {
        if (slotKey >= 0)
        {
            atkIndex = -1;
            return false;
        }

        atkIndex = (-slotKey) - 1;
        return true;
    }

    private static string EncodeTranslationKey(int slotKey)
    {
        return $"k{slotKey.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string EncodeTextNodeTranslationKey(string textNodeKey)
    {
        return $"t{textNodeKey}";
    }

    private static async Task TranslateAndMergeChunkAsync(
        TranslationService translationService,
        string chunk,
        IDictionary<string, string> translatedMap,
        string sourceLanguage,
        string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(chunk))
        {
            return;
        }

        var translatedChunk = await translationService.TranslateAsync(
            chunk,
            sourceLanguage,
            targetLanguage);
        if (string.IsNullOrWhiteSpace(translatedChunk))
        {
            return;
        }

        var parts = translatedChunk.Split('|');
        for (var index = 0; index < parts.Length - 1; index += 2)
        {
            translatedMap[parts[index]] = parts[index + 1];
        }
    }

    private static bool HasCompleteTranslatedPayload(
        StringArrayStructuredPayload originalPayload,
        StringArrayStructuredPayload translatedPayload)
    {
        foreach (var pair in originalPayload.Slots)
        {
            if (!pair.Value.IsTranslatable)
            {
                continue;
            }

            if (!translatedPayload.Slots.TryGetValue(pair.Key, out var translatedSlot) ||
                string.IsNullOrWhiteSpace(translatedSlot.TranslatedText))
            {
                return false;
            }
        }

        foreach (var pair in originalPayload.TextNodes)
        {
            if (!pair.Value.IsTranslatable)
            {
                continue;
            }

            if (!translatedPayload.TextNodes.TryGetValue(
                    pair.Key,
                    out var translatedTextNode) ||
                string.IsNullOrWhiteSpace(translatedTextNode.TranslatedText))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
///     Represents one live addon payload projected from a translated canonical
///     string-array row.
/// </summary>
/// <param name="AtkValues">The translated ATK value strings.</param>
/// <param name="StringArrayValues">The translated StringArrayData strings.</param>
/// <param name="TextNodes">The translated visible text-node strings.</param>
public sealed record DbFirstStructuredStringArrayProjection(
    SortedDictionary<int, string> AtkValues,
    SortedDictionary<int, string> StringArrayValues,
    SortedDictionary<string, string> TextNodes)
{
    /// <summary>
    ///     Gets an empty projection.
    /// </summary>
    public static DbFirstStructuredStringArrayProjection Empty =>
        new(
            new SortedDictionary<int, string>(),
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal));
}
