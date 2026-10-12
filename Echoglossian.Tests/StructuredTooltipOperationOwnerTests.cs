// <copyright file="StructuredTooltipOperationOwnerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>Tests bounded terminal ownership independently of native state and provider behavior.</summary>
public sealed class StructuredTooltipOperationOwnerTests
{
    [ThreadStatic]
    private static bool admissionActive;

    /// <summary>Admission queues orchestration off the calling stack and shares the exact active completion.</summary>
    [Fact]
    public async Task TryStart_QueuesOffCallerAndJoinsExactCompletion()
    {
        var owner = new StructuredTooltipOperationOwner();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? first;
        try
        {
            admissionActive = true;
            first = owner.TryStart("canonical", async () =>
            {
                entered.SetResult(admissionActive);
                await release.Task;
            }, CancellationToken.None);
        }
        finally
        {
            admissionActive = false;
        }

        try
        {
            Assert.NotNull(first);
            Assert.False(await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Same(first, owner.TryStart("canonical",
                () => throw new InvalidOperationException("Joined work must not execute."), CancellationToken.None));
            Assert.False(first.IsCompleted);
        }
        finally
        {
            release.TrySetResult(true);
        }

        await first!.WaitAsync(TimeSpan.FromSeconds(5));
    }

    /// <summary>Active distinct identities consume at most the existing 128 ownership slots.</summary>
    [Fact]
    public async Task TryStart_ActiveCapacityIsBounded()
    {
        var owner = new StructuredTooltipOperationOwner();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = new List<Task>();
        try
        {
            for (var index = 0; index < 128; index++)
            {
                var completion = owner.TryStart("canonical-" + index, () => release.Task, CancellationToken.None);
                Assert.NotNull(completion);
                active.Add(completion);
            }

            Assert.Null(owner.TryStart("overflow", () => throw new InvalidOperationException("Overflow must not run."), CancellationToken.None));
            Assert.Same(active[0], owner.TryStart("canonical-0", () => Task.CompletedTask, CancellationToken.None));
            Assert.All(active, completion => Assert.False(completion.IsCompleted));
        }
        finally
        {
            release.TrySetResult(true);
            await Task.WhenAll(active).WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Failed orchestration completes quietly and retains the same terminal cooldown for repeated frames.</summary>
    [Fact]
    public async Task TryStart_FailureRetainsTerminalCooldown()
    {
        var owner = new StructuredTooltipOperationOwner();
        var calls = 0;
        var completion = owner.TryStart("canonical", () =>
        {
            Interlocked.Increment(ref calls);
            throw new InvalidOperationException("Injected failure.");
        }, CancellationToken.None)!;
        await completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(completion.IsCompletedSuccessfully);
        for (var frame = 0; frame < 20; frame++)
        {
            Assert.Same(completion, owner.TryStart("canonical", () =>
            {
                Interlocked.Increment(ref calls);
                return Task.CompletedTask;
            }, CancellationToken.None));
        }

        Assert.Equal(1, calls);
    }

    /// <summary>An already cancelled plugin lifetime rejects admission without invoking orchestration.</summary>
    [Fact]
    public void TryStart_CancelledLifetimeRejectsWithoutRunning()
    {
        var owner = new StructuredTooltipOperationOwner();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var calls = 0;
        Assert.Null(owner.TryStart("canonical", () =>
        {
            calls++;
            return Task.CompletedTask;
        }, cancellation.Token));
        Assert.Equal(0, calls);
    }
}
