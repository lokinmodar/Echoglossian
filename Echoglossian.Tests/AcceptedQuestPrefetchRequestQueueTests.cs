// <copyright file="AcceptedQuestPrefetchRequestQueueTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers priority requests from visible quest surfaces to the shared
///     accepted-quest prefetch runtime.
/// </summary>
public class AcceptedQuestPrefetchRequestQueueTests
{
  /// <summary>
  ///     Ensures repeated UI refreshes request a quest only once until the
  ///     background prefetch runtime dequeues it.
  /// </summary>
  [Fact]
  public void Request_DeduplicatesQuestUntilDequeued()
  {
    var requests = new AcceptedQuestPrefetchRequestQueue();

    Assert.True(requests.Request(68799, "JournalHandler.Translate", out var sources));
    Assert.Equal("JournalHandler.Translate", sources);
    Assert.False(requests.Request(68799, "JournalHandler.Translate", out sources));
    Assert.Equal("JournalHandler.Translate", sources);
    Assert.Equal(1, requests.Count);
    Assert.True(requests.TryDequeue(out var questId, out sources));
    Assert.Equal(68799u, questId);
    Assert.Equal("JournalHandler.Translate", sources);
    Assert.Equal(0, requests.Count);
    Assert.True(requests.Request(68799, "JournalHandler.Translate", out sources));
  }

  /// <summary>
  ///     Ensures the UI cannot create a prefetch request without a stable
  ///     accepted quest identity.
  /// </summary>
  [Fact]
  public void Request_RejectsZeroQuestId()
  {
    var requests = new AcceptedQuestPrefetchRequestQueue();

    Assert.False(requests.Request(0, "JournalHandler.Translate", out var sources));
    Assert.Equal(string.Empty, sources);
    Assert.Equal(0, requests.Count);
    Assert.False(requests.TryDequeue(out _, out _));
  }

  /// <summary>
  ///     Ensures deduplicated requests preserve every visible quest surface
  ///     that asked for the same accepted quest before the runtime dequeues it.
  /// </summary>
  [Fact]
  public void Request_MergesDistinctSourcesUntilDequeued()
  {
    var requests = new AcceptedQuestPrefetchRequestQueue();

    Assert.True(requests.Request(68799, "JournalHandler.Translate", out var sources));
    Assert.Equal("JournalHandler.Translate", sources);
    Assert.False(requests.Request(68799, "ToDoListHandler.Refresh", out sources));
    Assert.Equal(
        "JournalHandler.Translate|ToDoListHandler.Refresh",
        sources);

    Assert.True(requests.TryDequeue(out var questId, out sources));
    Assert.Equal(68799u, questId);
    Assert.Equal(
        "JournalHandler.Translate|ToDoListHandler.Refresh",
        sources);
  }
}
