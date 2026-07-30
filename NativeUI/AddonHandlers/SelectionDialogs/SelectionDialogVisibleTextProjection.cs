// <copyright file="SelectionDialogVisibleTextProjection.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.SelectionDialogs;

/// <summary>
///     Projects currently visible selection-dialog text nodes onto the ordered
///     source payload used for translation and persistence.
/// </summary>
internal static class SelectionDialogVisibleTextProjection
{
    /// <summary>
    ///     Represents one visible selection-dialog text candidate together with
    ///     its current screen position.
    /// </summary>
    /// <param name="VisibleIndex">
    ///     The original visible-text index captured from node traversal.
    /// </param>
    /// <param name="Text">The visible text content.</param>
    /// <param name="ScreenY">The current on-screen Y position.</param>
    /// <param name="ScreenX">The current on-screen X position.</param>
    public readonly record struct SelectionDialogVisibleTextCandidate(
        int VisibleIndex,
        string Text,
        int ScreenY,
        int ScreenX);

    /// <summary>
    ///     Matches the currently visible ordered text-node values against the
    ///     authoritative ordered payload, allowing the visible surface to be a
    ///     stable subsequence when the addon exposes hidden structured strings.
    /// </summary>
    /// <param name="sourceTexts">The authoritative ordered payload texts.</param>
    /// <param name="visibleTexts">The currently visible ordered text-node texts.</param>
    /// <returns>The visible-to-source index matches in encounter order.</returns>
    public static IReadOnlyList<SelectionDialogVisibleTextMatch> MatchVisibleTexts(
        IReadOnlyList<string> sourceTexts,
        IReadOnlyList<string> visibleTexts)
    {
        ArgumentNullException.ThrowIfNull(visibleTexts);

        return MatchVisibleTexts(
            sourceTexts,
            visibleTexts.Select(
                static (text, index) =>
                    new SelectionDialogVisibleTextCandidate(
                        index,
                        text,
                        0,
                        index)).ToList());
    }

    /// <summary>
    ///     Matches visible text candidates against the authoritative payload
    ///     after ordering the candidates by their current screen position.
    /// </summary>
    /// <param name="sourceTexts">The authoritative ordered payload texts.</param>
    /// <param name="visibleTexts">
    ///     The captured visible text candidates in traversal order.
    /// </param>
    /// <returns>The visible-to-source index matches in visual encounter order.</returns>
    public static IReadOnlyList<SelectionDialogVisibleTextMatch> MatchVisibleTexts(
        IReadOnlyList<string> sourceTexts,
        IReadOnlyList<SelectionDialogVisibleTextCandidate> visibleTexts)
    {
        ArgumentNullException.ThrowIfNull(sourceTexts);
        ArgumentNullException.ThrowIfNull(visibleTexts);

        var matches = new List<SelectionDialogVisibleTextMatch>();
        var sourceSearchStart = 0;
        var orderedVisibleTexts = visibleTexts
            .OrderBy(static candidate => candidate.ScreenY)
            .ThenBy(static candidate => candidate.ScreenX)
            .ThenBy(static candidate => candidate.VisibleIndex)
            .ToList();

        foreach (var visibleText in orderedVisibleTexts)
        {
            var normalizedVisibleText = NormalizeText(visibleText.Text);
            if (normalizedVisibleText.Length == 0)
            {
                continue;
            }

            for (var sourceIndex = sourceSearchStart;
                 sourceIndex < sourceTexts.Count;
                 sourceIndex++)
            {
                if (!TextsMatch(sourceTexts[sourceIndex], normalizedVisibleText))
                {
                    continue;
                }

                matches.Add(new SelectionDialogVisibleTextMatch(
                    visibleText.VisibleIndex,
                    sourceIndex));
                sourceSearchStart = sourceIndex + 1;
                break;
            }
        }

        return matches;
    }

    private static bool TextsMatch(string? sourceText, string? visibleText)
    {
        return string.Equals(
            NormalizeText(sourceText),
            NormalizeText(visibleText),
            StringComparison.Ordinal);
    }

    private static string NormalizeText(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Trim();
    }
}

/// <summary>
///     Represents one ordered visible text-node match projected back onto the
///     authoritative selection-dialog source payload.
/// </summary>
/// <param name="VisibleIndex">The ordered visible text-node index.</param>
/// <param name="SourceIndex">The ordered source payload index.</param>
internal readonly record struct SelectionDialogVisibleTextMatch(
    int VisibleIndex,
    int SourceIndex);
