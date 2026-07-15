// <copyright file="ToDoListRuntimeAvailability.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
/// Describes whether the current ToDoList runtime can render any translated
/// state and whether it still has pending quest rows to retry.
/// </summary>
/// <param name="HasRenderableEntries">
/// Whether the current runtime resolved at least one visible quest or
/// objective row.
/// </param>
/// <param name="HasPendingTranslations">
/// Whether at least one visible quest still lacks canonical translated data.
/// </param>
internal readonly record struct ToDoListRuntimeAvailability(
    bool HasRenderableEntries,
    bool HasPendingTranslations)
{
    /// <summary>
    /// Builds one availability snapshot from the current resolved-entry and
    /// blocking-quest counts.
    /// </summary>
    /// <param name="resolvedEntryCount">The number of resolved runtime rows.</param>
    /// <param name="blockingQuestCount">
    /// The number of visible quests still waiting on translated data.
    /// </param>
    /// <returns>The derived availability snapshot.</returns>
    public static ToDoListRuntimeAvailability FromCounts(
        int resolvedEntryCount,
        int blockingQuestCount)
    {
        return new ToDoListRuntimeAvailability(
            HasRenderableEntries: resolvedEntryCount > 0,
            HasPendingTranslations: blockingQuestCount > 0);
    }
}
