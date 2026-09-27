// <copyright file="QuestPlateRuntimeCache.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;
using System.Text;

using Echoglossian.EFCoreSqlite.Models.Journal;
using Echoglossian.Persistence;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Stores committed QuestPlate projections and owns one in-flight operation
///     per canonical lookup identity. This registry does not perform I/O.
/// </summary>
internal sealed class QuestPlateRuntimeCache
{
    private readonly object gate = new();
    private readonly Dictionary<QuestPlateRuntimeKey, Entry> entries = [];
    private readonly TimeSpan cooldown;
    private readonly Func<DateTimeOffset> utcNow;
    private long generation;

    /// <summary>Initializes a cache with a deterministic clock seam.</summary>
    internal QuestPlateRuntimeCache(
        Func<DateTimeOffset>? utcNow = null,
        TimeSpan? cooldown = null)
    {
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        this.cooldown = cooldown ?? TimeSpan.FromSeconds(1);
    }

    /// <summary>Gets the current ownership generation.</summary>
    internal long Generation => Volatile.Read(ref this.generation);

    /// <summary>Invalidates every projection and in-flight owner for reload.</summary>
    internal long AdvanceGeneration()
    {
        lock (this.gate)
        {
            this.entries.Clear();
            return Interlocked.Increment(ref this.generation);
        }
    }

    /// <summary>Gets whether an operation still belongs to the active generation.</summary>
    internal bool IsCurrentGeneration(long operationGeneration) => this.Generation == operationGeneration;

    /// <summary>Builds the collision-safe identity for one runtime lookup.</summary>
    /// <param name="plate">The immutable lookup payload.</param>
    /// <param name="scope">The captured translation reuse scope.</param>
    /// <returns>The canonical runtime key.</returns>
    internal static QuestPlateRuntimeKey CreateKey(QuestPlate plate, TranslationReuseScope scope)
    {
        ArgumentNullException.ThrowIfNull(plate);
        return new QuestPlateRuntimeKey(BuildIdentity(
            plate.QuestId,
            plate.QuestName,
            plate.OriginalQuestMessage,
            NormalizeLanguage(scope.SourceLanguageCode),
            NormalizeLanguage(scope.TargetLanguageCode),
            scope.RequireMatchingEngine ? scope.TranslationEngine?.ToString(CultureInfo.InvariantCulture) : "*",
            plate.SourceContentHash,
            plate.GameVersion));
    }

    /// <summary>Attempts to get a committed immutable projection.</summary>
    /// <param name="plate">The lookup payload.</param>
    /// <param name="scope">The lookup scope.</param>
    /// <param name="projection">The cloned committed projection.</param>
    /// <returns>True when a committed projection is available.</returns>
    internal bool TryGet(QuestPlate plate, TranslationReuseScope scope, out QuestPlate projection)
    {
        var key = CreateKey(plate, scope);
        lock (this.gate)
        {
            if (this.entries.TryGetValue(key, out var entry) && entry.Projection is not null)
            {
                projection = entry.Projection.Clone();
                return true;
            }
        }

        projection = null!;
        return false;
    }

    /// <summary>Registers the operation for a cache miss.</summary>
    /// <param name="key">The canonical lookup key.</param>
    /// <param name="completion">The terminal operation completion.</param>
    /// <returns>True only for the operation owner.</returns>
    internal bool TryRegister(QuestPlateRuntimeKey key, Task<QuestPlateRuntimeResult> completion)
        => this.TryRegisterCore(key, completion, out _);

    /// <summary>Atomically captures generation and registers one operation owner.</summary>
    internal bool TryRegister(
        QuestPlateRuntimeKey key,
        TaskCompletionSource<QuestPlateRuntimeResult> completionSource,
        out long operationGeneration)
    {
        ArgumentNullException.ThrowIfNull(completionSource);
        return this.TryRegisterCore(key, completionSource.Task, out operationGeneration);
    }

