// <copyright file="ConcurrentTranslationRequestCache.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;

namespace Echoglossian.Translators.Helpers;

/// <summary>
///     Provides a small per-translator cache that keeps accepted translations in
///     memory and coalesces concurrent in-flight requests for the same source
///     key.
/// </summary>
public sealed class ConcurrentTranslationRequestCache
{
    private readonly ConcurrentDictionary<string, Task<string?>> inFlightTranslations = new();
    private readonly ConcurrentDictionary<string, string> persistedTranslations = new();

    /// <summary>
    ///     Tries to get a completed cached translation for the supplied key.
    /// </summary>
    /// <param name="cacheKey">The normalized translation cache key.</param>
    /// <param name="translatedText">Receives the cached translated text.</param>
    /// <returns>
    ///     <see langword="true" /> when a completed cached translation exists;
    ///     otherwise, <see langword="false" />.
    /// </returns>
    public bool TryGetValue(string cacheKey, out string translatedText)
    {
        return this.persistedTranslations.TryGetValue(cacheKey, out translatedText!);
    }

    /// <summary>
    ///     Stores a completed translated text for the supplied key.
    /// </summary>
    /// <param name="cacheKey">The normalized translation cache key.</param>
    /// <param name="translatedText">The translated text to remember.</param>
    public void Remember(string cacheKey, string translatedText)
    {
        this.persistedTranslations[cacheKey] = translatedText;
    }

    /// <summary>
    ///     Returns a shared in-flight task for the supplied key or creates one
    ///     when no equivalent translation request is currently running.
    /// </summary>
    /// <param name="cacheKey">The normalized translation cache key.</param>
    /// <param name="factory">
    ///     Factory used to create the translation task when no request is
    ///     already in flight for the key.
    /// </param>
    /// <returns>
    ///     The completed translated text for the shared request, or
    ///     <see langword="null" /> when the request produced no translation.
    /// </returns>
    public async Task<string?> GetOrAddAsync(
        string cacheKey,
        Func<Task<string?>> factory)
    {
        var translationTask = this.inFlightTranslations.GetOrAdd(
            cacheKey,
            _ => factory());

        try
        {
            return await translationTask.ConfigureAwait(false);
        }
        finally
        {
            this.inFlightTranslations.TryRemove(
                new KeyValuePair<string, Task<string?>>(cacheKey, translationTask));
        }
    }
}
