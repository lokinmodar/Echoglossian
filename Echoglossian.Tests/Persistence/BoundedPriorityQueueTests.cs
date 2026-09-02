// <copyright file="BoundedPriorityQueueTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;
using System.Threading.Channels;

using Echoglossian.EFCoreSqlite;
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
  ///     Ensures write requests retain the positional record contract consumed
  ///     by persistence callers.
  /// </summary>
  [Fact]
  public void PersistenceWriteRequest_SupportsPositionalDeconstruction()
  {
    var key = new PersistenceWorkKey("capability", "model-a");
    Func<EchoglossianDbContext, CancellationToken, Task<PersistenceWriteMutation>> applyAsync =
        (_, _) => Task.FromResult(PersistenceWriteMutation.UnchangedResult);
    Action publishAfterCommit = () => { };
    var request = new PersistenceWriteRequest(
        key,
        PersistencePriority.Background,
        applyAsync,
        publishAfterCommit);

    var (deconstructedKey, priority, deconstructedApplyAsync, publication) = request;

    Assert.Equal(key, deconstructedKey);
    Assert.Equal(PersistencePriority.Background, priority);
    Assert.Same(applyAsync, deconstructedApplyAsync);
    Assert.Same(publishAfterCommit, publication);
  }

  /// <summary>
  ///     Ensures write requests reject a missing transactional mutation.
  /// </summary>
  [Fact]
  public void PersistenceWriteRequest_WithNullApplyAsync_ThrowsArgumentNullException()
  {
    var exception = Assert.Throws<ArgumentNullException>(
        () => new PersistenceWriteRequest(
            new PersistenceWorkKey("capability", "model-a"),
            PersistencePriority.Background,
            null!,
            () => { }));

    Assert.Equal("ApplyAsync", exception.ParamName);
  }

  /// <summary>
  ///     Ensures write requests reject a missing post-commit publication.
  /// </summary>
  [Fact]
  public void PersistenceWriteRequest_WithNullPublishAfterCommit_ThrowsArgumentNullException()
  {
    Func<EchoglossianDbContext, CancellationToken, Task<PersistenceWriteMutation>> applyAsync =
        (_, _) => Task.FromResult(PersistenceWriteMutation.UnchangedResult);
    var exception = Assert.Throws<ArgumentNullException>(
        () => new PersistenceWriteRequest(
            new PersistenceWorkKey("capability", "model-a"),
            PersistencePriority.Background,
            applyAsync,
            null!));

    Assert.Equal("PublishAfterCommit", exception.ParamName);
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
  ///     Ensures concurrent readers consume one background item in each first
  ///     four-item selection while both lanes are ready.
  /// </summary>
  /// <returns>A task that completes after concurrent selection is verified.</returns>
  [Fact]
  public async Task ConcurrentDequeueAsync_WhenBothLanesAreReady_PreservesThreeToOneBudget()
  {
    for (var iteration = 0; iteration < 32; iteration++)
    {
      var queue = new BoundedPriorityQueue<string>(4, 1);
      foreach (var label in new[] { "I1", "I2", "I3", "I4" })
      {
        Assert.True(queue.TryEnqueue(label, PersistencePriority.Interactive));
      }

      Assert.True(queue.TryEnqueue("B1", PersistencePriority.Background));
      var start = new TaskCompletionSource<bool>(
          TaskCreationOptions.RunContinuationsAsynchronously);
      var selections = Enumerable.Range(0, 4)
          .Select(
              _ => Task.Run(
                  async () =>
                  {
                    await start.Task.ConfigureAwait(false);
                    return await queue.DequeueAsync(CancellationToken.None).ConfigureAwait(false);
                  }))
          .ToArray();

      start.SetResult(true);
      var selected = await Task.WhenAll(selections).WaitAsync(TimeSpan.FromSeconds(5));

      Assert.Equal(3, selected.Count(item => item.StartsWith("I", StringComparison.Ordinal)));
      Assert.Equal(1, selected.Count(item => item.StartsWith("B", StringComparison.Ordinal)));
    }
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

  /// <summary>
  ///     Ensures concurrent writers and a live reader never expose a negative
  ///     depth or lose an accepted item between channel admission and counting.
  /// </summary>
  /// <returns>A task that completes after concurrent work drains.</returns>
  [Fact]
  public async Task ConcurrentEnqueueAndDequeue_MaintainsNonnegativeDepth()
  {
    const int producerCount = 4;
    const int itemsPerProducer = 500;
    var queue = new BoundedPriorityQueue<int>(64, 1);
    var start = new TaskCompletionSource<bool>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var producers = Enumerable.Range(0, producerCount)
        .Select(
            producer => Task.Run(
                async () =>
                {
                  await start.Task.ConfigureAwait(false);
                  for (var item = 0; item < itemsPerProducer; item++)
                  {
                    while (!queue.TryEnqueue(
                        (producer * itemsPerProducer) + item,
                        PersistencePriority.Interactive))
                    {
                      Thread.Yield();
                    }
                  }
                }))
        .ToArray();
    var consumer = Task.Run(
        async () =>
        {
          await start.Task.ConfigureAwait(false);
          for (var item = 0; item < producerCount * itemsPerProducer; item++)
          {
            _ = await queue.DequeueAsync(CancellationToken.None).ConfigureAwait(false);
            Assert.InRange(queue.InteractiveDepth, 0, 64);
          }
        });

    start.SetResult(true);
    await Task.WhenAll(producers.Append(consumer)).WaitAsync(TimeSpan.FromSeconds(5));

    Assert.Equal(0, queue.InteractiveDepth);
  }

  /// <summary>
  ///     Ensures a blocked reader does not close before all concurrent
  ///     admission that reports success has drained during completion.
  /// </summary>
  /// <returns>A task that completes after completion races are verified.</returns>
  [Fact]
  public async Task ConcurrentCompletionAndAdmission_DrainsEveryReportedAcceptedItem()
  {
    for (var iteration = 0; iteration < 100; iteration++)
    {
      var queue = new BoundedPriorityQueue<int>(16, 1);
      var start = new TaskCompletionSource<bool>(
          TaskCreationOptions.RunContinuationsAsynchronously);
      var accepted = new ConcurrentBag<int>();
      var blockedDequeue = queue.DequeueAsync(CancellationToken.None).AsTask();
      var enqueuers = Enumerable.Range(0, 8)
          .Select(
              item => Task.Run(
                  async () =>
                  {
                    await start.Task.ConfigureAwait(false);
                    if (queue.TryEnqueue(item, PersistencePriority.Interactive))
                    {
                      accepted.Add(item);
                    }
                  }))
          .ToArray();
      var completion = Task.Run(
          async () =>
          {
            await start.Task.ConfigureAwait(false);
            queue.Complete();
          });

      start.SetResult(true);
      await Task.WhenAll(enqueuers.Append(completion)).WaitAsync(TimeSpan.FromSeconds(5));

      if (accepted.IsEmpty)
      {
        await Assert.ThrowsAsync<ChannelClosedException>(async () => await blockedDequeue);
        continue;
      }

      var drained = new List<int> { await blockedDequeue.WaitAsync(TimeSpan.FromSeconds(5)) };
      while (drained.Count < accepted.Count)
      {
        drained.Add(
            await queue.DequeueAsync(CancellationToken.None).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5)));
      }

      Assert.Equal(accepted.Order(), drained.Order());
    }
  }
}
