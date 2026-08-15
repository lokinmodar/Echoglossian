// <copyright file="DialogueGlossaryTermProtector.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text;

namespace Echoglossian.Translators.Helpers;

/// <summary>
///     Protects exact dialogue glossary terms with deterministic opaque markers
///     before provider translation and restores configured targets after the
///     provider returns those markers unchanged.
/// </summary>
public static class DialogueGlossaryTermProtector
{
    private const string BaseMarkerPrefix = "[[EGLO_GLOSS_";
    private const string MarkerSuffix = "]]";

    /// <summary>
    ///     Captures the protected source text plus the marker-to-target mapping
    ///     required to restore the final translated output.
    /// </summary>
    /// <param name="OriginalText">The original visible source text.</param>
    /// <param name="ProtectedText">The source text rewritten with opaque markers.</param>
    /// <param name="MarkerPrefix">The deterministic marker prefix chosen for the request.</param>
    /// <param name="Occurrences">The protected glossary occurrences in source order.</param>
    public readonly record struct ProtectionResult(
        string OriginalText,
        string ProtectedText,
        string MarkerPrefix,
        IReadOnlyList<ProtectedOccurrence> Occurrences);

    /// <summary>
    ///     Captures one protected glossary occurrence.
    /// </summary>
    /// <param name="Marker">The opaque marker inserted into the protected text.</param>
    /// <param name="SourceText">The matched source glossary term.</param>
    /// <param name="TargetText">The configured target glossary term.</param>
    public readonly record struct ProtectedOccurrence(
        string Marker,
        string SourceText,
        string TargetText);

    /// <summary>
    ///     Describes the outcome of restoring protected glossary markers from a
    ///     provider response.
    /// </summary>
    /// <param name="Succeeded">Whether restoration succeeded.</param>
    /// <param name="RestoredText">The restored text when successful.</param>
    /// <param name="FailureReason">The stable failure reason when restoration failed.</param>
    public readonly record struct RestoreResult(
        bool Succeeded,
        string RestoredText,
        string? FailureReason);

    /// <summary>
    ///     Rewrites source glossary terms in the current source text to stable
    ///     opaque markers using longest-match-first phrase matching.
    /// </summary>
    /// <param name="sourceText">The current visible source text.</param>
    /// <param name="glossaryEntries">The glossary entries active for the request.</param>
    /// <returns>The protected text plus the marker mapping required for restoration.</returns>
    public static ProtectionResult Protect(
        string sourceText,
        IReadOnlyList<StructuredDialogueGlossaryEntry>? glossaryEntries)
    {
        if (string.IsNullOrEmpty(sourceText) ||
            glossaryEntries == null ||
            glossaryEntries.Count == 0)
        {
            return new ProtectionResult(
                sourceText ?? string.Empty,
                sourceText ?? string.Empty,
                string.Empty,
                []);
        }

        var candidates = glossaryEntries
            .Select(
                static (entry, index) => new IndexedGlossaryEntry(index, entry))
            .Where(static indexed => !string.IsNullOrWhiteSpace(indexed.Entry.SourceText))
            .Where(static indexed => !string.IsNullOrWhiteSpace(indexed.Entry.TargetText))
            .OrderByDescending(static indexed => indexed.Entry.SourceText.Length)
            .ThenBy(static indexed => indexed.Index)
            .ToList();
        if (candidates.Count == 0)
        {
            return new ProtectionResult(
                sourceText,
                sourceText,
                string.Empty,
                []);
        }

        var markerPrefix = ResolveMarkerPrefix(sourceText);
        var builder = new StringBuilder(sourceText.Length);
        var occurrences = new List<ProtectedOccurrence>();
        var cursor = 0;
        while (cursor < sourceText.Length)
        {
            var match = FindMatch(sourceText, cursor, candidates);
            if (match == null)
            {
                builder.Append(sourceText[cursor]);
                cursor++;
                continue;
            }

            var matchedEntry = match.Value;
            var marker = BuildMarker(markerPrefix, occurrences.Count);
            builder.Append(marker);
            occurrences.Add(
                new ProtectedOccurrence(
                    marker,
                    matchedEntry.SourceText,
                    matchedEntry.TargetText));
            cursor += matchedEntry.SourceText.Length;
        }

        return new ProtectionResult(
            sourceText,
            builder.ToString(),
            markerPrefix,
            occurrences);
    }

    /// <summary>
    ///     Restores configured glossary targets from one provider response that
    ///     must preserve every required opaque marker exactly once.
    /// </summary>
    /// <param name="translatedText">The provider response text.</param>
    /// <param name="protectionResult">The protection mapping for the request.</param>
    /// <returns>The restoration result.</returns>
    public static RestoreResult TryRestore(
        string translatedText,
        ProtectionResult protectionResult)
    {
        if (protectionResult.Occurrences.Count == 0)
        {
            return new RestoreResult(
                true,
                translatedText,
                null);
        }

        foreach (var occurrence in protectionResult.Occurrences)
        {
            var markerCount = CountOccurrences(
                translatedText,
                occurrence.Marker);
            if (markerCount == 0)
            {
                return new RestoreResult(
                    false,
                    string.Empty,
                    "missing-required-marker");
            }

            if (markerCount > 1)
            {
                return new RestoreResult(
                    false,
                    string.Empty,
                    "duplicated-required-marker");
            }
        }

        var unmatchedMarkers = translatedText;
        foreach (var occurrence in protectionResult.Occurrences)
        {
            unmatchedMarkers = unmatchedMarkers.Replace(
                occurrence.Marker,
                string.Empty,
                StringComparison.Ordinal);
        }

        if (!string.IsNullOrEmpty(protectionResult.MarkerPrefix) &&
            unmatchedMarkers.Contains(
                protectionResult.MarkerPrefix,
                StringComparison.Ordinal))
        {
            return new RestoreResult(
                false,
                string.Empty,
                "unexpected-marker");
        }

        var restoredText = translatedText;
        foreach (var occurrence in protectionResult.Occurrences)
        {
            restoredText = restoredText.Replace(
                occurrence.Marker,
                occurrence.TargetText,
                StringComparison.Ordinal);
        }

        return new RestoreResult(
            true,
            restoredText,
            null);
    }

