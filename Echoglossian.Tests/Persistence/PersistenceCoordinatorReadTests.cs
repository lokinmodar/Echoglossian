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
  ///     Ensures transient read failures use the coordinator's bounded retry
  ///     policy and lease a fresh context for every attempt.
  /// </summary>
  /// <returns>A task that completes after the retried read succeeds.</returns>
  [Fact]
  public async Task TryScheduleRead_WhenTransientFailuresOccur_RetriesWithFreshContexts()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    var attempts = 0;
    var publications = 0;
    var delays = new List<TimeSpan>();
    await using var coordinator = new PersistenceCoordinator(
        factory,
        new PersistenceCoordinatorOptions(
            4,
            4,
            1,
            32,
            TimeSpan.FromMilliseconds(5),
            3,
            new[] { TimeSpan.FromMilliseconds(25), TimeSpan.FromMilliseconds(100) },
            4,
            1,
            TimeSpan.FromSeconds(5)),
        (delay, _) =>
        {
          delays.Add(delay);
          return Task.CompletedTask;
        },
        transientFailureClassifier: _ => true);

    Assert.Equal(
        PersistenceAdmissionStatus.Accepted,
        coordinator.TryScheduleRead(
            new PersistenceWorkKey("test", "transient-read"),
            PersistencePriority.Interactive,
            (_, _) =>
            {
              if (Interlocked.Increment(ref attempts) < 3)
              {
                throw new InvalidOperationException("transient read failure");
              }

              return Task.FromResult("recovered");
            },
            _ => publications++,
            out var completion));

    var result = await completion.WaitAsync(TimeSpan.FromSeconds(5));

    Assert.Equal(PersistenceCompletionStatus.Succeeded, result.Status);
    Assert.Equal("recovered", result.Value);
    Assert.Equal(3, attempts);
    Assert.Equal(
        new[] { TimeSpan.FromMilliseconds(25), TimeSpan.FromMilliseconds(100) },
        delays);
    Assert.Equal(3, factory.ContextIds.Count);
    Assert.Equal(1, publications);
    Assert.Equal(2, coordinator.GetMetrics().RetryCount);
    Assert.Equal(0, coordinator.GetMetrics().TerminalFailures);
  }

  /// <summary>
  ///     Ensures a projection failure does not repeat a successful database
  ///     read or invoke the projection more than once.
  /// </summary>
  /// <returns>A task that completes after the projection fails.</returns>
  [Fact]
  public async Task TryScheduleRead_WhenPublishThrows_DoesNotRetryReadOrPublish()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    var attempts = 0;
    var publications = 0;
    await using var coordinator = new PersistenceCoordinator(
        factory,
        new PersistenceCoordinatorOptions(
            4,
            4,
            1,
            32,
            TimeSpan.FromMilliseconds(5),
            3,
            new[] { TimeSpan.Zero, TimeSpan.Zero },
            4,
            1,
            TimeSpan.FromSeconds(5)),
        transientFailureClassifier: _ => true);

    Assert.Equal(
        PersistenceAdmissionStatus.Accepted,
        coordinator.TryScheduleRead(
            new PersistenceWorkKey("test", "publish-failure"),
            PersistencePriority.Interactive,
            (_, _) =>
            {
              attempts++;
              return Task.FromResult("value");
            },
            _ =>
            {
              publications++;
              throw new InvalidOperationException("projection failure");
            },
            out var completion));

    var result = await completion.WaitAsync(TimeSpan.FromSeconds(5));

    Assert.Equal(PersistenceCompletionStatus.Failed, result.Status);
    Assert.Equal(1, attempts);
    Assert.Equal(1, publications);
    Assert.Single(factory.ContextIds);
    Assert.Equal(0, coordinator.GetMetrics().RetryCount);
    Assert.Equal(1, coordinator.GetMetrics().TerminalFailures);
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

  /// <summary>
  ///     Ensures a hostile error sink cannot prevent a failed read completion
  ///     or stop the reader that processes later work.
  /// </summary>
  /// <returns>A task that completes after the later read succeeds.</returns>
  [Fact]
  public async Task TryScheduleRead_WhenErrorLogThrows_CompletesFailureAndProcessesLaterRead()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    await using var coordinator = new PersistenceCoordinator(
        factory,
        new PersistenceCoordinatorOptions(
            4,
            4,
            1,
            32,
            TimeSpan.FromMilliseconds(5),
            3,
            new[] { TimeSpan.FromMilliseconds(25), TimeSpan.FromMilliseconds(100) },
            4,
            1,
            TimeSpan.FromSeconds(5)),
        errorLog: _ => throw new InvalidOperationException("hostile sink"));

    Assert.Equal(
        PersistenceAdmissionStatus.Accepted,
        coordinator.TryScheduleRead<string>(
            new PersistenceWorkKey("test", "failure"),
            PersistencePriority.Interactive,
            (_, _) => throw new InvalidOperationException("query failure"),
            null,
            out var failedCompletion));

    var failure = await failedCompletion.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Equal(PersistenceCompletionStatus.Failed, failure.Status);

    Assert.Equal(
        PersistenceAdmissionStatus.Accepted,
        coordinator.TryScheduleRead(
            new PersistenceWorkKey("test", "later"),
            PersistencePriority.Interactive,
            (_, _) => Task.FromResult("later value"),
            null,
            out var laterCompletion));

    var later = await laterCompletion.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Equal(PersistenceCompletionStatus.Succeeded, later.Status);
    Assert.Equal("later value", later.Value);
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
