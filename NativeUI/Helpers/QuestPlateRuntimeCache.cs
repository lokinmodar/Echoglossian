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
    private long generation;

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
    {
        ArgumentNullException.ThrowIfNull(completion);
        lock (this.gate)
        {
            if (this.entries.TryGetValue(key, out var current))
            {
                if (current.CooldownUntil is null || current.CooldownUntil > DateTimeOffset.UtcNow)
                {
                    return false;
                }

                _ = this.entries.Remove(key);
            }

            this.entries.Add(key, new Entry(completion));
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
    internal void Publish(QuestPlateRuntimeKey key, QuestPlate projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        lock (this.gate)
        {
            if (!this.entries.TryGetValue(key, out var entry))
            {
                entry = new Entry(Task.FromResult(new QuestPlateRuntimeResult(
                    PersistenceCompletionStatus.Succeeded,
                    projection.Clone())));
                this.entries.Add(key, entry);
            }

            entry.Projection = projection.Clone();
        }
    }

    /// <summary>Removes a non-successful operation so a later cooldown owner can retry.</summary>
    /// <param name="key">The terminal operation key.</param>
    internal void RemoveOperation(QuestPlateRuntimeKey key)
    {
        lock (this.gate)
        {
            if (this.entries.TryGetValue(key, out var entry) && entry.Projection is null)
            {
                entry.CooldownUntil = DateTimeOffset.UtcNow.AddSeconds(1);
            }
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

    private sealed class Entry(Task<QuestPlateRuntimeResult> completion)
    {
        internal Task<QuestPlateRuntimeResult> Completion { get; } = completion;

        internal QuestPlate? Projection { get; set; }

        internal DateTimeOffset? CooldownUntil { get; set; }
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
