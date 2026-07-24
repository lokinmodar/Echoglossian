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

    Assert.True(requests.Request(68799));
    Assert.False(requests.Request(68799));
    Assert.Equal(1, requests.Count);
    Assert.True(requests.TryDequeue(out var questId));
    Assert.Equal(68799u, questId);
    Assert.Equal(0, requests.Count);
    Assert.True(requests.Request(68799));
  }

  /// <summary>
  ///     Ensures the UI cannot create a prefetch request without a stable
  ///     accepted quest identity.
  /// </summary>
  [Fact]
  public void Request_RejectsZeroQuestId()
  {
    var requests = new AcceptedQuestPrefetchRequestQueue();

    Assert.False(requests.Request(0));
    Assert.Equal(0, requests.Count);
    Assert.False(requests.TryDequeue(out _));
  }
}
