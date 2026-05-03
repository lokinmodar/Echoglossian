// <copyright file="QuestHoverTranslationCache.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;

namespace Echoglossian.Cache;

/// <summary>
/// Stores quest-family hover translations keyed by the node pointer used to anchor the tooltip.
/// </summary>
public static class QuestHoverTranslationCache
{
    private static readonly ConcurrentDictionary<nint, QuestHoverTranslationSnapshot> Cache = new();

    /// <summary>
    /// Attempts to get a cached hover translation snapshot for the given key.
    /// </summary>
    /// <param name="key">The stable node key.</param>
    /// <param name="snapshot">The cached snapshot if one exists.</param>
    /// <returns>True when a cached snapshot is available.</returns>
    public static bool TryGet(nint key, out QuestHoverTranslationSnapshot snapshot)
    {
        return Cache.TryGetValue(key, out snapshot!);
    }

    /// <summary>
    /// Stores or replaces a hover translation snapshot for the given key.
    /// </summary>
    /// <param name="key">The stable node key.</param>
    /// <param name="originalText">The original visible text.</param>
    /// <param name="translatedText">The translated visible text.</param>
    public static void Remember(nint key, string originalText, string translatedText)
    {
        Cache[key] = new QuestHoverTranslationSnapshot(originalText, translatedText);
    }

    /// <summary>
    /// Clears all cached hover translations.
    /// </summary>
    public static void Clear()
    {
        Cache.Clear();
        PluginRuntimeLog.Debug("[QuestHoverTranslationCache] Cleared quest hover translation cache.");
    }
}

/// <summary>
/// Represents a quest hover translation snapshot.
/// </summary>
/// <param name="OriginalText">The original visible text.</param>
/// <param name="TranslatedText">The translated visible text.</param>
public sealed record QuestHoverTranslationSnapshot(
    string OriginalText,
    string TranslatedText);


