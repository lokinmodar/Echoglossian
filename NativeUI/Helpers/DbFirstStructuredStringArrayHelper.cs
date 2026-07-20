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
    /// <param name="translatorResolution">The captured translator resolution.</param>
    /// <param name="originContext">The diagnostic origin context.</param>
    /// <returns>The translated canonical payload.</returns>
    internal static async Task<StringArrayStructuredPayload> TranslatePayloadAsync(
        StringArrayStructuredPayload originalPayload,
        TranslationService translationService,
        SourceClientLanguage sourceLanguage,
        string targetLanguage,
        TranslationService.TranslatorResolution? translatorResolution = null,
        string? originContext = null)
    {
        ArgumentNullException.ThrowIfNull(originalPayload);
        ArgumentNullException.ThrowIfNull(translationService);

        var translatedPayload = StringArrayStructuredPayloadResolver
            .ResolveTranslatedPayload(
                StringArrayDataPersistenceHelper.CreateCanonicalRow(
                    originalPayload.Type,
                    sourceLanguage.PersistenceCode,
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

        var translatedMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var translationEntries = BuildTranslationEntries(slotTexts, textNodeTexts);

        await TranslateEntriesInChunksAsync(
            translationService,
            translationEntries,
            translatedMap,
            sourceLanguage,
            targetLanguage,
            translatorResolution,
            originContext);

        await TranslateMissingEntriesIndividuallyAsync(
            translationService,
            translatedMap,
            slotTexts,
            textNodeTexts,
            sourceLanguage,
            targetLanguage,
            translatorResolution,
            originContext);

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
    /// <param name="originContext">The diagnostic origin context.</param>
    /// <returns>The persisted row snapshot.</returns>
    public static async Task<StringArrayDatas?> TranslateAndPersistAsync(
        StringArrayStructuredPayload originalPayload,
        TranslationService translationService,
        SourceClientLanguage sourceLanguage,
        string targetLanguage,
        int? translationEngine,
        string? gameVersion,
        string configDirectory,
        string? originContext = null)
    {
        ArgumentNullException.ThrowIfNull(originalPayload);
        ArgumentNullException.ThrowIfNull(translationService);
        ArgumentNullException.ThrowIfNull(configDirectory);

        var translatedPayload = await TranslatePayloadAsync(
            originalPayload,
            translationService,
            sourceLanguage,
            targetLanguage,
            originContext: originContext);
        if (!HasCompleteTranslatedPayload(originalPayload, translatedPayload))
        {
            return null;
        }

        var row = StringArrayDataPersistenceHelper.CreateCanonicalRow(
            originalPayload.Type,
            sourceLanguage.PersistenceCode,
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

    /// <summary>
    ///     Builds translation entries in deterministic payload order.
    /// </summary>
    /// <param name="slotTexts">The structured StringArrayData entries.</param>
    /// <param name="textNodeTexts">The structured visible text-node entries.</param>
    /// <returns>The translation entries to batch.</returns>
    private static List<StructuredTranslationEntry> BuildTranslationEntries(
        IReadOnlyCollection<KeyValuePair<int, StringArrayStructuredSlot>> slotTexts,
        IReadOnlyCollection<KeyValuePair<string, StringArrayStructuredSlot>> textNodeTexts)
    {
        var entries = new List<StructuredTranslationEntry>(
            slotTexts.Count + textNodeTexts.Count);
        foreach (var pair in slotTexts)
        {
            entries.Add(new StructuredTranslationEntry(
                EncodeTranslationKey(pair.Key),
                pair.Value.OriginalText ?? string.Empty));
        }

        foreach (var pair in textNodeTexts)
        {
            entries.Add(new StructuredTranslationEntry(
                EncodeTextNodeTranslationKey(pair.Key),
                pair.Value.OriginalText ?? string.Empty));
        }

        return entries;
    }

    /// <summary>
    ///     Translates structured entries in bounded batches.
    /// </summary>
    /// <param name="translationService">The translation service.</param>
    /// <param name="entries">The entries to translate.</param>
    /// <param name="translatedMap">The translated text map to populate.</param>
    /// <param name="sourceLanguage">The source client language.</param>
    /// <param name="targetLanguage">The target language.</param>
    /// <param name="translatorResolution">The optional translator resolution.</param>
    /// <param name="originContext">The optional translation origin context.</param>
    /// <returns>The asynchronous operation.</returns>
    private static async Task TranslateEntriesInChunksAsync(
        TranslationService translationService,
        IReadOnlyList<StructuredTranslationEntry> entries,
        IDictionary<string, string> translatedMap,
        SourceClientLanguage sourceLanguage,
        string targetLanguage,
        TranslationService.TranslatorResolution? translatorResolution,
        string? originContext)
    {
        var chunkEntries = new List<StructuredTranslationEntry>();
        var chunkLength = 0;
        foreach (var entry in entries)
        {
            var encodedEntryLength = GetTransportEntryLength(
                chunkEntries.Count,
                entry);
            if (chunkEntries.Count > 0 &&
                chunkLength + encodedEntryLength + 1 > MaxChunkLength)
            {
                await TranslateAndMergeChunkAsync(
                    translationService,
                    chunkEntries,
                    translatedMap,
                    sourceLanguage,
                    targetLanguage,
                    translatorResolution,
                    originContext);
                chunkEntries.Clear();
                chunkLength = 0;
                encodedEntryLength = GetTransportEntryLength(0, entry);
            }

            if (chunkEntries.Count > 0)
            {
                chunkLength++;
            }

            chunkEntries.Add(entry);
            chunkLength += encodedEntryLength;
        }

        if (chunkEntries.Count > 0)
        {
            await TranslateAndMergeChunkAsync(
                translationService,
                chunkEntries,
                translatedMap,
                sourceLanguage,
                targetLanguage,
                translatorResolution,
                originContext);
        }
    }

    /// <summary>
    ///     Translates one structured batch and merges any recoverable results.
    /// </summary>
    /// <param name="translationService">The translation service.</param>
    /// <param name="entries">The entries included in the batch.</param>
    /// <param name="translatedMap">The translated text map to populate.</param>
    /// <param name="sourceLanguage">The source client language.</param>
    /// <param name="targetLanguage">The target language.</param>
    /// <param name="translatorResolution">The optional translator resolution.</param>
    /// <param name="originContext">The optional translation origin context.</param>
    /// <returns>The asynchronous operation.</returns>
    private static async Task TranslateAndMergeChunkAsync(
        TranslationService translationService,
        IReadOnlyList<StructuredTranslationEntry> entries,
        IDictionary<string, string> translatedMap,
        SourceClientLanguage sourceLanguage,
        string targetLanguage,
        TranslationService.TranslatorResolution? translatorResolution,
        string? originContext)
    {
        var chunk = BuildTranslationChunk(entries);
        if (string.IsNullOrWhiteSpace(chunk))
        {
            return;
        }

        var translatedChunk = translatorResolution.HasValue
            ? await translationService.TranslateAsync(
                chunk,
                sourceLanguage,
                targetLanguage,
                TranslationSurfaceGroup.Default,
                translatorResolution.Value,
                originContext)
            : await translationService.TranslateAsync(
                chunk,
                sourceLanguage,
                targetLanguage,
                originContext);
        if (string.IsNullOrWhiteSpace(translatedChunk))
        {
            return;
        }

        MergeTranslatedChunk(entries, translatedChunk, translatedMap);
    }

    /// <summary>
    ///     Builds the delimited translation chunk sent to the provider.
    /// </summary>
    /// <param name="entries">The entries to encode.</param>
    /// <returns>The encoded chunk.</returns>
    private static string BuildTranslationChunk(
        IReadOnlyList<StructuredTranslationEntry> entries)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < entries.Count; index++)
        {
            if (builder.Length > 0)
            {
                builder.Append('|');
            }

            var entry = entries[index];
            builder
                .Append(GetTransportKey(index))
                .Append('|')
                .Append(entry.OriginalText);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Merges a translated chunk, accepting numeric transport keys,
    ///     canonical legacy keys, or ordered text-only parts when the provider
    ///     preserved item order but dropped keys.
    /// </summary>
    /// <param name="entries">The entries that were included in the request.</param>
    /// <param name="translatedChunk">The translated provider response.</param>
    /// <param name="translatedMap">The translated text map to populate.</param>
    private static void MergeTranslatedChunk(
        IReadOnlyList<StructuredTranslationEntry> entries,
        string translatedChunk,
        IDictionary<string, string> translatedMap)
    {
        var parts = translatedChunk.Split('|');
        var keyedMatches = 0;
        for (var index = 0; index < parts.Length - 1; index += 2)
        {
            if (!TryGetKeyedEntryIndex(
                    parts[index].Trim(),
                    entries,
                    out var entryIndex))
            {
                continue;
            }

            var translatedText = parts[index + 1].Trim();
            if (!string.IsNullOrWhiteSpace(translatedText))
            {
                translatedMap[entries[entryIndex].ResultKey] = translatedText;
                keyedMatches++;
            }
        }

        if (keyedMatches != 0)
        {
            return;
        }

        if (parts.Length != entries.Count)
        {
            return;
        }

        for (var index = 0; index < entries.Count; index++)
        {
            var translatedText = parts[index].Trim();
            if (!string.IsNullOrWhiteSpace(translatedText))
            {
                translatedMap[entries[index].ResultKey] = translatedText;
            }
        }
    }

    /// <summary>
    ///     Gets the encoded entry length in the current transport chunk.
    /// </summary>
    /// <param name="transportIndex">The chunk-local transport index.</param>
    /// <param name="entry">The translation entry.</param>
    /// <returns>The encoded entry length.</returns>
    private static int GetTransportEntryLength(
        int transportIndex,
        StructuredTranslationEntry entry)
    {
        return GetTransportKey(transportIndex).Length + 1 +
               entry.OriginalText.Length;
    }

    /// <summary>
    ///     Gets the numeric chunk-local transport key.
    /// </summary>
    /// <param name="transportIndex">The chunk-local transport index.</param>
    /// <returns>The transport key.</returns>
    private static string GetTransportKey(int transportIndex)
    {
        return transportIndex.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Attempts to read a numeric or canonical key from a translated
    ///     response.
    /// </summary>
    /// <param name="responseKey">The translated response key.</param>
    /// <param name="entries">The entries in the chunk.</param>
    /// <param name="entryIndex">The decoded entry index.</param>
    /// <returns><see langword="true" /> when the key is valid.</returns>
    private static bool TryGetKeyedEntryIndex(
        string responseKey,
        IReadOnlyList<StructuredTranslationEntry> entries,
        out int entryIndex)
    {
        if (!int.TryParse(
                responseKey,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out entryIndex))
        {
            for (var index = 0; index < entries.Count; index++)
            {
                if (string.Equals(
                        responseKey,
                        entries[index].ResultKey,
                        StringComparison.Ordinal))
                {
                    entryIndex = index;
                    return true;
                }
            }

            return false;
        }

        return entryIndex >= 0 && entryIndex < entries.Count &&
               responseKey == GetTransportKey(entryIndex);
    }

    private static async Task TranslateMissingEntriesIndividuallyAsync(
        TranslationService translationService,
        IDictionary<string, string> translatedMap,
        IReadOnlyCollection<KeyValuePair<int, StringArrayStructuredSlot>> slotTexts,
        IReadOnlyCollection<KeyValuePair<string, StringArrayStructuredSlot>> textNodeTexts,
        SourceClientLanguage sourceLanguage,
        string targetLanguage,
        TranslationService.TranslatorResolution? translatorResolution,
        string? originContext)
    {
        foreach (var pair in slotTexts)
        {
            await TranslateMissingEntryIndividuallyAsync(
                translationService,
                translatedMap,
                EncodeTranslationKey(pair.Key),
                pair.Value.OriginalText,
                sourceLanguage,
                targetLanguage,
                translatorResolution,
                originContext);
        }

        foreach (var pair in textNodeTexts)
        {
            await TranslateMissingEntryIndividuallyAsync(
                translationService,
                translatedMap,
                EncodeTextNodeTranslationKey(pair.Key),
                pair.Value.OriginalText,
                sourceLanguage,
                targetLanguage,
                translatorResolution,
                originContext);
        }
    }

    private static async Task TranslateMissingEntryIndividuallyAsync(
        TranslationService translationService,
        IDictionary<string, string> translatedMap,
        string encodedKey,
        string? originalText,
        SourceClientLanguage sourceLanguage,
        string targetLanguage,
        TranslationService.TranslatorResolution? translatorResolution,
        string? originContext)
    {
        if (translatedMap.TryGetValue(encodedKey, out var translatedText) &&
            !string.IsNullOrWhiteSpace(translatedText))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(originalText))
        {
            return;
        }

        var individualTranslation = translatorResolution.HasValue
            ? await translationService.TranslateAsync(
                originalText,
                sourceLanguage,
                targetLanguage,
                TranslationSurfaceGroup.Default,
                translatorResolution.Value,
                originContext)
            : await translationService.TranslateAsync(
                originalText,
                sourceLanguage,
                targetLanguage,
                originContext);
        if (!string.IsNullOrWhiteSpace(individualTranslation))
        {
            translatedMap[encodedKey] = individualTranslation;
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

    /// <summary>
    ///     Represents one key/text pair inside a structured translation batch.
    /// </summary>
    /// <param name="ResultKey">The canonical result-map key.</param>
    /// <param name="OriginalText">The original text to translate.</param>
    private sealed record StructuredTranslationEntry(
        string ResultKey,
        string OriginalText);
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
