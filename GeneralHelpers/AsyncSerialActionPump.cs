// <copyright file="AsyncSerialActionPump.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;

namespace Echoglossian;

/// <summary>
///     Runs queued actions on a dedicated background worker so hot-path callers
///     can hand off blocking work without doing file I/O inline.
/// </summary>
internal sealed class AsyncSerialActionPump : IDisposable
{
    private static readonly TimeSpan DefaultDisposeTimeout = TimeSpan.FromSeconds(5);

    private readonly Lock syncRoot = new();
    private readonly ConcurrentQueue<Action> pendingActions = new();
    private readonly ManualResetEventSlim idleSignal = new(initialState: true);
    private readonly SemaphoreSlim queueSignal = new(0);

    private bool acceptingWrites = true;
    private bool disposed;
    private Task? workerTask;

    /// <summary>
    ///     Enqueues an action for serialized background execution.
    /// </summary>
    /// <param name="action">The action to run on the worker thread.</param>
    public void Enqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        lock (this.syncRoot)
        {
            if (this.disposed || !this.acceptingWrites)
            {
                return;
            }

            this.EnsureWorkerStartedNoLock();
            this.idleSignal.Reset();
            this.pendingActions.Enqueue(action);
            this.queueSignal.Release();
        }
    }

    /// <summary>
    ///     Waits for the currently queued actions to finish.
    /// </summary>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <returns><see langword="true" /> when the queue drained in time.</returns>
    public bool Flush(TimeSpan timeout)
    {
        return this.idleSignal.Wait(timeout);
    }

    /// <summary>
    ///     Stops accepting new actions and waits for the worker to drain the
    ///     queue before returning.
    /// </summary>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <returns><see langword="true" /> when shutdown completed in time.</returns>
    public bool Shutdown(TimeSpan timeout)
    {
        Task? workerToWait;
        lock (this.syncRoot)
        {
            this.acceptingWrites = false;
            workerToWait = this.workerTask;
            if (workerToWait != null)
            {
                this.queueSignal.Release();
            }
        }

        if (workerToWait == null)
        {
            this.idleSignal.Set();
            return true;
        }

        var idleReached = this.idleSignal.Wait(timeout);
        try
        {
            return idleReached && workerToWait.Wait(timeout);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (this.syncRoot)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        _ = this.Shutdown(DefaultDisposeTimeout);
        this.queueSignal.Dispose();
        this.idleSignal.Dispose();
        while (this.pendingActions.TryDequeue(out _))
        {
        }
    }

    private void EnsureWorkerStartedNoLock()
    {
        if (this.workerTask != null)
        {
            return;
        }

        this.workerTask = Task.Factory.StartNew(
            this.ProcessQueue,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private void ProcessQueue()
    {
        while (true)
        {
            this.queueSignal.Wait();

            while (this.pendingActions.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch
                {
                    // Logging helpers must never throw back into the caller.
                }
            }

            lock (this.syncRoot)
            {
                if (!this.pendingActions.IsEmpty)
                {
                    continue;
                }

                this.idleSignal.Set();
                if (!this.acceptingWrites)
                {
                    this.workerTask = null;
                    return;
                }
            }
        }
    }
}
