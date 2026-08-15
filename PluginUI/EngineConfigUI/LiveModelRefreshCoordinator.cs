// <copyright file="LiveModelRefreshCoordinator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

namespace Echoglossian.PluginUI.EngineConfigUI;

/// <summary>
///     Coordinates one-shot live model refresh requests for engine
///     configuration UIs so refreshes happen when inputs change instead of on
///     every frame.
/// </summary>
public static class LiveModelRefreshCoordinator
{
    private static readonly Dictionary<string, RefreshScopeState> ScopeStates =
        new(StringComparer.Ordinal);
    private static readonly object SyncLock = new();
    private static OwnedAsyncOperationSet ownedRefreshOperations =
        new(ReportUnexpectedExceptionToRuntimeLog);
    private static int suppressionDepth;

    /// <summary>
    ///     Suppresses live refresh requests until the returned scope is
    ///     disposed.
    /// </summary>
    /// <returns>The disposable suppression scope.</returns>
    public static IDisposable SuppressRequests()
    {
        lock (SyncLock)
        {
            suppressionDepth++;
        }

        return new RefreshSuppressionScope();
    }

    /// <summary>
    ///     Clears retained refresh state for the provided UI scope.
    /// </summary>
    /// <param name="scope">The stable scope key.</param>
    public static void Clear(string scope)
    {
        lock (SyncLock)
        {
            ClearCore(scope);
        }
    }

    /// <summary>
    ///     Forces a refresh for the provided scope with the current signature.
    /// </summary>
    /// <param name="scope">The stable scope key.</param>
    /// <param name="signature">The current refresh signature.</param>
    /// <param name="refreshAsync">The refresh work to execute.</param>
    public static void ForceRefresh(
        string scope,
        string signature,
        Func<Task> refreshAsync)
    {
        ForceRefresh(scope, signature, _ => refreshAsync());
    }

    /// <summary>
    ///     Forces a refresh for the provided scope with the current signature.
    /// </summary>
    /// <param name="scope">The stable scope key.</param>
    /// <param name="signature">The current refresh signature.</param>
    /// <param name="refreshAsync">The refresh work to execute.</param>
    internal static void ForceRefresh(
        string scope,
        string signature,
        Func<CancellationToken, Task> refreshAsync)
    {
        ArgumentNullException.ThrowIfNull(refreshAsync);

        OwnedAsyncOperationSet operations;
        var shouldStartRefresh = false;
        lock (SyncLock)
        {
            if (suppressionDepth > 0)
            {
                ClearCore(scope);
                return;
            }

            var state = GetOrCreateState(scope);
            state.RequestedSignature = signature;
            if (state.InFlight)
            {
                return;
            }

            state.InFlight = true;
            state.ExecutingSignature = signature;
            operations = ownedRefreshOperations;
            shouldStartRefresh = true;
        }

        if (shouldStartRefresh)
        {
            StartRefreshLoop(scope, refreshAsync, operations);
        }
    }

    /// <summary>
    ///     Requests a refresh when live listing is enabled and the input
    ///     signature differs from the last request for the same scope.
    /// </summary>
    /// <param name="scope">The stable scope key.</param>
    /// <param name="enabled">Whether live model listing is enabled.</param>
    /// <param name="signature">The current refresh signature.</param>
    /// <param name="refreshAsync">The refresh work to execute.</param>
    public static void RequestIfNeeded(
        string scope,
        bool enabled,
        string signature,
        Func<Task> refreshAsync)
    {
        RequestIfNeeded(scope, enabled, signature, _ => refreshAsync());
    }

    /// <summary>
    ///     Requests a refresh when live listing is enabled and the input
    ///     signature differs from the last successful request for the same
    ///     scope.
    /// </summary>
    /// <param name="scope">The stable scope key.</param>
    /// <param name="enabled">Whether live model listing is enabled.</param>
    /// <param name="signature">The current refresh signature.</param>
    /// <param name="refreshAsync">The refresh work to execute.</param>
    internal static void RequestIfNeeded(
        string scope,
        bool enabled,
        string signature,
        Func<CancellationToken, Task> refreshAsync)
    {
        ArgumentNullException.ThrowIfNull(refreshAsync);

        if (!enabled)
        {
            Clear(scope);
            return;
        }

        OwnedAsyncOperationSet operations;
        var shouldStartRefresh = false;
        lock (SyncLock)
        {
            if (suppressionDepth > 0)
            {
                ClearCore(scope);
                return;
            }

            var state = GetOrCreateState(scope);
            if (state.InFlight)
            {
                state.RequestedSignature = signature;
                return;
            }

            if (string.Equals(
                    state.LastCompletedSignature,
                    signature,
                    StringComparison.Ordinal))
            {
                return;
            }

            state.RequestedSignature = signature;
            state.InFlight = true;
            state.ExecutingSignature = signature;
            operations = ownedRefreshOperations;
            shouldStartRefresh = true;
        }

        if (shouldStartRefresh)
        {
            StartRefreshLoop(scope, refreshAsync, operations);
        }
    }

    /// <summary>
    ///     Cancels in-flight refresh work and resets retained state during
    ///     plugin shutdown without blocking the caller.
    /// </summary>
    internal static void ResetForPluginShutdown()
    {
        ResetCore(new OwnedAsyncOperationSet(ReportUnexpectedExceptionToRuntimeLog));
    }

    /// <summary>
    ///     Resets retained state for isolated tests and optionally injects a
    ///     custom unexpected-exception observer.
    /// </summary>
    /// <param name="errorObserver">The observer for unexpected failures.</param>
    internal static void ResetForTests(Action<Exception>? errorObserver = null)
    {
        ResetCore(new OwnedAsyncOperationSet(errorObserver));
    }

