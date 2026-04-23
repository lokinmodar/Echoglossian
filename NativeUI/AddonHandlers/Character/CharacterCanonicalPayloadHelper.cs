// <copyright file="CharacterCanonicalPayloadHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.Helpers;

namespace Echoglossian.NativeUI.AddonHandlers.Character;

/// <summary>
///     Provides exact-text canonicalization helpers for Character-family
///     payloads backed by the shared canonical <c>addon:Character</c>
///     structured row.
/// </summary>
internal static class CharacterCanonicalPayloadHelper
{
    /// <summary>
    ///     Appends canonical original and translated text pairs to the supplied
    ///     lookups.
    /// </summary>
    /// <param name="slots">The structured slots to inspect.</param>
    /// <param name="originalLookup">
    ///     The lookup mapping any known visible text back to the canonical
    ///     original text.
    /// </param>
    /// <param name="translatedLookup">
    ///     The lookup mapping any known visible text to the canonical
    ///     translated text.
    /// </param>
    /// <param name="knownTexts">
    ///     The known-text set used to keep capture shape stable in both
    ///     original and translated states.
    /// </param>
    public static void AppendLookupEntries(
        IEnumerable<StringArrayStructuredSlot> slots,
        IDictionary<string, string> originalLookup,
        IDictionary<string, string> translatedLookup,
        ISet<string> knownTexts)
    {
        foreach (var slot in slots)
        {
            if (string.IsNullOrWhiteSpace(slot.OriginalText))
            {
                continue;
            }

            var originalText = slot.OriginalText;
            var translatedText = string.IsNullOrWhiteSpace(slot.TranslatedText)
                ? originalText
                : slot.TranslatedText;

            originalLookup[originalText] = originalText;
            translatedLookup[originalText] = translatedText!;
            knownTexts.Add(originalText);
            knownTexts.Add(translatedText!);

            originalLookup[translatedText!] = originalText;
            translatedLookup[translatedText!] = translatedText!;
        }
    }

    /// <summary>
    ///     Appends original/translated payload pairs to the supplied lookups.
    /// </summary>
    /// <param name="originalValues">The canonical original values.</param>
    /// <param name="translatedValues">The canonical translated values.</param>
    /// <param name="originalLookup">
    ///     The lookup mapping any known visible text back to the canonical
    ///     original text.
    /// </param>
    /// <param name="translatedLookup">
    ///     The lookup mapping any known visible text to the canonical
    ///     translated text.
    /// </param>
    /// <param name="knownTexts">
    ///     The known-text set used to keep capture shape stable in both
    ///     original and translated states.
    /// </param>
    /// <param name="requireDifference">
    ///     When set, only slots whose original and translated text differ are
    ///     appended.
    /// </param>
    public static void AppendLookupEntries(
        IReadOnlyDictionary<int, string> originalValues,
        IReadOnlyDictionary<int, string> translatedValues,
        IDictionary<string, string> originalLookup,
        IDictionary<string, string> translatedLookup,
        ISet<string> knownTexts,
        bool requireDifference = false)
    {
        foreach (var (key, originalText) in originalValues)
        {
            if (!translatedValues.TryGetValue(key, out var translatedText))
            {
                continue;
            }

            AppendLookupEntry(
                originalText,
                translatedText,
                originalLookup,
                translatedLookup,
                knownTexts,
                requireDifference);
        }
    }

    /// <summary>
    ///     Appends text-node original/translated payload pairs to the supplied
    ///     lookups.
    /// </summary>
    /// <param name="originalValues">The canonical original values.</param>
    /// <param name="translatedValues">The canonical translated values.</param>
    /// <param name="originalLookup">
    ///     The lookup mapping any known visible text back to the canonical
    ///     original text.
    /// </param>
    /// <param name="translatedLookup">
    ///     The lookup mapping any known visible text to the canonical
    ///     translated text.
    /// </param>
    /// <param name="knownTexts">
    ///     The known-text set used to keep capture shape stable in both
    ///     original and translated states.
    /// </param>
    /// <param name="requireDifference">
    ///     When set, only slots whose original and translated text differ are
    ///     appended.
    /// </param>
    public static void AppendLookupEntries(
        IReadOnlyDictionary<string, string> originalValues,
        IReadOnlyDictionary<string, string> translatedValues,
        IDictionary<string, string> originalLookup,
        IDictionary<string, string> translatedLookup,
        ISet<string> knownTexts,
        bool requireDifference = false)
    {
        foreach (var (key, originalText) in originalValues)
        {
            if (!translatedValues.TryGetValue(key, out var translatedText))
            {
                continue;
            }

            AppendLookupEntry(
                originalText,
                translatedText,
                originalLookup,
                translatedLookup,
                knownTexts,
                requireDifference);
        }
    }

