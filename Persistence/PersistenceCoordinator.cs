// <copyright file="PersistenceCoordinator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Threading.Channels;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Echoglossian.Persistence;

/// <summary>
///     Owns bounded, coalesced asynchronous persistence operations.
/// </summary>
internal sealed class PersistenceCoordinator : IPersistenceCoordinator
{
  private readonly object admissionGate = new();
  private readonly IDbContextFactory<EchoglossianDbContext> contextFactory;
  private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
  private readonly Action<string>? errorLog;
  private readonly Dictionary<PersistenceWorkKey, ReadWork> inFlightReads = new();
  private readonly Dictionary<PersistenceWorkKey, WriteWork> pendingWrites = new();
  private readonly PersistenceCoordinatorMetrics metrics = new();
  private readonly PersistenceCoordinatorOptions options;
  private readonly BoundedPriorityQueue<ReadWork> readQueue;
  private readonly CancellationTokenSource shutdownCancellation = new();
  private readonly Func<Exception, bool> transientFailureClassifier;
  private readonly Func<DateTimeOffset> utcNow;
  private readonly Action<string>? warningLog;
  private readonly Task[] readerWorkers;
  private readonly BoundedPriorityQueue<WriteWork> writeQueue;
  private readonly Task writerWorker;
  private Task? completionTask;
  private int accepting = 1;

  /// <summary>
  ///     Initializes a new instance of the <see cref="PersistenceCoordinator" /> class.
  /// </summary>
  /// <param name="contextFactory">The factory for short-lived contexts.</param>
  /// <param name="options">The immutable coordinator bounds.</param>
  /// <param name="delayAsync">The worker retry delay seam.</param>
  /// <param name="utcNow">The UTC clock seam.</param>
  /// <param name="transientFailureClassifier">The worker retry classifier seam.</param>
  /// <param name="warningLog">The summarized warning sink.</param>
  /// <param name="errorLog">The summarized error sink.</param>
  internal PersistenceCoordinator(
      IDbContextFactory<EchoglossianDbContext> contextFactory,
      PersistenceCoordinatorOptions? options = null,
      Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
      Func<DateTimeOffset>? utcNow = null,
      Func<Exception, bool>? transientFailureClassifier = null,
      Action<string>? warningLog = null,
      Action<string>? errorLog = null)
  {
    ArgumentNullException.ThrowIfNull(contextFactory);
    this.contextFactory = contextFactory;
    options ??= PersistenceCoordinatorOptions.Default;
    this.options = options;
    this.readQueue = new BoundedPriorityQueue<ReadWork>(
        options.InteractiveCapacity,
        options.BackgroundCapacity);
    this.writeQueue = new BoundedPriorityQueue<WriteWork>(
        options.InteractiveCapacity,
        options.BackgroundCapacity);
    this.delayAsync = delayAsync ?? Task.Delay;
    this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    this.transientFailureClassifier = transientFailureClassifier ?? IsSqliteBusyOrLocked;
    this.warningLog = warningLog;
    this.errorLog = errorLog;
    this.readerWorkers = Enumerable.Range(0, options.ReaderConcurrency)
        .Select(_ => Task.Run(this.RunReadWorkerAsync))
        .ToArray();
    this.writerWorker = Task.Run(this.RunWriterWorkerAsync);
  }

