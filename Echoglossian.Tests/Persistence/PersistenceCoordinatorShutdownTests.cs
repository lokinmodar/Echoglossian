// <copyright file="PersistenceCoordinatorShutdownTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.Persistence;

using Xunit;

namespace Echoglossian.Tests.Persistence;

/// <summary>Verifies bounded, idempotent persistence coordinator completion.</summary>
public sealed class PersistenceCoordinatorShutdownTests
{
  /// <summary>Ensures completion closes admission and drains accepted writes.</summary>
  [Fact]
  public async Task CompleteAsync_RejectsNewAdmissionAndDrainsAcceptedWrites()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    await using var coordinator = CreateCoordinator(factory, TimeSpan.FromSeconds(5));
    Assert.Equal(PersistenceAdmissionStatus.Accepted, coordinator.TryScheduleWrite(Request("accepted", async (_, _) =>
    {
      entered.SetResult(true);
      await release.Task.ConfigureAwait(false);
      return PersistenceWriteMutation.UnchangedResult;
    }), out var accepted));
    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

    var completion = coordinator.CompleteAsync();
    Assert.Equal(PersistenceAdmissionStatus.RejectedShutdown, coordinator.TryScheduleWrite(
        Request("later", (_, _) => Task.FromResult(PersistenceWriteMutation.UnchangedResult)), out _));
    release.SetResult(true);
    await completion.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Equal(PersistenceCompletionStatus.Unchanged, (await accepted).Status);
  }

  /// <summary>Ensures concurrent callers share the same completion work.</summary>
  [Fact]
  public async Task CompleteAsync_IsIdempotent()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    await using var coordinator = CreateCoordinator(factory, TimeSpan.FromSeconds(5));
    var first = coordinator.CompleteAsync();
    var second = coordinator.CompleteAsync();

    await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Same(first, second);
  }

  private static PersistenceCoordinator CreateCoordinator(
      PersistenceCoordinatorTestContextFactory factory,
      TimeSpan shutdownTimeout)
  {
    return new PersistenceCoordinator(factory, new PersistenceCoordinatorOptions(
        4, 4, 1, 32, TimeSpan.FromMilliseconds(1), 3,
        new[] { TimeSpan.Zero, TimeSpan.Zero }, 4, 1, shutdownTimeout));
  }

  private static PersistenceWriteRequest Request(
      string identity,
      Func<EchoglossianDbContext, CancellationToken, Task<PersistenceWriteMutation>> applyAsync)
  {
    return new PersistenceWriteRequest(new PersistenceWorkKey("shutdown", identity),
        PersistencePriority.Interactive, applyAsync, () => { });
  }
}
