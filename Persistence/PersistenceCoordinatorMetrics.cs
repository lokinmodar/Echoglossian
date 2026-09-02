// <copyright file="PersistenceCoordinatorMetrics.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Persistence;

/// <summary>Provides an immutable persistence coordinator metrics snapshot.</summary>
internal readonly record struct PersistenceMetricsSnapshot(
    int ReadInteractiveDepth,
    int ReadBackgroundDepth,
    int WriteInteractiveDepth,
    int WriteBackgroundDepth,
    int ReadInteractiveHighWaterMark,
    int ReadBackgroundHighWaterMark,
    int WriteInteractiveHighWaterMark,
    int WriteBackgroundHighWaterMark,
    long AcceptedOperations,
    long RejectedOperations,
    long CoalescedOperations,
    int ActiveReaders,
    int MaximumActiveReaders,
    int ActiveWriters,
    int MaximumActiveWriters,
    long BatchCount,
    long CommittedWrites,
    long UnchangedWrites,
    long RetryCount,
    long TerminalFailures,
    long CancelledOperations,
    TimeSpan OldestQueuedAge);

/// <summary>Collects coordinator metrics with lock-free counter updates.</summary>
internal sealed class PersistenceCoordinatorMetrics
{
  private long acceptedOperations;
  private int activeReaders;
  private int activeWriters;
  private long batchCount;
  private long cancelledOperations;
  private long coalescedOperations;
  private long committedWrites;
  private int maximumActiveReaders;
  private int maximumActiveWriters;
  private long rejectedOperations;
  private int readBackgroundHighWaterMark;
  private int readInteractiveHighWaterMark;
  private long retryCount;
  private long terminalFailures;
  private long unchangedWrites;
  private int writeBackgroundHighWaterMark;
  private int writeInteractiveHighWaterMark;

  /// <summary>Records accepted work.</summary>
  internal void RecordAccepted()
  {
    _ = Interlocked.Increment(ref this.acceptedOperations);
  }

  /// <summary>Records rejected work.</summary>
  internal void RecordRejected() => _ = Interlocked.Increment(ref this.rejectedOperations);

  /// <summary>Records a coalesced operation.</summary>
  internal void RecordCoalesced() => _ = Interlocked.Increment(ref this.coalescedOperations);

  /// <summary>Records a reader becoming active.</summary>
  internal void RecordReaderStarted()
  {
    var active = Interlocked.Increment(ref this.activeReaders);
    UpdateMaximum(ref this.maximumActiveReaders, active);
  }

  /// <summary>Records a reader becoming inactive.</summary>
  internal void RecordReaderStopped() => _ = Interlocked.Decrement(ref this.activeReaders);

  /// <summary>Records a terminal failure.</summary>
  internal void RecordTerminalFailure() => _ = Interlocked.Increment(ref this.terminalFailures);

  /// <summary>Records cancelled work.</summary>
  internal void RecordCancelled() => _ = Interlocked.Increment(ref this.cancelledOperations);

  /// <summary>Records a writer becoming active.</summary>
  internal void RecordWriterStarted()
  {
    var active = Interlocked.Increment(ref this.activeWriters);
    UpdateMaximum(ref this.maximumActiveWriters, active);
  }

  /// <summary>Records a writer becoming inactive.</summary>
  internal void RecordWriterStopped() => _ = Interlocked.Decrement(ref this.activeWriters);

  /// <summary>Records one completed write batch.</summary>
  internal void RecordBatch() => _ = Interlocked.Increment(ref this.batchCount);

  /// <summary>Records one committed write.</summary>
  internal void RecordCommittedWrite() => _ = Interlocked.Increment(ref this.committedWrites);

  /// <summary>Records one unchanged write.</summary>
  internal void RecordUnchangedWrite() => _ = Interlocked.Increment(ref this.unchangedWrites);

  /// <summary>Records one retry attempt.</summary>
  internal void RecordRetry() => _ = Interlocked.Increment(ref this.retryCount);

  /// <summary>Updates the observed read lane high-water marks.</summary>
  /// <param name="interactiveDepth">The current interactive depth.</param>
  /// <param name="backgroundDepth">The current background depth.</param>
  internal void ObserveReadDepths(int interactiveDepth, int backgroundDepth)
  {
    UpdateMaximum(ref this.readInteractiveHighWaterMark, interactiveDepth);
    UpdateMaximum(ref this.readBackgroundHighWaterMark, backgroundDepth);
  }

  /// <summary>Updates the observed write lane high-water marks.</summary>
  /// <param name="interactiveDepth">The current interactive depth.</param>
  /// <param name="backgroundDepth">The current background depth.</param>
  internal void ObserveWriteDepths(int interactiveDepth, int backgroundDepth)
  {
    UpdateMaximum(ref this.writeInteractiveHighWaterMark, interactiveDepth);
    UpdateMaximum(ref this.writeBackgroundHighWaterMark, backgroundDepth);
  }

  /// <summary>Builds the immutable metrics snapshot.</summary>
  /// <param name="readInteractiveDepth">The current read interactive depth.</param>
  /// <param name="readBackgroundDepth">The current read background depth.</param>
  /// <param name="writeInteractiveDepth">The current write interactive depth.</param>
  /// <param name="writeBackgroundDepth">The current write background depth.</param>
  /// <param name="oldestQueuedAge">The current oldest queued age.</param>
  /// <returns>The immutable metrics snapshot.</returns>
  internal PersistenceMetricsSnapshot Snapshot(
      int readInteractiveDepth,
      int readBackgroundDepth,
      int writeInteractiveDepth,
      int writeBackgroundDepth,
      TimeSpan oldestQueuedAge)
  {
    return new PersistenceMetricsSnapshot(
        readInteractiveDepth,
        readBackgroundDepth,
        writeInteractiveDepth,
        writeBackgroundDepth,
        Volatile.Read(ref this.readInteractiveHighWaterMark),
        Volatile.Read(ref this.readBackgroundHighWaterMark),
        Volatile.Read(ref this.writeInteractiveHighWaterMark),
        Volatile.Read(ref this.writeBackgroundHighWaterMark),
        Interlocked.Read(ref this.acceptedOperations),
        Interlocked.Read(ref this.rejectedOperations),
        Interlocked.Read(ref this.coalescedOperations),
        Volatile.Read(ref this.activeReaders),
        Volatile.Read(ref this.maximumActiveReaders),
        Volatile.Read(ref this.activeWriters),
        Volatile.Read(ref this.maximumActiveWriters),
        Interlocked.Read(ref this.batchCount),
        Interlocked.Read(ref this.committedWrites),
        Interlocked.Read(ref this.unchangedWrites),
        Interlocked.Read(ref this.retryCount),
        Interlocked.Read(ref this.terminalFailures),
        Interlocked.Read(ref this.cancelledOperations),
        oldestQueuedAge);
  }

  private static void UpdateMaximum(ref int maximum, int candidate)
  {
    while (candidate > Volatile.Read(ref maximum))
    {
      var observed = Volatile.Read(ref maximum);
      if (Interlocked.CompareExchange(ref maximum, candidate, observed) == observed)
      {
        return;
      }
    }
  }
}
