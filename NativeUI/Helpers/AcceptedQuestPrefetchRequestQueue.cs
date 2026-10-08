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
  private readonly object gate = new();

  private readonly Queue<uint> questIds = [];

  private readonly Dictionary<uint, HashSet<string>> requestSourcesByQuestId = [];

  private readonly HashSet<uint> processingQuestIds = [];

  /// <summary>
  ///     Gets the number of priority requests waiting to be prefetched.
  /// </summary>
  public int Count
  {
    get
    {
      lock (this.gate)
      {
        return this.questIds.Count;
      }
    }
  }

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
    lock (this.gate)
    {
      if (this.requestSourcesByQuestId.TryGetValue(questId, out var existingSources))
      {
        existingSources.Add(normalizedSource);
        requestSources = FormatSources(existingSources);
        return false;
      }

      this.requestSourcesByQuestId[questId] = [normalizedSource];
      this.questIds.Enqueue(questId);
      requestSources = normalizedSource;
      return true;
    }
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
    lock (this.gate)
    {
      while (this.questIds.TryDequeue(out var requestedQuestId))
      {
        if (requestedQuestId == 0 ||
            !this.requestSourcesByQuestId.TryGetValue(
                requestedQuestId,
                out var sources))
        {
          continue;
        }

        _ = this.processingQuestIds.Add(requestedQuestId);
        questId = requestedQuestId;
        requestSources = FormatSources(sources);
        return true;
      }
    }

    return false;
  }

  /// <summary>
  ///     Releases a processing quest only after every accepted-quest operation
  ///     has reached a terminal state.
  /// </summary>
  /// <param name="questId">The quest identity to release.</param>
  /// <returns>True when a processing request was completed.</returns>
  public bool Complete(uint questId)
  {
    if (questId == 0)
    {
      return false;
    }

    lock (this.gate)
    {
      if (!this.processingQuestIds.Remove(questId))
      {
        return false;
      }

      _ = this.requestSourcesByQuestId.Remove(questId);
      return true;
    }
  }

  /// <summary>
  ///     Clears every pending priority request.
  /// </summary>
  public void Clear()
  {
    lock (this.gate)
    {
      this.questIds.Clear();
      this.requestSourcesByQuestId.Clear();
      this.processingQuestIds.Clear();
    }
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
