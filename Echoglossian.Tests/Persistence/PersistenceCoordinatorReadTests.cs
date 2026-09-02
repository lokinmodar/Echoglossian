// <copyright file="PersistenceCoordinatorReadTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;

using Echoglossian.Persistence;

using Xunit;

namespace Echoglossian.Tests.Persistence;

/// <summary>
///     Covers bounded, coalesced persistence reads.
/// </summary>
public sealed class PersistenceCoordinatorReadTests
{
  /// <summary>
  ///     Ensures admission does not wait for a read worker already executing.
  /// </summary>
  [Fact]
  public async Task TryScheduleRead_WhenWorkerIsBlocked_ReturnsAcceptedWithoutWaiting()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    using var queryStarted = new ManualResetEventSlim();
    var releaseQuery = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var coordinator = this.CreateCoordinator(factory, readerConcurrency: 1);
    try
    {
      Assert.Equal(
          PersistenceAdmissionStatus.Accepted,
          coordinator.TryScheduleRead(
              new PersistenceWorkKey("test", "blocked"),
              PersistencePriority.Interactive,
              async (_, _) =>
              {
                queryStarted.Set();
                await releaseQuery.Task.ConfigureAwait(false);
                return "blocked";
              },
              null,
              out _));
      Assert.True(queryStarted.Wait(TimeSpan.FromSeconds(5)));

      var status = coordinator.TryScheduleRead(
          new PersistenceWorkKey("test", "queued"),
          PersistencePriority.Interactive,
          (_, _) => Task.FromResult("queued"),
          null,
          out var completion);

      Assert.Equal(PersistenceAdmissionStatus.Accepted, status);
      Assert.False(completion.IsCompleted);
      releaseQuery.SetResult(true);
    }
    finally
    {
      await coordinator.DisposeAsync();
    }
  }

  /// <summary>
  ///     Ensures a full background lane immediately rejects more background work.
  /// </summary>
  [Fact]
  public async Task TryScheduleRead_WhenBackgroundLaneIsFull_ReturnsRejectedCapacityImmediately()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    using var queryStarted = new ManualResetEventSlim();
    var releaseQuery = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var coordinator = this.CreateCoordinator(factory, readerConcurrency: 1, backgroundCapacity: 1);
    try
    {
      Assert.Equal(
          PersistenceAdmissionStatus.Accepted,
          coordinator.TryScheduleRead(
              new PersistenceWorkKey("test", "running"),
              PersistencePriority.Interactive,
              async (_, _) =>
              {
                queryStarted.Set();
                await releaseQuery.Task.ConfigureAwait(false);
                return "running";
              },
              null,
              out _));
      Assert.True(queryStarted.Wait(TimeSpan.FromSeconds(5)));
      Assert.Equal(
          PersistenceAdmissionStatus.Accepted,
          coordinator.TryScheduleRead(
              new PersistenceWorkKey("test", "background-1"),
              PersistencePriority.Background,
              (_, _) => Task.FromResult("background-1"),
              null,
              out _));

      var status = coordinator.TryScheduleRead(
          new PersistenceWorkKey("test", "background-2"),
          PersistencePriority.Background,
          (_, _) => Task.FromResult("background-2"),
          null,
          out var completion);

      Assert.Equal(PersistenceAdmissionStatus.RejectedCapacity, status);
      Assert.True(completion.IsCompleted);
      Assert.Equal(PersistenceCompletionStatus.Rejected, (await completion).Status);
      releaseQuery.SetResult(true);
    }
    finally
    {
      await coordinator.DisposeAsync();
    }
  }

  /// <summary>
  ///     Ensures interactive admission retains capacity when background is full.
  /// </summary>
  [Fact]
  public async Task TryScheduleRead_WhenBackgroundIsFull_StillUsesReservedInteractiveCapacity()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    using var queryStarted = new ManualResetEventSlim();
    var releaseQuery = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var coordinator = this.CreateCoordinator(factory, readerConcurrency: 1, interactiveCapacity: 1, backgroundCapacity: 1);
    try
    {
      Assert.Equal(
          PersistenceAdmissionStatus.Accepted,
          coordinator.TryScheduleRead(
              new PersistenceWorkKey("test", "running"),
              PersistencePriority.Interactive,
              async (_, _) =>
              {
                queryStarted.Set();
                await releaseQuery.Task.ConfigureAwait(false);
                return "running";
              },
              null,
              out _));
      Assert.True(queryStarted.Wait(TimeSpan.FromSeconds(5)));
      Assert.Equal(
          PersistenceAdmissionStatus.Accepted,
          coordinator.TryScheduleRead(
              new PersistenceWorkKey("test", "background"),
              PersistencePriority.Background,
              (_, _) => Task.FromResult("background"),
              null,
              out _));

      var status = coordinator.TryScheduleRead(
          new PersistenceWorkKey("test", "interactive"),
          PersistencePriority.Interactive,
          (_, _) => Task.FromResult("interactive"),
          null,
          out _);

      Assert.Equal(PersistenceAdmissionStatus.Accepted, status);
      releaseQuery.SetResult(true);
    }
    finally
    {
      await coordinator.DisposeAsync();
    }
  }

  /// <summary>
  ///     Ensures duplicate in-flight reads share one query and publication.
  /// </summary>
  /// <returns>A task that completes after both callers observe the shared result.</returns>
  [Fact]
  public async Task TryScheduleRead_WithSameInFlightKey_JoinsOneQueryAndOnePublication()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    using var queryStarted = new ManualResetEventSlim();
    var releaseQuery = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var queryCount = 0;
    var publicationCount = 0;
    await using var coordinator = this.CreateCoordinator(factory, readerConcurrency: 1);
    var key = new PersistenceWorkKey("test", "same");

    var firstStatus = coordinator.TryScheduleRead(
        key,
        PersistencePriority.Interactive,
        async (_, _) =>
        {
          Interlocked.Increment(ref queryCount);
          queryStarted.Set();
          await releaseQuery.Task.ConfigureAwait(false);
          return "value";
        },
        _ => Interlocked.Increment(ref publicationCount),
        out var firstCompletion);
    Assert.True(queryStarted.Wait(TimeSpan.FromSeconds(5)));
    var secondStatus = coordinator.TryScheduleRead(
        key,
        PersistencePriority.Interactive,
        (_, _) => Task.FromResult("other"),
        _ => Interlocked.Increment(ref publicationCount),
        out var secondCompletion);

    Assert.Equal(PersistenceAdmissionStatus.Accepted, firstStatus);
    Assert.Equal(PersistenceAdmissionStatus.Joined, secondStatus);
    releaseQuery.SetResult(true);
    var results = await Task.WhenAll(firstCompletion, secondCompletion).WaitAsync(TimeSpan.FromSeconds(5));

    Assert.All(results, result => Assert.Equal(PersistenceCompletionStatus.Succeeded, result.Status));
    Assert.Equal(1, queryCount);
    Assert.Equal(1, publicationCount);
  }

  /// <summary>
  ///     Ensures concurrently executing reads have distinct contexts and obey
  ///     the configured reader bound.
  /// </summary>
  /// <returns>A task that completes after the reader bound is observed.</returns>
  [Fact]
  public async Task ConcurrentReads_LeaseDistinctContextsAndRespectReaderConcurrency()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    using var bothQueriesStarted = new ManualResetEventSlim();
    var releaseQueries = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var startedCount = 0;
    var contextIds = new ConcurrentBag<int>();
    await using var coordinator = this.CreateCoordinator(factory, readerConcurrency: 2);
    var completions = new List<Task<PersistenceReadResult<string>>>();
    for (var index = 0; index < 3; index++)
    {
      var status = coordinator.TryScheduleRead(
          new PersistenceWorkKey("test", $"concurrent-{index}"),
          PersistencePriority.Interactive,
          async (context, _) =>
          {
            contextIds.Add(factory.GetContextId(context));
            if (Interlocked.Increment(ref startedCount) == 2)
            {
              bothQueriesStarted.Set();
            }

            await releaseQueries.Task.ConfigureAwait(false);
            return "value";
          },
          null,
          out var completion);
      Assert.Equal(PersistenceAdmissionStatus.Accepted, status);
      completions.Add(completion);
    }

    Assert.True(bothQueriesStarted.Wait(TimeSpan.FromSeconds(5)));
    Assert.Equal(2, factory.MaximumConcurrentLeases);
    Assert.Equal(2, contextIds.Distinct().Count());
    releaseQueries.SetResult(true);
    await Task.WhenAll(completions).WaitAsync(TimeSpan.FromSeconds(5));

    Assert.Equal(3, factory.ContextIds.Distinct().Count());
    Assert.Equal(2, factory.MaximumConcurrentLeases);
  }

  /// <summary>
  ///     Ensures stopped admission rejects later reads without running a query.
  /// </summary>
  [Fact]
  public async Task TryScheduleRead_AfterStopAccepting_ReturnsRejectedShutdown()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    var coordinator = this.CreateCoordinator(factory);
    try
    {
      coordinator.StopAccepting();

      var status = coordinator.TryScheduleRead(
          new PersistenceWorkKey("test", "stopped"),
          PersistencePriority.Interactive,
          (_, _) => Task.FromResult("value"),
          null,
          out var completion);

      Assert.Equal(PersistenceAdmissionStatus.RejectedShutdown, status);
      Assert.True(completion.IsCompleted);
      Assert.Equal(PersistenceCompletionStatus.Rejected, (await completion).Status);
      Assert.Empty(factory.ContextIds);
    }
    finally
    {
      await coordinator.DisposeAsync();
    }
  }

  private PersistenceCoordinator CreateCoordinator(
      PersistenceCoordinatorTestContextFactory factory,
      int interactiveCapacity = 4,
      int backgroundCapacity = 4,
      int readerConcurrency = 2)
  {
    return new PersistenceCoordinator(
        factory,
        new PersistenceCoordinatorOptions(
            interactiveCapacity,
            backgroundCapacity,
            readerConcurrency,
            32,
            TimeSpan.FromMilliseconds(5),
            3,
            new[] { TimeSpan.FromMilliseconds(25), TimeSpan.FromMilliseconds(100) },
            4,
            1,
            TimeSpan.FromSeconds(5)));
  }
}