    /// <summary>Atomically either registers an owner or returns the current owner completion.</summary>
    internal bool TryRegisterOrJoin(
        QuestPlateRuntimeKey key,
        TaskCompletionSource<QuestPlateRuntimeResult> completionSource,
        out long operationGeneration,
        out Task<QuestPlateRuntimeResult>? joinedCompletion)
    {
        ArgumentNullException.ThrowIfNull(completionSource);
        lock (this.gate)
        {
            operationGeneration = this.generation;
            if (this.entries.TryGetValue(key, out var current))
            {
                if (current.Projection is null && current.DeferredWriteCompletion is null && current.CooldownUntil is not null && current.CooldownUntil <= this.utcNow())
                {
                    _ = this.entries.Remove(key);
                }
                else
                {
                    joinedCompletion = current.Completion;
                    return false;
                }
            }

            this.entries.Add(key, new Entry(completionSource, operationGeneration));
            joinedCompletion = null;
            return true;
        }
    }

    /// <summary>Atomically registers a write, joins an existing write, or claims a deferred write after a read.</summary>
    internal bool TryRegisterWriteOrJoin(
        QuestPlateRuntimeKey key,
        TaskCompletionSource<QuestPlateRuntimeResult> completionSource,
        out long operationGeneration,
        out Task<QuestPlateRuntimeResult>? joinedCompletion,
        out Task<QuestPlateRuntimeResult>? readCompletion)
    {
        ArgumentNullException.ThrowIfNull(completionSource);
        lock (this.gate)
        {
            operationGeneration = this.generation;
            if (!this.entries.TryGetValue(key, out var current) ||
                (current.Projection is null && current.DeferredWriteCompletion is null && current.CooldownUntil is not null && current.CooldownUntil <= this.utcNow()))
            {
                _ = this.entries.Remove(key);
                this.entries[key] = new Entry(completionSource, operationGeneration, isWrite: true);
                joinedCompletion = null;
                readCompletion = null;
                return true;
            }

            if (current.IsWrite)
            {
                joinedCompletion = current.Completion;
                readCompletion = null;
                return false;
            }

            if (current.DeferredWriteCompletion is not null)
            {
                joinedCompletion = current.DeferredWriteCompletion.Task;
                readCompletion = null;
                return false;
            }

            current.DeferredWriteCompletion = completionSource;
            joinedCompletion = null;
            readCompletion = current.Completion;
            return true;
        }
    }

    /// <summary>Promotes a previously claimed deferred write after its read is terminal.</summary>
    internal bool TryPromoteDeferredWrite(
        QuestPlateRuntimeKey key,
        long operationGeneration,
        TaskCompletionSource<QuestPlateRuntimeResult> completionSource)
    {
        lock (this.gate)
        {
            if (this.generation != operationGeneration || !this.entries.TryGetValue(key, out var current) ||
                current.Generation != operationGeneration || current.DeferredWriteCompletion != completionSource)
            {
                return false;
            }

            this.entries[key] = new Entry(completionSource, operationGeneration, isWrite: true);
            return true;
        }
    }

    private bool TryRegisterCore(
        QuestPlateRuntimeKey key,
        Task<QuestPlateRuntimeResult> completion,
        out long operationGeneration)
    {
        ArgumentNullException.ThrowIfNull(completion);
        lock (this.gate)
        {
            operationGeneration = this.generation;
            if (this.entries.TryGetValue(key, out var current))
            {
                if (current.DeferredWriteCompletion is not null || current.CooldownUntil is null || current.CooldownUntil > this.utcNow())
                {
                    return false;
                }

                _ = this.entries.Remove(key);
            }

            this.entries.Add(key, new Entry(completion, operationGeneration));
            return true;
        }
    }

    /// <summary>Attempts to get a same-key operation completion.</summary>
    /// <param name="key">The canonical lookup key.</param>
    /// <param name="completion">The existing completion.</param>
    /// <returns>True when an operation is already owned.</returns>
    internal bool TryGetCompletion(QuestPlateRuntimeKey key, out Task<QuestPlateRuntimeResult> completion)
    {
        lock (this.gate)
        {
            if (this.entries.TryGetValue(key, out var entry))
            {
                if (entry.Projection is null && entry.DeferredWriteCompletion is null && entry.CooldownUntil is not null && entry.CooldownUntil <= this.utcNow())
                {
                    _ = this.entries.Remove(key);
                    completion = null!;
                    return false;
                }

                completion = entry.Completion;
                return true;
            }
        }

        completion = null!;
        return false;
    }

