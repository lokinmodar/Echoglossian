// <copyright file="TranslationFailureKey.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Security.Cryptography;

namespace Echoglossian.Translators;

/// <summary>
///     Builds stable keys for exact translation-failure tracking.
/// </summary>
internal static class TranslationFailureKey
{
    /// <summary>
    ///     Computes a short stable hash for one exact source text.
    /// </summary>
    /// <param name="sourceText">The exact sanitized source text.</param>
    /// <returns>The stable lowercase hash.</returns>
    public static string ComputeSourceTextHash(string sourceText)
    {
        var inputBytes = Encoding.UTF8.GetBytes(sourceText);
        var hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes, 0, 8).ToLowerInvariant();
    }

    /// <summary>
    ///     Builds the in-memory lookup key for one source/target language pair
    ///     and translation engine.
    /// </summary>
    /// <param name="sourceTextHash">The stable source-text hash.</param>
    /// <param name="sourceLanguage">The source language code.</param>
    /// <param name="targetLanguage">The target language code.</param>
    /// <param name="translationEngine">The translation-engine identifier.</param>
    /// <returns>The stable lookup key.</returns>
    public static string BuildLookupKey(
        string sourceTextHash,
        string? sourceLanguage,
        string? targetLanguage,
        int translationEngine)
    {
        return
            $"{sourceTextHash}|{RuntimeLanguageHelper.NormalizeLanguage(sourceLanguage)}|{RuntimeLanguageHelper.NormalizeLanguage(targetLanguage)}|{translationEngine}";
    }
}
