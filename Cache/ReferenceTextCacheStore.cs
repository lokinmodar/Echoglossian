// <copyright file="ReferenceTextCacheStore.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian.Cache;

/// <summary>
///     Manages one in-memory cache of canonical reference-text rows.
/// </summary>
/// <typeparam name="TRow">The concrete row type.</typeparam>
public sealed class ReferenceTextCacheStore<TRow>
    where TRow : ReferenceTextRowBase
{
    private readonly Dictionary<uint, List<TRow>> cache = [];
    private readonly string cacheName;
    private readonly Dictionary<string, Dictionary<string, string>>
        forwardTextLookupCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>>
        reverseTextLookupCache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="ReferenceTextCacheStore{TRow}" /> class.
    /// </summary>
    /// <param name="cacheName">The diagnostic cache name.</param>
    public ReferenceTextCacheStore(string cacheName)
    {
        this.cacheName = cacheName;
    }

    /// <summary>
    ///     Loads all canonical reference-text rows into memory.
    /// </summary>
    /// <param name="configDir">The plugin configuration directory.</param>
    /// <param name="setSelector">Selects the matching DbSet.</param>
    public void Preload(
        string configDir,
        Func<EchoglossianDbContext, DbSet<TRow>> setSelector)
    {
        try
        {
            using var context = new EchoglossianDbContext(configDir);
            var allRows = setSelector(context)
                .AsNoTracking()
                .Where(row => row.ReferenceId > 0)
                .ToList();

            this.cache.Clear();
            foreach (var row in allRows)
            {
                if (!this.cache.TryGetValue(row.ReferenceId, out var rows))
                {
                    rows = [];
                    this.cache[row.ReferenceId] = rows;
                }

                rows.Add(row);
            }

            this.forwardTextLookupCache.Clear();
            this.reverseTextLookupCache.Clear();
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(
                this.cacheName,
                $"Failed to preload cache: {ex}");
        }
    }

    /// <summary>
    ///     Updates or inserts one cached reference-text row.
    /// </summary>
    /// <param name="newRecord">The row to cache.</param>
    public void Update(TRow newRecord)
    {
        if (newRecord == null || newRecord.ReferenceId == 0)
        {
            return;
        }

        if (!this.cache.TryGetValue(newRecord.ReferenceId, out var rows))
        {
            rows = [];
            this.cache[newRecord.ReferenceId] = rows;
        }

        var existing = rows.FirstOrDefault(row =>
            row.ReferenceId == newRecord.ReferenceId &&
            RuntimeLanguageHelper.LanguagesMatch(
                row.OriginalLang,
                newRecord.OriginalLang) &&
            RuntimeLanguageHelper.LanguagesMatch(
                row.TranslationLang,
                newRecord.TranslationLang) &&
            row.TranslationEngine == newRecord.TranslationEngine &&
            GameVersionLookupHelper.MatchesStoredVersion(
                row.GameVersion,
                newRecord.GameVersion) &&
            row.SourceContentHash == newRecord.SourceContentHash);
        if (existing != null)
        {
            rows.Remove(existing);
        }

        rows.Add(newRecord);
        this.forwardTextLookupCache.Clear();
        this.reverseTextLookupCache.Clear();
    }

    /// <summary>
    ///     Tries to find one canonical row in memory.
    /// </summary>
    /// <param name="referenceId">The sheet-row identifier.</param>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="sourceContentHash">The stable source-content hash.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public TRow? TryFindCanonicalMatch(
        uint referenceId,
        TranslationReuseScope scope,
        string? gameVersion,
        string sourceContentHash)
    {
        if (referenceId == 0 ||
            string.IsNullOrWhiteSpace(sourceContentHash))
        {
            return null;
        }

        if (!this.cache.TryGetValue(referenceId, out var rows) ||
            rows.Count == 0)
        {
            return null;
        }

        return rows.FirstOrDefault(row =>
            scope.Matches(
                row.OriginalLang,
                row.TranslationLang,
                row.TranslationEngine) &&
            GameVersionLookupHelper.MatchesStoredVersion(
            row.GameVersion,
            gameVersion) &&
            row.SourceContentHash == sourceContentHash);
    }

    /// <summary>
    ///     Tries to find the best translated reference-text row by stable
    ///     identity when the stricter canonical hash does not match.
    /// </summary>
    /// <param name="referenceId">The sheet-row identifier.</param>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <returns>The best translated row, or <see langword="null" />.</returns>
    public TRow? TryFindIdentityMatch(
        uint referenceId,
        TranslationReuseScope scope,
        string? gameVersion)
    {
        if (referenceId == 0)
        {
            return null;
        }

        if (!this.cache.TryGetValue(referenceId, out var rows) ||
            rows.Count == 0)
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
                HasCompleteTranslation(row))
            .OrderByDescending(row => ComputeIdentityMatchScore(
                row,
                gameVersion))
            .ThenByDescending(row => row.UpdatedDate)
            .FirstOrDefault();
    }

    /// <summary>
    ///     Tries to resolve one exact translated text from this cache scope.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="originalText">The original text to translate.</param>
    /// <param name="translatedText">The resolved translated text.</param>
    /// <returns>
    ///     <see langword="true" /> when one translated text was found;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public bool TryFindTranslatedText(
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

        if (this.TryFindTranslatedTextInScope(
                scope,
                gameVersion,
                originalText,
                out translatedText))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            return this.TryFindTranslatedTextInScope(
                scope,
                version: null,
                originalText,
                out translatedText);
        }

        return false;
    }

    /// <summary>
    ///     Tries to resolve one exact canonical original text from this cache
    ///     scope.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="translatedText">The translated text to reverse.</param>
    /// <param name="originalText">The resolved canonical original text.</param>
    /// <returns>
    ///     <see langword="true" /> when one original text was found;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public bool TryFindOriginalText(
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

        if (this.TryFindOriginalTextInScope(
                scope,
                gameVersion,
                translatedText,
                out originalText))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            return this.TryFindOriginalTextInScope(
                scope,
                version: null,
                translatedText,
                out originalText);
        }

        return false;
    }

    /// <summary>
    ///     Determines whether one canonical original text already exists in
    ///     this cache for the requested scope.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="originalText">The canonical original text to test.</param>
    /// <returns>
    ///     <see langword="true" /> when the original text already exists;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public bool ContainsOriginalText(
        TranslationReuseScope scope,
        string? gameVersion,
        string originalText)
    {
        if (string.IsNullOrWhiteSpace(originalText))
        {
            return false;
        }

        if (this.ContainsOriginalTextInScope(
                scope,
                gameVersion,
                originalText))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            return this.ContainsOriginalTextInScope(
                scope,
                version: null,
                originalText);
        }

        return false;
    }

    /// <summary>
    ///     Provides the legacy canonical lookup shape until callers migrate.
    /// </summary>
    /// <param name="referenceId">The sheet-row identifier.</param>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The game version.</param>
    /// <param name="sourceContentHash">The source-content hash.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public TRow? TryFindCanonicalMatch(
        uint referenceId,
        string lang,
        int engine,
        string? gameVersion,
        string sourceContentHash)
    {
        return TryCreateLegacyScope(lang, engine, out var scope)
            ? this.TryFindCanonicalMatch(
                referenceId,
                scope,
                gameVersion,
                sourceContentHash)
            : null;
    }

    /// <summary>
    ///     Provides the legacy identity lookup shape until callers migrate.
    /// </summary>
    /// <param name="referenceId">The sheet-row identifier.</param>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The game version.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public TRow? TryFindIdentityMatch(
        uint referenceId,
        string lang,
        int engine,
        string? gameVersion)
    {
        return TryCreateLegacyScope(lang, engine, out var scope)
            ? this.TryFindIdentityMatch(referenceId, scope, gameVersion)
            : null;
    }

    /// <summary>
    ///     Provides the source-explicit identity lookup shape until callers
    ///     migrate to the shared scope.
    /// </summary>
    /// <param name="referenceId">The sheet-row identifier.</param>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The game version.</param>
    /// <param name="originalLanguage">The persisted source language.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public TRow? TryFindIdentityMatch(
        uint referenceId,
        string lang,
        int engine,
        string? gameVersion,
        string originalLanguage)
    {
        var scope = new TranslationReuseScope(
            originalLanguage,
            lang,
            engine,
            true);
        return this.TryFindIdentityMatch(referenceId, scope, gameVersion);
    }

    /// <summary>
    ///     Provides the legacy forward text lookup shape until callers migrate.
    /// </summary>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The game version.</param>
    /// <param name="originalText">The original text.</param>
    /// <param name="translatedText">The translated text.</param>
    /// <returns>Whether a translation was found.</returns>
    public bool TryFindTranslatedText(
        string lang,
        int engine,
        string? gameVersion,
        string originalText,
        out string translatedText)
    {
        translatedText = string.Empty;
        return TryCreateLegacyScope(lang, engine, out var scope) &&
               this.TryFindTranslatedText(
                   scope,
                   gameVersion,
                   originalText,
                   out translatedText);
    }

    /// <summary>
    ///     Provides the source-explicit forward lookup shape until callers
    ///     migrate to the shared scope.
    /// </summary>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The game version.</param>
    /// <param name="originalText">The original text.</param>
    /// <param name="originalLanguage">The persisted source language.</param>
    /// <param name="translatedText">The translated text.</param>
    /// <returns>Whether a translation was found.</returns>
    public bool TryFindTranslatedText(
        string lang,
        int engine,
        string? gameVersion,
        string originalText,
        string originalLanguage,
        out string translatedText)
    {
        var scope = new TranslationReuseScope(
            originalLanguage,
            lang,
            engine,
            true);
        return this.TryFindTranslatedText(
            scope,
            gameVersion,
            originalText,
            out translatedText);
    }

    /// <summary>
    ///     Provides the legacy reverse text lookup shape until callers migrate.
    /// </summary>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The game version.</param>
    /// <param name="translatedText">The translated text.</param>
    /// <param name="originalText">The original text.</param>
    /// <returns>Whether an original text was found.</returns>
    public bool TryFindOriginalText(
        string lang,
        int engine,
        string? gameVersion,
        string translatedText,
        out string originalText)
    {
        originalText = string.Empty;
        return TryCreateLegacyScope(lang, engine, out var scope) &&
               this.TryFindOriginalText(
                   scope,
                   gameVersion,
                   translatedText,
                   out originalText);
    }

    /// <summary>
    ///     Provides the legacy original-text check until callers migrate.
    /// </summary>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The game version.</param>
    /// <param name="originalText">The original text.</param>
    /// <returns>Whether the original text exists.</returns>
    public bool ContainsOriginalText(
        string lang,
        int engine,
        string? gameVersion,
        string originalText)
    {
        return TryCreateLegacyScope(lang, engine, out var scope) &&
               this.ContainsOriginalText(scope, gameVersion, originalText);
    }

    /// <summary>
    ///     Clears the in-memory cache.
    /// </summary>
    public void Clear()
    {
        this.cache.Clear();
        this.forwardTextLookupCache.Clear();
        this.reverseTextLookupCache.Clear();
        PluginRuntimeLog.Debug(
            this.cacheName,
            "Cleared reference-text cache.");
    }

    /// <summary>
    ///     Tries to resolve one translated text from one cached scope.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="version">The exact stored game-version scope.</param>
    /// <param name="originalText">The original text to translate.</param>
    /// <param name="translatedText">The translated text.</param>
    /// <returns>
    ///     <see langword="true" /> when one translated text was found;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private bool TryFindTranslatedTextInScope(
        TranslationReuseScope scope,
        string? version,
        string originalText,
        out string translatedText)
    {
        translatedText = string.Empty;

        var scopeKey = BuildScopeKey(scope, version);
        if (!this.forwardTextLookupCache.TryGetValue(scopeKey, out var lookup))
        {
            lookup = this.BuildForwardLookup(
                scope,
                version);
            this.forwardTextLookupCache[scopeKey] = lookup;
        }

        return lookup.TryGetValue(originalText, out translatedText);
    }

    /// <summary>
    ///     Tries to resolve one original text from one cached scope.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="version">The exact stored game-version scope.</param>
    /// <param name="translatedText">The translated text to reverse.</param>
    /// <param name="originalText">The resolved original text.</param>
    /// <returns>
    ///     <see langword="true" /> when one original text was found;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private bool TryFindOriginalTextInScope(
        TranslationReuseScope scope,
        string? version,
        string translatedText,
        out string originalText)
    {
        originalText = string.Empty;

        var scopeKey = BuildScopeKey(scope, version);
        if (!this.reverseTextLookupCache.TryGetValue(scopeKey, out var lookup))
        {
            lookup = this.BuildReverseLookup(scope, version);
            this.reverseTextLookupCache[scopeKey] = lookup;
        }

        return lookup.TryGetValue(translatedText, out originalText);
    }

    /// <summary>
    ///     Determines whether one exact canonical original text exists in one
    ///     cache scope.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="version">The exact stored game-version scope.</param>
    /// <param name="originalText">The canonical original text to test.</param>
    /// <returns>
    ///     <see langword="true" /> when the original text exists in the
    ///     requested scope; otherwise <see langword="false" />.
    /// </returns>
    private bool ContainsOriginalTextInScope(
        TranslationReuseScope scope,
        string? version,
        string originalText)
    {
        return this.cache.Values.SelectMany(static rows => rows)
            .Where(row =>
                scope.Matches(
                    row.OriginalLang,
                    row.TranslationLang,
                    row.TranslationEngine) &&
                string.Equals(
                    row.GameVersion,
                    version,
                    StringComparison.Ordinal))
            .Any(row =>
                string.Equals(
                    row.OriginalName,
                    originalText,
                    StringComparison.Ordinal) ||
                string.Equals(
                    row.OriginalDescription,
                    originalText,
                    StringComparison.Ordinal));
    }

    /// <summary>
    ///     Builds one forward original-to-translated lookup for one cached
    ///     scope.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="version">The exact stored game-version scope.</param>
    /// <returns>The lookup.</returns>
    private Dictionary<string, string> BuildForwardLookup(
        TranslationReuseScope scope,
        string? version)
    {
        var lookup = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var row in this.cache.Values.SelectMany(static rows => rows))
        {
            if (!scope.Matches(
                    row.OriginalLang,
                    row.TranslationLang,
                    row.TranslationEngine) ||
                !GameVersionLookupHelper.MatchesStoredVersion(
                    row.GameVersion,
                    version))
            {
                continue;
            }

            TryAddForwardTextPair(
                lookup,
                row.OriginalName,
                row.TranslatedName);
            TryAddForwardTextPair(
                lookup,
                row.OriginalDescription,
                row.TranslatedDescription);
        }

        return lookup;
    }

    /// <summary>
    ///     Builds one reverse translated-to-original lookup for one cached
    ///     scope while excluding ambiguous mappings.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="version">The exact stored game-version scope.</param>
    /// <returns>The lookup.</returns>
    private Dictionary<string, string> BuildReverseLookup(
        TranslationReuseScope scope,
        string? version)
    {
        var lookup = new Dictionary<string, string>(StringComparer.Ordinal);
        var ambiguousKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in this.cache.Values.SelectMany(static rows => rows))
        {
            if (!scope.Matches(
                    row.OriginalLang,
                    row.TranslationLang,
                    row.TranslationEngine) ||
                !GameVersionLookupHelper.MatchesStoredVersion(
                    row.GameVersion,
                    version))
            {
                continue;
            }

            TryAddReverseTextPair(
                lookup,
                ambiguousKeys,
                row.OriginalName,
                row.TranslatedName);
            TryAddReverseTextPair(
                lookup,
                ambiguousKeys,
                row.OriginalDescription,
                row.TranslatedDescription);
        }

        foreach (var ambiguousKey in ambiguousKeys)
        {
            lookup.Remove(ambiguousKey);
        }

        return lookup;
    }

    /// <summary>
    ///     Adds one forward text pair to the lookup when both sides are
    ///     populated.
    /// </summary>
    /// <param name="lookup">The lookup to update.</param>
    /// <param name="originalText">The original text.</param>
    /// <param name="translatedText">The translated text.</param>
    private static void TryAddForwardTextPair(
        IDictionary<string, string> lookup,
        string? originalText,
        string? translatedText)
    {
        if (string.IsNullOrWhiteSpace(originalText) ||
            string.IsNullOrWhiteSpace(translatedText))
        {
            return;
        }

        lookup[originalText] = translatedText;
    }

    /// <summary>
    ///     Adds one reverse text pair to the lookup while tracking ambiguous
    ///     translated strings.
    /// </summary>
    /// <param name="lookup">The reverse lookup to update.</param>
    /// <param name="ambiguousKeys">The translated texts already known to be ambiguous.</param>
    /// <param name="originalText">The original text.</param>
    /// <param name="translatedText">The translated text.</param>
    private static void TryAddReverseTextPair(
        IDictionary<string, string> lookup,
        ISet<string> ambiguousKeys,
        string? originalText,
        string? translatedText)
    {
        if (string.IsNullOrWhiteSpace(originalText) ||
            string.IsNullOrWhiteSpace(translatedText))
        {
            return;
        }

        if (lookup.TryGetValue(translatedText, out var existingOriginal) &&
            !string.Equals(existingOriginal, originalText, StringComparison.Ordinal))
        {
            ambiguousKeys.Add(translatedText);
            return;
        }

        if (!ambiguousKeys.Contains(translatedText))
        {
            lookup[translatedText] = originalText;
        }
    }

    /// <summary>
    ///     Computes one ordering score for tolerant identity-based reference
    ///     text lookup.
    /// </summary>
    /// <param name="row">The candidate row.</param>
    /// <param name="gameVersion">The requested game version.</param>
    /// <returns>The ordering score.</returns>
    private static int ComputeIdentityMatchScore(
        TRow row,
        string? gameVersion)
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

        return score;
    }

    /// <summary>
    ///     Gets whether one reference-text row contains every translated field
    ///     required by the live runtime.
    /// </summary>
    /// <param name="row">The candidate row.</param>
    /// <returns>
    ///     <see langword="true" /> when the row contains a translated name and
    ///     any required translated description; otherwise
    ///     <see langword="false" />.
    /// </returns>
    private static bool HasCompleteTranslation(TRow row)
    {
        return !string.IsNullOrWhiteSpace(row.TranslatedName) &&
               (string.IsNullOrWhiteSpace(row.OriginalDescription) ||
                !string.IsNullOrWhiteSpace(row.TranslatedDescription));
    }

    /// <summary>
    ///     Builds one cache key from the complete reuse scope and version.
    /// </summary>
    /// <param name="scope">The required translation reuse scope.</param>
    /// <param name="version">The exact stored game-version scope.</param>
    /// <returns>The scope key.</returns>
    private static string BuildScopeKey(
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
        return $"{source}\u001F{target}\u001F{engine}\u001F{version ?? string.Empty}";
    }

    /// <summary>
    ///     Resolves a source-aware scope for an unmigrated caller.
    /// </summary>
    /// <param name="targetLanguage">The requested target language.</param>
    /// <param name="translationEngine">The requested translation engine.</param>
    /// <param name="scope">The resolved source-aware scope.</param>
    /// <returns>Whether the current source language was resolved.</returns>
    private static bool TryCreateLegacyScope(
        string targetLanguage,
        int translationEngine,
        out TranslationReuseScope scope)
    {
        if (string.IsNullOrWhiteSpace(targetLanguage) ||
            !RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
                out var sourceLanguage))
        {
            scope = default;
            return false;
        }

        scope = new TranslationReuseScope(
            sourceLanguage.PersistenceCode,
            targetLanguage,
            translationEngine,
            true);
        return true;
    }
}
