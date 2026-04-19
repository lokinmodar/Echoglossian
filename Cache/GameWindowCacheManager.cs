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
    PluginLog.Debug("[GameWindowCacheManager] Preloading GameWindow entries from DB...");

    try
    {
      using var context = new EchoglossianDbContext(configDir);
      var all = context.GameWindow.AsNoTracking().ToList();

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
      PluginLog.Debug($"[GameWindowCacheManager] Loaded {all.Count} records into {Cache.Count} addon buckets.");
    }
    catch (Exception ex)
    {
      isPreloaded = false;
      PluginLog.Error($"[GameWindowCacheManager] Failed to preload cache: {ex}");
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
      PluginLog.Warning("[GameWindowCacheManager.Update] Attempted to update cache with null or invalid record.");
      return;
    }

    var existing = GetAddonBucket(newRecord.WindowAddonName).FirstOrDefault(g =>
        RuntimeLanguageHelper.LanguagesMatch(
            g.TranslationLang,
            newRecord.TranslationLang) &&
        g.TranslationEngine == newRecord.TranslationEngine &&
        GameVersionLookupHelper.MatchesStoredVersion(
            g.GameVersion,
            newRecord.GameVersion) &&
        g.OriginalWindowStrings == newRecord.OriginalWindowStrings);

    if (existing != null)
    {
      RemoveIndexedRecord(existing);
      PluginLog.Debug("[GameWindowCacheManager.Update] Replacing duplicate GameWindow in cache.");
    }

    IndexRecord(newRecord);
    PluginLog.Debug(
        $"[GameWindowCacheManager.Update] Cached GameWindow for addon: {newRecord.WindowAddonName} (now {GetAddonBucket(newRecord.WindowAddonName).Count} entries).");
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
    PluginLog.Debug("[GameWindowCacheManager] Cleared GameWindow cache.");
  }

  /// <summary>
  ///     Attempts to find a matching GameWindow entry in the in-memory cache.
  /// </summary>
  /// <param name="addonName">The addon name to match.</param>
  /// <param name="lang">The translation language code to match.</param>
  /// <param name="engine">The translation engine ID to match.</param>
  /// <param name="version">The game version string to match (nullable allowed).</param>
  /// <param name="originalJson">The serialized original content to match.</param>
  /// <returns>A matching <see cref="GameWindow"/> if found; otherwise, <see langword="null"/>.</returns>
  public static GameWindow? TryFindMatch(string addonName, string lang, int engine, string? version, string originalJson)
  {
    if (string.IsNullOrWhiteSpace(addonName) || string.IsNullOrWhiteSpace(lang))
    {
      PluginLog.Warning("[GameWindowCacheManager.TryFindMatch] Invalid parameters.");
      return null;
    }

    if (ExactCache.TryGetValue(
            BuildExactKey(addonName, lang, engine, version, originalJson),
            out var exactMatch))
    {
      return exactMatch;
    }

    if (!string.IsNullOrWhiteSpace(version) &&
        ExactCache.TryGetValue(
            BuildExactKey(addonName, lang, engine, version: null, originalJson),
            out var versionAgnosticMatch))
    {
      return versionAgnosticMatch;
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
  /// <returns>The matching cached rows.</returns>
  public static IReadOnlyList<GameWindow> GetCandidates(
      string addonName,
      string lang,
      int engine,
      string? version)
  {
    if (string.IsNullOrWhiteSpace(addonName) || string.IsNullOrWhiteSpace(lang))
    {
      return [];
    }

    var exactRows = ScopeCache.TryGetValue(
        BuildScopeKey(addonName, lang, engine, version),
        out var scopedRows)
        ? scopedRows
        : null;

    if (string.IsNullOrWhiteSpace(version))
    {
      return exactRows ?? [];
    }

    var versionAgnosticRows = ScopeCache.TryGetValue(
        BuildScopeKey(addonName, lang, engine, version: null),
        out var fallbackRows)
        ? fallbackRows
        : null;

    if (exactRows == null || exactRows.Count == 0)
    {
      return versionAgnosticRows ?? [];
    }

    if (versionAgnosticRows == null || versionAgnosticRows.Count == 0)
    {
      return exactRows;
    }

    var mergedRows = exactRows
        .Concat(versionAgnosticRows)
        .GroupBy(row => row.Id)
        .Select(group => group.First())
        .ToList();
    return mergedRows;
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
      string? version)
  {
    var scopeKey = BuildScopeKey(addonName, lang, engine, version);
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
        record.GameVersion).Add(record);

    ExactCache[BuildExactKey(
        addonName,
        normalizedLanguage,
        translationEngine,
        record.GameVersion,
        record.OriginalWindowStrings ?? string.Empty)] = record;
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
        record.GameVersion);
    scopeBucket.Remove(record);
    if (scopeBucket.Count == 0)
    {
      ScopeCache.Remove(
          BuildScopeKey(
              addonName,
              normalizedLanguage,
              translationEngine,
              record.GameVersion));
    }

    ExactCache.Remove(
        BuildExactKey(
            addonName,
            normalizedLanguage,
            translationEngine,
            record.GameVersion,
            record.OriginalWindowStrings ?? string.Empty));
  }

  private static string BuildScopeKey(
      string addonName,
      string? lang,
      int engine,
      string? version)
  {
    var normalizedLanguage = RuntimeLanguageHelper.NormalizeLanguage(lang);
    return $"{addonName}|{normalizedLanguage}|{engine}|{version ?? string.Empty}";
  }

  private static string BuildExactKey(
      string addonName,
      string? lang,
      int engine,
      string? version,
      string originalJson)
  {
    return $"{BuildScopeKey(addonName, lang, engine, version)}|{originalJson}";
  }
}
