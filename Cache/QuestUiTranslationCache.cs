// <copyright file="QuestUiTranslationCache.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Cache;

/// <summary>
///     Stores quest-related UI texts that were already applied to addon nodes.
///     This lets the quest handlers skip reprocessing text that is already on screen.
/// </summary>
public static class QuestUiTranslationCache
{
  private static readonly ConcurrentDictionary<string, QuestUiTranslationSnapshot> Cache =
      new(StringComparer.Ordinal);

  /// <summary>
  ///     Tries to get a cached applied text snapshot for the given visible text.
  /// </summary>
  /// <param name="appliedText">The currently visible text.</param>
  /// <param name="snapshot">The cached snapshot if the text was previously applied.</param>
  /// <returns>True when the visible text is already known to be translated.</returns>
  public static bool TryGetAppliedSnapshot(
      string appliedText,
      out QuestUiTranslationSnapshot snapshot)
  {
    snapshot = default!;
    return !string.IsNullOrWhiteSpace(appliedText) &&
           Cache.TryGetValue(appliedText, out snapshot);
  }

  /// <summary>
  ///     Remembers the applied text for a quest-related node.
  /// </summary>
  /// <param name="originalText">The source text before translation.</param>
  /// <param name="appliedText">The exact text written to the UI.</param>
  public static void Remember(string originalText, string appliedText)
  {
    if (string.IsNullOrWhiteSpace(appliedText))
    {
      return;
    }

    Cache[appliedText] = new QuestUiTranslationSnapshot(
        originalText ?? string.Empty,
        appliedText,
        DateTime.UtcNow);
  }

  /// <summary>
  ///     Clears the cached quest UI texts.
  /// </summary>
  public static void Clear()
  {
    Cache.Clear();
    PluginRuntimeLog.Debug("[QuestUiTranslationCache] Cleared quest UI translation cache.");
  }
}

/// <summary>
///     Captures the original and applied quest UI text for a node.
/// </summary>
/// <param name="OriginalText">The original text read from the addon.</param>
/// <param name="AppliedText">The text written to the addon or shown in the UI.</param>
/// <param name="LastUpdatedUtc">The time the cached text was last updated.</param>
public readonly record struct QuestUiTranslationSnapshot(
    string OriginalText,
    string AppliedText,
    DateTime LastUpdatedUtc);


