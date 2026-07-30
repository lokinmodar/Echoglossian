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
        ArgumentNullException.ThrowIfNull(sourceTexts);
        ArgumentNullException.ThrowIfNull(visibleTexts);

        var matches = new List<SelectionDialogVisibleTextMatch>();
        var sourceSearchStart = 0;

        for (var visibleIndex = 0; visibleIndex < visibleTexts.Count; visibleIndex++)
        {
            var visibleText = NormalizeText(visibleTexts[visibleIndex]);
            if (visibleText.Length == 0)
            {
                continue;
            }

            for (var sourceIndex = sourceSearchStart;
                 sourceIndex < sourceTexts.Count;
                 sourceIndex++)
            {
                if (!TextsMatch(sourceTexts[sourceIndex], visibleText))
                {
                    continue;
                }

                matches.Add(new SelectionDialogVisibleTextMatch(
                    visibleIndex,
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
