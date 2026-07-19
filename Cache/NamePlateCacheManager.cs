// <copyright file="NamePlateCacheManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game.Gui.NamePlate;

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian.Cache;

/// <summary>
///     Manages in-memory nameplate translation rows so the NamePlateGui runtime
///     does not query SQLite during frequent nameplate redraws.
/// </summary>
public static class NamePlateCacheManager
{
  private static readonly object CacheLock = new();

  private static readonly Dictionary<string, NamePlateMessage> ExactCache =
      new(StringComparer.Ordinal);

  private static long revision;

  /// <summary>
  ///     Gets the cache revision, incremented after every cache mutation.
  /// </summary>
  public static long Revision => Interlocked.Read(ref revision);

  /// <summary>
  ///     Loads all nameplate translation rows into memory.
  /// </summary>
  /// <param name="configDir">The plugin configuration directory.</param>
  public static void Preload(string configDir)
  {
    try
    {
      using var context = new EchoglossianDbContext(configDir);
      var allRows = context.NamePlateMessages
          .AsNoTracking()
          .Where(row =>
              row.NamePlateKind != null &&
              !string.IsNullOrWhiteSpace(row.OriginalNamePlateText) &&
              !string.IsNullOrWhiteSpace(row.OriginalLang) &&
              !string.IsNullOrWhiteSpace(row.TranslationLang) &&
              !string.IsNullOrWhiteSpace(row.TranslatedNamePlateText))
          .OrderBy(row => row.Id)
          .ToList();

      lock (CacheLock)
      {
        ExactCache.Clear();
        foreach (var row in allRows)
        {
          ExactCache[BuildKey(row)] = row;
        }

        Interlocked.Increment(ref revision);
      }

      PluginRuntimeLog.Debug(
          "NamePlateCacheManager",
          $"Loaded {allRows.Count} nameplate translation rows.");
    }
    catch (Exception ex)
    {
      PluginRuntimeLog.Error(
          "NamePlateCacheManager",
          $"Failed to preload cache: {ex}");
    }
  }

  /// <summary>
  ///     Updates or inserts one cached nameplate translation row.
  /// </summary>
  /// <param name="newRecord">The row to cache.</param>
  public static void Update(NamePlateMessage newRecord)
  {
    if (newRecord == null ||
        newRecord.NamePlateKind == null ||
        string.IsNullOrWhiteSpace(newRecord.OriginalNamePlateText))
    {
      return;
    }

    lock (CacheLock)
    {
      ExactCache[BuildKey(newRecord)] = newRecord;
      Interlocked.Increment(ref revision);
    }
  }

  /// <summary>
  ///     Attempts to find a cached row for the specified nameplate scope.
  /// </summary>
  /// <param name="kind">The Dalamud nameplate kind.</param>
  /// <param name="originalText">The original visible nameplate text.</param>
  /// <param name="scope">The required translation reuse scope.</param>
  /// <returns>The matching cached row, or <see langword="null" />.</returns>
  public static NamePlateMessage? TryFindMatch(
      NamePlateKind kind,
      string originalText,
      TranslationReuseScope scope)
  {
    if (string.IsNullOrWhiteSpace(originalText) ||
        string.IsNullOrWhiteSpace(scope.SourceLanguageCode) ||
        string.IsNullOrWhiteSpace(scope.TargetLanguageCode))
    {
      return null;
    }

    lock (CacheLock)
    {
      if (scope.RequireMatchingEngine)
      {
        return ExactCache.TryGetValue(
            BuildKey(
                (int)kind,
                originalText,
                scope.SourceLanguageCode,
                scope.TargetLanguageCode,
                scope.TranslationEngine),
            out var strictMatch) &&
            scope.Matches(
                strictMatch.OriginalLang,
                strictMatch.TranslationLang,
                strictMatch.TranslationEngine)
            ? strictMatch
            : null;
      }

      return ExactCache.Values.FirstOrDefault(row =>
          row.NamePlateKind == (int)kind &&
          string.Equals(
              row.OriginalNamePlateText,
              originalText,
              StringComparison.Ordinal) &&
          scope.Matches(
              row.OriginalLang,
              row.TranslationLang,
              row.TranslationEngine));
    }
  }

  /// <summary>
  ///     Clears the in-memory nameplate cache.
  /// </summary>
  public static void Clear()
  {
    lock (CacheLock)
    {
      ExactCache.Clear();
      Interlocked.Increment(ref revision);
    }

    PluginRuntimeLog.Debug(
        "NamePlateCacheManager",
        "Cleared NamePlate cache.");
  }

  private static string BuildKey(NamePlateMessage row)
  {
    return BuildKey(
        row.NamePlateKind,
        row.OriginalNamePlateText,
        row.OriginalLang,
        row.TranslationLang,
        row.TranslationEngine);
  }

  private static string BuildKey(
      int? kind,
      string? originalText,
      string? sourceLanguage,
      string? targetLanguage,
      int? engine)
  {
    var normalizedSourceLanguage = RuntimeLanguageHelper.NormalizeLanguage(
        sourceLanguage);
    var normalizedTargetLanguage = RuntimeLanguageHelper.NormalizeLanguage(
        targetLanguage);
    var engineIdentity = engine?.ToString(CultureInfo.InvariantCulture) ?? "null";
    return $"{kind?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}|{normalizedSourceLanguage}|{normalizedTargetLanguage}|{engineIdentity}|{originalText ?? string.Empty}";
  }
}
