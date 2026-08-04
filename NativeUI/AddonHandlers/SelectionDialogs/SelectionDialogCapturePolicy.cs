// <copyright file="SelectionDialogCapturePolicy.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.SelectionDialogs;

/// <summary>
///     Resolves the preferred capture path for generic selection-dialog
///     surfaces while keeping UI collection ordered from the most structured
///     source to the loosest fallback.
/// </summary>
internal static class SelectionDialogCapturePolicy
{
    /// <summary>
    ///     Resolves the best available selection-dialog capture source.
    /// </summary>
    /// <param name="hasAtkValueText">Whether <c>AtkValue</c> text exists.</param>
    /// <param name="hasStringArrayText">
    ///     Whether <c>StringArrayData</c> text exists.
    /// </param>
    /// <param name="hasReadableTextNodes">
    ///     Whether readable text nodes exist.
    /// </param>
    /// <returns>The preferred capture source kind.</returns>
    public static SelectionDialogCaptureSourceKind ResolveBestSource(
        bool hasAtkValueText,
        bool hasStringArrayText,
        bool hasReadableTextNodes)
    {
        if (hasAtkValueText)
        {
            return SelectionDialogCaptureSourceKind.AtkValues;
        }

        if (hasStringArrayText)
        {
            return SelectionDialogCaptureSourceKind.StringArrayData;
        }

        return hasReadableTextNodes
            ? SelectionDialogCaptureSourceKind.TextNodes
            : SelectionDialogCaptureSourceKind.None;
    }

    /// <summary>
    ///     Determines whether a visible text-node payload should replace the
    ///     otherwise preferred structured payload because both expose the same
    ///     ordered texts.
    /// </summary>
    /// <param name="structuredPayload">
    ///     The ATK-value or string-array payload chosen by structure.
    /// </param>
    /// <param name="textNodePayload">The visible text-node payload.</param>
    /// <returns>
    ///     <see langword="true" /> when the visible text-node payload should
    ///     become authoritative; otherwise, <see langword="false" />.
    /// </returns>
    public static bool ShouldPreferTextNodePayload(
        SelectionDialogPayload structuredPayload,
        SelectionDialogPayload textNodePayload)
    {
        ArgumentNullException.ThrowIfNull(structuredPayload);
        ArgumentNullException.ThrowIfNull(textNodePayload);

        if (structuredPayload.SourceKind ==
                SelectionDialogCaptureSourceKind.TextNodes ||
            textNodePayload.SourceKind !=
                SelectionDialogCaptureSourceKind.TextNodes ||
            textNodePayload.TextNodeAddresses.Count == 0 ||
            structuredPayload.Texts.Count != textNodePayload.Texts.Count)
        {
            return false;
        }

        for (var index = 0; index < structuredPayload.Texts.Count; index++)
        {
            if (!TextsMatch(
                    structuredPayload.Texts[index],
                    textNodePayload.Texts[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TextsMatch(string? left, string? right)
    {
        return string.Equals(
            NormalizeText(left),
            NormalizeText(right),
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
