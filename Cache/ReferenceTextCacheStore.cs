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
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="sourceContentHash">The stable source-content hash.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public TRow? TryFindCanonicalMatch(
        uint referenceId,
        string lang,
        int engine,
        string? gameVersion,
        string sourceContentHash)
    {
        if (referenceId == 0 ||
            string.IsNullOrWhiteSpace(lang) ||
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
            RuntimeLanguageHelper.LanguagesMatch(
                row.TranslationLang,
                lang) &&
            row.TranslationEngine == engine &&
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
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <returns>The best translated row, or <see langword="null" />.</returns>
    public TRow? TryFindIdentityMatch(
        uint referenceId,
        string lang,
        int engine,
        string? gameVersion)
    {
        if (referenceId == 0 || string.IsNullOrWhiteSpace(lang))
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
                RuntimeLanguageHelper.LanguagesMatch(
                    row.TranslationLang,
                    lang) &&
                row.TranslationEngine == engine &&
                GameVersionLookupHelper.MatchesStoredVersion(
                    row.GameVersion,
                    gameVersion))
            .OrderByDescending(row => ComputeIdentityMatchScore(
                row,
                gameVersion))
            .ThenByDescending(row => row.UpdatedDate)
            .FirstOrDefault();
    }

    /// <summary>
    ///     Tries to resolve one exact translated text from this cache scope.
    /// </summary>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="originalText">The original text to translate.</param>
    /// <param name="translatedText">The resolved translated text.</param>
    /// <returns>
    ///     <see langword="true" /> when one translated text was found;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public bool TryFindTranslatedText(
        string lang,
        int engine,
        string? gameVersion,
        string originalText,
        out string translatedText)
    {
        translatedText = string.Empty;

        if (string.IsNullOrWhiteSpace(lang) ||
            string.IsNullOrWhiteSpace(originalText))
        {
            return false;
        }

        if (this.TryFindTranslatedTextInScope(
                lang,
                engine,
                gameVersion,
                originalText,
                out translatedText))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            return this.TryFindTranslatedTextInScope(
                lang,
                engine,
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
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="translatedText">The translated text to reverse.</param>
    /// <param name="originalText">The resolved canonical original text.</param>
    /// <returns>
    ///     <see langword="true" /> when one original text was found;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public bool TryFindOriginalText(
        string lang,
        int engine,
        string? gameVersion,
        string translatedText,
        out string originalText)
    {
        originalText = string.Empty;

        if (string.IsNullOrWhiteSpace(lang) ||
            string.IsNullOrWhiteSpace(translatedText))
        {
            return false;
        }

        if (this.TryFindOriginalTextInScope(
                lang,
                engine,
                gameVersion,
                translatedText,
                out originalText))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            return this.TryFindOriginalTextInScope(
                lang,
                engine,
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
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="originalText">The canonical original text to test.</param>
    /// <returns>
    ///     <see langword="true" /> when the original text already exists;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public bool ContainsOriginalText(
        string lang,
        int engine,
        string? gameVersion,
        string originalText)
    {
        if (string.IsNullOrWhiteSpace(lang) ||
            string.IsNullOrWhiteSpace(originalText))
        {
            return false;
        }

        if (this.ContainsOriginalTextInScope(
                lang,
                engine,
                gameVersion,
                originalText))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            return this.ContainsOriginalTextInScope(
                lang,
                engine,
                version: null,
                originalText);
        }

        return false;
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
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="version">The exact stored game-version scope.</param>
    /// <param name="originalText">The original text to translate.</param>
    /// <param name="translatedText">The translated text.</param>
    /// <returns>
    ///     <see langword="true" /> when one translated text was found;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private bool TryFindTranslatedTextInScope(
        string lang,
        int engine,
        string? version,
        string originalText,
        out string translatedText)
    {
        translatedText = string.Empty;

        var scopeKey = BuildScopeKey(lang, engine, version);
        if (!this.forwardTextLookupCache.TryGetValue(scopeKey, out var lookup))
        {
            lookup = this.BuildForwardLookup(lang, engine, version);
            this.forwardTextLookupCache[scopeKey] = lookup;
        }

        return lookup.TryGetValue(originalText, out translatedText);
    }

    /// <summary>
    ///     Tries to resolve one original text from one cached scope.
    /// </summary>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="version">The exact stored game-version scope.</param>
    /// <param name="translatedText">The translated text to reverse.</param>
    /// <param name="originalText">The resolved original text.</param>
    /// <returns>
    ///     <see langword="true" /> when one original text was found;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private bool TryFindOriginalTextInScope(
        string lang,
        int engine,
        string? version,
        string translatedText,
        out string originalText)
    {
        originalText = string.Empty;

        var scopeKey = BuildScopeKey(lang, engine, version);
        if (!this.reverseTextLookupCache.TryGetValue(scopeKey, out var lookup))
        {
            lookup = this.BuildReverseLookup(lang, engine, version);
            this.reverseTextLookupCache[scopeKey] = lookup;
        }

        return lookup.TryGetValue(translatedText, out originalText);
    }

    /// <summary>
    ///     Determines whether one exact canonical original text exists in one
    ///     cache scope.
    /// </summary>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="version">The exact stored game-version scope.</param>
    /// <param name="originalText">The canonical original text to test.</param>
    /// <returns>
    ///     <see langword="true" /> when the original text exists in the
    ///     requested scope; otherwise <see langword="false" />.
    /// </returns>
    private bool ContainsOriginalTextInScope(
        string lang,
        int engine,
        string? version,
        string originalText)
    {
        return this.cache.Values.SelectMany(static rows => rows)
            .Where(row =>
                RuntimeLanguageHelper.LanguagesMatch(
                    row.TranslationLang,
                    lang) &&
                row.TranslationEngine == engine &&
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
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="version">The exact stored game-version scope.</param>
    /// <returns>The lookup.</returns>
    private Dictionary<string, string> BuildForwardLookup(
        string lang,
        int engine,
        string? version)
    {
        var lookup = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var row in this.cache.Values.SelectMany(static rows => rows))
        {
            if (!RuntimeLanguageHelper.LanguagesMatch(
                    row.TranslationLang,
                    lang) ||
                row.TranslationEngine != engine ||
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
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="version">The exact stored game-version scope.</param>
    /// <returns>The lookup.</returns>
    private Dictionary<string, string> BuildReverseLookup(
        string lang,
        int engine,
        string? version)
    {
        var lookup = new Dictionary<string, string>(StringComparer.Ordinal);
        var ambiguousKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in this.cache.Values.SelectMany(static rows => rows))
        {
            if (!RuntimeLanguageHelper.LanguagesMatch(
                    row.TranslationLang,
                    lang) ||
                row.TranslationEngine != engine ||
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
    ///     Builds one cache scope key from language, engine, and version.
    /// </summary>
    /// <param name="lang">The target language code.</param>
    /// <param name="engine">The translation-engine identifier.</param>
    /// <param name="version">The exact stored game-version scope.</param>
    /// <returns>The scope key.</returns>
    private static string BuildScopeKey(
        string lang,
        int engine,
        string? version)
    {
        return
            $"{RuntimeLanguageHelper.NormalizeLanguage(lang)}\u001F{engine}\u001F{version ?? string.Empty}";
    }
}
