// <copyright file="PersistenceCoordinator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Threading.Channels;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Echoglossian.Persistence;

/// <summary>
///     Owns bounded, coalesced asynchronous persistence reads.
/// </summary>
internal sealed class PersistenceCoordinator : IPersistenceCoordinator
{
  private readonly object admissionGate = new();
  private readonly IDbContextFactory<EchoglossianDbContext> contextFactory;
  private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
  private readonly Action<string>? errorLog;
  private readonly Dictionary<PersistenceWorkKey, ReadWork> inFlightReads = new();
  private readonly PersistenceCoordinatorMetrics metrics = new();
  private readonly BoundedPriorityQueue<ReadWork> readQueue;
  private readonly Func<Exception, bool> transientFailureClassifier;
  private readonly Func<DateTimeOffset> utcNow;
  private readonly Action<string>? warningLog;
  private readonly Task[] readerWorkers;
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
    this.readQueue = new BoundedPriorityQueue<ReadWork>(
        options.InteractiveCapacity,
        options.BackgroundCapacity);
    this.delayAsync = delayAsync ?? Task.Delay;
    this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    this.transientFailureClassifier = transientFailureClassifier ?? (_ => false);
    this.warningLog = warningLog;
    this.errorLog = errorLog;
    this.readerWorkers = Enumerable.Range(0, options.ReaderConcurrency)
        .Select(_ => Task.Run(this.RunReadWorkerAsync))
        .ToArray();
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
    throw new NotSupportedException("Persistence write coordination is implemented by Task 3.");
  }

  /// <inheritdoc />
  public PersistenceMetricsSnapshot GetMetrics()
  {
    return this.metrics.Snapshot(
        this.readQueue.InteractiveDepth,
        this.readQueue.BackgroundDepth,
        0,
        0,
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
    }
  }

  /// <inheritdoc />
  public async Task CompleteAsync(CancellationToken cancellationToken = default)
  {
    this.StopAccepting();
    await Task.WhenAll(this.readerWorkers).WaitAsync(cancellationToken).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async ValueTask DisposeAsync()
  {
    await this.CompleteAsync().ConfigureAwait(false);
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
