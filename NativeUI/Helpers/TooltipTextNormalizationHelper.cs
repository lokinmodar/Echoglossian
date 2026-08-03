// <copyright file="TooltipTextNormalizationHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;
using System.Text;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Normalizes Tooltip addon text so capture and native-state comparisons
///     operate on semantic text instead of wrapped SeString payload noise.
/// </summary>
internal static class TooltipTextNormalizationHelper
{
    private const string RawSeStringLineBreakPayload = "\u0002\u0010\u0001\u0003";

    /// <summary>
    ///     Removes raw wrap payload bytes and residual formatting controls while
    ///     preserving semantic line breaks owned by the source text itself.
    /// </summary>
    /// <param name="text">The raw Tooltip text.</param>
    /// <returns>The normalized capture text.</returns>
    public static string NormalizeForCapture(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalizedText = text
            .Replace(RawSeStringLineBreakPayload, string.Empty, StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var builder = new StringBuilder(normalizedText.Length);
        var pendingSpace = false;

        foreach (var character in normalizedText)
        {
            if (character == '\n')
            {
                TrimTrailingSpaces(builder);
                if (builder.Length > 0 &&
                    builder[builder.Length - 1] != '\n')
                {
                    builder.Append('\n');
                }

                pendingSpace = false;
                continue;
            }

            if (character == ' ' || character == '\t')
            {
                pendingSpace = builder.Length > 0 &&
                               builder[builder.Length - 1] != '\n';
                continue;
            }

            var category = char.GetUnicodeCategory(character);
            if (char.IsControl(character) ||
                category == UnicodeCategory.Format ||
                category == UnicodeCategory.PrivateUse)
            {
                continue;
            }

            if (pendingSpace &&
                builder.Length > 0 &&
                builder[builder.Length - 1] != '\n')
            {
                builder.Append(' ');
            }

            builder.Append(character);
            pendingSpace = false;
        }

        TrimTrailingSpaces(builder);
        return builder
            .ToString()
            .Trim('\n');
    }

    /// <summary>
    ///     Normalizes Tooltip text for semantic recovery by removing
    ///     whitespace-only layout churn after capture normalization.
    /// </summary>
    /// <param name="text">The raw Tooltip text.</param>
    /// <returns>The semantic recovery key.</returns>
    public static string NormalizeForRecovery(string? text)
    {
        var normalizedText = NormalizeForCapture(text);
        if (normalizedText.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(normalizedText.Length);
        foreach (var character in normalizedText)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Removes trailing spaces that were only introduced by wrapped payload
    ///     bytes before one semantic line break.
    /// </summary>
    /// <param name="builder">The builder under normalization.</param>
    private static void TrimTrailingSpaces(StringBuilder builder)
    {
        while (builder.Length > 0 &&
               builder[builder.Length - 1] == ' ')
        {
            builder.Length--;
        }
    }
}
