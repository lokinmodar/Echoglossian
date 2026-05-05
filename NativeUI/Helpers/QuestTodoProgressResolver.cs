// <copyright file="QuestTodoProgressResolver.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;
using System.Globalization;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;

namespace Echoglossian;

/// <summary>
///     Resolves live quest todo state from the native quest progress arrays so
///     quest handlers can key their cache and hover state off the current quest
///     sequence instead of depending only on the visible addon text.
/// </summary>
internal static class QuestTodoProgressResolver
{
    private static readonly ConcurrentDictionary<string, QuestTodoProgressSnapshot>
        QuestTodoProgressCache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Clears cached quest todo lookups.
    /// </summary>
    public static void Clear()
    {
        QuestTodoProgressCache.Clear();
        PluginRuntimeLog.Debug("[QuestTodoProgressResolver] Cleared quest todo progress cache.");
    }

    /// <summary>
    ///     Tries to resolve the current quest todo snapshot for the supplied
    ///     quest text.
    /// </summary>
    /// <param name="questText">The visible quest text or quest name.</param>
    /// <param name="snapshot">The resolved live todo snapshot, if any.</param>
    /// <returns>True when the quest todo state could be resolved.</returns>
    public static unsafe bool TryResolveQuestTodoProgress(
        string? questText,
        out QuestTodoProgressSnapshot snapshot)
    {
        snapshot = default;

        // Resolve the numeric quest ID from the display name so the underlying
        // progress resolver can perform its Lumina-keyed lookup. Passing a raw
        // display name to TryResolveQuestProgress(string) would fail because
        // that overload expects a numeric row-id string.
        if (!QuestLuminaResolver.TryResolveQuestId(questText, out var questIdStr) ||
            !uint.TryParse(
                questIdStr,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var questId))
        {
            return false;
        }

        return TryResolveQuestTodoProgress(questId, out snapshot);
    }

    /// <summary>
    ///     Tries to resolve the current quest todo snapshot for the supplied
    ///     quest row id.
    /// </summary>
    /// <param name="questId">The resolved quest row id.</param>
    /// <param name="snapshot">The resolved live todo snapshot, if any.</param>
    /// <returns>True when the quest todo state could be resolved.</returns>
    public static unsafe bool TryResolveQuestTodoProgress(
        uint questId,
        out QuestTodoProgressSnapshot snapshot)
    {
        snapshot = default;

        if (!QuestProgressResolver.TryResolveQuestProgress(
                questId.ToString(CultureInfo.InvariantCulture),
                out var questProgressSnapshot))
        {
            return false;
        }

        return TryResolveQuestTodoProgress(
            questProgressSnapshot,
            out snapshot);
    }

    /// <summary>
    ///     Tries to resolve the current quest todo snapshot for an already
    ///     resolved quest progress snapshot.
    /// </summary>
    /// <param name="questProgressSnapshot">The resolved quest progress snapshot.</param>
    /// <param name="snapshot">The resolved live todo snapshot, if any.</param>
    /// <returns>True when the quest todo state could be resolved.</returns>
    private static unsafe bool TryResolveQuestTodoProgress(
        QuestProgressSnapshot questProgressSnapshot,
        out QuestTodoProgressSnapshot snapshot)
    {
        snapshot = default;

        var todoArray = ToDoListNumberArray.Instance();
        if (todoArray == null)
        {
            return false;
        }

        var questSequence = questProgressSnapshot.QuestSequence;
        var objectiveProgress = todoArray->ObjectiveProgress;
        var objectiveCount = todoArray->ObjectiveCountForQuest;
        if ((uint)questSequence >= (uint)objectiveProgress.Length ||
            (uint)questSequence >= (uint)objectiveCount.Length)
        {
            return false;
        }

        var liveCacheKey =
            $"{questProgressSnapshot.CacheKey}:{objectiveProgress[questSequence]}:{objectiveCount[questSequence]}:{todoArray->QuestCount}";
        if (QuestTodoProgressCache.TryGetValue(liveCacheKey, out snapshot))
        {
            return true;
        }

        snapshot = new QuestTodoProgressSnapshot(
            questProgressSnapshot,
            objectiveProgress[questSequence],
            objectiveCount[questSequence],
            todoArray->QuestCount);

        QuestTodoProgressCache[liveCacheKey] = snapshot;
        return true;
    }
}

/// <summary>
///     Represents the live todo progress for a quest.
/// </summary>
/// <param name="QuestProgress">The resolved quest progress snapshot.</param>
/// <param name="ObjectiveProgress">The current objective progress value.</param>
/// <param name="ObjectiveCount">The current objective count value.</param>
/// <param name="CurrentDutyObjective">The current duty objective index.</param>
internal readonly record struct QuestTodoProgressSnapshot(
    QuestProgressSnapshot QuestProgress,
    int ObjectiveProgress,
    int ObjectiveCount,
    int QuestCount)
{
    /// <summary>
    ///     Gets a stable key for the live todo progress snapshot.
    /// </summary>
    public string CacheKey =>
        $"{this.QuestProgress.CacheKey}:{this.ObjectiveProgress}:{this.ObjectiveCount}:{this.QuestCount}";
}


