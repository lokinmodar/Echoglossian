// <copyright file="TooltipSemanticLineHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Preserves Tooltip semantic line structure across provider translation
///     and persisted payload reuse.
/// </summary>
internal static class TooltipSemanticLineHelper
{
    /// <summary>
    ///     Flattens Tooltip text nodes into per-line translation entries so
    ///     semantic source line breaks survive provider translation.
    /// </summary>
    /// <param name="textNodes">The semantic Tooltip text nodes.</param>
    /// <returns>The per-line translation payload.</returns>
    public static SortedDictionary<string, string> FlattenTextNodesForTranslation(
        IReadOnlyDictionary<string, string> textNodes)
    {
        var flattenedTextNodes = new SortedDictionary<string, string>(
            StringComparer.Ordinal);
        foreach (var (textNodeKey, text) in textNodes)
        {
            var semanticLines = SplitSemanticLines(text);
            for (var index = 0; index < semanticLines.Length; index++)
            {
                flattenedTextNodes[BuildLineKey(textNodeKey, index)] =
                    semanticLines[index];
            }
        }

        return flattenedTextNodes;
    }

    /// <summary>
    ///     Rebuilds semantic Tooltip text nodes from one per-line translated
    ///     payload.
    /// </summary>
    /// <param name="originalTextNodes">
    ///     The original Tooltip text-node shape and semantic line counts.
    /// </param>
    /// <param name="translatedLineTextNodes">The per-line translated payload.</param>
    /// <param name="rebuiltTextNodes">Receives the rebuilt text nodes.</param>
    /// <returns>
    ///     <see langword="true" /> when every translated semantic line was
    ///     present; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryRebuildTranslatedTextNodes(
        IReadOnlyDictionary<string, string> originalTextNodes,
        IReadOnlyDictionary<string, string> translatedLineTextNodes,
        out SortedDictionary<string, string> rebuiltTextNodes)
    {
        rebuiltTextNodes = new SortedDictionary<string, string>(
            StringComparer.Ordinal);
        foreach (var (textNodeKey, originalText) in originalTextNodes)
        {
            var originalLines = SplitSemanticLines(originalText);
            var translatedLines = new string[originalLines.Length];

            for (var index = 0; index < originalLines.Length; index++)
            {
                if (!translatedLineTextNodes.TryGetValue(
                        BuildLineKey(textNodeKey, index),
                        out var translatedLine) ||
                    string.IsNullOrWhiteSpace(translatedLine))
                {
                    rebuiltTextNodes = new SortedDictionary<string, string>(
                        StringComparer.Ordinal);
                    return false;
                }

                translatedLines[index] = NormalizeTranslatedLine(translatedLine);
            }

            rebuiltTextNodes[textNodeKey] = string.Join('\n', translatedLines);
        }

        return true;
    }

    /// <summary>
    ///     Determines whether one resolved Tooltip payload still preserves the
    ///     semantic source line structure needed for native apply.
    /// </summary>
    /// <param name="originalTextNodes">The original Tooltip payload.</param>
    /// <param name="translatedTextNodes">The resolved translated payload.</param>
    /// <returns>
    ///     <see langword="true" /> when line structure is compatible; otherwise
    ///     <see langword="false" />.
    /// </returns>
    public static bool HasCompatibleSemanticLineStructure(
        IReadOnlyDictionary<string, string> originalTextNodes,
        IReadOnlyDictionary<string, string> translatedTextNodes)
    {
        foreach (var (textNodeKey, originalText) in originalTextNodes)
        {
            if (!translatedTextNodes.TryGetValue(textNodeKey, out var translatedText) ||
                !HasCompatibleSemanticLineStructure(originalText, translatedText))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Determines whether one translated Tooltip text preserves the same
    ///     semantic line count as its original source.
    /// </summary>
    /// <param name="originalText">The semantic original text.</param>
    /// <param name="translatedText">The translated text candidate.</param>
    /// <returns>
    ///     <see langword="true" /> when the translated text keeps the semantic
    ///     line count required by the source text; otherwise
    ///     <see langword="false" />.
    /// </returns>
    public static bool HasCompatibleSemanticLineStructure(
        string? originalText,
        string? translatedText)
    {
        if (string.IsNullOrWhiteSpace(translatedText))
        {
            return false;
        }

        var originalLines = SplitSemanticLines(originalText);
        if (originalLines.Length <= 1)
        {
            return true;
        }

        var translatedLines = SplitSemanticLines(translatedText);
        return translatedLines.Length == originalLines.Length &&
               translatedLines.All(line => !string.IsNullOrWhiteSpace(line));
    }

    /// <summary>
    ///     Splits one semantic Tooltip text into its persisted logical lines.
    /// </summary>
    /// <param name="text">The semantic Tooltip text.</param>
    /// <returns>The logical lines.</returns>
    private static string[] SplitSemanticLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [string.Empty];
        }

        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.None);
    }

    /// <summary>
    ///     Builds one stable per-line translation key for a Tooltip text node.
    /// </summary>
    /// <param name="textNodeKey">The semantic text-node key.</param>
    /// <param name="lineIndex">The zero-based semantic line index.</param>
    /// <returns>The per-line key.</returns>
    private static string BuildLineKey(string textNodeKey, int lineIndex)
    {
        return $"{textNodeKey}#{lineIndex}";
    }

    /// <summary>
    ///     Normalizes one translated semantic line before it is rebuilt into a
    ///     Tooltip text node.
    /// </summary>
    /// <param name="translatedLine">The translated semantic line.</param>
    /// <returns>The normalized line.</returns>
    private static string NormalizeTranslatedLine(string translatedLine)
    {
        return translatedLine
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim('\n');
    }
}
