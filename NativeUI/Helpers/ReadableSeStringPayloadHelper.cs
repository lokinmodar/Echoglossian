// <copyright file="ReadableSeStringPayloadHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;
using System.Text;

using FFXIVClientStructs.FFXIV.Component.GUI;

using Lumina.Text.ReadOnly;
using LuminaSeStringBuilder = Lumina.Text.SeStringBuilder;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Reads, matches, captures, and reprojects readable native
///     <see cref="ReadOnlySeString" /> payloads while preserving macro payload
///     structure wherever possible.
/// </summary>
internal static class ReadableSeStringPayloadHelper
{
    /// <summary>
    ///     Reads the best readable text representation available from one live
    ///     native text node without mutating it.
    /// </summary>
    /// <param name="textNode">The live text node to inspect.</param>
    /// <returns>The preferred readable text, or an empty string.</returns>
    public static unsafe string ReadReadableTextNode(AtkTextNode* textNode)
    {
        if (textNode == null)
        {
            return string.Empty;
        }

        string currentText;
        try
        {
            currentText = textNode->NodeText.ToString();
        }
        catch
        {
            currentText = string.Empty;
        }

        string originalText;
        try
        {
            originalText = textNode->OriginalTextPointer
                .AsReadOnlySeStringSpan()
                .ExtractText();
        }
        catch
        {
            originalText = string.Empty;
        }

        string legacyText;
        try
        {
            legacyText = MemoryHelper.ReadSeStringAsString(
                             out _,
                             (nint)textNode->NodeText.StringPtr.Value) ??
                         string.Empty;
        }
        catch
        {
            legacyText = string.Empty;
        }

        return ResolveReadableTextNodeText(currentText, originalText, legacyText);
    }

    /// <summary>
    ///     Chooses the safest readable representation for one native text node.
    /// </summary>
    /// <param name="currentText">
    ///     The direct current text returned by the live native string wrapper.
    /// </param>
    /// <param name="originalText">
    ///     The readable text extracted from the node's structured original
    ///     payload.
    /// </param>
    /// <param name="legacyText">
    ///     The legacy SeString buffer-read fallback.
    /// </param>
    /// <returns>The preferred readable text for matching and hover work.</returns>
    public static string ResolveReadableTextNodeText(
        string currentText,
        string originalText,
        string legacyText)
    {
        if (ShouldPreferRicherReadableText(currentText, originalText))
        {
            return currentText;
        }

        if (!string.IsNullOrWhiteSpace(originalText))
        {
            return originalText;
        }

        if (!string.IsNullOrWhiteSpace(currentText))
        {
            return currentText;
        }

        return legacyText;
    }

    /// <summary>
    ///     Attempts to extract readable text from one persisted or captured
    ///     SeString payload.
    /// </summary>
    /// <param name="payload">The encoded payload bytes.</param>
    /// <param name="readableText">The readable text when extraction succeeds.</param>
    /// <returns><c>true</c> when readable text was extracted.</returns>
    public static bool TryExtractReadablePayloadText(
        byte[]? payload,
        out string readableText)
    {
        readableText = string.Empty;
        if (payload == null || payload.Length == 0)
        {
            return false;
        }

        try
        {
            readableText = new ReadOnlySeString(payload).ExtractText();
            return !string.IsNullOrWhiteSpace(readableText);
        }
        catch
        {
            readableText = string.Empty;
            return false;
        }
    }

    /// <summary>
    ///     Returns the captured payload only when its readable text still
    ///     matches the expected source text.
    /// </summary>
    /// <param name="payload">The captured payload bytes.</param>
    /// <param name="expectedText">The expected readable source text.</param>
    /// <returns>The original payload when it still matches; otherwise <see langword="null" />.</returns>
    public static byte[]? RetainMatchingPayload(
        byte[]? payload,
        string expectedText)
    {
        return TryExtractReadablePayloadText(payload, out var readableText) &&
               PayloadMatches(readableText, expectedText)
            ? payload
            : null;
    }

