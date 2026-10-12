// <copyright file="StructuredTooltipOperationOwner.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>Retains bounded canonical ownership through translation and persistence completion.</summary>
internal sealed class StructuredTooltipOperationOwner
{
    private const int Capacity = 128;
    private readonly Dictionary<string, Entry> operations = new(StringComparer.Ordinal);
    private readonly object gate = new();

    /// <summary>Shares active work or retains a terminal cooldown without creating another queue.</summary>
    /// <param name="key">The complete captured canonical identity.</param>
    /// <param name="operation">The managed orchestration to run on a worker.</param>
    /// <param name="cancellationToken">The plugin lifetime.</param>
    /// <returns>The shared completion, or null when capacity or shutdown rejects admission.</returns>
    internal Task? TryStart(string key, Func<Task> operation, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var now = DateTime.UtcNow;
            foreach (var expired in this.operations.Where(pair =>
                         pair.Value.CompletedAt.HasValue &&
                         now - pair.Value.CompletedAt.Value >= TimeSpan.FromSeconds(30)).Select(pair => pair.Key).ToArray())
            {
                this.operations.Remove(expired);
            }

            if (this.operations.TryGetValue(key, out var existing))
            {
                return existing.Completion;
            }

            if (this.operations.Count >= Capacity)
            {
                return null;
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
                        entry.CompletedAt = DateTime.UtcNow;
                    }
                }
            });
            return entry.Completion;
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
