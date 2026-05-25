// <copyright file="NativeReplacementTextNormalizationHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Normalizes translated payload text for native replacement-only flows.
/// </summary>
internal static class NativeReplacementTextNormalizationHelper
{
    /// <summary>
    ///     Applies one text normalizer to every translated value that can be
    ///     written back into one native addon payload.
    /// </summary>
    /// <param name="payload">The translated payload to normalize.</param>
    /// <param name="normalizeText">
    ///     The per-string normalization callback.
    /// </param>
    /// <returns>
    ///     The normalized payload. When no value changes, the original payload
    ///     instance is returned unchanged.
    /// </returns>
    public static DbFirstGameWindowPayload NormalizePayload(
        DbFirstGameWindowPayload payload,
        Func<string, string> normalizeText)
    {
        var atkValuesChanged = false;
        var stringArrayValuesChanged = false;
        var textNodesChanged = false;

        var normalizedAtkValues = NormalizeMap(
            payload.AtkValues,
            normalizeText,
            ref atkValuesChanged);
        var normalizedStringArrayValues = NormalizeMap(
            payload.StringArrayValues,
            normalizeText,
            ref stringArrayValuesChanged);
        var normalizedTextNodes = NormalizeMap(
            payload.TextNodes,
            normalizeText,
            ref textNodesChanged);

        if (!atkValuesChanged &&
            !stringArrayValuesChanged &&
            !textNodesChanged)
        {
            return payload;
        }

        return new DbFirstGameWindowPayload(
            normalizedAtkValues,
            normalizedStringArrayValues,
            normalizedTextNodes);
    }

    /// <summary>
    ///     Applies one text normalizer to one numeric-keyed payload map.
    /// </summary>
    /// <param name="sourceValues">The source values.</param>
    /// <param name="normalizeText">The per-string normalization callback.</param>
    /// <param name="changed">
    ///     Tracks whether any normalized value differed from the source value.
    /// </param>
    /// <returns>The normalized payload map.</returns>
    private static SortedDictionary<int, string> NormalizeMap(
        SortedDictionary<int, string> sourceValues,
        Func<string, string> normalizeText,
        ref bool changed)
    {
        var normalizedValues = new SortedDictionary<int, string>();

        foreach (var (key, sourceValue) in sourceValues)
        {
            var normalizedValue = normalizeText(sourceValue);
            if (!changed &&
                !string.Equals(
                    sourceValue,
                    normalizedValue,
                    StringComparison.Ordinal))
            {
                changed = true;
            }

            normalizedValues[key] = normalizedValue;
        }

        return normalizedValues;
    }

    /// <summary>
    ///     Applies one text normalizer to one text-node payload map.
    /// </summary>
    /// <param name="sourceValues">The source values.</param>
    /// <param name="normalizeText">The per-string normalization callback.</param>
    /// <param name="changed">
    ///     Tracks whether any normalized value differed from the source value.
    /// </param>
    /// <returns>The normalized payload map.</returns>
    private static SortedDictionary<string, string> NormalizeMap(
        SortedDictionary<string, string> sourceValues,
        Func<string, string> normalizeText,
        ref bool changed)
    {
        var normalizedValues = new SortedDictionary<string, string>(
            StringComparer.Ordinal);

        foreach (var (key, sourceValue) in sourceValues)
        {
            var normalizedValue = normalizeText(sourceValue);
            if (!changed &&
                !string.Equals(
                    sourceValue,
                    normalizedValue,
                    StringComparison.Ordinal))
            {
                changed = true;
            }

            normalizedValues[key] = normalizedValue;
        }

        return normalizedValues;
    }
}
