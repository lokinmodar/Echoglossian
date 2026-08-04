// <copyright file="StructuredTooltipTranslationValidation.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Validates whether one structured tooltip contains a complete, meaningful
///     translation rather than source text copied into translated fields.
/// </summary>
internal static class StructuredTooltipTranslationValidation
{
    /// <summary>
    ///     Gets whether the translated name and description fully cover the
    ///     source payload without preserving a required source description.
    /// </summary>
    /// <param name="originalName">The canonical source name.</param>
    /// <param name="originalDescription">The canonical source description.</param>
    /// <param name="translatedName">The candidate translated name.</param>
    /// <param name="translatedDescription">The candidate translated description.</param>
    /// <returns>
    ///     <see langword="true" /> when the candidate is complete and differs
    ///     meaningfully from the source payload; otherwise <see langword="false" />.
    /// </returns>
    public static bool HasCompleteMeaningfulTranslation(
        string? originalName,
        string? originalDescription,
        string? translatedName,
        string? translatedDescription)
    {
        if (string.IsNullOrWhiteSpace(translatedName))
        {
            return false;
        }

        var hasSourceDescription = !string.IsNullOrWhiteSpace(
            originalDescription);
        if (hasSourceDescription && string.IsNullOrWhiteSpace(
                translatedDescription))
        {
            return false;
        }

        var translatedNameMatchesSource = MatchesSourceText(
            originalName,
            translatedName);
        if (!hasSourceDescription)
        {
            return !translatedNameMatchesSource;
        }

        return !MatchesSourceText(originalDescription, translatedDescription);
    }

    /// <summary>
    ///     Returns a translated field only when it contains meaningful text
    ///     distinct from its canonical source field.
    /// </summary>
    /// <param name="sourceText">The canonical source text.</param>
    /// <param name="candidateText">The candidate translated text.</param>
    /// <returns>
    ///     The candidate translated text, or <see langword="null" /> when it
    ///     is blank or source-equivalent.
    /// </returns>
    public static string? GetMeaningfulTranslationOrNull(
        string? sourceText,
        string? candidateText)
    {
        return string.IsNullOrWhiteSpace(candidateText) ||
               MatchesSourceText(sourceText, candidateText)
            ? null
            : candidateText;
    }

    /// <summary>
    ///     Gets whether two values represent the same visible text after native
    ///     SeString formatting noise has been removed.
    /// </summary>
    /// <param name="sourceText">The canonical source text.</param>
    /// <param name="candidateText">The translated candidate text.</param>
    /// <returns><see langword="true" /> when both values are source-equivalent.</returns>
    private static bool MatchesSourceText(string? sourceText, string? candidateText)
    {
        return string.Equals(
            NativeTextComparisonNormalizationHelper.NormalizeForComparison(sourceText),
            NativeTextComparisonNormalizationHelper.NormalizeForComparison(candidateText),
            StringComparison.Ordinal);
    }
}
