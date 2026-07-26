// <copyright file="TraitCacheManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.Helpers;

namespace Echoglossian.Cache;

/// <summary>
///     Manages an in-memory cache of canonical <see cref="Trait" /> rows.
/// </summary>
public static class TraitCacheManager
{
    private static readonly Dictionary<uint, List<Trait>> Cache = [];
    private static readonly Dictionary<string, HashSet<string>>
        OriginalTextLookupCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Dictionary<string, string>>
        TextLookupCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Dictionary<string, string>>
        ReverseTextLookupCache = new(StringComparer.Ordinal);
    private static long revision;

    /// <summary>
    ///     Gets the monotonically increasing cache revision.
    /// </summary>
    public static long Revision => Interlocked.Read(ref revision);

    /// <summary>
    ///     Loads all canonical trait rows into memory.
    /// </summary>
    /// <param name="configDir">The plugin configuration directory.</param>
    public static void Preload(string configDir)
    {
        try
        {
            using var context = new EchoglossianDbContext(configDir);
            var allRows = context.Traits
                .AsNoTracking()
                .Where(row => row.TraitId > 0)
                .ToList();

            Cache.Clear();
            foreach (var row in allRows)
            {
                if (!Cache.TryGetValue(row.TraitId, out var rows))
                {
                    rows = [];
                    Cache[row.TraitId] = rows;
                }

                rows.Add(row);
            }

            TextLookupCache.Clear();
            ReverseTextLookupCache.Clear();
            OriginalTextLookupCache.Clear();
            Interlocked.Increment(ref revision);
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(
                "TraitCacheManager",
                $"Failed to preload cache: {ex}");
        }
    }

    /// <summary>
    ///     Updates or inserts one cached trait row.
    /// </summary>
    /// <param name="newRecord">The row to cache.</param>
    public static void Update(Trait newRecord)
    {
        if (newRecord == null || newRecord.TraitId == 0)
        {
            return;
        }

        if (!Cache.TryGetValue(newRecord.TraitId, out var rows))
        {
            rows = [];
            Cache[newRecord.TraitId] = rows;
        }

        var matchingRows = rows
            .Where(row => HasSameCacheIdentity(row, newRecord))
            .ToList();
        var preferredRecord = matchingRows
            .Append(newRecord)
            .OrderByDescending(GetTranslationCompletenessScore)
            .ThenByDescending(static row => row.UpdatedDate)
            .First();

        rows.RemoveAll(row => HasSameCacheIdentity(row, newRecord));
        rows.Add(preferredRecord);
        TextLookupCache.Clear();
        ReverseTextLookupCache.Clear();
        OriginalTextLookupCache.Clear();
        Interlocked.Increment(ref revision);
    }

