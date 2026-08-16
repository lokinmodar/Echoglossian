// <copyright file="LiveModelRefreshCoordinatorTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.EngineConfigUI;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers queued live-model refresh behavior when inputs change during an
///     in-flight refresh.
/// </summary>
public class LiveModelRefreshCoordinatorTests
{
    /// <summary>
    ///     Ensures a forced refresh returns immediately even while the refresh
    ///     work is suspended.
    /// </summary>
    [Fact]
    public void ForceRefresh_ReturnsBeforeSuspendedRefreshCompletes()
    {
        var scope = Guid.NewGuid().ToString("N");
        using var refreshStarted = new ManualResetEventSlim();
        using var releaseRefresh = new ManualResetEventSlim();
        using var requestReturned = new ManualResetEventSlim();
        LiveModelRefreshCoordinator.ResetForTests();
        var callerThread = new Thread(
            () =>
            {
                LiveModelRefreshCoordinator.ForceRefresh(
                    scope,
                    "signature-a",
                    _ =>
                    {
                        refreshStarted.Set();
                        releaseRefresh.Wait();
                        return Task.CompletedTask;
                    });
                requestReturned.Set();
            });

        try
        {
            callerThread.Start();

            Assert.True(refreshStarted.Wait(TimeSpan.FromSeconds(1)));
            Assert.True(requestReturned.Wait(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            releaseRefresh.Set();
            callerThread.Join(TimeSpan.FromSeconds(1));
            LiveModelRefreshCoordinator.ResetForTests();
        }
    }

    /// <summary>
    ///     Ensures preview suppression prevents passive render-time refresh
    ///     requests from contacting live providers.
    /// </summary>
    [Fact]
    public void SuppressRequests_RequestIfNeeded_DoesNotInvokeRefresh()
    {
        var scope = Guid.NewGuid().ToString("N");
        var invocationCount = 0;

        try
        {
            LiveModelRefreshCoordinator.ResetForTests();
            using var suppression = LiveModelRefreshCoordinator.SuppressRequests();
            LiveModelRefreshCoordinator.RequestIfNeeded(
                scope,
                true,
                "signature-a",
                () =>
                {
                    Interlocked.Increment(ref invocationCount);
                    return Task.CompletedTask;
                });

            Volatile.Read(ref invocationCount).Should().Be(0);
        }
        finally
        {
            LiveModelRefreshCoordinator.ResetForTests();
        }
    }

    /// <summary>
    ///     Ensures preview suppression also blocks explicit live-model reload
    ///     actions from triggering refresh work.
    /// </summary>
    [Fact]
    public void SuppressRequests_ForceRefresh_DoesNotInvokeRefresh()
    {
        var scope = Guid.NewGuid().ToString("N");
        var invocationCount = 0;

        try
        {
            LiveModelRefreshCoordinator.ResetForTests();
            using var suppression = LiveModelRefreshCoordinator.SuppressRequests();
            LiveModelRefreshCoordinator.ForceRefresh(
                scope,
                "signature-a",
                _ =>
                {
                    Interlocked.Increment(ref invocationCount);
                    return Task.CompletedTask;
                });

            Volatile.Read(ref invocationCount).Should().Be(0);
        }
        finally
        {
            LiveModelRefreshCoordinator.ResetForTests();
        }
    }

    /// <summary>
    ///     Ensures a forced refresh reruns once when the requested signature
    ///     changes during an in-flight refresh.
    /// </summary>
    [Fact]
    public async Task ForceRefresh_WithUpdatedSignatureDuringInFlight_RerunsAfterCompletion()
    {
        var scope = Guid.NewGuid().ToString("N");
        var firstRelease = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRelease = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;

        try
        {
            LiveModelRefreshCoordinator.ResetForTests();

            Task RefreshAsync(CancellationToken _)
            {
                var invocation = Interlocked.Increment(ref invocationCount);
                return invocation switch
                {
                    1 => firstRelease.Task,
                    2 => AwaitSecondRefreshAsync(),
                    _ => Task.CompletedTask,
                };
            }

            async Task AwaitSecondRefreshAsync()
            {
                secondStarted.TrySetResult(true);
                await secondRelease.Task.ConfigureAwait(false);
            }

            LiveModelRefreshCoordinator.ForceRefresh(scope, "signature-a", RefreshAsync);
            LiveModelRefreshCoordinator.ForceRefresh(scope, "signature-b", RefreshAsync);

            firstRelease.SetResult(true);

            await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Volatile.Read(ref invocationCount).Should().Be(2);

            secondRelease.SetResult(true);
        }
        finally
        {
            LiveModelRefreshCoordinator.ResetForTests();
        }
    }

    /// <summary>
    ///     Ensures passive input-change refresh requests are replayed once
    ///     after the current refresh finishes.
    /// </summary>
    [Fact]
    public async Task RequestIfNeeded_WithUpdatedSignatureDuringInFlight_RerunsAfterCompletion()
    {
        var scope = Guid.NewGuid().ToString("N");
        var firstRelease = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRelease = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;

        try
        {
            LiveModelRefreshCoordinator.ResetForTests();

            Task RefreshAsync(CancellationToken _)
            {
                var invocation = Interlocked.Increment(ref invocationCount);
                return invocation switch
                {
                    1 => firstRelease.Task,
                    2 => AwaitSecondRefreshAsync(),
                    _ => Task.CompletedTask,
                };
            }

            async Task AwaitSecondRefreshAsync()
            {
                secondStarted.TrySetResult(true);
                await secondRelease.Task.ConfigureAwait(false);
            }

            LiveModelRefreshCoordinator.RequestIfNeeded(
                scope,
                true,
                "signature-a",
                RefreshAsync);
            LiveModelRefreshCoordinator.RequestIfNeeded(
                scope,
                true,
                "signature-b",
                RefreshAsync);

            firstRelease.SetResult(true);

            await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Volatile.Read(ref invocationCount).Should().Be(2);

            secondRelease.SetResult(true);
        }
        finally
        {
            LiveModelRefreshCoordinator.ResetForTests();
        }
    }

    /// <summary>
    ///     Ensures unexpected refresh exceptions are observed once rather than
    ///     escaping as unobserved task failures.
    /// </summary>
    /// <returns>A task that completes when the error observer sees the failure.</returns>
    [Fact]
    public async Task ForceRefresh_UnexpectedException_InvokesErrorObserverOnce()
    {
        var scope = Guid.NewGuid().ToString("N");
        var observedExceptions = new List<Exception>();
        var exceptionObserved = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            LiveModelRefreshCoordinator.ResetForTests(
                exception =>
                {
                    observedExceptions.Add(exception);
                    exceptionObserved.TrySetResult(exception);
                });
            LiveModelRefreshCoordinator.ForceRefresh(
                scope,
                "signature-a",
                _ => Task.FromException(new InvalidOperationException("boom")));

            var exception = await exceptionObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.IsType<InvalidOperationException>(exception);
            Assert.Equal("boom", exception.Message);
            Assert.Single(observedExceptions);
        }
        finally
        {
            LiveModelRefreshCoordinator.ResetForTests();
        }
    }

    /// <summary>
    ///     Ensures plugin shutdown cancels in-flight refresh work without
    ///     waiting for the caller thread.
    /// </summary>
    /// <returns>A task that completes when cancellation is observed.</returns>
    [Fact]
    public async Task ResetForPluginShutdown_CancelsInFlightRefresh()
    {
        var scope = Guid.NewGuid().ToString("N");
        var refreshStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            LiveModelRefreshCoordinator.ResetForTests();
            LiveModelRefreshCoordinator.ForceRefresh(
                scope,
                "signature-a",
                async cancellationToken =>
                {
                    using var registration = cancellationToken.Register(
                        () => cancellationObserved.TrySetResult(true));
                    refreshStarted.TrySetResult(true);
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                });
            await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

            LiveModelRefreshCoordinator.ResetForPluginShutdown();

            await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally
        {
            LiveModelRefreshCoordinator.ResetForTests();
        }
    }
}
