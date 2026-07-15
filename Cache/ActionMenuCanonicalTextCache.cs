// <copyright file="ActionMenuCanonicalTextCache.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;

namespace Echoglossian.Cache;

/// <summary>
///     Aggregates all action-adjacent canonical text stores into one
///     ActionMenu-friendly exact lookup snapshot per reuse scope and version.
/// </summary>
internal static class ActionMenuCanonicalTextCache
{
    private static readonly ConcurrentDictionary<string, CanonicalTextLookupSnapshot>
        Cache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Gets one aggregated canonical ActionMenu lookup snapshot for the
    ///     requested reuse scope and game version.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <returns>The aggregated canonical lookup snapshot.</returns>
    public static CanonicalTextLookupSnapshot GetSnapshot(
        TranslationReuseScope scope,
        string? gameVersion)
    {
        if (Cache.Count > 128)
        {
            Cache.Clear();
        }

        var cacheKey = BuildCacheKey(
            scope,
            gameVersion,
            ActionTooltipCacheManager.Revision,
            TraitCacheManager.Revision,
            ReferenceTextCacheRegistry.Revision);
        return Cache.GetOrAdd(
            cacheKey,
            static (_, state) => BuildSnapshot(
                state.Scope,
                state.GameVersion),
            (Scope: scope, GameVersion: gameVersion));
    }

    /// <summary>
    ///     Builds one aggregated canonical lookup snapshot.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <returns>The aggregated lookup snapshot.</returns>
    private static CanonicalTextLookupSnapshot BuildSnapshot(
        TranslationReuseScope scope,
        string? gameVersion)
    {
        return CanonicalTextLookupSnapshot.Combine(
            ActionTooltipCacheManager.GetTextLookupSnapshot(
                scope,
                gameVersion),
            TraitCacheManager.GetTextLookupSnapshot(
                scope,
                gameVersion),
            ReferenceTextCacheRegistry.GetTextLookupSnapshot(
                scope,
                gameVersion));
    }

    /// <summary>
    ///     Builds one stable aggregated-cache key from the requested scope and
    ///     the current canonical-cache revisions.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="actionRevision">The action-tooltip cache revision.</param>
    /// <param name="traitRevision">The trait cache revision.</param>
    /// <param name="referenceRevision">The reference-text cache revision.</param>
    /// <returns>The stable cache key payload.</returns>
    private static string BuildCacheKey(
        TranslationReuseScope scope,
        string? gameVersion,
        long actionRevision,
        long traitRevision,
        int referenceRevision)
    {
        var source = RuntimeLanguageHelper.NormalizeLanguage(
            scope.SourceLanguageCode);
        var target = RuntimeLanguageHelper.NormalizeLanguage(
            scope.TargetLanguageCode);
        var engine = scope.RequireMatchingEngine
            ? scope.TranslationEngine?.ToString() ?? string.Empty
            : "*";
        return $"{source}\u001F{target}\u001F{engine}\u001F{gameVersion ?? string.Empty}\u001F{actionRevision}\u001F{traitRevision}\u001F{referenceRevision}";
    }
}
