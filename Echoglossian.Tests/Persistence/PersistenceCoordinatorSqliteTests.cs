// <copyright file="PersistenceCoordinatorSqliteTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.Persistence;

using Xunit;

namespace Echoglossian.Tests.Persistence;

/// <summary>Verifies deterministic write retries and unchanged suppression.</summary>
public sealed class PersistenceCoordinatorSqliteTests
{
  /// <summary>Ensures retry-safe work is attempted three times with two delays.</summary>
  [Fact]
  public async Task BusyThenBusyThenSuccess_UsesExactlyThreeAttemptsAndTwoDelays()
  {
    var factory = new PersistenceCoordinatorTestContextFactory();
    var attempts = 0;
    var delays = new List<TimeSpan>();
    await using var coordinator = new PersistenceCoordinator(
        factory,
        new PersistenceCoordinatorOptions(4, 4, 1, 32, TimeSpan.FromMilliseconds(1), 3,
            new[] { TimeSpan.FromMilliseconds(25), TimeSpan.FromMilliseconds(100) }, 4, 1, TimeSpan.FromSeconds(5)),
        (delay, _) =>
        {
          delays.Add(delay);
          return Task.CompletedTask;
        },
        transientFailureClassifier: _ => true);
    var request = new PersistenceWriteRequest(new PersistenceWorkKey("retry", "three"), PersistencePriority.Interactive,
        (_, _) =>
        {
          if (Interlocked.Increment(ref attempts) < 3)
          {
            throw new InvalidOperationException("busy");
          }

          return Task.FromResult(PersistenceWriteMutation.UnchangedResult);
        }, () => { });

    Assert.Equal(PersistenceAdmissionStatus.Accepted, coordinator.TryScheduleWrite(request, out var completion));
    Assert.Equal(PersistenceCompletionStatus.Unchanged, (await completion.WaitAsync(TimeSpan.FromSeconds(5))).Status);
    Assert.Equal(3, attempts);
    Assert.Equal(
        new[] { TimeSpan.FromMilliseconds(25), TimeSpan.FromMilliseconds(100) },
        delays.Skip(1));
    Assert.Equal(2, coordinator.GetMetrics().RetryCount);
  }
}
