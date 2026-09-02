// <copyright file="PersistenceCoordinatorSqliteTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Data.Common;

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Persistence;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

using Xunit;

namespace Echoglossian.Tests.Persistence;

/// <summary>Verifies deterministic write retries and unchanged suppression.</summary>
public sealed class PersistenceCoordinatorSqliteTests
{
  [Fact]
  public async Task FailedBatch_RollsBackEveryRowAndPublishesNothing()
  {
    await using var owner = await SqliteOwner.CreateAsync();
    var published = 0;
    var collecting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var releaseCollection = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    await using var coordinator = CreateCoordinator(owner.Factory, async (_, _) =>
    {
      collecting.SetResult(true);
      await releaseCollection.Task.ConfigureAwait(false);
    });
    coordinator.TryScheduleWrite(Insert("one", () => Interlocked.Increment(ref published)), out var one);
    await collecting.Task.WaitAsync(TimeSpan.FromSeconds(5));
    coordinator.TryScheduleWrite(new PersistenceWriteRequest(new PersistenceWorkKey("sqlite", "fail"), PersistencePriority.Interactive,
        (_, _) => throw new InvalidOperationException("fail"), () => Interlocked.Increment(ref published)), out var two);
    releaseCollection.SetResult(true);
    await Task.WhenAll(one, two).WaitAsync(TimeSpan.FromSeconds(5));
    await using var check = owner.Factory.CreateDbContext();
    Assert.Empty(check.LlmModelCapabilityObservations);
    Assert.Equal(0, published);
  }

  [Fact]
  public async Task UnchangedMutation_EmitsNoUpdateAndReportsUnchanged()
  {
    await using var owner = await SqliteOwner.CreateAsync();
    await using var coordinator = CreateCoordinator(owner.Factory);
    coordinator.TryScheduleWrite(new PersistenceWriteRequest(new PersistenceWorkKey("sqlite", "unchanged"), PersistencePriority.Interactive,
        (_, _) => Task.FromResult(PersistenceWriteMutation.UnchangedResult), () => { }), out var completion);
    Assert.Equal(PersistenceCompletionStatus.Unchanged, (await completion.WaitAsync(TimeSpan.FromSeconds(5))).Status);
    Assert.Equal(0, owner.Interceptor.UpdateCount);
  }

  [Fact]
  public async Task ChangedMutation_EmitsOneUpdateAndPublishesAfterCommit()
  {
    var commitGate = new CommitGateInterceptor();
    await using var owner = await SqliteOwner.CreateAsync(commitGate);
    await using (var seed = owner.Factory.CreateDbContext()) { seed.LlmModelCapabilityObservations.Add(Observation("row")); await seed.SaveChangesAsync(); }
    commitGate.Enabled = true;
    var published = 0;
    await using var coordinator = CreateCoordinator(owner.Factory);
    coordinator.TryScheduleWrite(new PersistenceWriteRequest(new PersistenceWorkKey("sqlite", "changed"), PersistencePriority.Interactive,
        async (context, token) => { var row = await context.LlmModelCapabilityObservations.SingleAsync(token); row.ProviderErrorCode = "changed"; return PersistenceWriteMutation.ChangedResult; }, () => Interlocked.Increment(ref published)), out var completion);
    await commitGate.CommitEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Equal(0, published);
    Assert.False(completion.IsCompleted);
    commitGate.ReleaseCommit.SetResult(true);
    Assert.Equal(PersistenceCompletionStatus.Succeeded, (await completion.WaitAsync(TimeSpan.FromSeconds(5))).Status);
    Assert.Equal(1, owner.Interceptor.UpdateCount); Assert.Equal(1, published);
  }
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

  [Fact]
  public async Task ThreeBusyFailures_StopWithoutPublicationOrFourthAttempt()
  {
    var factory = new PersistenceCoordinatorTestContextFactory(); var attempts = 0; var published = 0;
    await using var coordinator = new PersistenceCoordinator(factory, PersistenceCoordinatorOptions.Default,
        (_, _) => Task.CompletedTask, transientFailureClassifier: _ => true);
    coordinator.TryScheduleWrite(new PersistenceWriteRequest(new PersistenceWorkKey("retry", "terminal"), PersistencePriority.Interactive,
        (_, _) => { Interlocked.Increment(ref attempts); throw new InvalidOperationException("busy"); }, () => Interlocked.Increment(ref published)), out var completion);
    Assert.Equal(PersistenceCompletionStatus.Failed, (await completion.WaitAsync(TimeSpan.FromSeconds(5))).Status);
    Assert.Equal(3, attempts); Assert.Equal(0, published); Assert.Equal(1, coordinator.GetMetrics().TerminalFailures);
  }

