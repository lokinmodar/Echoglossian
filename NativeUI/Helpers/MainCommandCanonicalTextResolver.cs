// <copyright file="MainCommandCanonicalTextResolver.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Resolves canonical original and translated MainCommand labels from the
///     sheet-backed <see cref="MainCommandText" /> cache.
/// </summary>
public static class MainCommandCanonicalTextResolver
{
    /// <summary>
    ///     Tries to translate one integer-keyed MainCommand payload map from
    ///     canonical cache data.
    /// </summary>
    /// <param name="sourceValues">The original visible values.</param>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="translatedValues">
    ///     Receives the translated values when any entry resolves.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when at least one visible entry resolves to
    ///     a different translated value; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryResolveTranslatedIntMap(
        SortedDictionary<int, string> sourceValues,
        TranslationReuseScope scope,
        string? gameVersion,
        out SortedDictionary<int, string> translatedValues)
    {
        translatedValues = [];
        var changed = false;

        foreach (var (key, originalText) in sourceValues)
        {
            var translatedText = ResolveTranslatedText(
                originalText,
                scope,
                gameVersion);
            if (!string.Equals(
                    translatedText,
                    originalText,
                    StringComparison.Ordinal))
            {
                changed = true;
            }

            translatedValues[key] = translatedText;
        }

        return changed;
    }

    /// <summary>
    ///     Tries to recover one canonical original integer-keyed MainCommand
    ///     payload map from visible translated values.
    /// </summary>
    /// <param name="sourceValues">The currently visible values.</param>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="originalValues">
    ///     Receives the canonical original values when any entry resolves.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when at least one visible entry resolves to
    ///     a different canonical original value; otherwise
    ///     <see langword="false" />.
    /// </returns>
    public static bool TryResolveOriginalIntMap(
        SortedDictionary<int, string> sourceValues,
        TranslationReuseScope scope,
        string? gameVersion,
        out SortedDictionary<int, string> originalValues)
    {
        originalValues = [];
        var changed = false;

        foreach (var (key, visibleText) in sourceValues)
        {
            var originalText = ResolveOriginalText(
                visibleText,
                scope,
                gameVersion);
            if (!string.Equals(
                    originalText,
                    visibleText,
                    StringComparison.Ordinal))
            {
                changed = true;
            }

            originalValues[key] = originalText;
        }

        return changed;
    }

    /// <summary>
    ///     Tries to translate one text-node MainCommand payload map from
    ///     canonical cache data.
    /// </summary>
    /// <param name="sourceValues">The original visible values.</param>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="translatedValues">
    ///     Receives the translated values when any entry resolves.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when at least one visible entry resolves to
    ///     a different translated value; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryResolveTranslatedTextMap(
        SortedDictionary<string, string> sourceValues,
        TranslationReuseScope scope,
        string? gameVersion,
        out SortedDictionary<string, string> translatedValues)
    {
        translatedValues = new SortedDictionary<string, string>(
            StringComparer.Ordinal);
        var changed = false;

        foreach (var (key, originalText) in sourceValues)
        {
            var translatedText = ResolveTranslatedText(
                originalText,
                scope,
                gameVersion);
            if (!string.Equals(
                    translatedText,
                    originalText,
                    StringComparison.Ordinal))
            {
                changed = true;
            }

            translatedValues[key] = translatedText;
        }

        return changed;
    }

    /// <summary>
    ///     Tries to recover one canonical original text-node MainCommand
    ///     payload map from visible translated values.
    /// </summary>
    /// <param name="sourceValues">The currently visible values.</param>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="originalValues">
    ///     Receives the canonical original values when any entry resolves.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when at least one visible entry resolves to
    ///     a different canonical original value; otherwise
    ///     <see langword="false" />.
    /// </returns>
    public static bool TryResolveOriginalTextMap(
        SortedDictionary<string, string> sourceValues,
        TranslationReuseScope scope,
        string? gameVersion,
        out SortedDictionary<string, string> originalValues)
    {
        originalValues = new SortedDictionary<string, string>(
            StringComparer.Ordinal);
        var changed = false;

        foreach (var (key, visibleText) in sourceValues)
        {
            var originalText = ResolveOriginalText(
                visibleText,
                scope,
                gameVersion);
            if (!string.Equals(
                    originalText,
                    visibleText,
                    StringComparison.Ordinal))
            {
                changed = true;
            }

            originalValues[key] = originalText;
        }

        return changed;
    }

    /// <summary>
    ///     Resolves one translated MainCommand label from canonical cache,
    ///     falling back to the original text when no exact translation exists.
    /// </summary>
    /// <param name="originalText">The original visible text.</param>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <returns>The translated text, or the original text.</returns>
    public static string ResolveTranslatedText(
        string originalText,
        TranslationReuseScope scope,
        string? gameVersion)
    {
        return ReferenceTextCacheRegistry.MainCommandTexts.TryFindTranslatedText(
            scope,
            gameVersion,
            originalText,
            out var translatedText)
            ? translatedText
            : originalText;
    }

    /// <summary>
    ///     Resolves one canonical original MainCommand label from canonical
    ///     cache, falling back to the visible text when no exact reverse match
    ///     exists.
    /// </summary>
    /// <param name="visibleText">The currently visible text.</param>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <returns>The canonical original text, or the visible text.</returns>
    public static string ResolveOriginalText(
        string visibleText,
        TranslationReuseScope scope,
        string? gameVersion)
    {
        return ReferenceTextCacheRegistry.MainCommandTexts.TryFindOriginalText(
            scope,
            gameVersion,
            visibleText,
            out var originalText)
            ? originalText
            : visibleText;
    }
}
