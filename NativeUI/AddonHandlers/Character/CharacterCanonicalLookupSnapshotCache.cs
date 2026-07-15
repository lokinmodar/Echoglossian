// <copyright file="CharacterCanonicalLookupSnapshotCache.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Character;

/// <summary>
///     Tracks the identity of one handler-local Character canonical lookup
///     snapshot without coupling it to the cache row types.
/// </summary>
internal sealed class CharacterCanonicalLookupSnapshotCache
{
    private TranslationReuseScope? scope;
    private string? gameVersion;
    private long structuredCacheRevision;
    private long gameWindowCacheRevision;
    private bool hasSnapshot;

    /// <summary>
    ///     Determines whether the cached snapshot remains valid for the
    ///     current translation scope and both canonical data sources.
    /// </summary>
    /// <param name="scope">The complete translation reuse scope.</param>
    /// <param name="gameVersion">The requested game version.</param>
    /// <param name="structuredCacheRevision">
    ///     The current StringArrayData cache revision.
    /// </param>
    /// <param name="gameWindowCacheRevision">
    ///     The current legacy GameWindow cache revision.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when the existing snapshot can be reused;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public bool IsCurrent(
        TranslationReuseScope scope,
        string? gameVersion,
        long structuredCacheRevision,
        long gameWindowCacheRevision)
    {
        return this.hasSnapshot &&
               this.scope is { } cachedScope &&
               cachedScope.Equals(scope) &&
               string.Equals(
                   this.gameVersion,
                   gameVersion,
                   StringComparison.Ordinal) &&
               this.structuredCacheRevision == structuredCacheRevision &&
               this.gameWindowCacheRevision == gameWindowCacheRevision;
    }

    /// <summary>
    ///     Stores the identity of the freshly built lookup snapshot.
    /// </summary>
    /// <param name="scope">The complete translation reuse scope.</param>
    /// <param name="gameVersion">The requested game version.</param>
    /// <param name="structuredCacheRevision">
    ///     The current StringArrayData cache revision.
    /// </param>
    /// <param name="gameWindowCacheRevision">
    ///     The current legacy GameWindow cache revision.
    /// </param>
    public void Store(
        TranslationReuseScope scope,
        string? gameVersion,
        long structuredCacheRevision,
        long gameWindowCacheRevision)
    {
        this.scope = scope;
        this.gameVersion = gameVersion;
        this.structuredCacheRevision = structuredCacheRevision;
        this.gameWindowCacheRevision = gameWindowCacheRevision;
        this.hasSnapshot = true;
    }
}