    /// <summary>Publishes a projection after its database operation succeeds.</summary>
    /// <param name="key">The operation key.</param>
    /// <param name="projection">The completed projection.</param>
    internal bool Publish(QuestPlateRuntimeKey key, long operationGeneration, QuestPlate projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        lock (this.gate)
        {
            if (this.Generation != operationGeneration)
            {
                return false;
            }

            if (!this.entries.TryGetValue(key, out var entry))
            {
                entry = new Entry(Task.FromResult(new QuestPlateRuntimeResult(
                    PersistenceCompletionStatus.Succeeded,
                    projection.Clone())), operationGeneration);
                this.entries.Add(key, entry);
            }

            if (entry.Generation != operationGeneration)
            {
                return false;
            }

            entry.Projection = projection.Clone();
            return true;
        }
    }

    /// <summary>Removes a non-successful operation so a later cooldown owner can retry.</summary>
    /// <param name="key">The terminal operation key.</param>
    internal void RemoveOperation(QuestPlateRuntimeKey key, long operationGeneration)
    {
        lock (this.gate)
        {
            if (this.Generation == operationGeneration && this.entries.TryGetValue(key, out var entry) && entry.Generation == operationGeneration && entry.Projection is null)
            {
                entry.CooldownUntil = this.utcNow().Add(this.cooldown);
            }
        }
    }

    /// <summary>Completes a registered operation and atomically records its terminal state.</summary>
    internal bool Complete(
        QuestPlateRuntimeKey key,
        long operationGeneration,
        QuestPlateRuntimeResult result)
    {
        lock (this.gate)
        {
            if (this.generation != operationGeneration || !this.entries.TryGetValue(key, out var entry) || entry.Generation != operationGeneration)
            {
                return false;
            }

            if (result.Projection is not null && result.Status is PersistenceCompletionStatus.Succeeded or PersistenceCompletionStatus.Unchanged)
            {
                entry.Projection = result.Projection.Clone();
                entry.CooldownUntil = null;
            }
            else
            {
                entry.CooldownUntil = this.utcNow().Add(this.cooldown);
            }

            entry.Complete(result);
            return true;
        }
    }

    private static string NormalizeLanguage(string? language) => RuntimeLanguageHelper.NormalizeLanguage(language).ToUpperInvariant();

    private static string BuildIdentity(params string?[] segments)
    {
        var builder = new StringBuilder();
        foreach (var segment in segments)
        {
            var value = segment ?? string.Empty;
            _ = builder.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
        }

        return builder.ToString();
    }

    private sealed class Entry
    {
        private readonly TaskCompletionSource<QuestPlateRuntimeResult>? completionSource;

        internal Entry(Task<QuestPlateRuntimeResult> completion, long generation)
        {
            this.Completion = completion;
            this.Generation = generation;
        }

        internal Entry(TaskCompletionSource<QuestPlateRuntimeResult> completionSource, long generation, bool isWrite = false)
        {
            this.completionSource = completionSource;
            this.Completion = completionSource.Task;
            this.Generation = generation;
            this.IsWrite = isWrite;
        }

        internal Task<QuestPlateRuntimeResult> Completion { get; }

        internal QuestPlate? Projection { get; set; }

        internal long Generation { get; }

        internal bool IsWrite { get; }

        internal TaskCompletionSource<QuestPlateRuntimeResult>? DeferredWriteCompletion { get; set; }

        internal DateTimeOffset? CooldownUntil { get; set; }

        internal void Complete(QuestPlateRuntimeResult result) => _ = this.completionSource?.TrySetResult(result);

    }
}

/// <summary>Identifies one canonical QuestPlate runtime operation.</summary>
/// <param name="Value">The length-prefixed lookup identity.</param>
internal readonly record struct QuestPlateRuntimeKey(string Value);

/// <summary>Represents a completed QuestPlate runtime operation.</summary>
/// <param name="Status">The coordinator terminal status.</param>
/// <param name="Projection">The immutable projection when found.</param>
internal readonly record struct QuestPlateRuntimeResult(
    PersistenceCompletionStatus Status,
    QuestPlate? Projection);
