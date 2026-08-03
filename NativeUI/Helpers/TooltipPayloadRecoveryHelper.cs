// <copyright file="TooltipPayloadRecoveryHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Recovers canonical Tooltip originals from persisted candidates even
///     when the live node text has been mutated by native wrapping or prior
///     translated application.
/// </summary>
internal static class TooltipPayloadRecoveryHelper
{
    /// <summary>
    ///     Rewrites one live Tooltip text-node map back to the canonical
    ///     original or translated payload text whenever the visible node only
    ///     differs by layout-only whitespace churn.
    /// </summary>
    /// <param name="liveValues">The freshly captured live text-node values.</param>
    /// <param name="originalValues">The canonical original text-node values.</param>
    /// <param name="translatedValues">
    ///     The canonical translated text-node values.
    /// </param>
    /// <returns>The canonicalized text-node map.</returns>
    public static SortedDictionary<string, string> CanonicalizeLiveTextNodes(
        IReadOnlyDictionary<string, string> liveValues,
        IReadOnlyDictionary<string, string> originalValues,
        IReadOnlyDictionary<string, string> translatedValues)
    {
        var canonicalizedValues = new SortedDictionary<string, string>(
            StringComparer.Ordinal);
        foreach (var (index, liveText) in liveValues)
        {
            if (translatedValues.TryGetValue(index, out var translatedText) &&
                MatchesRecoveryText(liveText, translatedText))
            {
                canonicalizedValues[index] = translatedText;
                continue;
            }

            if (originalValues.TryGetValue(index, out var originalText) &&
                MatchesRecoveryText(liveText, originalText))
            {
                canonicalizedValues[index] = originalText;
                continue;
            }

            canonicalizedValues[index] = liveText;
        }

        return canonicalizedValues;
    }

    /// <summary>
    ///     Determines whether one original and translated Tooltip payload pair
    ///     carries any semantic difference beyond whitespace-only layout
    ///     mutations.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="translatedPayload">The candidate translated payload.</param>
    /// <returns>
    ///     <see langword="true" /> when at least one slot differs
    ///     semantically; otherwise <see langword="false" /> when the pair is
    ///     only a poisoned whitespace mutation chain.
    /// </returns>
    public static bool HasSemanticallyDistinctPayloads(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload)
    {
        return MapHasSemanticDifference(
                   originalPayload.AtkValues,
                   translatedPayload.AtkValues) ||
               MapHasSemanticDifference(
                   originalPayload.StringArrayValues,
                   translatedPayload.StringArrayValues) ||
               MapHasSemanticDifference(
                   originalPayload.TextNodes,
                   translatedPayload.TextNodes);
    }

