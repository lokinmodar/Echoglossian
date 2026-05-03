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
  private const string ActionMenuWindowName = "ActionMenu";
  private const string CharacterWindowNamePrefix = "Character";

  private static bool isPreloaded;

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
    PluginRuntimeLog.Debug(
        "GameWindowCacheManager",
        "Cleared GameWindow cache.");
  }

  /// <summary>
  ///     Attempts to find a matching GameWindow entry in the in-memory cache.
  /// </summary>
  /// <param name="addonName">The addon name to match.</param>
  /// <param name="lang">The translation language code to match.</param>
  /// <param name="engine">The translation engine ID to match.</param>
  /// <param name="version">The game version string to match (nullable allowed).</param>
  /// <param name="classJobId">
  ///     The optional class/job identifier to match for job-sensitive windows.
  /// </param>
  /// <param name="originalJson">The serialized original content to match.</param>
  /// <returns>A matching <see cref="GameWindow"/> if found; otherwise, <see langword="null"/>.</returns>
  public static GameWindow? TryFindMatch(
      string addonName,
      string lang,
      int engine,
      string? version,
      string originalJson,
      uint? classJobId = null)
  {
    if (string.IsNullOrWhiteSpace(addonName) || string.IsNullOrWhiteSpace(lang))
    {
      PluginRuntimeLog.Warning(
          "GameWindowCacheManager.TryFindMatch",
          "Invalid parameters.");
      return null;
    }

    if (ExactCache.TryGetValue(
            BuildExactKey(
                addonName,
                lang,
                engine,
                version,
                originalJson,
                classJobId),
            out var exactMatch))
    {
      return exactMatch;
    }

    if (!string.IsNullOrWhiteSpace(version) &&
        ExactCache.TryGetValue(
            BuildExactKey(
                addonName,
                lang,
                engine,
                version: null,
                originalJson,
                classJobId),
            out var versionAgnosticMatch))
    {
      return versionAgnosticMatch;
    }

    if (classJobId.HasValue &&
        ExactCache.TryGetValue(
            BuildExactKey(
                addonName,
                lang,
                engine,
                version,
                originalJson,
                classJobId: null),
            out var legacyExactMatch))
    {
      return legacyExactMatch;
    }

    if (classJobId.HasValue &&
        !string.IsNullOrWhiteSpace(version) &&
        ExactCache.TryGetValue(
            BuildExactKey(
                addonName,
                lang,
                engine,
                version: null,
                originalJson,
                classJobId: null),
            out var legacyVersionAgnosticMatch))
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
  /// <param name="lang">The translation language code to match.</param>
  /// <param name="engine">The translation engine ID to match.</param>
  /// <param name="version">The game version string to match.</param>
  /// <param name="classJobId">
  ///     The optional class/job identifier to match for job-sensitive windows.
  /// </param>
  /// <returns>The matching cached rows.</returns>
  public static IReadOnlyList<GameWindow> GetCandidates(
      string addonName,
      string lang,
      int engine,
      string? version,
      uint? classJobId = null)
  {
    if (string.IsNullOrWhiteSpace(addonName) || string.IsNullOrWhiteSpace(lang))
    {
      return [];
    }

    var exactRows = ScopeCache.TryGetValue(
        BuildScopeKey(addonName, lang, engine, version, classJobId),
        out var scopedRows)
        ? scopedRows
        : null;

    List<GameWindow>? legacyRows = null;
    if (classJobId.HasValue)
    {
      legacyRows = ScopeCache.TryGetValue(
          BuildScopeKey(addonName, lang, engine, version, classJobId: null),
          out var legacyScopedRows)
          ? legacyScopedRows
          : null;
    }

    if (string.IsNullOrWhiteSpace(version))
    {
      if (IsActionMenu(addonName))
      {
        return GetPreferredActionMenuCandidates(exactRows, legacyRows);
      }

      if (IsCharacterWindow(addonName))
      {
        return GetPreferredCharacterCandidates(exactRows, legacyRows);
      }

      return MergeCandidateLists(exactRows, legacyRows);
    }

    var versionAgnosticRows = ScopeCache.TryGetValue(
        BuildScopeKey(addonName, lang, engine, version: null, classJobId),
        out var fallbackRows)
        ? fallbackRows
        : null;

    List<GameWindow>? legacyVersionAgnosticRows = null;
    if (classJobId.HasValue)
    {
      legacyVersionAgnosticRows = ScopeCache.TryGetValue(
          BuildScopeKey(addonName, lang, engine, version: null, classJobId: null),
          out var legacyFallbackRows)
          ? legacyFallbackRows
          : null;
    }

    if (exactRows == null || exactRows.Count == 0)
    {
      if (IsActionMenu(addonName))
      {
        return GetPreferredActionMenuCandidates(
            versionAgnosticRows,
            legacyRows,
            legacyVersionAgnosticRows);
      }

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
      if (IsActionMenu(addonName))
      {
        return GetPreferredActionMenuCandidates(
            exactRows,
            legacyRows,
            legacyVersionAgnosticRows);
      }

      if (IsCharacterWindow(addonName))
      {
        return GetPreferredCharacterCandidates(
            exactRows,
            legacyRows,
            legacyVersionAgnosticRows);
      }

      return MergeCandidateLists(exactRows, legacyRows, legacyVersionAgnosticRows);
    }

    if (IsActionMenu(addonName))
    {
      return GetPreferredActionMenuCandidates(
          exactRows,
          versionAgnosticRows,
          legacyRows,
          legacyVersionAgnosticRows);
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
  ///     Tries to find an existing cached row that should be replaced by the
  ///     supplied record.
  /// </summary>
  /// <param name="newRecord">The new record.</param>
  /// <returns>The cached row to replace, or <see langword="null"/>.</returns>
  private static GameWindow? TryFindExistingCacheRow(GameWindow newRecord)
  {
    var addonBucket = GetAddonBucket(newRecord.WindowAddonName!);
    if (IsActionMenu(newRecord.WindowAddonName))
    {
      return addonBucket.FirstOrDefault(g =>
          RuntimeLanguageHelper.LanguagesMatch(
              g.TranslationLang,
              newRecord.TranslationLang) &&
          g.ClassJobId == newRecord.ClassJobId &&
          g.TranslationEngine == newRecord.TranslationEngine &&
          GameVersionLookupHelper.MatchesStoredVersion(
              g.GameVersion,
              newRecord.GameVersion));
    }

    return addonBucket.FirstOrDefault(g =>
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
      string lang,
      int engine,
      string? version,
      uint? classJobId)
  {
    var scopeKey = BuildScopeKey(
        addonName,
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
    var translationEngine = record.TranslationEngine ?? 0;
    var addonBucket = GetAddonBucket(addonName);
    addonBucket.Add(record);

    var normalizedLanguage =
        RuntimeLanguageHelper.NormalizeLanguage(record.TranslationLang);
    GetScopeBucket(
        addonName,
        normalizedLanguage,
        translationEngine,
        record.GameVersion,
        record.ClassJobId).Add(record);

    ExactCache[BuildExactKey(
        addonName,
        normalizedLanguage,
        translationEngine,
        record.GameVersion,
        record.OriginalWindowStrings ?? string.Empty,
        record.ClassJobId)] = record;
  }

  private static void RemoveIndexedRecord(GameWindow record)
  {
    var addonName = record.WindowAddonName!;
    var translationEngine = record.TranslationEngine ?? 0;
    GetAddonBucket(addonName).Remove(record);

    var normalizedLanguage =
        RuntimeLanguageHelper.NormalizeLanguage(record.TranslationLang);
    var scopeBucket = GetScopeBucket(
        addonName,
        normalizedLanguage,
        translationEngine,
        record.GameVersion,
        record.ClassJobId);
    scopeBucket.Remove(record);
    if (scopeBucket.Count == 0)
    {
      ScopeCache.Remove(
          BuildScopeKey(
              addonName,
              normalizedLanguage,
              translationEngine,
              record.GameVersion,
              record.ClassJobId));
    }

    ExactCache.Remove(
        BuildExactKey(
            addonName,
            normalizedLanguage,
            translationEngine,
            record.GameVersion,
            record.OriginalWindowStrings ?? string.Empty,
            record.ClassJobId));
  }

  private static string BuildScopeKey(
      string addonName,
      string? lang,
      int engine,
      string? version,
      uint? classJobId)
  {
    var normalizedLanguage = RuntimeLanguageHelper.NormalizeLanguage(lang);
    return $"{addonName}|{normalizedLanguage}|{engine}|{version ?? string.Empty}|{classJobId?.ToString() ?? string.Empty}";
  }

  private static string BuildExactKey(
      string addonName,
      string? lang,
      int engine,
      string? version,
      string originalJson,
      uint? classJobId)
  {
    return $"{BuildScopeKey(addonName, lang, engine, version, classJobId)}|{originalJson}";
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
  ///     Determines whether the specified addon name belongs to
  ///     <c>ActionMenu</c>.
  /// </summary>
  /// <param name="addonName">The addon name to test.</param>
  /// <returns>
  ///     <see langword="true"/> when the addon is <c>ActionMenu</c>;
  ///     otherwise <see langword="false"/>.
  /// </returns>
  private static bool IsActionMenu(string? addonName)
  {
    return string.Equals(
        addonName,
        ActionMenuWindowName,
        StringComparison.Ordinal);
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
  ///     Chooses the preferred ActionMenu candidate set and collapses it to
  ///     the newest row so recovery and gate checks do not scan historical
  ///     duplicates from the same lookup scope.
  /// </summary>
  /// <param name="candidateSets">The candidate sets in preference order.</param>
  /// <returns>The preferred collapsed candidate list.</returns>
  private static IReadOnlyList<GameWindow> GetPreferredActionMenuCandidates(
      params List<GameWindow>?[] candidateSets)
  {
    foreach (var candidateSet in candidateSets)
    {
      if (candidateSet == null || candidateSet.Count == 0)
      {
        continue;
      }

      var preferred = candidateSet
          .OrderByDescending(static row => row.UpdatedDate ?? row.CreatedDate ?? DateTime.MinValue)
          .ThenByDescending(static row => row.Id)
          .First();
      return [preferred];
    }

    return [];
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
