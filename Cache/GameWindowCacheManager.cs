// <copyright file="GameWindowCacheManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

using System.Linq;

namespace Echoglossian.Cache;

/// <summary>
///     Manages an in-memory cache of <see cref="GameWindow"/> records to reduce redundant DB access.
/// </summary>
public static class GameWindowCacheManager
{
  private const string CharacterWindowNamePrefix = "Character";

  private static bool isPreloaded;
  private static long revision;

  private static readonly Dictionary<string, GameWindow> ExactCache =
      new(StringComparer.Ordinal);
  private static readonly Dictionary<string, List<GameWindow>> ScopeCache =
      new(StringComparer.Ordinal);

  /// <summary>
  ///     In-memory cache for GameWindow entries, grouped by addon name.
  ///     Each key maps to a list of all entries for that addon.
  /// </summary>
  private static readonly Dictionary<string, List<GameWindow>> Cache =
      new(StringComparer.Ordinal);

  /// <summary>
  ///     Gets a value indicating whether the cache has been preloaded from the
  ///     database for the current runtime session.
  /// </summary>
  public static bool IsPreloaded => isPreloaded;

  /// <summary>
  ///     Gets the monotonically increasing revision of the canonical cache.
  ///     Runtime-local lookup snapshots must rebuild when this changes.
  /// </summary>
  public static long Revision => Interlocked.Read(ref revision);

  /// <summary>
  ///     Loads all GameWindow records from the database into memory.
  /// </summary>
  /// <param name="configDir">The plugin's configuration directory path.</param>
  public static void Preload(string configDir)
  {
    PluginRuntimeLog.Debug(
        "GameWindowCacheManager",
        "Preloading GameWindow entries from DB...");

    try
    {
      using var context = new EchoglossianDbContext(configDir);
      var all = context.GameWindow
          .AsNoTracking()
          .OrderBy(record => record.Id)
          .ToList();

      Cache.Clear();
      ExactCache.Clear();
      ScopeCache.Clear();
      Interlocked.Increment(ref revision);

      foreach (var record in all)
      {
        if (string.IsNullOrWhiteSpace(record.WindowAddonName))
        {
          continue;
        }

        IndexRecord(record);
      }

      isPreloaded = true;
      PluginRuntimeLog.Debug(
          "GameWindowCacheManager",
          $"Loaded {all.Count} records into {Cache.Count} addon buckets.");
    }
    catch (Exception ex)
    {
      isPreloaded = false;
      PluginRuntimeLog.Error(
          "GameWindowCacheManager",
          $"Failed to preload cache: {ex}");
    }
  }

  /// <summary>
  ///     Adds a GameWindow entry to the cache if it is not already present.
  ///     Ensures no duplicates by keying on addon name + lang + engine + version + original data.
  /// </summary>
  /// <param name="newRecord">The new <see cref="GameWindow"/> record to add.</param>
  public static void Update(GameWindow newRecord)
  {
    if (newRecord == null || string.IsNullOrWhiteSpace(newRecord.WindowAddonName))
    {
      PluginRuntimeLog.Warning(
          "GameWindowCacheManager.Update",
          "Attempted to update cache with null or invalid record.");
      return;
    }

    var existing = TryFindExistingCacheRow(newRecord);

    if (existing != null)
    {
      RemoveIndexedRecord(existing);
      PluginRuntimeLog.Debug(
          "GameWindowCacheManager.Update",
          "Replacing duplicate GameWindow in cache.");
    }

    IndexRecord(newRecord);
    Interlocked.Increment(ref revision);
    PluginRuntimeLog.Debug(
        "GameWindowCacheManager.Update",
        $"Cached GameWindow for addon: {newRecord.WindowAddonName} (now {GetAddonBucket(newRecord.WindowAddonName).Count} entries).");
  }

