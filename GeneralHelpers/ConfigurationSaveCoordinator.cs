// <copyright file="ConfigurationSaveCoordinator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Threading.Channels;

namespace Echoglossian;

/// <summary>
///     Serializes background configuration persistence so the UI thread can
///     queue immutable snapshots without doing file I/O inline.
/// </summary>
public sealed class ConfigurationSaveCoordinator : IAsyncDisposable
{
    private readonly Action<Exception>? errorObserver;
    private readonly Channel<Config> pendingSnapshots;
    private readonly Func<Config, CancellationToken, Task> persistAsync;
    private readonly Task pumpTask;
    private int acceptingWrites = 1;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="ConfigurationSaveCoordinator" /> class.
    /// </summary>
    /// <param name="persistAsync">
    ///     The persistence delegate that writes one accepted immutable
    ///     snapshot.
    /// </param>
    /// <param name="errorObserver">
    ///     Optional observer for unexpected persistence failures.
    /// </param>
    public ConfigurationSaveCoordinator(
        Func<Config, CancellationToken, Task> persistAsync,
        Action<Exception>? errorObserver = null)
    {
        ArgumentNullException.ThrowIfNull(persistAsync);

        this.persistAsync = persistAsync;
        this.errorObserver = errorObserver;
        this.pendingSnapshots = Channel.CreateBounded<Config>(
            new BoundedChannelOptions(1)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });
        this.pumpTask = Task.Run(this.PumpAsync);
    }

    /// <summary>
    ///     Queues one immutable configuration snapshot for background
    ///     persistence.
    /// </summary>
    /// <param name="snapshot">The immutable snapshot to persist.</param>
    public void QueueSave(Config snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (Volatile.Read(ref this.acceptingWrites) == 0)
        {
            return;
        }

        _ = this.pendingSnapshots.Writer.TryWrite(snapshot);
    }

    /// <summary>
    ///     Stops accepting new saves and waits for the final accepted snapshot
    ///     to drain.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels waiting.</param>
    /// <returns>A task that completes when the save pump finishes.</returns>
    public async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref this.acceptingWrites, 0) == 1)
        {
            this.pendingSnapshots.Writer.TryComplete();
        }

        await this.pumpTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await this.CompleteAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Runs the serialized background persistence pump.
    /// </summary>
    /// <returns>A task that completes when the writer is completed.</returns>
    private async Task PumpAsync()
    {
        await foreach (var snapshot in this.pendingSnapshots.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                var persistTask = this.persistAsync(
                    snapshot,
                    CancellationToken.None) ?? Task.FromException(
                    new InvalidOperationException(
                        "Configuration persistence delegate returned a null task."));
                await persistTask.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                this.ReportUnexpectedException(exception);
            }
        }
    }

    /// <summary>
    ///     Reports one unexpected background persistence failure without
    ///     allowing observer failures to escape the save pump.
    /// </summary>
    /// <param name="exception">The exception to report.</param>
    private void ReportUnexpectedException(Exception exception)
    {
        try
        {
            this.errorObserver?.Invoke(exception);
        }
        catch (Exception)
        {
        }
    }
}