  /// <inheritdoc />
  public PersistenceAdmissionStatus TryScheduleRead<T>(
      PersistenceWorkKey key,
      PersistencePriority priority,
      Func<EchoglossianDbContext, CancellationToken, Task<T>> readAsync,
      Action<T>? publish,
      out Task<PersistenceReadResult<T>> completion)
  {
    ArgumentNullException.ThrowIfNull(readAsync);
    lock (this.admissionGate)
    {
      if (Volatile.Read(ref this.accepting) == 0)
      {
        this.metrics.RecordRejected();
        completion = Task.FromResult(
            new PersistenceReadResult<T>(PersistenceCompletionStatus.Rejected, default, null));
        return PersistenceAdmissionStatus.RejectedShutdown;
      }

      if (this.inFlightReads.TryGetValue(key, out var existing))
      {
        if (existing is ReadWork<T> typedExisting)
        {
          this.metrics.RecordCoalesced();
          completion = typedExisting.Completion;
          return PersistenceAdmissionStatus.Joined;
        }

        throw new InvalidOperationException(
            "A persistence read key cannot coalesce different result types.");
      }

      var work = new ReadWork<T>(key, readAsync, publish, this.utcNow());
      this.inFlightReads.Add(key, work);
      if (!this.readQueue.TryEnqueue(work, priority))
      {
        _ = this.inFlightReads.Remove(key);
        this.metrics.RecordRejected();
        completion = Task.FromResult(
            new PersistenceReadResult<T>(PersistenceCompletionStatus.Rejected, default, null));
        return PersistenceAdmissionStatus.RejectedCapacity;
      }

      this.metrics.RecordAccepted();
      this.metrics.ObserveReadDepths(
          this.readQueue.InteractiveDepth,
          this.readQueue.BackgroundDepth);
      completion = work.Completion;
      return PersistenceAdmissionStatus.Accepted;
    }
  }

  /// <inheritdoc />
  public PersistenceAdmissionStatus TryScheduleWrite(
      PersistenceWriteRequest request,
      out Task<PersistenceWriteResult> completion)
  {
    ArgumentNullException.ThrowIfNull(request);
    lock (this.admissionGate)
    {
      if (Volatile.Read(ref this.accepting) == 0)
      {
        this.metrics.RecordRejected();
        completion = RejectedWriteCompletion;
        return PersistenceAdmissionStatus.RejectedShutdown;
      }

      if (this.pendingWrites.TryGetValue(request.Key, out var existing))
      {
        existing.Replace(request);
        this.metrics.RecordCoalesced();
        completion = existing.Completion;
        return PersistenceAdmissionStatus.Replaced;
      }

      var work = new WriteWork(request, this.utcNow());
      if (!this.writeQueue.TryEnqueue(work, request.Priority))
      {
        this.metrics.RecordRejected();
        completion = RejectedWriteCompletion;
        return PersistenceAdmissionStatus.RejectedCapacity;
      }

      this.pendingWrites.Add(request.Key, work);
      this.metrics.RecordAccepted();
      this.metrics.ObserveWriteDepths(
          this.writeQueue.InteractiveDepth,
          this.writeQueue.BackgroundDepth);
      completion = work.Completion;
      return PersistenceAdmissionStatus.Accepted;
    }
  }

  /// <inheritdoc />
  public PersistenceMetricsSnapshot GetMetrics()
  {
    return this.metrics.Snapshot(
        this.readQueue.InteractiveDepth,
        this.readQueue.BackgroundDepth,
        this.writeQueue.InteractiveDepth,
        this.writeQueue.BackgroundDepth,
        this.GetOldestQueuedAge());
  }

  /// <inheritdoc />
  public void StopAccepting()
  {
    lock (this.admissionGate)
    {
      if (Interlocked.Exchange(ref this.accepting, 0) == 0)
      {
        return;
      }

      this.readQueue.Complete();
      this.writeQueue.Complete();
    }
  }

  /// <inheritdoc />
  public Task CompleteAsync(CancellationToken cancellationToken = default)
  {
    this.StopAccepting();
    lock (this.admissionGate)
    {
      this.completionTask ??= this.CompleteWorkersAsync();
      return this.completionTask.WaitAsync(cancellationToken);
    }
  }

  /// <inheritdoc />
  public async ValueTask DisposeAsync()
  {
    await this.CompleteAsync().ConfigureAwait(false);
    this.shutdownCancellation.Dispose();
  }

  private async Task RunReadWorkerAsync()
  {
    while (true)
    {
      ReadWork work;
      try
      {
        work = await this.readQueue.DequeueAsync(CancellationToken.None).ConfigureAwait(false);
        work.MarkDequeued();
        this.metrics.ObserveReadDepths(
            this.readQueue.InteractiveDepth,
            this.readQueue.BackgroundDepth);
      }
      catch (ChannelClosedException)
      {
        return;
      }

      await this.ExecuteReadAsync(work).ConfigureAwait(false);
    }
  }

