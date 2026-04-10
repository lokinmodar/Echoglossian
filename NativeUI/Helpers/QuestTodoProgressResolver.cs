// <copyright file="QuestTodoProgressResolver.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;
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

        if (!QuestProgressResolver.TryResolveQuestProgress(
                questText,
                out var questProgressSnapshot))
        {
            return false;
        }

        var cacheKey = questProgressSnapshot.CacheKey;
        if (QuestTodoProgressCache.TryGetValue(cacheKey, out snapshot))
        {
            return true;
        }

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

        snapshot = new QuestTodoProgressSnapshot(
            questProgressSnapshot,
            objectiveProgress[questSequence],
            objectiveCount[questSequence],
            todoArray->QuestCount);

        QuestTodoProgressCache[cacheKey] = snapshot;
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
