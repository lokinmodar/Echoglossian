// <copyright file="PersistenceCoordinatorWriteTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;

using Echoglossian.Persistence;

using Xunit;

namespace Echoglossian.Tests.Persistence;

/// <summary>Verifies bounded, serialized persistence-write admission.</summary>
public sealed class PersistenceCoordinatorWriteTests
{
  [Fact]
  public async Task TryScheduleWrite_WhenLaneIsFull_ReturnsRejectedCapacityWithoutDroppingAcceptedWork()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    await using var coordinator = CreateCoordinator(factory, 1);
    coordinator.TryScheduleWrite(Request("run", async (_, _) => { entered.SetResult(true); await release.Task; return PersistenceWriteMutation.UnchangedResult; }), out _);
    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    coordinator.TryScheduleWrite(Request("kept", (_, _) => Task.FromResult(PersistenceWriteMutation.UnchangedResult)), out var kept);
    Assert.Equal(PersistenceAdmissionStatus.RejectedCapacity, coordinator.TryScheduleWrite(Request("reject", (_, _) => Task.FromResult(PersistenceWriteMutation.UnchangedResult)), out var rejected));
    Assert.Equal(PersistenceCompletionStatus.Rejected, (await rejected).Status);
    release.SetResult(true);
    Assert.Equal(PersistenceCompletionStatus.Unchanged, (await kept.WaitAsync(TimeSpan.FromSeconds(5))).Status);
  }

  [Fact]
  public async Task DuplicateAfterClaim_UsesANewBoundedSlotWithoutMutatingActiveRequest()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var active = 0;
    var later = 0;
    await using var coordinator = CreateCoordinator(factory, 2);
    coordinator.TryScheduleWrite(Request("same", async (_, _) => { active++; entered.SetResult(true); await release.Task; return PersistenceWriteMutation.UnchangedResult; }), out var first);
    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Equal(PersistenceAdmissionStatus.Accepted, coordinator.TryScheduleWrite(Request("same", (_, _) => { later++; return Task.FromResult(PersistenceWriteMutation.UnchangedResult); }), out var second));
    Assert.NotSame(first, second);
    release.SetResult(true);
    await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Equal(1, active); Assert.Equal(1, later);
  }

  [Fact]
  public async Task Writer_CollectsCompatibleRequestsIntoOneBoundedBatch()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    var collecting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var releaseCollection = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    await using var coordinator = CreateCoordinator(factory, 4, async (_, _) =>
    {
      collecting.SetResult(true);
      await releaseCollection.Task.ConfigureAwait(false);
    });
    coordinator.TryScheduleWrite(Request("one", (_, _) => Task.FromResult(PersistenceWriteMutation.UnchangedResult)), out var one);
    await collecting.Task.WaitAsync(TimeSpan.FromSeconds(5));
    coordinator.TryScheduleWrite(Request("two", (_, _) => Task.FromResult(PersistenceWriteMutation.UnchangedResult)), out var two);
    releaseCollection.SetResult(true);
    await Task.WhenAll(one, two).WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Equal(1, coordinator.GetMetrics().BatchCount);
    Assert.Single(factory.ContextIds);
  }

  [Fact]
  public async Task Publication_DoesNotRunUntilCommitCompletes()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var published = false;
    await using var coordinator = CreateCoordinator(factory, 2);
    var request = new PersistenceWriteRequest(new PersistenceWorkKey("write", "publication"), PersistencePriority.Interactive,
        async (_, _) => { await gate.Task; return PersistenceWriteMutation.UnchangedResult; }, () => published = true);
    coordinator.TryScheduleWrite(request, out var completion);
    Assert.False(published);
    gate.SetResult(true);
    await completion.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.True(published);
  }
  /// <summary>Ensures write admission transfers ownership without waiting.</summary>
  [Fact]
  public async Task TryScheduleWrite_WhenWorkerIsBlocked_ReturnsAcceptedWithoutWaiting()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    await using var coordinator = CreateCoordinator(factory, 2);

    Assert.Equal(PersistenceAdmissionStatus.Accepted, coordinator.TryScheduleWrite(
        Request("running", async (_, _) =>
        {
          started.SetResult(true);
          await release.Task.ConfigureAwait(false);
          return PersistenceWriteMutation.UnchangedResult;
        }), out _));
    await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

    var status = coordinator.TryScheduleWrite(
        Request("queued", (_, _) => Task.FromResult(PersistenceWriteMutation.UnchangedResult)), out var completion);

    Assert.Equal(PersistenceAdmissionStatus.Accepted, status);
    Assert.False(completion.IsCompleted);
    release.SetResult(true);
  }

  /// <summary>Ensures a pending duplicate replaces its immutable payload.</summary>
  [Fact]
  public async Task DuplicatePendingWrite_ReplacesPayloadWithLatestAndJoinsCompletion()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var observed = 0;
    await using var coordinator = CreateCoordinator(factory, 4);

    Assert.Equal(PersistenceAdmissionStatus.Accepted, coordinator.TryScheduleWrite(
        Request("blocker", async (_, _) =>
        {
          firstStarted.SetResult(true);
          await releaseFirst.Task.ConfigureAwait(false);
          return PersistenceWriteMutation.UnchangedResult;
        }), out _));
    await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Equal(PersistenceAdmissionStatus.Accepted, coordinator.TryScheduleWrite(
        Request("same", (_, _) =>
        {
          observed = 1;
          return Task.FromResult(PersistenceWriteMutation.UnchangedResult);
        }), out var first));
    Assert.Equal(PersistenceAdmissionStatus.Replaced, coordinator.TryScheduleWrite(
        Request("same", (_, _) =>
        {
          observed = 2;
          return Task.FromResult(PersistenceWriteMutation.UnchangedResult);
        }), out var second));

    Assert.Same(first, second);
    releaseFirst.SetResult(true);
    await second.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Equal(2, observed);
  }

  /// <summary>Ensures only the single writer leases a context at a time.</summary>
  [Fact]
  public async Task Writer_NeverUsesMoreThanOneContextConcurrently()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    await using var coordinator = CreateCoordinator(factory, 4);
    var completions = Enumerable.Range(0, 4).Select(index =>
    {
      Assert.Equal(PersistenceAdmissionStatus.Accepted, coordinator.TryScheduleWrite(
          Request(index.ToString(), (_, _) => Task.FromResult(PersistenceWriteMutation.UnchangedResult)), out var completion));
      return completion;
    }).ToArray();

    await Task.WhenAll(completions).WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Equal(1, factory.MaximumConcurrentLeases);
    Assert.Equal(1, coordinator.GetMetrics().MaximumActiveWriters);
  }

  private static PersistenceCoordinator CreateCoordinator(
      PersistenceCoordinatorTestContextFactory factory,
      int capacity,
      Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
  {
    return new PersistenceCoordinator(factory, new PersistenceCoordinatorOptions(
        capacity, capacity, 1, 32, TimeSpan.FromMilliseconds(1), 3,
        new[] { TimeSpan.Zero, TimeSpan.Zero }, 4, 1, TimeSpan.FromSeconds(5)), delayAsync);
  }

  private static PersistenceWriteRequest Request(
      string identity,
      Func<EchoglossianDbContext, CancellationToken, Task<PersistenceWriteMutation>> applyAsync)
  {
    return new PersistenceWriteRequest(
        new PersistenceWorkKey("write", identity),
        PersistencePriority.Interactive,
        applyAsync,
        () => { });
  }
}
