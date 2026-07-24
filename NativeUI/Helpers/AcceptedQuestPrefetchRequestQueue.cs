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

  private readonly HashSet<uint> queuedQuestIds = [];

  /// <summary>
  ///     Gets the number of priority requests waiting to be prefetched.
  /// </summary>
  public int Count => this.questIds.Count;

  /// <summary>
  ///     Adds an accepted quest to the priority queue if it is not already
  ///     waiting to be processed.
  /// </summary>
  /// <param name="questId">The accepted quest identifier.</param>
  /// <returns>True when the request was added.</returns>
  public bool Request(uint questId)
  {
    if (questId == 0 || !this.queuedQuestIds.Add(questId))
    {
      return false;
    }

    this.questIds.Enqueue(questId);
    return true;
  }

  /// <summary>
  ///     Tries to dequeue the next accepted quest requested by a visible
  ///     quest surface.
  /// </summary>
  /// <param name="questId">The requested accepted quest identifier.</param>
  /// <returns>True when a request was available.</returns>
  public bool TryDequeue(out uint questId)
  {
    questId = 0;
    while (this.questIds.TryDequeue(out var requestedQuestId))
    {
      this.queuedQuestIds.Remove(requestedQuestId);
      if (requestedQuestId == 0)
      {
        continue;
      }

      questId = requestedQuestId;
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
    this.queuedQuestIds.Clear();
  }
}