    /// <summary>
    ///     Tries to canonicalize a live payload back to the original source
    ///     language using one exact text lookup.
    /// </summary>
    /// <param name="sourcePayload">The live payload.</param>
    /// <param name="originalLookup">
    ///     The lookup mapping known visible text to canonical original text.
    /// </param>
    /// <param name="canonicalPayload">
    ///     Receives the canonicalized payload.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when at least one value was canonicalized;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public static bool TryCanonicalizePayload(
        DbFirstGameWindowPayload sourcePayload,
        IReadOnlyDictionary<string, string> originalLookup,
        out DbFirstGameWindowPayload canonicalPayload)
    {
        var changed = false;
        var canonicalAtkValues = CanonicalizeIntMap(
            sourcePayload.AtkValues,
            originalLookup,
            ref changed);
        var canonicalStringArrayValues = CanonicalizeIntMap(
            sourcePayload.StringArrayValues,
            originalLookup,
            ref changed);
        var canonicalTextNodes = CanonicalizeStringMap(
            sourcePayload.TextNodes,
            originalLookup,
            ref changed);

        canonicalPayload = new DbFirstGameWindowPayload(
            canonicalAtkValues,
            canonicalStringArrayValues,
            canonicalTextNodes);
        return changed;
    }

    /// <summary>
    ///     Tries to translate a payload using one exact text lookup.
    /// </summary>
    /// <param name="sourcePayload">The original payload.</param>
    /// <param name="translatedLookup">
    ///     The lookup mapping known visible text to canonical translated text.
    /// </param>
    /// <param name="translatedPayload">
    ///     Receives the translated payload.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when at least one value changed; otherwise
    ///     <see langword="false" />.
    /// </returns>
    public static bool TryTranslatePayload(
        DbFirstGameWindowPayload sourcePayload,
        IReadOnlyDictionary<string, string> translatedLookup,
        out DbFirstGameWindowPayload translatedPayload)
    {
        var changed = false;
        var translatedAtkValues = CanonicalizeIntMap(
            sourcePayload.AtkValues,
            translatedLookup,
            ref changed);
        var translatedStringArrayValues = CanonicalizeIntMap(
            sourcePayload.StringArrayValues,
            translatedLookup,
            ref changed);
        var translatedTextNodes = CanonicalizeStringMap(
            sourcePayload.TextNodes,
            translatedLookup,
            ref changed);

        translatedPayload = new DbFirstGameWindowPayload(
            translatedAtkValues,
            translatedStringArrayValues,
            translatedTextNodes);
        return changed;
    }

    /// <summary>
    ///     Collapses duplicate visible text values while preserving the first
    ///     stable key that carried each distinct value.
    /// </summary>
    /// <param name="sourceValues">The captured text-node payload.</param>
    /// <returns>
    ///     A payload whose values are unique by visible text.
    /// </returns>
    public static SortedDictionary<string, string> CollapseDuplicateTextValues(
        IReadOnlyDictionary<string, string> sourceValues)
    {
        var collapsedValues = new SortedDictionary<string, string>(
            StringComparer.Ordinal);
        var seenValues = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (key, value) in sourceValues)
        {
            if (!seenValues.Add(value))
            {
                continue;
            }

            collapsedValues[key] = value;
        }

        return collapsedValues;
    }

    /// <summary>
    ///     Builds one exact source-text to target-text map from matching
    ///     payload keys so dynamic list surfaces can rewrite nodes by current
    ///     visible text instead of unstable per-row ordinals.
    /// </summary>
    /// <param name="sourceValues">The source-facing text-node payload.</param>
    /// <param name="targetValues">The target-facing text-node payload.</param>
    /// <returns>
    ///     One exact text map that rewrites source-facing visible values to the
    ///     desired target-facing values.
    /// </returns>
    public static Dictionary<string, string> BuildValueMap(
        IReadOnlyDictionary<string, string> sourceValues,
        IReadOnlyDictionary<string, string> targetValues)
    {
        var valueMap = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, sourceText) in sourceValues)
        {
            if (!targetValues.TryGetValue(key, out var targetText) ||
                string.IsNullOrWhiteSpace(sourceText) ||
                string.IsNullOrWhiteSpace(targetText) ||
                string.Equals(
                    sourceText,
                    targetText,
                    StringComparison.Ordinal))
            {
                continue;
            }

            valueMap[sourceText] = targetText;
        }

        return valueMap;
    }

    /// <summary>
    ///     Builds one exact source-text to target-text map from matching
    ///     payload keys across ATK values, string-array values, and text nodes
    ///     so dynamic Character-family surfaces can rewrite visible nodes by
    ///     current text even when the live node is backed by a non-text-node
    ///     payload source.
    /// </summary>
    /// <param name="sourcePayload">The source-facing payload.</param>
    /// <param name="targetPayload">The target-facing payload.</param>
    /// <returns>
    ///     One exact text map that rewrites source-facing visible values to the
    ///     desired target-facing values.
    /// </returns>
    public static Dictionary<string, string> BuildValueMap(
        DbFirstGameWindowPayload sourcePayload,
        DbFirstGameWindowPayload targetPayload)
    {
        var valueMap = BuildValueMap(
            sourcePayload.TextNodes,
            targetPayload.TextNodes);

        AppendValueMapEntries(
            valueMap,
            sourcePayload.AtkValues,
            targetPayload.AtkValues);
        AppendValueMapEntries(
            valueMap,
            sourcePayload.StringArrayValues,
            targetPayload.StringArrayValues);

        return valueMap;
    }

    /// <summary>
    ///     Tries to resolve one fallback text transition from the canonical
    ///     Character-family lookups when the direct payload pair did not carry
    ///     the exact visible text.
    /// </summary>
    /// <param name="currentText">The currently visible text.</param>
    /// <param name="directValueMap">
    ///     The direct value map built from the payload pair currently being
    ///     applied.
    /// </param>
    /// <param name="originalLookup">
    ///     The canonical lookup that maps any known value back to its original
    ///     text.
    /// </param>
    /// <param name="translatedLookup">
    ///     The canonical lookup that maps any known value to its translated
    ///     text.
    /// </param>
    /// <param name="targetText">
    ///     Receives the fallback target text when resolution succeeds.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when one fallback target could be resolved;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public static bool TryResolveCanonicalFallbackTarget(
        string currentText,
        IReadOnlyDictionary<string, string> directValueMap,
        IReadOnlyDictionary<string, string> originalLookup,
        IReadOnlyDictionary<string, string> translatedLookup,
        out string targetText)
    {
        targetText = string.Empty;
        if (string.IsNullOrWhiteSpace(currentText) ||
            !TryDetermineDirectValueMapDirection(
                directValueMap,
                originalLookup,
                translatedLookup,
                out var sourceIsOriginal))
        {
            return false;
        }

        if (sourceIsOriginal)
        {
            if (!translatedLookup.TryGetValue(currentText, out targetText) ||
                string.Equals(
                    currentText,
                    targetText,
                    StringComparison.Ordinal))
            {
                targetText = string.Empty;
                return false;
            }

            return true;
        }

        if (!originalLookup.TryGetValue(currentText, out targetText) ||
            string.Equals(
                currentText,
                targetText,
                StringComparison.Ordinal))
        {
            targetText = string.Empty;
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Counts how many non-empty source text values are not yet present in
    ///     one known-text set.
    /// </summary>
    /// <param name="sourceValues">The source text values to inspect.</param>
    /// <param name="knownTexts">The known text set.</param>
    /// <returns>
    ///     The number of distinct source text values that are not yet known.
    /// </returns>
    public static int CountUnseenTextValues(
        IReadOnlyDictionary<string, string> sourceValues,
        ISet<string> knownTexts)
    {
        var unseenTexts = new HashSet<string>(StringComparer.Ordinal);

        foreach (var sourceText in sourceValues.Values)
        {
            if (string.IsNullOrWhiteSpace(sourceText) ||
                knownTexts.Contains(sourceText))
            {
                continue;
            }

            unseenTexts.Add(sourceText);
        }

        return unseenTexts.Count;
    }

    /// <summary>
    ///     Counts how many distinct non-empty text values are stored under one
    ///     stable text-node key prefix.
    /// </summary>
    /// <param name="sourceValues">The source text values to inspect.</param>
    /// <param name="keyPrefix">The stable key prefix to match.</param>
    /// <returns>
    ///     The number of distinct non-empty values whose keys start with the
    ///     requested prefix.
    /// </returns>
    public static int CountDistinctTextValuesWithKeyPrefix(
        IReadOnlyDictionary<string, string> sourceValues,
        string keyPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPrefix);

        var matchingTexts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, sourceText) in sourceValues)
        {
            if (!key.StartsWith(keyPrefix, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(sourceText))
            {
                continue;
            }

            matchingTexts.Add(sourceText);
        }

        return matchingTexts.Count;
    }

    /// <summary>
    ///     Canonicalizes one integer-keyed payload map through an exact text
    ///     lookup.
    /// </summary>
    /// <param name="sourceValues">The source values.</param>
    /// <param name="lookup">The exact text lookup.</param>
    /// <param name="changed">
    ///     Receives whether any resulting value differs from the source.
    /// </param>
    /// <returns>The canonicalized map.</returns>
    private static SortedDictionary<int, string> CanonicalizeIntMap(
        SortedDictionary<int, string> sourceValues,
        IReadOnlyDictionary<string, string> lookup,
        ref bool changed)
    {
        var resolvedValues = new SortedDictionary<int, string>();

        foreach (var (key, sourceText) in sourceValues)
        {
            var resolvedText = lookup.TryGetValue(sourceText, out var mappedText)
                ? mappedText
                : sourceText;
            if (!string.Equals(
                    resolvedText,
                    sourceText,
                    StringComparison.Ordinal))
            {
                changed = true;
            }

            resolvedValues[key] = resolvedText;
        }

        return resolvedValues;
    }

    /// <summary>
    ///     Canonicalizes one text-node payload map through an exact text
    ///     lookup.
    /// </summary>
    /// <param name="sourceValues">The source values.</param>
    /// <param name="lookup">The exact text lookup.</param>
    /// <param name="changed">
    ///     Receives whether any resulting value differs from the source.
    /// </param>
    /// <returns>The canonicalized map.</returns>
    private static SortedDictionary<string, string> CanonicalizeStringMap(
        SortedDictionary<string, string> sourceValues,
        IReadOnlyDictionary<string, string> lookup,
        ref bool changed)
    {
        var resolvedValues = new SortedDictionary<string, string>(
            StringComparer.Ordinal);

        foreach (var (key, sourceText) in sourceValues)
        {
            var resolvedText = lookup.TryGetValue(sourceText, out var mappedText)
                ? mappedText
                : sourceText;
            if (!string.Equals(
                    resolvedText,
                    sourceText,
                    StringComparison.Ordinal))
            {
                changed = true;
            }

            resolvedValues[key] = resolvedText;
        }

        return resolvedValues;
    }

    /// <summary>
    ///     Appends one exact original/translated text pair to the supplied
    ///     lookups.
    /// </summary>
    /// <param name="originalText">The canonical original text.</param>
    /// <param name="translatedText">The canonical translated text.</param>
    /// <param name="originalLookup">
    ///     The lookup mapping any known visible text back to the canonical
    ///     original text.
    /// </param>
    /// <param name="translatedLookup">
    ///     The lookup mapping any known visible text to the canonical
    ///     translated text.
    /// </param>
    /// <param name="knownTexts">
    ///     The known-text set used to keep capture shape stable in both
    ///     original and translated states.
    /// </param>
    /// <param name="requireDifference">
    ///     When set, only pairs whose original and translated text differ are
    ///     appended.
    /// </param>
    private static void AppendLookupEntry(
        string? originalText,
        string? translatedText,
        IDictionary<string, string> originalLookup,
        IDictionary<string, string> translatedLookup,
        ISet<string> knownTexts,
        bool requireDifference)
    {
        if (string.IsNullOrWhiteSpace(originalText) ||
            string.IsNullOrWhiteSpace(translatedText))
        {
            return;
        }

        if (requireDifference &&
            string.Equals(
                originalText,
                translatedText,
                StringComparison.Ordinal))
        {
            return;
        }

        originalLookup[originalText] = originalText;
        translatedLookup[originalText] = translatedText;
        knownTexts.Add(originalText);
        knownTexts.Add(translatedText);

        originalLookup[translatedText] = originalText;
        translatedLookup[translatedText] = translatedText;
    }

    /// <summary>
    ///     Appends one exact source-text to target-text mapping from matching
    ///     payload keys.
    /// </summary>
    /// <typeparam name="TKey">The payload key type.</typeparam>
    /// <param name="valueMap">The target value map to augment.</param>
    /// <param name="sourceValues">The source-facing payload values.</param>
    /// <param name="targetValues">The target-facing payload values.</param>
    private static void AppendValueMapEntries<TKey>(
        IDictionary<string, string> valueMap,
        IReadOnlyDictionary<TKey, string> sourceValues,
        IReadOnlyDictionary<TKey, string> targetValues)
        where TKey : notnull
    {
        foreach (var (key, sourceText) in sourceValues)
        {
            if (!targetValues.TryGetValue(key, out var targetText) ||
                string.IsNullOrWhiteSpace(sourceText) ||
                string.IsNullOrWhiteSpace(targetText) ||
                string.Equals(
                    sourceText,
                    targetText,
                    StringComparison.Ordinal))
            {
                continue;
            }

            valueMap[sourceText] = targetText;
        }
    }

    /// <summary>
    ///     Determines whether the direct payload-pair map represents one
    ///     source-original to target-translated transition or the reverse.
    /// </summary>
    /// <param name="directValueMap">The direct payload-pair map.</param>
    /// <param name="originalLookup">The canonical original lookup.</param>
    /// <param name="translatedLookup">The canonical translated lookup.</param>
    /// <param name="sourceIsOriginal">
    ///     Receives <see langword="true" /> when the direct map flows from
    ///     original to translated text; otherwise <see langword="false" />
    ///     when it flows from translated to original text.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when one direction could be inferred;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private static bool TryDetermineDirectValueMapDirection(
        IReadOnlyDictionary<string, string> directValueMap,
        IReadOnlyDictionary<string, string> originalLookup,
        IReadOnlyDictionary<string, string> translatedLookup,
        out bool sourceIsOriginal)
    {
        sourceIsOriginal = false;

        foreach (var (sourceText, targetText) in directValueMap)
        {
            if (string.Equals(
                    sourceText,
                    targetText,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (translatedLookup.TryGetValue(sourceText, out var translated) &&
                string.Equals(
                    translated,
                    targetText,
                    StringComparison.Ordinal) &&
                originalLookup.TryGetValue(sourceText, out var original) &&
                string.Equals(
                    original,
                    sourceText,
                    StringComparison.Ordinal))
            {
                sourceIsOriginal = true;
                return true;
            }

            if (originalLookup.TryGetValue(sourceText, out original) &&
                string.Equals(
                    original,
                    targetText,
                    StringComparison.Ordinal) &&
                translatedLookup.TryGetValue(sourceText, out translated) &&
                string.Equals(
                    translated,
                    sourceText,
                    StringComparison.Ordinal))
            {
                sourceIsOriginal = false;
                return true;
            }
        }

        return false;
    }
}
