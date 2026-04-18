// <copyright file="DbFirstPayloadRecoveryHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Recovers canonical original payloads when a live addon surface is
///     already showing translated or mixed original/translated text.
/// </summary>
internal static class DbFirstPayloadRecoveryHelper
{
    /// <summary>
    ///     Tries to recover the original payload from one set of persisted
    ///     original/translated candidates.
    /// </summary>
    /// <param name="livePayload">The currently visible live payload.</param>
    /// <param name="candidates">
    ///     The candidate original/translated payload pairs for the same addon
    ///     scope.
    /// </param>
    /// <param name="originalPayload">
    ///     The recovered original payload when a unique candidate matches.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when one unique candidate matches the live
    ///     payload; otherwise <see langword="false" />.
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
    ///     Determines whether the live payload contains clear evidence that at
    ///     least one visible slot is already showing translated text from one
    ///     of the persisted candidates.
    /// </summary>
    /// <param name="livePayload">The currently visible live payload.</param>
    /// <param name="candidates">
    ///     The candidate original/translated payload pairs for the same addon
    ///     scope.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when at least one live slot matches a
    ///     translated candidate value while differing from its original value.
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
                    candidate.TranslatedPayload.StringArrayValues))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Scores one candidate against the currently visible live payload.
    /// </summary>
    /// <param name="livePayload">The currently visible payload.</param>
    /// <param name="candidate">The candidate pair to evaluate.</param>
    /// <param name="score">The resulting match score.</param>
    /// <returns>
    ///     <see langword="true" /> when the live payload is fully explainable
    ///     by the candidate's original/translated states.
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
                out var stringArrayScore))
        {
            return false;
        }

        score = atkScore + stringArrayScore;
        return score > 0;
    }

    /// <summary>
    ///     Scores one payload map against the candidate original/translated map
    ///     pair.
    /// </summary>
    /// <param name="liveValues">The currently visible values.</param>
    /// <param name="originalValues">The persisted original values.</param>
    /// <param name="translatedValues">The persisted translated values.</param>
    /// <param name="score">The resulting match score.</param>
    /// <returns>
    ///     <see langword="true" /> when every live slot matches either the
    ///     original or translated slot of the candidate.
    /// </returns>
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

            if (string.Equals(liveText, translatedText, StringComparison.Ordinal) ||
                string.Equals(liveText, originalText, StringComparison.Ordinal))
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
    ///     Determines whether one live payload map already contains translated
    ///     text from a candidate.
    /// </summary>
    /// <param name="liveValues">The currently visible values.</param>
    /// <param name="originalValues">The persisted original values.</param>
    /// <param name="translatedValues">The persisted translated values.</param>
    /// <returns>
    ///     <see langword="true" /> when at least one slot matches a translated
    ///     candidate value but not the corresponding original value.
    /// </returns>
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

            if (string.Equals(liveText, translatedText, StringComparison.Ordinal) &&
                !string.Equals(liveText, originalText, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
///     Represents one persisted original/translated candidate pair used to
///     recover a canonical DB-first payload.
/// </summary>
/// <param name="OriginalPayload">The persisted original payload.</param>
/// <param name="TranslatedPayload">The persisted translated payload.</param>
internal readonly record struct DbFirstPayloadRecoveryCandidate(
    DbFirstGameWindowPayload OriginalPayload,
    DbFirstGameWindowPayload TranslatedPayload);