    /// <summary>
    ///     Holds one glossary row plus its original order so ties remain stable.
    /// </summary>
    /// <param name="Index">The original glossary row order.</param>
    /// <param name="Entry">The glossary row.</param>
    private readonly record struct IndexedGlossaryEntry(
        int Index,
        StructuredDialogueGlossaryEntry Entry);

    /// <summary>
    ///     Finds the longest glossary entry that matches the current cursor.
    /// </summary>
    /// <param name="sourceText">The current source text.</param>
    /// <param name="cursor">The current cursor position.</param>
    /// <param name="candidates">The ordered glossary candidates.</param>
    /// <returns>The matching glossary entry, or <see langword="null"/>.</returns>
    private static StructuredDialogueGlossaryEntry? FindMatch(
        string sourceText,
        int cursor,
        IReadOnlyList<IndexedGlossaryEntry> candidates)
    {
        foreach (var candidate in candidates)
        {
            var sourceTerm = candidate.Entry.SourceText;
            if (cursor + sourceTerm.Length > sourceText.Length)
            {
                continue;
            }

            if (!string.Equals(
                    sourceText.Substring(cursor, sourceTerm.Length),
                    sourceTerm,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsWholeTermMatch(sourceText, cursor, sourceTerm))
            {
                continue;
            }

            return candidate.Entry;
        }

        return null;
    }

    /// <summary>
    ///     Determines whether a matching glossary term stays outside unrelated
    ///     larger words.
    /// </summary>
    /// <param name="sourceText">The current source text.</param>
    /// <param name="cursor">The match cursor.</param>
    /// <param name="sourceTerm">The matched glossary term.</param>
    /// <returns><see langword="true"/> when the match is safe to protect.</returns>
    private static bool IsWholeTermMatch(
        string sourceText,
        int cursor,
        string sourceTerm)
    {
        var requiresLeadingBoundary = IsWordLike(sourceTerm[0]);
        var requiresTrailingBoundary = IsWordLike(sourceTerm[^1]);

        if (requiresLeadingBoundary &&
            cursor > 0 &&
            IsWordLike(sourceText[cursor - 1]))
        {
            return false;
        }

        var trailingIndex = cursor + sourceTerm.Length;
        if (requiresTrailingBoundary &&
            trailingIndex < sourceText.Length &&
            IsWordLike(sourceText[trailingIndex]))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Determines whether one character should participate in whole-term
    ///     boundary checks.
    /// </summary>
    /// <param name="value">The character to inspect.</param>
    /// <returns><see langword="true"/> when the character is word-like.</returns>
    private static bool IsWordLike(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_';
    }

    /// <summary>
    ///     Chooses a deterministic marker prefix that does not already appear in
    ///     the current source text.
    /// </summary>
    /// <param name="sourceText">The current source text.</param>
    /// <returns>The unique marker prefix for this request.</returns>
    private static string ResolveMarkerPrefix(string sourceText)
    {
        for (var nonce = 0; ; nonce++)
        {
            var candidate = $"{BaseMarkerPrefix}{nonce}_";
            if (!sourceText.Contains(candidate, StringComparison.Ordinal))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    ///     Builds one deterministic marker from the chosen prefix and
    ///     occurrence order.
    /// </summary>
    /// <param name="markerPrefix">The chosen request marker prefix.</param>
    /// <param name="occurrenceIndex">The protected occurrence index.</param>
    /// <returns>The marker text.</returns>
    private static string BuildMarker(
        string markerPrefix,
        int occurrenceIndex)
    {
        return $"{markerPrefix}{occurrenceIndex:D4}{MarkerSuffix}";
    }

    /// <summary>
    ///     Counts exact ordinal occurrences of one marker in one provider
    ///     response string.
    /// </summary>
    /// <param name="translatedText">The provider response text.</param>
    /// <param name="marker">The exact marker to count.</param>
    /// <returns>The occurrence count.</returns>
    private static int CountOccurrences(
        string translatedText,
        string marker)
    {
        if (string.IsNullOrEmpty(translatedText) ||
            string.IsNullOrEmpty(marker))
        {
            return 0;
        }

        var count = 0;
        var searchStart = 0;
        while (searchStart < translatedText.Length)
        {
            var markerIndex = translatedText.IndexOf(
                marker,
                searchStart,
                StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                break;
            }

            count++;
            searchStart = markerIndex + marker.Length;
        }

        return count;
    }
}