  /// <summary>
  ///     Clears all cached GameWindow entries.
  /// </summary>
  public static void Clear()
  {
    Cache.Clear();
    ExactCache.Clear();
    ScopeCache.Clear();
    isPreloaded = false;
    Interlocked.Increment(ref revision);
    PluginRuntimeLog.Debug(
        "GameWindowCacheManager",
        "Cleared GameWindow cache.");
  }

  /// <summary>
  ///     Attempts to find a matching GameWindow entry in the in-memory cache.
  /// </summary>
  /// <param name="addonName">The addon name to match.</param>
  /// <param name="scope">The required translation reuse scope.</param>
  /// <param name="version">The game version string to match (nullable allowed).</param>
  /// <param name="classJobId">
  ///     The optional class/job identifier to match for job-sensitive windows.
  /// </param>
  /// <param name="originalJson">The serialized original content to match.</param>
  /// <returns>A matching <see cref="GameWindow"/> if found; otherwise, <see langword="null"/>.</returns>
  public static GameWindow? TryFindMatch(
      string addonName,
      TranslationReuseScope scope,
      string? version,
      string originalJson,
      uint? classJobId = null)
  {
    if (string.IsNullOrWhiteSpace(addonName) ||
        string.IsNullOrWhiteSpace(scope.SourceLanguageCode) ||
        string.IsNullOrWhiteSpace(scope.TargetLanguageCode))
    {
      PluginRuntimeLog.Warning(
          "GameWindowCacheManager.TryFindMatch",
          "Invalid parameters.");
      return null;
    }

    var exactMatch = TryGetExactMatch(
        addonName,
        scope,
        version,
        originalJson,
        classJobId);
    if (exactMatch != null)
    {
      return exactMatch;
    }

    if (!string.IsNullOrWhiteSpace(version) &&
        TryGetExactMatch(
            addonName,
            scope,
            version: null,
            originalJson,
            classJobId) is { } versionAgnosticMatch)
    {
      return versionAgnosticMatch;
    }

    if (classJobId.HasValue &&
        TryGetExactMatch(
            addonName,
            scope,
            version,
            originalJson,
            classJobId: null) is { } legacyExactMatch)
    {
      return legacyExactMatch;
    }

    if (classJobId.HasValue &&
        !string.IsNullOrWhiteSpace(version) &&
        TryGetExactMatch(
            addonName,
            scope,
            version: null,
            originalJson,
            classJobId: null) is { } legacyVersionAgnosticMatch)
    {
      return legacyVersionAgnosticMatch;
    }

    return null;
  }

  /// <summary>
  ///     Returns cached candidates for one addon lookup scope so runtimes can
  ///     recover original payloads from already-translated live UI.
  /// </summary>
  /// <param name="addonName">The addon name to match.</param>
  /// <param name="scope">The required translation reuse scope.</param>
  /// <param name="version">The game version string to match.</param>
  /// <param name="classJobId">
  ///     The optional class/job identifier to match for job-sensitive windows.
  /// </param>
  /// <returns>The matching cached rows.</returns>
  public static IReadOnlyList<GameWindow> GetCandidates(
      string addonName,
      TranslationReuseScope scope,
      string? version,
      uint? classJobId = null)
  {
    if (string.IsNullOrWhiteSpace(addonName) ||
        string.IsNullOrWhiteSpace(scope.SourceLanguageCode) ||
        string.IsNullOrWhiteSpace(scope.TargetLanguageCode))
    {
      return [];
    }

    var exactRows = GetScopedRows(
        addonName,
        scope,
        version,
        classJobId);

    List<GameWindow>? legacyRows = null;
    if (classJobId.HasValue)
    {
      legacyRows = GetScopedRows(
          addonName,
          scope,
          version,
          classJobId: null);
    }

    if (string.IsNullOrWhiteSpace(version))
    {
      if (IsCharacterWindow(addonName))
      {
        return GetPreferredCharacterCandidates(exactRows, legacyRows);
      }

      return MergeCandidateLists(exactRows, legacyRows);
    }

    var versionAgnosticRows = GetScopedRows(
        addonName,
        scope,
        version: null,
        classJobId);

    List<GameWindow>? legacyVersionAgnosticRows = null;
    if (classJobId.HasValue)
    {
      legacyVersionAgnosticRows = GetScopedRows(
          addonName,
          scope,
          version: null,
          classJobId: null);
    }

    if (exactRows == null || exactRows.Count == 0)
    {
      if (IsCharacterWindow(addonName))
      {
        return GetPreferredCharacterCandidates(
            versionAgnosticRows,
            legacyRows,
            legacyVersionAgnosticRows);
      }

      return MergeCandidateLists(versionAgnosticRows, legacyRows, legacyVersionAgnosticRows);
    }

    if (versionAgnosticRows == null || versionAgnosticRows.Count == 0)
    {
      if (IsCharacterWindow(addonName))
      {
        return GetPreferredCharacterCandidates(
            exactRows,
            legacyRows,
            legacyVersionAgnosticRows);
      }

      return MergeCandidateLists(exactRows, legacyRows, legacyVersionAgnosticRows);
    }

    if (IsCharacterWindow(addonName))
    {
      return GetPreferredCharacterCandidates(
          exactRows,
          versionAgnosticRows,
          legacyRows,
          legacyVersionAgnosticRows);
    }

    return MergeCandidateLists(
        exactRows,
        versionAgnosticRows,
        legacyRows,
        legacyVersionAgnosticRows);
  }

