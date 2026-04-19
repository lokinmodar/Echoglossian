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
  /// <summary>
  ///     In-memory cache for GameWindow entries, grouped by addon name.
  ///     Each key maps to a list of all entries for that addon.
  /// </summary>
  private static readonly Dictionary<string, List<GameWindow>> Cache = new();

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

      foreach (var record in all)
      {
        if (string.IsNullOrWhiteSpace(record.WindowAddonName))
        {
          continue;
        }

        var addonKey = record.WindowAddonName;

        if (!Cache.TryGetValue(addonKey, out var list))
        {
          list = new List<GameWindow>();
          Cache[addonKey] = list;
        }

        list.Add(record);
      }

      PluginLog.Debug($"[GameWindowCacheManager] Loaded {all.Count} records into {Cache.Count} addon buckets.");
    }
    catch (Exception ex)
    {
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

    var addonKey = newRecord.WindowAddonName;

    if (!Cache.TryGetValue(addonKey, out var list))
    {
      list = new List<GameWindow>();
      Cache[addonKey] = list;
    }

    // Remove existing duplicate (same lang + engine + version + original strings)
    var existing = list.FirstOrDefault(g =>
        RuntimeLanguageHelper.LanguagesMatch(
            g.TranslationLang,
            newRecord.TranslationLang) &&
        g.TranslationEngine == newRecord.TranslationEngine &&
        g.GameVersion == newRecord.GameVersion &&
        g.OriginalWindowStrings == newRecord.OriginalWindowStrings);

    if (existing != null)
    {
      list.Remove(existing);
      PluginLog.Debug("[GameWindowCacheManager.Update] Replacing duplicate GameWindow in cache.");
    }

    list.Add(newRecord);
    PluginLog.Debug($"[GameWindowCacheManager.Update] Cached GameWindow for addon: {addonKey} (now {list.Count} entries).");
  }

  /// <summary>
  ///     Clears all cached GameWindow entries.
  /// </summary>
  public static void Clear()
  {
    Cache.Clear();
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

    if (!Cache.TryGetValue(addonName, out var list) || list is null || list.Count == 0)
    {
      return null;
    }

    var match = list.FirstOrDefault(g =>
        RuntimeLanguageHelper.LanguagesMatch(g.TranslationLang, lang) &&
        g.TranslationEngine == engine &&
        (g.GameVersion == null || g.GameVersion == version) &&
        g.OriginalWindowStrings == originalJson);

    return match;
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

    if (!Cache.TryGetValue(addonName, out var list) || list is null || list.Count == 0)
    {
      return [];
    }

    return list
        .Where(g =>
            RuntimeLanguageHelper.LanguagesMatch(g.TranslationLang, lang) &&
            g.TranslationEngine == engine &&
            (g.GameVersion == null || g.GameVersion == version))
        .ToList();
  }
}
