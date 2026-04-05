// <copyright file="QueuedTranslationBroker.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Keeps a shared in-memory translation cache and makes translation requests
///     asynchronous so legacy UI event handlers can stop blocking the game thread.
/// </summary>
public sealed class QueuedTranslationBroker
{
    private readonly ConcurrentDictionary<string, string> translationCache = new();
    private readonly ConcurrentDictionary<string, byte> translationInFlight = new();

    /// <summary>
    ///     Returns a cached translation if we already resolved it.
    /// </summary>
    public bool TryGetCached(string key, out string translatedText)
    {
        return this.translationCache.TryGetValue(key, out translatedText!);
    }

    /// <summary>
    ///     Queues a translation request if one is not already in flight for the key.
    /// </summary>
    public bool Queue(string key, Func<Task<string>> resolver, Action<string>? onResolved = null)
    {
        if (!this.translationInFlight.TryAdd(key, 0))
        {
            return false;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var translatedText = await resolver().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(translatedText))
                {
                    this.translationCache[key] = translatedText;
                    onResolved?.Invoke(translatedText);
                }
            }
            catch (Exception ex)
            {
                global::Echoglossian.Echoglossian.PluginLog.Error(
                    $"[QueuedTranslationBroker] Error resolving '{key}': {ex}");
            }
            finally
            {
                this.translationInFlight.TryRemove(key, out _);
            }
        });

        return true;
    }
}
