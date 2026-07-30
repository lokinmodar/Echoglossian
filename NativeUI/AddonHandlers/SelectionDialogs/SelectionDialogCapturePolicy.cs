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
    ///     Determines whether the visible text-node payload should be promoted
    ///     ahead of a detached structured payload so native mutation and hover
    ///     tooltips target the live nodes currently on screen.
    /// </summary>
    /// <param name="primaryTexts">The structured payload texts.</param>
    /// <param name="textNodeTexts">The visible text-node texts.</param>
    /// <returns>
    ///     <see langword="true" /> when the visible text nodes should be
    ///     preferred; otherwise, <see langword="false" />.
    /// </returns>
    public static bool ShouldPreferTextNodePayload(
        IReadOnlyList<string> primaryTexts,
        IReadOnlyList<string> textNodeTexts)
    {
        if (primaryTexts.Count == 0 ||
            primaryTexts.Count != textNodeTexts.Count)
        {
            return false;
        }

        for (var index = 0; index < primaryTexts.Count; index++)
        {
            if (!string.Equals(
                    NormalizeVisibleText(primaryTexts[index]),
                    NormalizeVisibleText(textNodeTexts[index]),
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeVisibleText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            text.Split(
                ['\r', '\n', '\t', ' '],
                StringSplitOptions.RemoveEmptyEntries));
    }
}
