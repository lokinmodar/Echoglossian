// <copyright file="BoundedPriorityQueueTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Threading.Channels;

using Echoglossian.Persistence;

using Xunit;

namespace Echoglossian.Tests.Persistence;

/// <summary>
///     Covers the bounded priority lanes that feed persistence coordination.
/// </summary>
public sealed class BoundedPriorityQueueTests
{
  /// <summary>
  ///     Ensures persistence keys require a stable nonblank domain.
  /// </summary>
  [Fact]
  public void PersistenceWorkKey_WithBlankDomain_ThrowsArgumentException()
  {
    var exception = Assert.Throws<ArgumentException>(
        () => new PersistenceWorkKey(" ", "identity"));

    Assert.Equal("domain", exception.ParamName);
  }

  /// <summary>
  ///     Ensures persistence keys require a stable nonblank canonical identity.
  /// </summary>
  [Fact]
  public void PersistenceWorkKey_WithBlankCanonicalIdentity_ThrowsArgumentException()
  {
    var exception = Assert.Throws<ArgumentException>(
        () => new PersistenceWorkKey("domain", " "));

    Assert.Equal("canonicalIdentity", exception.ParamName);
  }

  /// <summary>
  ///     Ensures the coordinator starts with the approved bounded defaults.
  /// </summary>
  [Fact]
  public void DefaultOptions_ExposeApprovedInternalBounds()
  {
    var options = PersistenceCoordinatorOptions.Default;

    Assert.Equal(64, options.InteractiveCapacity);
    Assert.Equal(256, options.BackgroundCapacity);
    Assert.Equal(2, options.ReaderConcurrency);
    Assert.Equal(32, options.MaxBatchSize);
    Assert.Equal(TimeSpan.FromMilliseconds(5), options.BatchCollectionWindow);
    Assert.Equal(3, options.MaxAttempts);
    Assert.Equal(
        new[] { TimeSpan.FromMilliseconds(25), TimeSpan.FromMilliseconds(100) },
        options.RetryDelays);
    Assert.Equal(4, options.ContextPoolSize);
    Assert.Equal(1, options.SqliteDefaultTimeoutSeconds);
    Assert.Equal(TimeSpan.FromSeconds(5), options.ShutdownTimeout);
  }

  /// <summary>
  ///     Ensures full lanes reject new work without dropping an accepted item.
  /// </summary>
  [Fact]
  public async Task TryEnqueue_WhenLaneIsFull_ReturnsFalseWithoutDroppingAcceptedItem()
  {
    var queue = new BoundedPriorityQueue<string>(1, 1);

    Assert.True(queue.TryEnqueue("I1", PersistencePriority.Interactive));
    Assert.False(queue.TryEnqueue("I2", PersistencePriority.Interactive));
    Assert.Equal(1, queue.InteractiveDepth);
    Assert.Equal("I1", await queue.DequeueAsync(CancellationToken.None));
  }

  /// <summary>
  ///     Ensures continuously ready lanes reserve one in four selections for
  ///     background work.
  /// </summary>
  /// <returns>A task that completes after the eight selected items are verified.</returns>
  [Fact]
  public async Task DequeueAsync_WhenBothLanesStayReady_SelectsThreeInteractiveThenOneBackground()
  {
    var queue = new BoundedPriorityQueue<string>(6, 2);
    foreach (var label in new[] { "I1", "I2", "I3", "I4", "I5", "I6" })
    {
      Assert.True(queue.TryEnqueue(label, PersistencePriority.Interactive));
    }

    Assert.True(queue.TryEnqueue("B1", PersistencePriority.Background));
    Assert.True(queue.TryEnqueue("B2", PersistencePriority.Background));

    var selected = new List<string>();
    for (var index = 0; index < 8; index++)
    {
      selected.Add(await queue.DequeueAsync(CancellationToken.None));
    }

    Assert.Equal(
        new[] { "I1", "I2", "I3", "B1", "I4", "I5", "I6", "B2" },
        selected);
  }

  /// <summary>
  ///     Ensures background work is immediately selected when interactive work
  ///     is unavailable.
  /// </summary>
  /// <returns>A task that completes after the background item is selected.</returns>
  [Fact]
  public async Task DequeueAsync_WhenInteractiveLaneIsEmpty_DoesNotDelayBackground()
  {
    var queue = new BoundedPriorityQueue<string>(1, 1);
    Assert.True(queue.TryEnqueue("B1", PersistencePriority.Background));

    var item = await queue.DequeueAsync(CancellationToken.None);

    Assert.Equal("B1", item);
  }

  /// <summary>
  ///     Ensures a completed queue terminates after every accepted item drains.
  /// </summary>
  /// <returns>A task that completes after closed completion is observed.</returns>
  [Fact]
  public async Task DequeueAsync_AfterCompletionAndDrain_ThrowsChannelClosedException()
  {
    var queue = new BoundedPriorityQueue<string>(1, 1);
    Assert.True(queue.TryEnqueue("I1", PersistencePriority.Interactive));
    queue.Complete();

    Assert.Equal("I1", await queue.DequeueAsync(CancellationToken.None));
    await Assert.ThrowsAsync<ChannelClosedException>(
        async () => await queue.DequeueAsync(CancellationToken.None));
  }
}