    /// <summary>
     ///     Clears retained refresh state without reacquiring the shared lock.
     /// </summary>
     /// <param name="scope">The stable scope key.</param>
    private static void ClearCore(string scope)
    {
        ScopeStates.Remove(scope);
    }

    /// <summary>
    ///     Replaces the owned refresh-operation set and clears retained state.
    /// </summary>
    /// <param name="replacementOperations">
    ///     The replacement owned refresh-operation set.
    /// </param>
    private static void ResetCore(OwnedAsyncOperationSet replacementOperations)
    {
        OwnedAsyncOperationSet previousOperations;
        lock (SyncLock)
        {
            previousOperations = ownedRefreshOperations;
            ownedRefreshOperations = replacementOperations;
            ScopeStates.Clear();
            suppressionDepth = 0;
        }

        previousOperations.Dispose();
    }

    /// <summary>
    ///     Gets or creates the retained state for one stable refresh scope.
    /// </summary>
    /// <param name="scope">The refresh scope key.</param>
    /// <returns>The retained scope state.</returns>
    private static RefreshScopeState GetOrCreateState(string scope)
    {
        if (!ScopeStates.TryGetValue(scope, out var state))
        {
            state = new RefreshScopeState();
            ScopeStates.Add(scope, state);
        }

        return state;
    }

    /// <summary>
    ///     Starts one owned refresh loop for the provided scope.
    /// </summary>
    /// <param name="scope">The stable scope key.</param>
    /// <param name="refreshAsync">The refresh work to execute.</param>
    /// <param name="operations">The owned operation set that starts the work.</param>
    private static void StartRefreshLoop(
        string scope,
        Func<CancellationToken, Task> refreshAsync,
        OwnedAsyncOperationSet operations)
    {
        if (operations.Run(
                cancellationToken => RunRefreshAsync(
                    scope,
                    refreshAsync,
                    cancellationToken)))
        {
            return;
        }

        lock (SyncLock)
        {
            if (ScopeStates.TryGetValue(scope, out var state))
            {
                state.InFlight = false;
                state.ExecutingSignature = null;
            }
        }
    }

    /// <summary>
    ///     Runs refresh work while releasing the scope lock afterward.
    /// </summary>
    /// <param name="scope">The stable scope key.</param>
    /// <param name="refreshAsync">The refresh work to execute.</param>
    private static async Task RunRefreshAsync(
        string scope,
        Func<CancellationToken, Task> refreshAsync,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            string requestedSignature;
            lock (SyncLock)
            {
                if (!ScopeStates.TryGetValue(scope, out var state) ||
                    string.IsNullOrWhiteSpace(state.ExecutingSignature))
                {
                    return;
                }

                requestedSignature = state.ExecutingSignature;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var refreshTask = refreshAsync(cancellationToken) ??
                                  Task.FromException(
                                      new InvalidOperationException(
                                          "Live model refresh delegate returned a null task."));
                await refreshTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                lock (SyncLock)
                {
                    ClearCore(scope);
                }

                throw;
            }
            catch
            {
                lock (SyncLock)
                {
                    if (ScopeStates.TryGetValue(scope, out var state))
                    {
                        state.InFlight = false;
                        state.ExecutingSignature = null;
                    }
                }

                throw;
            }

            var shouldRerun = false;
            lock (SyncLock)
            {
                if (!ScopeStates.TryGetValue(scope, out var state))
                {
                    return;
                }

                shouldRerun = !string.Equals(
                    state.RequestedSignature,
                    requestedSignature,
                    StringComparison.Ordinal);
                if (!shouldRerun)
                {
                    state.LastCompletedSignature = requestedSignature;
                    state.InFlight = false;
                    state.ExecutingSignature = null;
                }
                else
                {
                    state.ExecutingSignature = state.RequestedSignature;
                }
            }

            if (!shouldRerun)
            {
                return;
            }
        }
    }

    /// <summary>
    ///     Represents one live-refresh suppression scope.
    /// </summary>
    private sealed class RefreshSuppressionScope : IDisposable
    {
        private bool disposed;

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (SyncLock)
            {
                if (this.disposed)
                {
                    return;
                }

                if (suppressionDepth > 0)
                {
                    suppressionDepth--;
                }

                this.disposed = true;
            }
        }
    }

    /// <summary>
    ///     Reports unexpected refresh failures through the runtime logger when
    ///     no test-specific observer is installed.
    /// </summary>
    /// <param name="exception">The unexpected refresh failure.</param>
    private static void ReportUnexpectedExceptionToRuntimeLog(Exception exception)
    {
        PluginRuntimeLog.Error(
            "LiveModelRefresh",
            $"Unexpected live model refresh failure: {exception}");
    }

    /// <summary>
    ///     Retains the latest requested and last successful refresh signatures
    ///     for one scope.
    /// </summary>
    private sealed class RefreshScopeState
    {
        /// <summary>
        ///     Gets or sets a value indicating whether a refresh loop is active
        ///     for the scope.
        /// </summary>
        public bool InFlight { get; set; }

        /// <summary>
        ///     Gets or sets the latest requested refresh signature.
        /// </summary>
        public string? RequestedSignature { get; set; }

        /// <summary>
        ///     Gets or sets the signature currently being executed by the
        ///     active refresh loop.
        /// </summary>
        public string? ExecutingSignature { get; set; }

        /// <summary>
        ///     Gets or sets the last successful refresh signature.
        /// </summary>
        public string? LastCompletedSignature { get; set; }
    }
}
