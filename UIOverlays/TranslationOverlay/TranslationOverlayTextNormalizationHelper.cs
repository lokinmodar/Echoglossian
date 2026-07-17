// <copyright file="TranslationOverlayTextNormalizationHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;
using System.Text;

namespace Echoglossian.UIOverlays.TranslationOverlay;

/// <summary>
/// Normalizes overlay text for display when raw native payload separators leak
/// into the source-facing overlay path.
/// </summary>
internal static class TranslationOverlayTextNormalizationHelper
{
    private const string RawSeStringLineBreakPayload = "\u0002\u0010\u0001\u0003";

    /// <summary>
    /// Converts raw SeString line-break payloads and legacy carriage-return
    /// separators into regular new lines while stripping residual non-printing
    /// control bytes.
    /// </summary>
    /// <param name="text">The raw overlay text.</param>
    /// <returns>The normalized display text.</returns>
    public static string NormalizeForDisplay(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var normalizedText = text
            .Replace(RawSeStringLineBreakPayload, "\n", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var builder = new StringBuilder(normalizedText.Length);

        foreach (var character in normalizedText)
        {
            if (character == '\n' || character == '\t')
            {
                builder.Append(character);
                continue;
            }

            var category = char.GetUnicodeCategory(character);
            if (char.IsControl(character) || category == UnicodeCategory.Format)
            {
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
