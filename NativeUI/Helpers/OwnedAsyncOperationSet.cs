// <copyright file="OwnedAsyncOperationSet.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Owns background operations that must stop when their native UI runtime
///     is disposed.
/// </summary>
public sealed class OwnedAsyncOperationSet : IDisposable
{
    private readonly HashSet<Task> activeOperations = new();
    private readonly Action<Exception>? errorObserver;
    private readonly CancellationTokenSource shutdownTokenSource = new();
    private readonly object syncRoot = new();
    private bool disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OwnedAsyncOperationSet" />
    ///     class.
    /// </summary>
    /// <param name="errorObserver">Optional observer for unexpected operation failures.</param>
    public OwnedAsyncOperationSet(Action<Exception>? errorObserver = null)
    {
        this.errorObserver = errorObserver;
    }

    /// <summary>
    ///     Starts an operation owned by this set.
    /// </summary>
    /// <param name="operation">The operation to start with the shutdown-linked token.</param>
    /// <returns>
    ///     <see langword="true" /> if the operation was accepted; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    public bool Run(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        CancellationTokenSource? operationTokenSource = null;
        Task? operationTask = null;
        lock (this.syncRoot)
        {
            if (this.disposed)
            {
                return false;
            }

            operationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                this.shutdownTokenSource.Token);
            try
            {
                operationTask = operation(operationTokenSource.Token);
            }
            catch (Exception exception)
            {
                operationTask = Task.FromException(exception);
            }

            if (operationTask is null)
            {
                operationTask = Task.FromException(
                    new InvalidOperationException("Owned operation returned a null task."));
            }

            this.activeOperations.Add(operationTask);
        }

        _ = this.ObserveOperationAsync(operationTask, operationTokenSource);
        return true;
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
            this.shutdownTokenSource.Cancel();
        }
    }

    /// <summary>
    ///     Observes one operation and releases its owned cancellation source
    ///     after it completes.
    /// </summary>
    /// <param name="operationTask">The operation being observed.</param>
    /// <param name="operationTokenSource">The linked token source for the operation.</param>
    /// <returns>A task representing observation of the operation.</returns>
    private async Task ObserveOperationAsync(
        Task operationTask,
        CancellationTokenSource operationTokenSource)
    {
        try
        {
            await operationTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            this.errorObserver?.Invoke(exception);
        }
        finally
        {
            lock (this.syncRoot)
            {
                this.activeOperations.Remove(operationTask);
            }

            operationTokenSource.Dispose();
        }
    }
}