  private static PersistenceCoordinator CreateCoordinator(
      IDbContextFactory<EchoglossianDbContext> factory,
      Func<TimeSpan, CancellationToken, Task>? delayAsync = null) => new(factory,
      new PersistenceCoordinatorOptions(8, 8, 1, 32, TimeSpan.FromMilliseconds(1), 3, [TimeSpan.Zero, TimeSpan.Zero], 4, 1, TimeSpan.FromSeconds(5)), delayAsync);

  private static PersistenceWriteRequest Insert(string model, Action publish) => new(new PersistenceWorkKey("sqlite", model), PersistencePriority.Interactive,
      (context, _) => { context.LlmModelCapabilityObservations.Add(Observation(model)); return Task.FromResult(PersistenceWriteMutation.ChangedResult); }, publish);

  private static LlmModelCapabilityObservation Observation(string model) => new() { Engine = "test", ProviderScope = "test", EndpointScope = "test", ModelId = model, ParameterName = "parameter", StatusCode = 400, MessageExcerpt = "message" };

  private sealed class SqliteOwner : IAsyncDisposable
  {
    private readonly string directory;
    private SqliteOwner(string directory, UpdateInterceptor interceptor, CommitGateInterceptor? commitGate) { this.directory = directory; this.Interceptor = interceptor; this.Factory = new Factory(Path.Combine(directory, "Echoglossian.db"), interceptor, commitGate); }
    internal Factory Factory { get; }
    internal UpdateInterceptor Interceptor { get; }
    internal static async Task<SqliteOwner> CreateAsync(CommitGateInterceptor? commitGate = null) { var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); var owner = new SqliteOwner(directory, new UpdateInterceptor(), commitGate); await using var context = owner.Factory.CreateDbContext(); await context.Database.MigrateAsync(); return owner; }
    public ValueTask DisposeAsync() { SqliteConnection.ClearAllPools(); try { Directory.Delete(this.directory, true); } catch (IOException) { Directory.Delete(this.directory, true); } catch (UnauthorizedAccessException) { Directory.Delete(this.directory, true); } return ValueTask.CompletedTask; }
  }

  private sealed class Factory : IDbContextFactory<EchoglossianDbContext>
  {
    private readonly DbContextOptions<EchoglossianDbContext> options;
    internal Factory(string path, UpdateInterceptor interceptor, CommitGateInterceptor? commitGate) { var builder = new DbContextOptionsBuilder<EchoglossianDbContext>().UseSqlite($"Data Source={path}").AddInterceptors(interceptor); if (commitGate is not null) { builder.AddInterceptors(commitGate); } this.options = builder.Options; }
    public EchoglossianDbContext CreateDbContext() => new(this.options);
    public Task<EchoglossianDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(this.CreateDbContext());
  }

  private sealed class UpdateInterceptor : DbCommandInterceptor
  {
    private int updateCount;
    internal int UpdateCount => Volatile.Read(ref this.updateCount);
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) { if (command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)) { Interlocked.Increment(ref this.updateCount); } return result; }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) { if (command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)) { Interlocked.Increment(ref this.updateCount); } return ValueTask.FromResult(result); }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) { this.Count(command); return result; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) { this.Count(command); return ValueTask.FromResult(result); }
    private void Count(DbCommand command) { if (command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)) { Interlocked.Increment(ref this.updateCount); } }
  }

  private sealed class CommitGateInterceptor : DbTransactionInterceptor
  {
    internal bool Enabled { get; set; }
    internal TaskCompletionSource<bool> CommitEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource<bool> ReleaseCommit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
      if (!this.Enabled)
      {
        return result;
      }

      this.CommitEntered.TrySetResult(true);
      await this.ReleaseCommit.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
      return result;
    }
  }
}
