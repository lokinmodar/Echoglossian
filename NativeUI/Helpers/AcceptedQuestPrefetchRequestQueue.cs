// <copyright file="AcceptedQuestPrefetchRequestQueue.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Stores deduplicated priority requests for the accepted-quest prefetch
///     runtime.
/// </summary>
internal sealed class AcceptedQuestPrefetchRequestQueue
{
  private readonly Queue<uint> questIds = [];

  private readonly Dictionary<uint, HashSet<string>> queuedQuestSources = [];

  /// <summary>
  ///     Gets the number of priority requests waiting to be prefetched.
  /// </summary>
  public int Count => this.questIds.Count;

  /// <summary>
  ///     Adds an accepted quest to the priority queue if it is not already
  ///     waiting to be processed.
  /// </summary>
  /// <param name="questId">The accepted quest identifier.</param>
  /// <param name="source">The quest surface requesting the prefetch.</param>
  /// <param name="requestSources">
  ///     The normalized set of visible request sources currently associated
  ///     with this quest.
  /// </param>
  /// <returns>True when the request was added.</returns>
  public bool Request(
      uint questId,
      string? source,
      out string requestSources)
  {
    requestSources = string.Empty;
    if (questId == 0)
    {
      return false;
    }

    var normalizedSource = NormalizeSource(source);
    if (this.queuedQuestSources.TryGetValue(questId, out var existingSources))
    {
      existingSources.Add(normalizedSource);
      requestSources = FormatSources(existingSources);
      return false;
    }

    this.queuedQuestSources[questId] = [normalizedSource];
    this.questIds.Enqueue(questId);
    requestSources = normalizedSource;
    return true;
  }

  /// <summary>
  ///     Tries to dequeue the next accepted quest requested by a visible
  ///     quest surface.
  /// </summary>
  /// <param name="questId">The requested accepted quest identifier.</param>
  /// <param name="requestSources">
  ///     The normalized set of visible request sources currently associated
  ///     with this quest.
  /// </param>
  /// <returns>True when a request was available.</returns>
  public bool TryDequeue(
      out uint questId,
      out string requestSources)
  {
    questId = 0;
    requestSources = string.Empty;
    while (this.questIds.TryDequeue(out var requestedQuestId))
    {
      if (requestedQuestId == 0)
      {
        continue;
      }

      if (!this.queuedQuestSources.Remove(
              requestedQuestId,
              out var sources))
      {
        sources = [NormalizeSource(null)];
      }

      questId = requestedQuestId;
      requestSources = FormatSources(sources);
      return true;
    }

    return false;
  }

  /// <summary>
  ///     Clears every pending priority request.
  /// </summary>
  public void Clear()
  {
    this.questIds.Clear();
    this.queuedQuestSources.Clear();
  }

  /// <summary>
  ///     Normalizes one request source label for compact diagnostic output.
  /// </summary>
  /// <param name="source">The raw request source.</param>
  /// <returns>The normalized source label.</returns>
  private static string NormalizeSource(string? source)
  {
    return string.IsNullOrWhiteSpace(source)
        ? "unknown"
        : source.Trim();
  }

  /// <summary>
  ///     Formats the merged request sources for logging.
  /// </summary>
  /// <param name="sources">The merged request sources.</param>
  /// <returns>A compact source string.</returns>
  private static string FormatSources(IEnumerable<string> sources)
  {
    return string.Join(
        "|",
        sources
            .Where(static source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static source => source, StringComparer.Ordinal));
  }
}
