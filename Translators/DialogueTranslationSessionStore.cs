// <copyright file="DialogueTranslationSessionStore.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Maintains short-lived, runtime-only dialogue turn history for future
///     session-aware LLM translation requests.
/// </summary>
public static class DialogueTranslationSessionStore
{
  private static readonly Lock SyncRoot = new();
  private static readonly Dictionary<string, DialogueSessionEntry> Sessions = new(StringComparer.Ordinal);

  /// <summary>
  ///     Builds a runtime-only dialogue context from the retained session
  ///     history, then appends the current turn for future requests.
  /// </summary>
  /// <param name="sessionNamespace">The isolated dialogue namespace, such as Talk or BattleTalk.</param>
  /// <param name="sessionKey">The runtime session key within that namespace.</param>
  /// <param name="speakerName">The visible speaker name when available.</param>
  /// <param name="sourceText">The current source-side text.</param>
  /// <param name="historyLimit">The maximum number of prior turns to retain.</param>
  /// <param name="ttl">The maximum session idle age.</param>
  /// <param name="observedAtUtc">Optional explicit observation time for tests.</param>
  /// <returns>The current runtime-only dialogue context.</returns>
  public static DialogueTranslationContext BuildContext(
      string sessionNamespace,
      string sessionKey,
      string speakerName,
      string sourceText,
      int historyLimit,
      TimeSpan ttl,
      DateTime? observedAtUtc = null)
  {
    var now = observedAtUtc ?? DateTime.UtcNow;
    var normalizedNamespace = sessionNamespace ?? string.Empty;
    var normalizedKey = sessionKey ?? string.Empty;
    var normalizedSpeakerName = speakerName ?? string.Empty;
    var normalizedSourceText = sourceText ?? string.Empty;
    var composedKey = ComposeSessionMapKey(normalizedNamespace, normalizedKey);

    lock (SyncRoot)
    {
      PruneExpiredSessions(now, ttl);

      if (!Sessions.TryGetValue(composedKey, out var entry))
      {
        entry = new DialogueSessionEntry();
        Sessions[composedKey] = entry;
      }

      entry.LastObservedAtUtc = now;
      var priorTurns = entry.Turns.ToList();

      entry.Turns.Add(new DialogueTranslationTurn(
          normalizedSpeakerName,
          normalizedSourceText,
          now));
      TrimToHistoryLimit(entry.Turns, historyLimit);

      return new DialogueTranslationContext(
          normalizedNamespace,
          normalizedKey,
          normalizedSpeakerName,
          priorTurns);
    }
  }

  /// <summary>
  ///     Clears all runtime-only dialogue sessions.
  /// </summary>
  public static void Clear()
  {
    lock (SyncRoot)
    {
      Sessions.Clear();
    }
  }

  private static string ComposeSessionMapKey(string sessionNamespace, string sessionKey)
  {
    return $"{sessionNamespace}\u001F{sessionKey}";
  }

  private static void PruneExpiredSessions(DateTime now, TimeSpan ttl)
  {
    if (ttl <= TimeSpan.Zero || Sessions.Count == 0)
    {
      return;
    }

    var expiredKeys = Sessions
        .Where(kvp => now - kvp.Value.LastObservedAtUtc > ttl)
        .Select(kvp => kvp.Key)
        .ToList();

    foreach (var expiredKey in expiredKeys)
    {
      Sessions.Remove(expiredKey);
    }
  }

  private static void TrimToHistoryLimit(List<DialogueTranslationTurn> turns, int historyLimit)
  {
    var effectiveHistoryLimit = Math.Max(historyLimit, 0);
    if (turns.Count <= effectiveHistoryLimit)
    {
      return;
    }

    turns.RemoveRange(0, turns.Count - effectiveHistoryLimit);
  }

  private sealed class DialogueSessionEntry
  {
    public DateTime LastObservedAtUtc { get; set; }

    public List<DialogueTranslationTurn> Turns { get; } = [];
  }
}
