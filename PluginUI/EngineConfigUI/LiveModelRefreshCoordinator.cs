// <copyright file="LiveModelRefreshCoordinator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.EngineConfigUI;

/// <summary>
///     Coordinates one-shot live model refresh requests for engine
///     configuration UIs so refreshes happen when inputs change instead of on
///     every frame.
/// </summary>
public static class LiveModelRefreshCoordinator
{
    private static readonly HashSet<string> InFlightScopes = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> RequestedSignatures = new(StringComparer.Ordinal);
    private static readonly object SyncLock = new();
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
        lock (SyncLock)
        {
            if (suppressionDepth > 0)
            {
                ClearCore(scope);
                return;
            }

            RequestedSignatures[scope] = signature;
            if (!InFlightScopes.Add(scope))
            {
                return;
            }
        }

        _ = RunRefreshAsync(scope, refreshAsync);
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
        if (!enabled)
        {
            Clear(scope);
            return;
        }

        lock (SyncLock)
        {
            if (suppressionDepth > 0)
            {
                ClearCore(scope);
                return;
            }

            if (InFlightScopes.Contains(scope))
            {
                RequestedSignatures[scope] = signature;
                return;
            }

            if (RequestedSignatures.TryGetValue(scope, out string? previousSignature) &&
                string.Equals(previousSignature, signature, StringComparison.Ordinal))
            {
                return;
            }

            RequestedSignatures[scope] = signature;
            InFlightScopes.Add(scope);
        }

        _ = RunRefreshAsync(scope, refreshAsync);
    }

    /// <summary>
    ///     Clears retained refresh state without reacquiring the shared lock.
    /// </summary>
    /// <param name="scope">The stable scope key.</param>
    private static void ClearCore(string scope)
    {
        InFlightScopes.Remove(scope);
        RequestedSignatures.Remove(scope);
    }

    /// <summary>
    ///     Runs refresh work while releasing the scope lock afterward.
    /// </summary>
    /// <param name="scope">The stable scope key.</param>
    /// <param name="refreshAsync">The refresh work to execute.</param>
    private static async Task RunRefreshAsync(
        string scope,
        Func<Task> refreshAsync)
    {
        while (true)
        {
            string requestedSignature;
            var shouldRerun = false;
            lock (SyncLock)
            {
                if (!RequestedSignatures.TryGetValue(scope, out requestedSignature!))
                {
                    InFlightScopes.Remove(scope);
                    return;
                }
            }

            try
            {
                await refreshAsync().ConfigureAwait(false);
            }
            finally
            {
                lock (SyncLock)
                {
                    shouldRerun =
                        RequestedSignatures.TryGetValue(scope, out string? latestSignature) &&
                        !string.Equals(latestSignature, requestedSignature, StringComparison.Ordinal);
                    if (!shouldRerun)
                    {
                        InFlightScopes.Remove(scope);
                    }
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
}