    /// <summary>
    ///     Resolves the preferred captured payload for one readable source
    ///     text, falling back from the original structured payload to the
    ///     current live payload when the original no longer matches the richer
    ///     visible text.
    /// </summary>
    /// <param name="originalPayload">The original structured payload bytes.</param>
    /// <param name="currentPayload">The current live payload bytes.</param>
    /// <param name="expectedText">The expected readable source text.</param>
    /// <returns>
    ///     The first matching payload that still resolves to the expected
    ///     readable text, or <see langword="null" /> when neither payload
    ///     matches.
    /// </returns>
    public static byte[]? ResolvePreferredMatchingPayload(
        byte[]? originalPayload,
        byte[]? currentPayload,
        string expectedText)
    {
        var originalMatchScore = GetPayloadMatchScore(
            originalPayload,
            expectedText);
        var currentMatchScore = GetPayloadMatchScore(
            currentPayload,
            expectedText);
        if (currentMatchScore > originalMatchScore)
        {
            return currentPayload;
        }

        if (originalMatchScore > 0)
        {
            return originalPayload;
        }

        return currentMatchScore > 0
            ? currentPayload
            : null;
    }

    /// <summary>
    ///     Captures the current original payload bytes from one live text node
    ///     only when the readable payload still matches the expected source
    ///     text.
    /// </summary>
    /// <param name="textNode">The live text node.</param>
    /// <param name="expectedText">The expected readable source text.</param>
    /// <returns>The captured payload bytes, if they still match.</returns>
    public static unsafe byte[]? TryCaptureMatchingPayload(
        AtkTextNode* textNode,
        string expectedText)
    {
        if (textNode == null || string.IsNullOrWhiteSpace(expectedText))
        {
            return null;
        }

        try
        {
            var originalPayload =
                ((ReadOnlySpan<byte>)textNode->OriginalTextPointer
                    .AsReadOnlySeStringSpan()).ToArray();
            byte[]? currentPayload = null;
            try
            {
                currentPayload = MemoryHelper.ReadSeStringNullTerminated(
                        (nint)textNode->NodeText.StringPtr.Value)
                    .Encode();
            }
            catch
            {
                currentPayload = null;
            }

            return ResolvePreferredMatchingPayload(
                originalPayload,
                currentPayload,
                expectedText);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Projects translated readable text onto one captured SeString payload
    ///     so native mutation can preserve original formatting macros when the
    ///     payload still matches the expected readable source text.
    /// </summary>
    /// <param name="originalPayload">The captured original SeString bytes.</param>
    /// <param name="originalText">The expected readable original text.</param>
    /// <param name="translatedText">The translated readable text.</param>
    /// <returns>
    ///     The projected payload bytes when payload reuse succeeds; otherwise,
    ///     <see langword="null" />.
    /// </returns>
    public static byte[]? ProjectReadablePayloadBytes(
        byte[]? originalPayload,
        string originalText,
        string translatedText)
    {
        var retainedPayload = RetainMatchingPayload(originalPayload, originalText);
        if (retainedPayload == null ||
            string.IsNullOrWhiteSpace(translatedText))
        {
            return null;
        }

        try
        {
            var sourcePayload = new ReadOnlySeString(retainedPayload);
            var sourceText = sourcePayload.ExtractText();
            var builder = new LuminaSeStringBuilder();
            builder.Append(sourcePayload);
            builder.ReplaceText(
                Encoding.UTF8.GetBytes(sourceText),
                ReadOnlySeString.FromText(translatedText));

            var projectedPayload = builder.ToReadOnlySeString();
            if (!PayloadMatches(
                    projectedPayload.ExtractText(),
                    translatedText))
            {
                return null;
            }

            var projectedBytes = projectedPayload.Data.ToArray();
            return projectedBytes.Length > 0 ? projectedBytes : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Compares visible native text with one expected payload while
    ///     allowing line wrapping and SeString whitespace differences.
    /// </summary>
    /// <param name="visibleText">The text read from the native node.</param>
    /// <param name="expectedText">The expected original or translated payload.</param>
    /// <returns><c>true</c> when the texts describe the same payload.</returns>
    public static bool PayloadMatches(
        string visibleText,
        string expectedText)
    {
        var normalizedVisibleText = NormalizePayloadComparisonText(visibleText);
        var normalizedExpectedText = NormalizePayloadComparisonText(expectedText);
        if (string.IsNullOrWhiteSpace(normalizedVisibleText) ||
            string.IsNullOrWhiteSpace(normalizedExpectedText))
        {
            return false;
        }

        if (string.Equals(
                normalizedVisibleText,
                normalizedExpectedText,
                StringComparison.Ordinal))
        {
            return true;
        }

        if (normalizedVisibleText.Length < 4 || normalizedExpectedText.Length < 4)
        {
            return false;
        }

        return normalizedVisibleText.Contains(
                   normalizedExpectedText,
                   StringComparison.Ordinal) ||
               normalizedExpectedText.Contains(
                   normalizedVisibleText,
                   StringComparison.Ordinal);
    }

    /// <summary>
    ///     Normalizes readable payload text and removes balanced outer setup
    ///     wrappers without altering payload content or general text matching.
    /// </summary>
    /// <param name="text">The payload text to normalize for comparison.</param>
    /// <returns>The normalized payload comparison text.</returns>
    private static string NormalizePayloadComparisonText(string text)
    {
        var normalizedText = NormalizeReadableText(text);
        while (normalizedText.Length > 4 &&
               normalizedText.StartsWith("**", StringComparison.Ordinal) &&
               normalizedText.EndsWith("**", StringComparison.Ordinal))
        {
            var unwrappedText = normalizedText[2..^2].Trim();
            if (unwrappedText.Length == 0)
            {
                break;
            }

            normalizedText = unwrappedText;
        }

        return normalizedText;
    }

    /// <summary>
    ///     Normalizes readable native text for popup text-node matching.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The normalized text.</returns>
    public static string NormalizeReadableText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var previousWasSeparator = false;
        foreach (var character in text.Trim())
        {
            if (char.IsWhiteSpace(character) || IsReadableTextNoise(character))
            {
                if (!previousWasSeparator)
                {
                    builder.Append(' ');
                    previousWasSeparator = true;
                }

                continue;
            }

            builder.Append(character);
            previousWasSeparator = false;
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    ///     Determines whether one character should be ignored while matching
    ///     readable popup text because it only represents SeString payload
    ///     noise.
    /// </summary>
    /// <param name="character">The character to inspect.</param>
    /// <returns><c>true</c> when the character should be treated as a separator.</returns>
    private static bool IsReadableTextNoise(char character)
    {
        if (character == '\uFFFD')
        {
            return true;
        }

        var category = char.GetUnicodeCategory(character);
        return category == UnicodeCategory.Control ||
               category == UnicodeCategory.Format ||
               category == UnicodeCategory.PrivateUse ||
               category == UnicodeCategory.Surrogate ||
               category == UnicodeCategory.OtherNotAssigned;
    }

    /// <summary>
    ///     Determines whether the current readable node text should win over
    ///     the structured original text because it is a strict readable
    ///     expansion of the original payload.
    /// </summary>
    /// <param name="currentText">The current live readable node text.</param>
    /// <param name="originalText">The structured original readable text.</param>
    /// <returns>
    ///     <c>true</c> when the current text is a richer readable expansion of
    ///     the original text; otherwise, <c>false</c>.
    /// </returns>
    private static bool ShouldPreferRicherReadableText(
        string currentText,
        string originalText)
    {
        var normalizedCurrentText = NormalizeReadableText(currentText);
        var normalizedOriginalText = NormalizeReadableText(originalText);
        if (string.IsNullOrWhiteSpace(normalizedCurrentText) ||
            string.IsNullOrWhiteSpace(normalizedOriginalText))
        {
            return false;
        }

        if (string.Equals(
                normalizedCurrentText,
                normalizedOriginalText,
                StringComparison.Ordinal))
        {
            return false;
        }

        return normalizedCurrentText.Length > normalizedOriginalText.Length &&
               normalizedCurrentText.Contains(
                   normalizedOriginalText,
                   StringComparison.Ordinal);
    }

    /// <summary>
    ///     Scores how confidently one captured payload represents the expected
    ///     readable source text.
    /// </summary>
    /// <param name="payload">The captured payload bytes.</param>
    /// <param name="expectedText">The expected readable source text.</param>
    /// <returns>
    ///     <c>2</c> for an exact normalized readable-text match, <c>1</c> for
    ///     a loose readable match, and <c>0</c> when the payload does not
    ///     match.
    /// </returns>
    private static int GetPayloadMatchScore(
        byte[]? payload,
        string expectedText)
    {
        if (!TryExtractReadablePayloadText(payload, out var readableText))
        {
            return 0;
        }

        var normalizedReadableText = NormalizePayloadComparisonText(readableText);
        var normalizedExpectedText = NormalizePayloadComparisonText(expectedText);
        if (!string.IsNullOrWhiteSpace(normalizedReadableText) &&
            string.Equals(
                normalizedReadableText,
                normalizedExpectedText,
                StringComparison.Ordinal))
        {
            return 2;
        }

        return PayloadMatches(readableText, expectedText)
            ? 1
            : 0;
    }
}