    /// <summary>
    ///     Tries to find one canonical trait row in memory.
    /// </summary>
    /// <param name="traitId">The trait row identifier.</param>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The game version.</param>
    /// <param name="sourceContentHash">The stable source-content hash.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public static Trait? TryFindCanonicalMatch(
        uint traitId,
        TranslationReuseScope scope,
        string? gameVersion,
        string sourceContentHash)
    {
        if (traitId == 0 ||
            string.IsNullOrWhiteSpace(sourceContentHash))
        {
            return null;
        }

        if (!Cache.TryGetValue(traitId, out var rows) || rows.Count == 0)
        {
            return null;
        }

        return rows
            .Where(row =>
                scope.Matches(
                    row.OriginalLang,
                    row.TranslationLang,
                    row.TranslationEngine) &&
                GameVersionLookupHelper.MatchesStoredVersion(
                    row.GameVersion,
                    gameVersion) &&
                row.SourceContentHash == sourceContentHash)
            .OrderByDescending(row => ComputeCanonicalMatchScore(
                row,
                gameVersion))
            .ThenByDescending(row => row.UpdatedDate)
            .FirstOrDefault();
    }

    /// <summary>
    ///     Tries to find one historical version-specific canonical trait row
    ///     whose source hash still matches the current payload.
    /// </summary>
    /// <param name="traitId">The trait row identifier.</param>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="requestedGameVersion">The current game version.</param>
    /// <param name="sourceContentHash">The stable source-content hash.</param>
    /// <returns>The best matching historical row, or <see langword="null" />.</returns>
    public static Trait? TryFindHistoricalCanonicalMatch(
        uint traitId,
        TranslationReuseScope scope,
        string? requestedGameVersion,
        string sourceContentHash)
    {
        if (traitId == 0 ||
            string.IsNullOrWhiteSpace(requestedGameVersion) ||
            string.IsNullOrWhiteSpace(sourceContentHash))
        {
            return null;
        }

        if (!Cache.TryGetValue(traitId, out var rows) || rows.Count == 0)
        {
            return null;
        }

        return rows
            .Where(row =>
                scope.Matches(
                    row.OriginalLang,
                    row.TranslationLang,
                    row.TranslationEngine) &&
                row.SourceContentHash == sourceContentHash &&
                !string.IsNullOrWhiteSpace(row.GameVersion) &&
                !string.Equals(
                    row.GameVersion,
                    requestedGameVersion,
                    StringComparison.Ordinal))
            .OrderByDescending(static row => HasAnyTranslatedContent(row))
            .ThenByDescending(row => row.UpdatedDate)
            .FirstOrDefault();
    }

    /// <summary>
    ///     Tries to find the best translated trait row by stable identity when
    ///     the stricter canonical hash does not match.
    /// </summary>
    /// <param name="traitId">The trait row identifier.</param>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="classJobId">The preferred class-job identifier.</param>
    /// <param name="classJobCategoryId">
    ///     The preferred class-job-category identifier.
    /// </param>
    /// <returns>The best translated row, or <see langword="null" />.</returns>
    public static Trait? TryFindIdentityMatch(
        uint traitId,
        TranslationReuseScope scope,
        string? gameVersion,
        uint classJobId,
        uint classJobCategoryId)
    {
        if (traitId == 0)
        {
            return null;
        }

        if (!Cache.TryGetValue(traitId, out var rows) || rows.Count == 0)
        {
            return null;
        }

        return rows
            .Where(row =>
                scope.Matches(
                    row.OriginalLang,
                    row.TranslationLang,
                    row.TranslationEngine) &&
                GameVersionLookupHelper.MatchesStoredVersion(
                    row.GameVersion,
                    gameVersion))
            .OrderByDescending(row => ComputeIdentityMatchScore(
                row,
                gameVersion,
                classJobId,
                classJobCategoryId))
            .ThenByDescending(row => row.UpdatedDate)
            .FirstOrDefault();
    }

    /// <summary>
    ///     Tries to resolve one translated trait text by exact original text
    ///     from the canonical trait cache.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="originalText">The original text to translate.</param>
    /// <param name="translatedText">The resolved translated text.</param>
    /// <returns>
    ///     <see langword="true" /> when an exact translated text was found in
    ///     canonical trait storage; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryFindTranslatedText(
        TranslationReuseScope scope,
        string? gameVersion,
        string originalText,
        out string translatedText)
    {
        translatedText = string.Empty;

        if (string.IsNullOrWhiteSpace(originalText))
        {
            return false;
        }

        if (TryFindTranslatedTextInScope(
                scope,
                gameVersion,
                originalText,
                out translatedText))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            return TryFindTranslatedTextInScope(
                scope,
                version: null,
                originalText,
                out translatedText);
        }

        return false;
    }

    /// <summary>
    ///     Tries to resolve one canonical original trait text by exact
    ///     translated text from the cached trait rows.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="translatedText">The translated text to reverse.</param>
    /// <param name="originalText">The resolved canonical original text.</param>
    /// <returns>
    ///     <see langword="true" /> when one exact canonical original text was
    ///     found; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryFindOriginalText(
        TranslationReuseScope scope,
        string? gameVersion,
        string translatedText,
        out string originalText)
    {
        originalText = string.Empty;

        if (string.IsNullOrWhiteSpace(translatedText))
        {
            return false;
        }

        if (TryFindOriginalTextInScope(
                scope,
                gameVersion,
                translatedText,
                out originalText))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            return TryFindOriginalTextInScope(
                scope,
                version: null,
                translatedText,
                out originalText);
        }

        return false;
    }

    /// <summary>
    ///     Determines whether one canonical original trait text already exists
    ///     in the cached trait rows for the requested scope.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="originalText">The canonical original text to test.</param>
    /// <returns>
    ///     <see langword="true" /> when the original text already exists in
    ///     canonical trait storage; otherwise <see langword="false" />.
    /// </returns>
    public static bool ContainsOriginalText(
        TranslationReuseScope scope,
        string? gameVersion,
        string originalText)
    {
        if (string.IsNullOrWhiteSpace(originalText))
        {
            return false;
        }

        if (TryContainsOriginalTextInScope(
                scope,
                gameVersion,
                originalText))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            return TryContainsOriginalTextInScope(
                scope,
                version: null,
                originalText);
        }

        return false;
    }

    /// <summary>
    ///     Gets one scope-aware exact-text lookup snapshot for canonical
    ///     ActionMenu reuse.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <returns>The canonical lookup snapshot.</returns>
    internal static CanonicalTextLookupSnapshot GetTextLookupSnapshot(
        TranslationReuseScope scope,
        string? gameVersion)
    {
        var preferredSnapshot = GetExactTextLookupSnapshot(
            scope,
            gameVersion);
        return string.IsNullOrWhiteSpace(gameVersion)
            ? preferredSnapshot
            : CanonicalTextLookupSnapshot.Combine(
                preferredSnapshot,
                GetExactTextLookupSnapshot(
                    scope,
                    version: null));
    }

    /// <summary>
    ///     Clears the in-memory cache.
    /// </summary>
    public static void Clear()
    {
        Cache.Clear();
        OriginalTextLookupCache.Clear();
        TextLookupCache.Clear();
        ReverseTextLookupCache.Clear();
        Interlocked.Increment(ref revision);
        PluginRuntimeLog.Debug("TraitCacheManager", "Cleared trait cache.");
    }

    /// <summary>
    ///     Tries to resolve one translated trait text from one cached scope.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="version">The exact stored game version scope.</param>
    /// <param name="originalText">The original text to translate.</param>
    /// <param name="translatedText">The translated text.</param>
    /// <returns>
    ///     <see langword="true" /> when a translated text was found in this
    ///     exact scope; otherwise <see langword="false" />.
    /// </returns>
    private static bool TryFindTranslatedTextInScope(
        TranslationReuseScope scope,
        string? version,
        string originalText,
        out string translatedText)
    {
        translatedText = string.Empty;

        var scopeKey = BuildTextLookupScopeKey(scope, version);
        if (!TextLookupCache.TryGetValue(scopeKey, out var lookup))
        {
            lookup = BuildTextLookup(scope, version);
            TextLookupCache[scopeKey] = lookup;
        }

        var found = lookup.TryGetValue(originalText, out var resolvedText);
        translatedText = resolvedText ?? string.Empty;
        return found;
    }

    /// <summary>
    ///     Tries to resolve one canonical original trait text from one cached
    ///     reverse lookup scope.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="version">The exact stored game version scope.</param>
    /// <param name="translatedText">The translated text to reverse.</param>
    /// <param name="originalText">The resolved original text.</param>
    /// <returns>
    ///     <see langword="true" /> when a canonical original text was found in
    ///     this exact scope; otherwise <see langword="false" />.
    /// </returns>
    private static bool TryFindOriginalTextInScope(
        TranslationReuseScope scope,
        string? version,
        string translatedText,
        out string originalText)
    {
        originalText = string.Empty;

        var scopeKey = BuildTextLookupScopeKey(scope, version);
        if (!ReverseTextLookupCache.TryGetValue(scopeKey, out var lookup))
        {
            lookup = BuildReverseTextLookup(scope, version);
            ReverseTextLookupCache[scopeKey] = lookup;
        }

        var found = lookup.TryGetValue(translatedText, out var resolvedText);
        originalText = resolvedText ?? string.Empty;
        return found;
    }

    /// <summary>
    ///     Determines whether one exact canonical original trait text exists
    ///     inside one cached trait scope.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="version">The exact stored game version scope.</param>
    /// <param name="originalText">The canonical original text to test.</param>
    /// <returns>
    ///     <see langword="true" /> when the original text exists in the
    ///     requested scope; otherwise <see langword="false" />.
    /// </returns>
    private static bool TryContainsOriginalTextInScope(
        TranslationReuseScope scope,
        string? version,
        string originalText)
    {
        var scopeKey = BuildTextLookupScopeKey(scope, version);
        if (!OriginalTextLookupCache.TryGetValue(scopeKey, out var lookup))
        {
            lookup = BuildOriginalTextLookup(scope, version);
            OriginalTextLookupCache[scopeKey] = lookup;
        }

        return lookup.Contains(originalText);
    }

    /// <summary>
    ///     Gets one exact-version canonical lookup snapshot.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="version">The exact stored game-version scope.</param>
    /// <returns>The exact-version lookup snapshot.</returns>
    private static CanonicalTextLookupSnapshot GetExactTextLookupSnapshot(
        TranslationReuseScope scope,
        string? version)
    {
        var scopeKey = BuildTextLookupScopeKey(scope, version);
        if (!TextLookupCache.TryGetValue(scopeKey, out var forwardLookup))
        {
            forwardLookup = BuildTextLookup(scope, version);
            TextLookupCache[scopeKey] = forwardLookup;
        }

        if (!ReverseTextLookupCache.TryGetValue(scopeKey, out var reverseLookup))
        {
            reverseLookup = BuildReverseTextLookup(scope, version);
            ReverseTextLookupCache[scopeKey] = reverseLookup;
        }

        if (!OriginalTextLookupCache.TryGetValue(scopeKey, out var originalLookup))
        {
            originalLookup = BuildOriginalTextLookup(scope, version);
            OriginalTextLookupCache[scopeKey] = originalLookup;
        }

        return new CanonicalTextLookupSnapshot(
            originalLookup,
            forwardLookup,
            reverseLookup);
    }

    /// <summary>
    ///     Builds one exact original-text lookup for a single trait cache
    ///     scope.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="version">The exact stored game-version scope.</param>
    /// <returns>The original-text lookup set.</returns>
    private static HashSet<string> BuildOriginalTextLookup(
        TranslationReuseScope scope,
        string? version)
    {
        var lookup = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in Cache.Values.SelectMany(static rows => rows))
        {
            if (!scope.Matches(
                    row.OriginalLang,
                    row.TranslationLang,
                    row.TranslationEngine) ||
                !string.Equals(
                    row.GameVersion,
                    version,
                    StringComparison.Ordinal))
            {
                continue;
            }

            TryAddOriginalText(lookup, row.TraitName);
            TryAddOriginalText(lookup, row.TraitDescription);
            TryAddOriginalText(lookup, row.OriginalTooltipText);
        }

        return lookup;
    }

    /// <summary>
    ///     Builds one translated-text lookup for a single trait cache scope.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="version">The exact stored game version scope.</param>
    /// <returns>The translated-text lookup map.</returns>
    private static Dictionary<string, string> BuildTextLookup(
        TranslationReuseScope scope,
        string? version)
    {
        var lookup = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var row in Cache.Values.SelectMany(static rows => rows))
        {
            if (!scope.Matches(
                    row.OriginalLang,
                    row.TranslationLang,
                    row.TranslationEngine) ||
                !string.Equals(
                    row.GameVersion,
                    version,
                    StringComparison.Ordinal))
            {
                continue;
            }

            TryAddLookupValue(
                lookup,
                row.TraitName,
                row.TranslatedTraitName);
            TryAddLookupValue(
                lookup,
                row.TraitDescription,
                row.TranslatedTraitDescription);
            TryAddLookupValue(
                lookup,
                row.OriginalTooltipText,
                row.TranslatedTooltipText);
        }

        return lookup;
    }

    /// <summary>
    ///     Builds one reverse translated-text lookup for a single trait cache scope.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="version">The exact stored game version scope.</param>
    /// <returns>The reverse translated-text lookup map.</returns>
    private static Dictionary<string, string> BuildReverseTextLookup(
        TranslationReuseScope scope,
        string? version)
    {
        var lookup = new Dictionary<string, string>(StringComparer.Ordinal);
        var ambiguousKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in Cache.Values.SelectMany(static rows => rows))
        {
            if (!scope.Matches(
                    row.OriginalLang,
                    row.TranslationLang,
                    row.TranslationEngine) ||
                !string.Equals(
                    row.GameVersion,
                    version,
                    StringComparison.Ordinal))
            {
                continue;
            }

            TryAddReverseLookupValue(
                lookup,
                ambiguousKeys,
                row.TraitName,
                row.TranslatedTraitName);
            TryAddReverseLookupValue(
                lookup,
                ambiguousKeys,
                row.TraitDescription,
                row.TranslatedTraitDescription);
            TryAddReverseLookupValue(
                lookup,
                ambiguousKeys,
                row.OriginalTooltipText,
                row.TranslatedTooltipText);
        }

        return lookup;
    }

    /// <summary>
    ///     Adds one translated-text lookup entry when both texts are usable.
    /// </summary>
    /// <param name="lookup">The lookup map to update.</param>
    /// <param name="originalText">The original text.</param>
    /// <param name="translatedText">The translated text.</param>
    private static void TryAddLookupValue(
        IDictionary<string, string> lookup,
        string? originalText,
        string? translatedText)
    {
        if (string.IsNullOrWhiteSpace(originalText) ||
            string.IsNullOrWhiteSpace(translatedText) ||
            string.Equals(
                originalText,
                translatedText,
                StringComparison.Ordinal))
        {
            return;
        }

        lookup.TryAdd(originalText, translatedText);
    }

    /// <summary>
    ///     Adds one reverse translated-text lookup entry when the translated
    ///     text maps uniquely back to a single canonical original text.
    /// </summary>
    /// <param name="lookup">The reverse lookup map to update.</param>
    /// <param name="ambiguousKeys">
    ///     Tracks translated texts that map to multiple originals and must be
    ///     excluded from reverse recovery.
    /// </param>
    /// <param name="originalText">The canonical original text.</param>
    /// <param name="translatedText">The translated text.</param>
    private static void TryAddReverseLookupValue(
        IDictionary<string, string> lookup,
        ISet<string> ambiguousKeys,
        string? originalText,
        string? translatedText)
    {
        if (string.IsNullOrWhiteSpace(originalText) ||
            string.IsNullOrWhiteSpace(translatedText) ||
            string.Equals(
                originalText,
                translatedText,
                StringComparison.Ordinal) ||
            ambiguousKeys.Contains(translatedText))
        {
            return;
        }

        if (lookup.TryGetValue(translatedText, out var existingOriginal))
        {
            if (!string.Equals(
                    existingOriginal,
                    originalText,
                    StringComparison.Ordinal))
            {
                lookup.Remove(translatedText);
                ambiguousKeys.Add(translatedText);
            }

            return;
        }

        lookup[translatedText] = originalText;
    }

    /// <summary>
    ///     Adds one canonical original text to the scope-local lookup when the
    ///     value is populated.
    /// </summary>
    /// <param name="lookup">The original-text lookup to update.</param>
    /// <param name="originalText">The original text to add.</param>
    private static void TryAddOriginalText(
        ISet<string> lookup,
        string? originalText)
    {
        if (!string.IsNullOrWhiteSpace(originalText))
        {
            lookup.Add(originalText);
        }
    }

    /// <summary>
    ///     Determines whether two trait rows represent the same effective
    ///     cache identity after source and target language normalization.
    /// </summary>
    /// <param name="left">The cached row.</param>
    /// <param name="right">The incoming row.</param>
    /// <returns>
    ///     <see langword="true" /> when both rows share one cache identity;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private static bool HasSameCacheIdentity(Trait left, Trait right)
    {
        return left.TraitId == right.TraitId &&
               RuntimeLanguageHelper.LanguagesMatch(
                   left.OriginalLang,
                   right.OriginalLang) &&
               RuntimeLanguageHelper.LanguagesMatch(
                   left.TranslationLang,
                   right.TranslationLang) &&
               left.TranslationEngine == right.TranslationEngine &&
               GameVersionLookupHelper.MatchesStoredVersion(
                   left.GameVersion,
                   right.GameVersion) &&
               left.SourceContentHash == right.SourceContentHash;
    }

    /// <summary>
    ///     Computes how many translated trait fields are populated so an
    ///     incomplete alias row cannot displace a completed canonical row.
    /// </summary>
    /// <param name="row">The trait row to score.</param>
    /// <returns>The number of populated translated fields.</returns>
    private static int GetTranslationCompletenessScore(Trait row)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(row.TranslatedTraitName))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(row.TranslatedTraitDescription))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(row.TranslatedTooltipText))
        {
            score++;
        }

        return score;
    }

    /// <summary>
    ///     Computes one ordering score for tolerant identity-based trait
    ///     lookup.
    /// </summary>
    /// <param name="row">The candidate row.</param>
    /// <param name="gameVersion">The requested game version.</param>
    /// <param name="classJobId">The preferred class-job identifier.</param>
    /// <param name="classJobCategoryId">
    ///     The preferred class-job-category identifier.
    /// </param>
    /// <returns>The ordering score.</returns>
    private static int ComputeIdentityMatchScore(
        Trait row,
        string? gameVersion,
        uint classJobId,
        uint classJobCategoryId)
    {
        var score = 0;

        if (HasCompleteTranslation(row))
        {
            score += 10_000;
        }

        if (!string.IsNullOrWhiteSpace(gameVersion) &&
            string.Equals(
                row.GameVersion,
                gameVersion,
                StringComparison.Ordinal))
        {
            score += 1_000;
        }

        if (row.GameVersion == null)
        {
            score += 100;
        }

        if (classJobId != 0 && row.ClassJobId == classJobId)
        {
            score += 50;
        }

        if (classJobCategoryId != 0 &&
            row.ClassJobCategoryId == classJobCategoryId)
        {
            score += 25;
        }

        return score;
    }

    /// <summary>
    ///     Computes one ordering score for exact source-hash matches so fully
    ///     translated reusable rows beat current-version placeholders.
    /// </summary>
    /// <param name="row">The candidate row.</param>
    /// <param name="gameVersion">The requested game version.</param>
    /// <returns>The ordering score.</returns>
    private static int ComputeCanonicalMatchScore(
        Trait row,
        string? gameVersion)
    {
        var score = 0;

        if (HasCompleteTranslation(row))
        {
            score += 10_000;
        }
        else if (HasAnyTranslatedContent(row))
        {
            score += 1_000;
        }

        if (!string.IsNullOrWhiteSpace(gameVersion) &&
            string.Equals(
                row.GameVersion,
                gameVersion,
                StringComparison.Ordinal))
        {
            score += 100;
        }

        if (string.IsNullOrWhiteSpace(row.GameVersion))
        {
            score += 10;
        }

        return score;
    }

    /// <summary>
    ///     Gets whether the row carries any translated canonical payload that
    ///     can be promoted to a newer game version.
    /// </summary>
    /// <param name="row">The candidate row.</param>
    /// <returns>True when the row contains translated content.</returns>
    private static bool HasAnyTranslatedContent(Trait row)
    {
        return !string.IsNullOrWhiteSpace(row.TranslatedTraitName) ||
               !string.IsNullOrWhiteSpace(row.TranslatedTraitDescription) ||
               !string.IsNullOrWhiteSpace(row.TranslatedTooltipText);
    }

    /// <summary>
    ///     Gets whether one trait row contains every translated field required
    ///     by the live tooltip runtime.
    /// </summary>
    /// <param name="row">The candidate row.</param>
    /// <returns>
    ///     <see langword="true" /> when the row contains a translated name and
    ///     any required translated description; otherwise
    ///     <see langword="false" />.
    /// </returns>
    private static bool HasCompleteTranslation(Trait row)
    {
        return StructuredTooltipTranslationValidation
            .HasCompleteMeaningfulTranslation(
                row.TraitName,
                row.TraitDescription,
                row.TranslatedTraitName,
                row.TranslatedTraitDescription);
    }

    /// <summary>
    ///     Builds one stable scope key for translated-text lookups.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="version">The exact stored game version scope.</param>
    /// <returns>The stable scope key.</returns>
    private static string BuildTextLookupScopeKey(
        TranslationReuseScope scope,
        string? version)
    {
        var source = RuntimeLanguageHelper.NormalizeLanguage(
            scope.SourceLanguageCode);
        var target = RuntimeLanguageHelper.NormalizeLanguage(
            scope.TargetLanguageCode);
        var engine = scope.RequireMatchingEngine
            ? scope.TranslationEngine?.ToString() ?? string.Empty
            : "*";
        return $"{source}|{target}|{engine}|{version ?? string.Empty}";
    }

}
