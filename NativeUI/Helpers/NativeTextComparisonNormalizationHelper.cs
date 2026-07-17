// <copyright file="NativeTextComparisonNormalizationHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;
using System.Text;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Normalizes live native text for equality checks when wrapped SeString
///     payloads inject raw control bytes into visible node text.
/// </summary>
internal static class NativeTextComparisonNormalizationHelper
{
    /// <summary>
    ///     Collapses control-format noise and line-break payload bytes so two
    ///     native text values can be compared by meaning instead of raw wrapped
    ///     representation.
    /// </summary>
    /// <param name="text">The live or replacement text to normalize.</param>
    /// <returns>The normalized comparison text.</returns>
    public static string NormalizeForComparison(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalizedText = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var builder = new StringBuilder(normalizedText.Length);

        foreach (var character in normalizedText)
        {
            if (character == '\n' || character == '\t' || character == ' ')
            {
                builder.Append(' ');
                continue;
            }

            var category = char.GetUnicodeCategory(character);
            if (char.IsControl(character) ||
                category == UnicodeCategory.Format ||
                category == UnicodeCategory.PrivateUse)
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(character);
        }

        return string.Join(
            " ",
            builder.ToString().Split(
                [' ', '\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries));
    }
}