    /// <summary>
    ///     Tries to recover the original payload from one set of Tooltip
    ///     candidates using semantic text matching that ignores layout-only
    ///     whitespace churn.
    /// </summary>
    /// <param name="livePayload">The currently visible live payload.</param>
    /// <param name="candidates">The candidate original and translated pairs.</param>
    /// <param name="originalPayload">
    ///     The recovered original payload when one unique candidate matches.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when one unique candidate explains the live
    ///     Tooltip state; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryRecoverOriginalPayload(
        DbFirstGameWindowPayload livePayload,
        IReadOnlyList<DbFirstPayloadRecoveryCandidate> candidates,
        out DbFirstGameWindowPayload originalPayload)
    {
        originalPayload = DbFirstGameWindowPayload.Empty;

        var bestScore = -1;
        string? bestSignature = null;
        DbFirstGameWindowPayload? bestPayload = null;
        var ambiguous = false;

        foreach (var candidate in candidates)
        {
            if (!TryScoreCandidate(
                    livePayload,
                    candidate,
                    out var candidateScore))
            {
                continue;
            }

            var candidateSignature = candidate.OriginalPayload.Serialize();
            if (candidateScore > bestScore)
            {
                bestScore = candidateScore;
                bestSignature = candidateSignature;
                bestPayload = candidate.OriginalPayload;
                ambiguous = false;
                continue;
            }

            if (candidateScore == bestScore &&
                bestSignature != null &&
                !string.Equals(
                    bestSignature,
                    candidateSignature,
                    StringComparison.Ordinal))
            {
                ambiguous = true;
            }
        }

        if (bestPayload == null || ambiguous)
        {
            return false;
        }

        originalPayload = bestPayload.Value;
        return true;
    }

    /// <summary>
    ///     Determines whether the live Tooltip payload still contains evidence
    ///     that one or more visible slots are already derived from translated
    ///     candidate text.
    /// </summary>
    /// <param name="livePayload">The currently visible live payload.</param>
    /// <param name="candidates">The candidate original and translated pairs.</param>
    /// <returns>
    ///     <see langword="true" /> when at least one live slot matches the
    ///     translated candidate semantically while differing from its original
    ///     candidate text.
    /// </returns>
    public static bool HasTranslatedSlotEvidence(
        DbFirstGameWindowPayload livePayload,
        IReadOnlyList<DbFirstPayloadRecoveryCandidate> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (MapHasTranslatedSlotEvidence(
                    livePayload.AtkValues,
                    candidate.OriginalPayload.AtkValues,
                    candidate.TranslatedPayload.AtkValues) ||
                MapHasTranslatedSlotEvidence(
                    livePayload.StringArrayValues,
                    candidate.OriginalPayload.StringArrayValues,
                    candidate.TranslatedPayload.StringArrayValues) ||
                MapHasTranslatedSlotEvidence(
                    livePayload.TextNodes,
                    candidate.OriginalPayload.TextNodes,
                    candidate.TranslatedPayload.TextNodes))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Scores one candidate against the live Tooltip payload.
    /// </summary>
    /// <param name="livePayload">The live payload.</param>
    /// <param name="candidate">The persisted candidate.</param>
    /// <param name="score">The resulting score.</param>
    /// <returns>
    ///     <see langword="true" /> when the live payload is fully explainable
    ///     by the candidate original or translated values.
    /// </returns>
    private static bool TryScoreCandidate(
        DbFirstGameWindowPayload livePayload,
        DbFirstPayloadRecoveryCandidate candidate,
        out int score)
    {
        score = 0;

        if (!TryScoreMap(
                livePayload.AtkValues,
                candidate.OriginalPayload.AtkValues,
                candidate.TranslatedPayload.AtkValues,
                out var atkScore) ||
            !TryScoreMap(
                livePayload.StringArrayValues,
                candidate.OriginalPayload.StringArrayValues,
                candidate.TranslatedPayload.StringArrayValues,
                out var stringArrayScore) ||
            !TryScoreMap(
                livePayload.TextNodes,
                candidate.OriginalPayload.TextNodes,
                candidate.TranslatedPayload.TextNodes,
                out var textNodeScore))
        {
            return false;
        }

        score = atkScore + stringArrayScore + textNodeScore;
        return score > 0;
    }

    /// <summary>
    ///     Scores one numeric payload map against a candidate pair.
    /// </summary>
    private static bool TryScoreMap(
        IReadOnlyDictionary<int, string> liveValues,
        IReadOnlyDictionary<int, string> originalValues,
        IReadOnlyDictionary<int, string> translatedValues,
        out int score)
    {
        score = 0;

        if (liveValues.Count == 0)
        {
            return true;
        }

        if (liveValues.Count > originalValues.Count ||
            liveValues.Count > translatedValues.Count)
        {
            return false;
        }

        foreach (var (index, liveText) in liveValues)
        {
            if (!originalValues.TryGetValue(index, out var originalText) ||
                !translatedValues.TryGetValue(index, out var translatedText))
            {
                return false;
            }

            if (MatchesRecoveryText(liveText, translatedText) ||
                MatchesRecoveryText(liveText, originalText))
            {
                score += 2;
                continue;
            }

            return false;
        }

        if (liveValues.Count == originalValues.Count &&
            liveValues.Count == translatedValues.Count)
        {
            score += liveValues.Count;
        }

        return score > 0;
    }

    /// <summary>
    ///     Scores one text-node payload map against a candidate pair.
    /// </summary>
    private static bool TryScoreMap(
        IReadOnlyDictionary<string, string> liveValues,
        IReadOnlyDictionary<string, string> originalValues,
        IReadOnlyDictionary<string, string> translatedValues,
        out int score)
    {
        score = 0;

        if (liveValues.Count == 0)
        {
            return true;
        }

        if (liveValues.Count > originalValues.Count ||
            liveValues.Count > translatedValues.Count)
        {
            return false;
        }

        foreach (var (index, liveText) in liveValues)
        {
            if (!originalValues.TryGetValue(index, out var originalText) ||
                !translatedValues.TryGetValue(index, out var translatedText))
            {
                return false;
            }

            if (MatchesRecoveryText(liveText, translatedText) ||
                MatchesRecoveryText(liveText, originalText))
            {
                score += 2;
                continue;
            }

            return false;
        }

        if (liveValues.Count == originalValues.Count &&
            liveValues.Count == translatedValues.Count)
        {
            score += liveValues.Count;
        }

        return score > 0;
    }

    /// <summary>
    ///     Determines whether a numeric payload map contains translated-slot
    ///     evidence after semantic normalization.
    /// </summary>
    private static bool MapHasTranslatedSlotEvidence(
        IReadOnlyDictionary<int, string> liveValues,
        IReadOnlyDictionary<int, string> originalValues,
        IReadOnlyDictionary<int, string> translatedValues)
    {
        foreach (var (index, liveText) in liveValues)
        {
            if (!originalValues.TryGetValue(index, out var originalText) ||
                !translatedValues.TryGetValue(index, out var translatedText))
            {
                continue;
            }

            if (MatchesRecoveryText(liveText, translatedText) &&
                !MatchesRecoveryText(liveText, originalText))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Determines whether a text-node payload map contains translated-slot
    ///     evidence after semantic normalization.
    /// </summary>
    private static bool MapHasTranslatedSlotEvidence(
        IReadOnlyDictionary<string, string> liveValues,
        IReadOnlyDictionary<string, string> originalValues,
        IReadOnlyDictionary<string, string> translatedValues)
    {
        foreach (var (index, liveText) in liveValues)
        {
            if (!originalValues.TryGetValue(index, out var originalText) ||
                !translatedValues.TryGetValue(index, out var translatedText))
            {
                continue;
            }

            if (MatchesRecoveryText(liveText, translatedText) &&
                !MatchesRecoveryText(liveText, originalText))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Determines whether two numeric payload maps differ semantically
    ///     after recovery normalization.
    /// </summary>
    private static bool MapHasSemanticDifference(
        IReadOnlyDictionary<int, string> leftValues,
        IReadOnlyDictionary<int, string> rightValues)
    {
        if (leftValues.Count != rightValues.Count)
        {
            return true;
        }

        foreach (var (index, leftText) in leftValues)
        {
            if (!rightValues.TryGetValue(index, out var rightText) ||
                !MatchesRecoveryText(leftText, rightText))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Determines whether two text-node payload maps differ semantically
    ///     after recovery normalization.
    /// </summary>
    private static bool MapHasSemanticDifference(
        IReadOnlyDictionary<string, string> leftValues,
        IReadOnlyDictionary<string, string> rightValues)
    {
        if (leftValues.Count != rightValues.Count)
        {
            return true;
        }

        foreach (var (index, leftText) in leftValues)
        {
            if (!rightValues.TryGetValue(index, out var rightText) ||
                !MatchesRecoveryText(leftText, rightText))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Compares two Tooltip text values using semantic recovery
    ///     normalization.
    /// </summary>
    private static bool MatchesRecoveryText(string? left, string? right)
    {
        return string.Equals(
            TooltipTextNormalizationHelper.NormalizeForRecovery(left),
            TooltipTextNormalizationHelper.NormalizeForRecovery(right),
            StringComparison.Ordinal);
    }
}