  /// <summary>
  ///     Finds one exact original-payload match for a single stored version
  ///     and class/job scope.
  /// </summary>
  /// <param name="addonName">The addon name.</param>
  /// <param name="scope">The required translation reuse scope.</param>
  /// <param name="version">The exact stored game version.</param>
  /// <param name="originalJson">The serialized original payload.</param>
  /// <param name="classJobId">The exact stored class/job scope.</param>
  /// <returns>The matching row, or <see langword="null" />.</returns>
  private static GameWindow? TryGetExactMatch(
      string addonName,
      TranslationReuseScope scope,
      string? version,
      string originalJson,
      uint? classJobId)
  {
    if (scope.RequireMatchingEngine)
    {
      return ExactCache.TryGetValue(
          BuildExactKey(
              addonName,
              scope.SourceLanguageCode,
              scope.TargetLanguageCode,
              scope.TranslationEngine,
              version,
              originalJson,
              classJobId),
          out var exactMatch) &&
          scope.Matches(
              exactMatch.OriginalWindowStringsLang,
              exactMatch.TranslationLang,
              exactMatch.TranslationEngine)
          ? exactMatch
          : null;
    }

    return GetScopedRows(addonName, scope, version, classJobId)?
        .FirstOrDefault(row => row.OriginalWindowStrings == originalJson);
  }

  /// <summary>
  ///     Gets rows for one exact stored version and class/job scope, using the
  ///     indexed engine when required and a scoped scan for engine-agnostic
  ///     reuse.
  /// </summary>
  /// <param name="addonName">The addon name.</param>
  /// <param name="scope">The required translation reuse scope.</param>
  /// <param name="version">The exact stored game version.</param>
  /// <param name="classJobId">The exact stored class/job scope.</param>
  /// <returns>The matching rows, or <see langword="null" />.</returns>
  private static List<GameWindow>? GetScopedRows(
      string addonName,
      TranslationReuseScope scope,
      string? version,
      uint? classJobId)
  {
    if (scope.RequireMatchingEngine)
    {
      if (!ScopeCache.TryGetValue(
          BuildScopeKey(
              addonName,
              scope.SourceLanguageCode,
              scope.TargetLanguageCode,
              scope.TranslationEngine,
              version,
              classJobId),
          out var scopedRows))
      {
        return null;
      }

      var strictMatchingRows = scopedRows
          .Where(row => scope.Matches(
              row.OriginalWindowStringsLang,
              row.TranslationLang,
              row.TranslationEngine))
          .ToList();
      return strictMatchingRows.Count == 0 ? null : strictMatchingRows;
    }

    if (!Cache.TryGetValue(addonName, out var addonRows))
    {
      return null;
    }

    var matchingRows = addonRows
        .Where(row =>
            scope.Matches(
                row.OriginalWindowStringsLang,
                row.TranslationLang,
                row.TranslationEngine) &&
            string.Equals(
                row.GameVersion,
                version,
                StringComparison.Ordinal) &&
            row.ClassJobId == classJobId)
        .ToList();
    return matchingRows.Count == 0 ? null : matchingRows;
  }

