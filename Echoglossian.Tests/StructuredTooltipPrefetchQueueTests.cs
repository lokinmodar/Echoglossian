// <copyright file="StructuredTooltipPrefetchQueueTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Echoglossian.NativeUI.Helpers;
using Xunit;
using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>Exercises the production prefetch cursors with real bounded ownership and deterministic cooldown.</summary>
public sealed class StructuredTooltipPrefetchQueueTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    /// <summary>Capacity rejection retains the same entry until completed ownership has cooled down.</summary>
    /// <param name="domain">The prefetch family.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    [InlineData("Item")]
    public async Task SaturatedOwnerRetainsCursorAndRetriesSameEntryAfterCooldown(string domain)
    {
        var nowTicks = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc).Ticks;
        var owner = new StructuredTooltipOperationOwner(() =>
            new DateTime(Interlocked.Read(ref nowTicks), DateTimeKind.Utc));
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = new List<Task>();
        var plugin = CreateQueue(domain, [4242]);
        var attempted = new List<uint>();
        var captured = new ConcurrentQueue<uint>();
        var admissions = new List<StructuredTooltipOperationAdmission>();
        Task? admittedCompletion = null;
        StructuredTooltipOperationAdmission Capture(uint id)
        {
            attempted.Add(id);
            var result = owner.TryStart(domain + "-" + id, () =>
            {
                captured.Enqueue(id);
                return Task.CompletedTask;
            }, CancellationToken.None);
            admissions.Add(result.Status);
            admittedCompletion = result.Completion;
            return result.Status;
        }

        try
        {
            for (var index = 0; index < 128; index++)
            {
                var result = owner.TryStart("held-" + index, () => release.Task, CancellationToken.None);
                Assert.Equal(StructuredTooltipOperationAdmission.Started, result.Status);
                held.Add(Assert.IsAssignableFrom<Task>(result.Completion));
            }

            Assert.Equal(1, Tick(plugin, domain, Capture));
            Assert.Equal(0, Cursor(plugin, domain));
            Assert.Equal(StructuredTooltipOperationAdmission.RejectedCapacity, admissions[0]);
            Assert.Null(admittedCompletion);
            Assert.Empty(captured);

            release.SetResult(true);
            await Task.WhenAll(held).WaitAsync(TimeSpan.FromSeconds(5));
            Interlocked.Add(ref nowTicks, TimeSpan.FromSeconds(29).Ticks);
            Assert.Equal(1, Tick(plugin, domain, Capture));
            Assert.Equal(0, Cursor(plugin, domain));
            Assert.Equal(StructuredTooltipOperationAdmission.RejectedCapacity, admissions[1]);
            Assert.Empty(captured);

            Interlocked.Add(ref nowTicks, TimeSpan.FromSeconds(2).Ticks);
            Assert.Equal(1, Tick(plugin, domain, Capture));
            Assert.Equal(1, Cursor(plugin, domain));
            Assert.Equal(StructuredTooltipOperationAdmission.Started, admissions[2]);
            await admittedCompletion!.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new uint[] { 4242, 4242, 4242 }, attempted);
            Assert.Equal(new uint[] { 4242 }, captured.ToArray());
        }
        finally
        {
            release.TrySetResult(true);
            await Task.WhenAll(held).WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Each real queue boundary captures at most its existing per-tick budget.</summary>
    /// <param name="domain">The prefetch family.</param>
    /// <param name="budget">The required capture count.</param>
    [Theory]
    [InlineData("Action", 6)]
    [InlineData("Trait", 6)]
    [InlineData("Item", 10)]
    public async Task QueueBoundaryPreservesCaptureBudget(string domain, int budget)
    {
        var ids = Enumerable.Range(1, budget + 2).Select(value => (uint)value).ToList();
        var plugin = CreateQueue(domain, ids);
        var owner = new StructuredTooltipOperationOwner();
        var captured = new ConcurrentQueue<uint>();
        var completions = new List<Task>();
        StructuredTooltipOperationAdmission Capture(uint id)
        {
            var result = owner.TryStart(domain + "-" + id, () =>
            {
                captured.Enqueue(id);
                return Task.CompletedTask;
            }, CancellationToken.None);
            Assert.Equal(StructuredTooltipOperationAdmission.Started, result.Status);
            completions.Add(result.Completion!);
            return result.Status;
        }

        Assert.Equal(budget, Tick(plugin, domain, Capture));
        Assert.Equal(budget, Cursor(plugin, domain));
        await Task.WhenAll(completions).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ids.Take(budget), captured.OrderBy(id => id));
    }

    /// <summary>Joined active work and terminal cooldown advance the cursor without running duplicate work.</summary>
    /// <param name="domain">The prefetch family.</param>
    [Theory]
    [InlineData("Action")]
    [InlineData("Trait")]
    [InlineData("Item")]
    public async Task JoinedActiveAndCooldownEntriesAdvanceWithoutDuplicateWork(string domain)
    {
        var owner = new StructuredTooltipOperationOwner(() => new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc));
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var original = owner.TryStart("existing", () =>
        {
            Interlocked.Increment(ref calls);
            return release.Task;
        }, CancellationToken.None);
        Assert.Equal(StructuredTooltipOperationAdmission.Started, original.Status);
        var plugin = CreateQueue(domain, [4242, 4242]);
        var attempts = 0;
        StructuredTooltipOperationAdmission Capture(uint id)
        {
            Assert.Equal(4242U, id);
            attempts++;
            var joined = owner.TryStart("existing",
                () => throw new InvalidOperationException("Joined capture cannot start another operation."),
                CancellationToken.None);
            Assert.Equal(StructuredTooltipOperationAdmission.Joined, joined.Status);
            Assert.Same(original.Completion, joined.Completion);
            return joined.Status;
        }

        try
        {
            Assert.Equal(2, Tick(plugin, domain, Capture));
            Assert.Equal(2, Cursor(plugin, domain));
            release.SetResult(true);
            await original.Completion!.WaitAsync(TimeSpan.FromSeconds(5));
            SetCursor(plugin, domain, 0);
            Assert.Equal(2, Tick(plugin, domain, Capture));
            Assert.Equal(2, Cursor(plugin, domain));
            Assert.Equal(4, attempts);
            Assert.Equal(1, calls);
        }
        finally
        {
            release.TrySetResult(true);
            await original.Completion!.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Invalid capture and ended lifetimes advance past terminal entries with distinct admission outcomes.</summary>
    /// <param name="domain">The prefetch family.</param>
    /// <param name="shutdown">Whether to exercise actual lifetime rejection.</param>
    [Theory]
    [InlineData("Action", false)]
    [InlineData("Trait", false)]
    [InlineData("Item", false)]
    [InlineData("Action", true)]
    [InlineData("Trait", true)]
    [InlineData("Item", true)]
    public void TerminalCaptureAndShutdownRejectionsAdvanceCursor(string domain, bool shutdown)
    {
        var plugin = CreateQueue(domain, [1, 2, 3]);
        var owner = new StructuredTooltipOperationOwner();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var attempted = new List<uint>();
        StructuredTooltipOperationAdmission Capture(uint id)
        {
            attempted.Add(id);
            if (!shutdown)
            {
                return StructuredTooltipOperationAdmission.RejectedCapture;
            }

            var result = owner.TryStart(domain + "-" + id,
                () => throw new InvalidOperationException("A cancelled lifetime cannot run."),
                cancellation.Token);
            Assert.Equal(StructuredTooltipOperationAdmission.RejectedShutdown, result.Status);
            Assert.Null(result.Completion);
            return result.Status;
        }

        Assert.Equal(3, Tick(plugin, domain, Capture));
        Assert.Equal(3, Cursor(plugin, domain));
        Assert.Equal(new uint[] { 1, 2, 3 }, attempted);
    }

    /// <summary>Initializes only real managed queue state so tests cannot accidentally depend on game services.</summary>
    private static PluginEntry CreateQueue(string domain, List<uint> ids)
    {
        var plugin = (PluginEntry)RuntimeHelpers.GetUninitializedObject(typeof(PluginEntry));
        typeof(PluginEntry).GetField(FieldPrefix(domain) + "Queue", Flags)!.SetValue(plugin, ids);
        SetCursor(plugin, domain, 0);
        return plugin;
    }

    /// <summary>Invokes the same typed queue boundary used by the Framework tick.</summary>
    private static int Tick(PluginEntry plugin, string domain, Func<uint, StructuredTooltipOperationAdmission> capture) => domain switch
    {
        "Action" => plugin.TickActionDetailPrefetchQueue(capture),
        "Trait" => plugin.TickTraitDetailPrefetchQueue(capture),
        "Item" => plugin.TickItemDetailPrefetchQueue(capture),
        _ => throw new ArgumentOutOfRangeException(nameof(domain)),
    };

    /// <summary>Reads the actual production prefetch cursor.</summary>
    private static int Cursor(PluginEntry plugin, string domain) =>
        (int)typeof(PluginEntry).GetField(FieldPrefix(domain) + "QueueIndex", Flags)!.GetValue(plugin)!;

    /// <summary>Resets the production cursor to replay identities already owned by active work or cooldown.</summary>
    private static void SetCursor(PluginEntry plugin, string domain, int value) =>
        typeof(PluginEntry).GetField(FieldPrefix(domain) + "QueueIndex", Flags)!.SetValue(plugin, value);

    /// <summary>Gets the existing per-domain queue field prefix.</summary>
    private static string FieldPrefix(string domain) => char.ToLowerInvariant(domain[0]) + domain[1..] + "DetailPrefetch";
}
