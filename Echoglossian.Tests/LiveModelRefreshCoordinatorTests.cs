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
            Task RefreshAsync()
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
            LiveModelRefreshCoordinator.Clear(scope);
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
            Task RefreshAsync()
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
            LiveModelRefreshCoordinator.Clear(scope);
        }
    }
}