  /// <summary>
  ///     Tries to find an existing cached row that should be replaced by the
  ///     supplied record.
  /// </summary>
  /// <param name="newRecord">The new record.</param>
  /// <returns>The cached row to replace, or <see langword="null"/>.</returns>
  private static GameWindow? TryFindExistingCacheRow(GameWindow newRecord)
  {
    var addonBucket = GetAddonBucket(newRecord.WindowAddonName!);
    return addonBucket.FirstOrDefault(g =>
        RuntimeLanguageHelper.LanguagesMatch(
            g.OriginalWindowStringsLang,
            newRecord.OriginalWindowStringsLang) &&
        RuntimeLanguageHelper.LanguagesMatch(
            g.TranslationLang,
            newRecord.TranslationLang) &&
        g.ClassJobId == newRecord.ClassJobId &&
        g.TranslationEngine == newRecord.TranslationEngine &&
        GameVersionLookupHelper.MatchesStoredVersion(
            g.GameVersion,
            newRecord.GameVersion) &&
        g.OriginalWindowStrings == newRecord.OriginalWindowStrings);
  }

  private static List<GameWindow> GetAddonBucket(string addonName)
  {
    if (!Cache.TryGetValue(addonName, out var list))
    {
      list = [];
      Cache[addonName] = list;
    }

    return list;
  }

  private static List<GameWindow> GetScopeBucket(
      string addonName,
      string sourceLanguage,
      string lang,
      int? engine,
      string? version,
      uint? classJobId)
  {
    var scopeKey = BuildScopeKey(
        addonName,
        sourceLanguage,
        lang,
        engine,
        version,
        classJobId);
    if (!ScopeCache.TryGetValue(scopeKey, out var list))
    {
      list = [];
      ScopeCache[scopeKey] = list;
    }

    return list;
  }

  private static void IndexRecord(GameWindow record)
  {
    var addonName = record.WindowAddonName!;
    var translationEngine = record.TranslationEngine;
    var addonBucket = GetAddonBucket(addonName);
    addonBucket.Add(record);

    var normalizedSourceLanguage = RuntimeLanguageHelper.NormalizeLanguage(
        record.OriginalWindowStringsLang);
    var normalizedTargetLanguage =
        RuntimeLanguageHelper.NormalizeLanguage(record.TranslationLang);
    GetScopeBucket(
        addonName,
        normalizedSourceLanguage,
        normalizedTargetLanguage,
        translationEngine,
        record.GameVersion,
        record.ClassJobId).Add(record);

    ExactCache[BuildExactKey(
        addonName,
        normalizedSourceLanguage,
        normalizedTargetLanguage,
        translationEngine,
        record.GameVersion,
        record.OriginalWindowStrings ?? string.Empty,
        record.ClassJobId)] = record;
  }

  private static void RemoveIndexedRecord(GameWindow record)
  {
    var addonName = record.WindowAddonName!;
    var translationEngine = record.TranslationEngine;
    GetAddonBucket(addonName).Remove(record);

    var normalizedSourceLanguage = RuntimeLanguageHelper.NormalizeLanguage(
        record.OriginalWindowStringsLang);
    var normalizedTargetLanguage =
        RuntimeLanguageHelper.NormalizeLanguage(record.TranslationLang);
    var scopeBucket = GetScopeBucket(
        addonName,
        normalizedSourceLanguage,
        normalizedTargetLanguage,
        translationEngine,
        record.GameVersion,
        record.ClassJobId);
    scopeBucket.Remove(record);
    if (scopeBucket.Count == 0)
    {
      ScopeCache.Remove(
          BuildScopeKey(
              addonName,
              normalizedSourceLanguage,
              normalizedTargetLanguage,
              translationEngine,
              record.GameVersion,
              record.ClassJobId));
    }

    ExactCache.Remove(
        BuildExactKey(
            addonName,
            normalizedSourceLanguage,
            normalizedTargetLanguage,
            translationEngine,
            record.GameVersion,
            record.OriginalWindowStrings ?? string.Empty,
            record.ClassJobId));
  }

