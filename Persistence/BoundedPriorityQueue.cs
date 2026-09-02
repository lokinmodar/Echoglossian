// <copyright file="BoundedPriorityQueue.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Threading.Channels;

namespace Echoglossian.Persistence;

/// <summary>
///     Provides independent bounded lanes with a deterministic 3:1 interactive
///     to background dequeue policy.
/// </summary>
/// <typeparam name="T">The type of queued work item.</typeparam>
internal sealed class BoundedPriorityQueue<T>
{
  private const int InteractiveBudget = 3;

  private readonly object admissionGate = new();
  private readonly Channel<T> backgroundLane;
  private readonly Channel<T> interactiveLane;
  private readonly Channel<bool> wakeSignals;
  private int backgroundDepth;
  private int completing;
  private int interactiveDepth;
  private int remainingInteractiveBudget = InteractiveBudget;

  /// <summary>
  ///     Initializes a new instance of the <see cref="BoundedPriorityQueue{T}" />
  ///     class.
  /// </summary>
  /// <param name="interactiveCapacity">The maximum interactive lane depth.</param>
  /// <param name="backgroundCapacity">The maximum background lane depth.</param>
  /// <exception cref="ArgumentOutOfRangeException">
  ///     <paramref name="interactiveCapacity" /> or
  ///     <paramref name="backgroundCapacity" /> is not positive.
  /// </exception>
  internal BoundedPriorityQueue(int interactiveCapacity, int backgroundCapacity)
  {
    if (interactiveCapacity <= 0)
    {
      throw new ArgumentOutOfRangeException(
          nameof(interactiveCapacity),
          "The interactive capacity must be positive.");
    }

    if (backgroundCapacity <= 0)
    {
      throw new ArgumentOutOfRangeException(
          nameof(backgroundCapacity),
          "The background capacity must be positive.");
    }

    this.interactiveLane = CreateLane(interactiveCapacity);
    this.backgroundLane = CreateLane(backgroundCapacity);
    this.wakeSignals = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1)
        {
          AllowSynchronousContinuations = false,
          FullMode = BoundedChannelFullMode.DropWrite,
          SingleReader = true,
          SingleWriter = false,
        });
  }

  /// <summary>
  ///     Gets the number of accepted interactive items that have not dequeued.
  /// </summary>
  internal int InteractiveDepth => Volatile.Read(ref this.interactiveDepth);

  /// <summary>
  ///     Gets the number of accepted background items that have not dequeued.
  /// </summary>
  internal int BackgroundDepth => Volatile.Read(ref this.backgroundDepth);

  /// <summary>
  ///     Attempts to add an item to its independent bounded lane.
  /// </summary>
  /// <param name="item">The work item to enqueue.</param>
  /// <param name="priority">The lane that receives the work item.</param>
  /// <returns>
  ///     <see langword="true" /> if the item was accepted; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  internal bool TryEnqueue(T item, PersistencePriority priority)
  {
    lock (this.admissionGate)
    {
      if (Volatile.Read(ref this.completing) != 0)
      {
        return false;
      }

      var lane = priority == PersistencePriority.Interactive
          ? this.interactiveLane
          : this.backgroundLane;
      if (!lane.Writer.TryWrite(item))
      {
        return false;
      }

      if (priority == PersistencePriority.Interactive)
      {
        Interlocked.Increment(ref this.interactiveDepth);
      }
      else
      {
        Interlocked.Increment(ref this.backgroundDepth);
      }

      this.SignalReader();
      return true;
    }
  }

  /// <summary>
  ///     Dequeues the next item using the 3:1 interactive to background policy.
  /// </summary>
  /// <param name="cancellationToken">The token that cancels waiting.</param>
  /// <returns>A task that produces the selected item.</returns>
  /// <exception cref="ChannelClosedException">
  ///     Completion began and every accepted item has drained.
  /// </exception>
  internal async ValueTask<T> DequeueAsync(CancellationToken cancellationToken)
  {
    while (true)
    {
      if (this.TryDequeue(out var item))
      {
        return item;
      }

      if (this.IsCompletedAndDrained())
      {
        throw new ChannelClosedException();
      }

      try
      {
        await this.wakeSignals.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
      }
      catch (ChannelClosedException) when (this.IsCompletedAndDrained())
      {
        throw;
      }
    }
  }

  /// <summary>
  ///     Stops admission and wakes a blocked reader to drain accepted work.
  /// </summary>
  internal void Complete()
  {
    lock (this.admissionGate)
    {
      if (Interlocked.Exchange(ref this.completing, 1) != 0)
      {
        return;
      }

      this.interactiveLane.Writer.TryComplete();
      this.backgroundLane.Writer.TryComplete();
      this.SignalReader();
      this.wakeSignals.Writer.TryComplete();
    }
  }

  /// <summary>
  ///     Creates an independent bounded work lane.
  /// </summary>
  /// <param name="capacity">The maximum number of accepted items.</param>
  /// <returns>The bounded work lane.</returns>
  private static Channel<T> CreateLane(int capacity)
  {
    return Channel.CreateBounded<T>(
        new BoundedChannelOptions(capacity)
        {
          AllowSynchronousContinuations = false,
          FullMode = BoundedChannelFullMode.Wait,
          SingleReader = true,
          SingleWriter = false,
        });
  }

  /// <summary>
  ///     Determines whether completion started and both lanes have drained.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> if no more item can be dequeued; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool IsCompletedAndDrained()
  {
    return Volatile.Read(ref this.completing) != 0
        && this.InteractiveDepth == 0
        && this.BackgroundDepth == 0;
  }

  /// <summary>
  ///     Signals the reader that an enqueue or completion may require work.
  /// </summary>
  private void SignalReader()
  {
    _ = this.wakeSignals.Writer.TryWrite(true);
  }

  /// <summary>
  ///     Attempts to dequeue the next item without waiting.
  /// </summary>
  /// <param name="item">When this method returns, contains the selected item.</param>
  /// <returns>
  ///     <see langword="true" /> if an item was selected; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool TryDequeue(out T item)
  {
    if (this.remainingInteractiveBudget > 0
        && this.TryReadInteractive(out item))
    {
      this.remainingInteractiveBudget--;
      this.ResignalIfItemsRemain();
      return true;
    }

    if (this.TryReadBackground(out item))
    {
      this.remainingInteractiveBudget = InteractiveBudget;
      this.ResignalIfItemsRemain();
      return true;
    }

    if (this.TryReadInteractive(out item))
    {
      this.ResignalIfItemsRemain();
      return true;
    }

    item = default!;
    return false;
  }

  /// <summary>
  ///     Attempts to read one accepted interactive item.
  /// </summary>
  /// <param name="item">When this method returns, contains the read item.</param>
  /// <returns>
  ///     <see langword="true" /> if an item was read; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool TryReadInteractive(out T item)
  {
    lock (this.admissionGate)
    {
      if (!this.interactiveLane.Reader.TryRead(out item))
      {
        return false;
      }

      _ = Interlocked.Decrement(ref this.interactiveDepth);
      return true;
    }
  }

  /// <summary>
  ///     Attempts to read one accepted background item.
  /// </summary>
  /// <param name="item">When this method returns, contains the read item.</param>
  /// <returns>
  ///     <see langword="true" /> if an item was read; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool TryReadBackground(out T item)
  {
    lock (this.admissionGate)
    {
      if (!this.backgroundLane.Reader.TryRead(out item))
      {
        return false;
      }

      _ = Interlocked.Decrement(ref this.backgroundDepth);
      return true;
    }
  }

  /// <summary>
  ///     Preserves a wake-up while accepted work remains queued.
  /// </summary>
  private void ResignalIfItemsRemain()
  {
    if (this.InteractiveDepth > 0 || this.BackgroundDepth > 0)
    {
      this.SignalReader();
    }
  }
}