  private async Task ExecuteReadAsync(ReadWork work)
  {
    try
    {
      await using (var context = await this.contextFactory
          .CreateDbContextAsync(CancellationToken.None)
          .ConfigureAwait(false))
      {
        this.metrics.RecordReaderStarted();
        try
        {
          await work.ExecuteAsync(context, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
          this.metrics.RecordReaderStopped();
        }
      }

      work.CompleteSucceeded();
    }
    catch (OperationCanceledException exception)
    {
      this.metrics.RecordCancelled();
      work.CompleteCancelled(exception);
    }
    catch (Exception exception)
    {
      this.metrics.RecordTerminalFailure();
      try
      {
        this.errorLog?.Invoke($"Persistence read failed for domain '{work.Key.Domain}'.");
      }
      catch (Exception)
      {
        // Host-provided diagnostics must not disrupt bounded worker progress.
      }

      work.CompleteFailed(exception);
    }
    finally
    {
      lock (this.admissionGate)
      {
        _ = this.inFlightReads.Remove(work.Key);
      }
    }
  }

  private static Task<PersistenceWriteResult> RejectedWriteCompletion => Task.FromResult(
      new PersistenceWriteResult(PersistenceCompletionStatus.Rejected, 0, null));

  private static bool IsSqliteBusyOrLocked(Exception exception)
  {
    if (exception is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
    {
      exception = aggregate.InnerExceptions[0];
    }
    else if (exception.InnerException is not null)
    {
      exception = exception.InnerException;
    }

    return exception is SqliteException sqliteException
        && (sqliteException.SqliteErrorCode == 5 || sqliteException.SqliteErrorCode == 6);
  }

  private async Task CompleteWorkersAsync()
  {
    var workers = Task.WhenAll(this.readerWorkers.Append(this.writerWorker));
    try
    {
      await workers.WaitAsync(this.options.ShutdownTimeout).ConfigureAwait(false);
    }
    catch (TimeoutException)
    {
      await this.shutdownCancellation.CancelAsync().ConfigureAwait(false);
      this.CancelPendingWrites();
      await workers.ConfigureAwait(false);
    }
  }

  private void CancelPendingWrites()
  {
    lock (this.admissionGate)
    {
      this.writeQueue.Cancel(work =>
      {
        work.CompleteCancelled(new OperationCanceledException(this.shutdownCancellation.Token));
        this.metrics.RecordCancelled();
      });
      foreach (var work in this.pendingWrites.Values)
      {
        work.CompleteCancelled(new OperationCanceledException(this.shutdownCancellation.Token));
        this.metrics.RecordCancelled();
      }

      this.pendingWrites.Clear();
    }
  }

  private async Task RunWriterWorkerAsync()
  {
    while (true)
    {
      WriteWork first;
      try
      {
        first = await this.writeQueue.DequeueAsync(this.shutdownCancellation.Token).ConfigureAwait(false);
      }
      catch (ChannelClosedException)
      {
        return;
      }
      catch (OperationCanceledException)
      {
        return;
      }

      var batch = new List<WriteWork> { this.ClaimWrite(first) };
      if (this.options.BatchCollectionWindow > TimeSpan.Zero)
      {
        try
        {
          await this.delayAsync(this.options.BatchCollectionWindow, this.shutdownCancellation.Token)
              .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
          this.CompleteBatchCancelled(batch);
          return;
        }
      }

      while (batch.Count < this.options.MaxBatchSize)
      {
        if (!this.writeQueue.TryDequeue(out var next))
        {
          break;
        }

        batch.Add(this.ClaimWrite(next));
      }

      await this.ExecuteWriteBatchAsync(batch).ConfigureAwait(false);
    }
  }

  private WriteWork ClaimWrite(WriteWork work)
  {
    lock (this.admissionGate)
    {
      _ = this.pendingWrites.Remove(work.Key);
      work.Claim();
      this.metrics.ObserveWriteDepths(
          this.writeQueue.InteractiveDepth,
          this.writeQueue.BackgroundDepth);
      return work;
    }
  }

  private async Task ExecuteWriteBatchAsync(IReadOnlyList<WriteWork> batch)
  {
    this.metrics.RecordWriterStarted();
    try
    {
      for (var attempt = 0; attempt < this.options.MaxAttempts; attempt++)
      {
        try
        {
          var result = await this.ExecuteWriteAttemptAsync(batch).ConfigureAwait(false);
          this.metrics.RecordBatch();
          foreach (var work in batch)
          {
            work.Publish();
            if (result.Changed.Contains(work))
            {
              this.metrics.RecordCommittedWrite();
              work.CompleteSucceeded(result.AffectedRows);
            }
            else
            {
              this.metrics.RecordUnchangedWrite();
              work.CompleteUnchanged();
            }
          }

          return;
        }
        catch (OperationCanceledException exception)
        {
          this.CompleteBatchCancelled(batch, exception);
          return;
        }
        catch (Exception exception) when (attempt + 1 < this.options.MaxAttempts
            && this.transientFailureClassifier(exception))
        {
          this.metrics.RecordRetry();
          try
          {
            await this.delayAsync(this.options.RetryDelays[attempt], this.shutdownCancellation.Token)
                .ConfigureAwait(false);
          }
          catch (OperationCanceledException cancellation)
          {
            this.CompleteBatchCancelled(batch, cancellation);
            return;
          }
        }
        catch (Exception exception)
        {
          this.metrics.RecordTerminalFailure();
          try
          {
            this.errorLog?.Invoke("Persistence write batch failed.");
          }
          catch (Exception)
          {
            // Host diagnostics must not interrupt bounded persistence work.
          }

          foreach (var work in batch)
          {
            work.CompleteFailed(exception);
          }

          return;
        }
      }
    }
    finally
    {
      this.metrics.RecordWriterStopped();
    }
  }

  private async Task<WriteAttemptResult> ExecuteWriteAttemptAsync(IReadOnlyList<WriteWork> batch)
  {
    await using var context = await this.contextFactory
        .CreateDbContextAsync(this.shutdownCancellation.Token)
        .ConfigureAwait(false);
    await using var transaction = await context.Database
        .BeginTransactionAsync(this.shutdownCancellation.Token)
        .ConfigureAwait(false);
    try
    {
      var changed = new HashSet<WriteWork>();
      foreach (var work in batch)
      {
        if ((await work.Request.ApplyAsync(context, this.shutdownCancellation.Token)
            .ConfigureAwait(false)).Changed)
        {
          _ = changed.Add(work);
        }
      }

      var affectedRows = changed.Count == 0
          ? 0
          : await context.SaveChangesAsync(this.shutdownCancellation.Token).ConfigureAwait(false);
      await transaction.CommitAsync(this.shutdownCancellation.Token).ConfigureAwait(false);
      return new WriteAttemptResult(changed, affectedRows);
    }
    catch
    {
      await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
      throw;
    }
  }

  private void CompleteBatchCancelled(
      IEnumerable<WriteWork> batch,
      OperationCanceledException? exception = null)
  {
    var cancellation = exception ?? new OperationCanceledException(this.shutdownCancellation.Token);
    foreach (var work in batch)
    {
      work.CompleteCancelled(cancellation);
      this.metrics.RecordCancelled();
    }
  }

  private TimeSpan GetOldestQueuedAge()
  {
    lock (this.admissionGate)
    {
      DateTimeOffset? oldestQueuedAt = null;
      foreach (var work in this.inFlightReads.Values)
      {
        if (!work.IsQueued)
        {
          continue;
        }

        if (oldestQueuedAt is null || work.QueuedAt < oldestQueuedAt.Value)
        {
          oldestQueuedAt = work.QueuedAt;
        }
      }

      foreach (var work in this.pendingWrites.Values)
      {
        if (oldestQueuedAt is null || work.QueuedAt < oldestQueuedAt.Value)
        {
          oldestQueuedAt = work.QueuedAt;
        }
      }

      return oldestQueuedAt is null ? TimeSpan.Zero : this.utcNow() - oldestQueuedAt.Value;
    }
  }

  private abstract class ReadWork
  {
    private int dequeued;

    protected ReadWork(PersistenceWorkKey key, DateTimeOffset queuedAt)
    {
      this.Key = key;
      this.QueuedAt = queuedAt;
    }

    internal PersistenceWorkKey Key { get; }

    internal DateTimeOffset QueuedAt { get; }

    internal bool IsQueued => Volatile.Read(ref this.dequeued) == 0;

    internal void MarkDequeued() => Interlocked.Exchange(ref this.dequeued, 1);

    internal abstract Task ExecuteAsync(EchoglossianDbContext context, CancellationToken cancellationToken);

    internal abstract void CompleteSucceeded();

    internal abstract void CompleteCancelled(OperationCanceledException exception);

    internal abstract void CompleteFailed(Exception exception);
  }

  private sealed class WriteWork
  {
    private readonly TaskCompletionSource<PersistenceWriteResult> completionSource = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private PersistenceWriteRequest request;
    private int claimed;

    internal WriteWork(PersistenceWriteRequest request, DateTimeOffset queuedAt)
    {
      this.request = request;
      this.QueuedAt = queuedAt;
    }

    internal PersistenceWorkKey Key => this.request.Key;

    internal DateTimeOffset QueuedAt { get; }

    internal Task<PersistenceWriteResult> Completion => this.completionSource.Task;

    internal PersistenceWriteRequest Request => this.request;

    internal void Claim() => Interlocked.Exchange(ref this.claimed, 1);

    internal void Replace(PersistenceWriteRequest replacement)
    {
      if (Volatile.Read(ref this.claimed) != 0)
      {
        throw new InvalidOperationException("A claimed write cannot be replaced.");
      }

      this.request = replacement;
    }

    internal void Publish()
    {
      try
      {
        this.request.PublishAfterCommit();
      }
      catch (Exception)
      {
        // A cache projection cannot reverse a committed database transaction.
      }
    }

    internal void CompleteSucceeded(int affectedRows) => _ = this.completionSource.TrySetResult(
        new PersistenceWriteResult(PersistenceCompletionStatus.Succeeded, affectedRows, null));

    internal void CompleteUnchanged() => _ = this.completionSource.TrySetResult(
        new PersistenceWriteResult(PersistenceCompletionStatus.Unchanged, 0, null));

    internal void CompleteCancelled(OperationCanceledException exception) => _ = this.completionSource.TrySetResult(
        new PersistenceWriteResult(PersistenceCompletionStatus.Cancelled, 0, exception));

    internal void CompleteFailed(Exception exception) => _ = this.completionSource.TrySetResult(
        new PersistenceWriteResult(PersistenceCompletionStatus.Failed, 0, exception));
  }

  private sealed record WriteAttemptResult(ISet<WriteWork> Changed, int AffectedRows);

  private sealed class ReadWork<T> : ReadWork
  {
    private readonly Action<T>? publish;
    private readonly Func<EchoglossianDbContext, CancellationToken, Task<T>> readAsync;
    private readonly TaskCompletionSource<PersistenceReadResult<T>> completionSource = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private T? value;

    internal ReadWork(
        PersistenceWorkKey key,
        Func<EchoglossianDbContext, CancellationToken, Task<T>> readAsync,
        Action<T>? publish,
        DateTimeOffset queuedAt)
        : base(key, queuedAt)
    {
      this.readAsync = readAsync;
      this.publish = publish;
    }

    internal Task<PersistenceReadResult<T>> Completion => this.completionSource.Task;

    internal override async Task ExecuteAsync(
        EchoglossianDbContext context,
        CancellationToken cancellationToken)
    {
      var value = await this.readAsync(context, cancellationToken).ConfigureAwait(false);
      this.publish?.Invoke(value);
      this.value = value;
    }

    internal override void CompleteSucceeded()
    {
      _ = this.completionSource.TrySetResult(
          new PersistenceReadResult<T>(PersistenceCompletionStatus.Succeeded, this.value, null));
    }

    internal override void CompleteCancelled(OperationCanceledException exception)
    {
      _ = this.completionSource.TrySetResult(
          new PersistenceReadResult<T>(PersistenceCompletionStatus.Cancelled, default, exception));
    }

    internal override void CompleteFailed(Exception exception)
    {
      _ = this.completionSource.TrySetResult(
          new PersistenceReadResult<T>(PersistenceCompletionStatus.Failed, default, exception));
    }
  }
}
