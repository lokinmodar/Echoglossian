// <copyright file="StructuredTooltipOperationOwner.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>Specifies whether canonical ownership was admitted or must retry.</summary>
internal enum StructuredTooltipOperationAdmission
{
    /// <summary>New work was started.</summary>
    Started,
    /// <summary>Active work or terminal cooldown already owns the identity.</summary>
    Joined,
    /// <summary>No slot is available; the current prefetch entry must retry.</summary>
    RejectedCapacity,
    /// <summary>The plugin lifetime has ended.</summary>
    RejectedShutdown,
    /// <summary>No valid captured payload is available for this entry.</summary>
    RejectedCapture,
}

/// <summary>Returns admission separately from the shared terminal completion.</summary>
/// <param name="Status">The nonblocking admission outcome.</param>
/// <param name="Completion">The shared completion, if work owns this identity.</param>
internal readonly record struct StructuredTooltipOperationStart(StructuredTooltipOperationAdmission Status, Task? Completion);

/// <summary>Retains bounded canonical ownership through translation and persistence completion.</summary>
internal sealed class StructuredTooltipOperationOwner
{
    private const int Capacity = 128;
    private readonly Dictionary<string, Entry> operations = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private readonly Func<DateTime> utcNow;

    /// <summary>Initializes bounded ownership with an injectable clock for deterministic cooldown tests.</summary>
    /// <param name="utcNow">The clock; production uses UTC wall time.</param>
    internal StructuredTooltipOperationOwner(Func<DateTime>? utcNow = null)
    {
        this.utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>Shares active work or retains a terminal cooldown without creating another queue.</summary>
    /// <param name="key">The complete captured canonical identity.</param>
    /// <param name="operation">The managed orchestration to run on a worker.</param>
    /// <param name="cancellationToken">The plugin lifetime.</param>
    /// <returns>The admission outcome and the shared completion when admitted.</returns>
    internal StructuredTooltipOperationStart TryStart(string key, Func<Task> operation, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new StructuredTooltipOperationStart(StructuredTooltipOperationAdmission.RejectedShutdown, null);
            }

            var now = this.utcNow();
            foreach (var expired in this.operations.Where(pair =>
                         pair.Value.Completion.IsCompleted && pair.Value.CompletedAt.HasValue &&
                         now - pair.Value.CompletedAt.Value >= TimeSpan.FromSeconds(30)).Select(pair => pair.Key).ToArray())
            {
                this.operations.Remove(expired);
            }

            if (this.operations.TryGetValue(key, out var existing))
            {
                return new StructuredTooltipOperationStart(StructuredTooltipOperationAdmission.Joined, existing.Completion);
            }

            if (this.operations.Count >= Capacity)
            {
                return new StructuredTooltipOperationStart(StructuredTooltipOperationAdmission.RejectedCapacity, null);
            }

            var entry = new Entry();
            this.operations.Add(key, entry);
            entry.Completion = Task.Run(async () =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await operation().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Plugin shutdown is a terminal outcome without cache publication.
                }
                catch (Exception exception)
                {
                    PluginRuntimeLog.Debug("StructuredTooltipPrefetch", $"Terminal operation failure: {exception.Message}");
                }
                finally
                {
                    lock (this.gate)
                    {
                        entry.CompletedAt = this.utcNow();
                    }
                }
            });
            return new StructuredTooltipOperationStart(StructuredTooltipOperationAdmission.Started, entry.Completion);
        }
    }

    /// <summary>Holds only lifetime ownership, never translated data.</summary>
    private sealed class Entry
    {
        /// <summary>Gets or sets the operation's shared terminal completion.</summary>
        internal Task Completion { get; set; } = Task.CompletedTask;

        /// <summary>Gets or sets terminal time for bounded failure cooldown.</summary>
        internal DateTime? CompletedAt { get; set; }
    }
}
