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
}