  private static string BuildScopeKey(
      string addonName,
      string? sourceLanguage,
      string? lang,
      int? engine,
      string? version,
      uint? classJobId)
  {
    var normalizedSourceLanguage = RuntimeLanguageHelper.NormalizeLanguage(
        sourceLanguage);
    var normalizedTargetLanguage = RuntimeLanguageHelper.NormalizeLanguage(lang);
    var engineIdentity = engine?.ToString() ?? "null";
    return $"{addonName}|{normalizedSourceLanguage}|{normalizedTargetLanguage}|{engineIdentity}|{version ?? string.Empty}|{classJobId?.ToString() ?? string.Empty}";
  }

  private static string BuildExactKey(
      string addonName,
      string? sourceLanguage,
      string? lang,
      int? engine,
      string? version,
      string originalJson,
      uint? classJobId)
  {
    return $"{BuildScopeKey(addonName, sourceLanguage, lang, engine, version, classJobId)}|{originalJson}";
  }

  private static IReadOnlyList<GameWindow> MergeCandidateLists(
      params List<GameWindow>?[] candidateSets)
  {
    return candidateSets
        .Where(set => set != null)
        .SelectMany(set => set!)
        .GroupBy(row => row.Id)
        .Select(group => group.First())
        .ToList();
  }

  /// <summary>
  ///     Determines whether the specified addon name belongs to one of the
  ///     Character-family windows whose DB-first lookup should ignore partial
  ///     historical rows.
  /// </summary>
  /// <param name="addonName">The addon name to test.</param>
  /// <returns>
  ///     <see langword="true"/> when the addon belongs to the Character
  ///     family; otherwise <see langword="false"/>.
  /// </returns>
  private static bool IsCharacterWindow(string? addonName)
  {
    return !string.IsNullOrWhiteSpace(addonName) &&
           addonName.StartsWith(
               CharacterWindowNamePrefix,
               StringComparison.Ordinal);
  }

  /// <summary>
  ///     Chooses the preferred Character-family candidate set and collapses
  ///     it to the richest row so DB-first reuse prefers the most complete
  ///     canonical payload rather than partial historical snapshots.
  /// </summary>
  /// <param name="candidateSets">The candidate sets in preference order.</param>
  /// <returns>The preferred collapsed candidate list.</returns>
  private static IReadOnlyList<GameWindow> GetPreferredCharacterCandidates(
      params List<GameWindow>?[] candidateSets)
  {
    foreach (var candidateSet in candidateSets)
    {
      if (candidateSet == null || candidateSet.Count == 0)
      {
        continue;
      }

      var preferred = candidateSet
          .OrderByDescending(ComputeCharacterCandidateScore)
          .ThenByDescending(static row => row.UpdatedDate ?? row.CreatedDate ?? DateTime.MinValue)
          .ThenByDescending(static row => row.Id)
          .First();
      return [preferred];
    }

    return [];
  }

  /// <summary>
  ///     Computes one completeness score for a Character-family candidate so
  ///     richer payloads outrank partial snapshots from the same lookup
  ///     scope.
  /// </summary>
  /// <param name="row">The candidate row to score.</param>
  /// <returns>The row completeness score.</returns>
  private static int ComputeCharacterCandidateScore(GameWindow row)
  {
    ArgumentNullException.ThrowIfNull(row);

    return (row.OriginalWindowStrings?.Length ?? 0) +
           (row.TranslatedWindowStrings?.Length ?? 0);
  }
}
