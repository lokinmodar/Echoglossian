// <copyright file="QuestLuminaResolver.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

using Echoglossian.EFCoreSqlite.Models.Journal;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Echoglossian;

/// <summary>
///     Resolves quest metadata from Lumina so quest records can be enriched
///     without changing the current UI-driven capture flow.
/// </summary>
public static class QuestLuminaResolver
{
    private static readonly ConcurrentDictionary<string, string> QuestIdCache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly object QuestIndexLock = new();

    private static Dictionary<string, string>? questNameIndex;

    /// <summary>
    ///     Clears cached Lumina quest lookups.
    /// </summary>
    public static void Clear()
    {
        QuestIdCache.Clear();
        lock (QuestIndexLock)
        {
            questNameIndex = null;
        }
    }

    /// <summary>
    ///     Tries to populate the quest identifier on an existing quest plate.
    /// </summary>
    /// <param name="questPlate">The quest plate to enrich.</param>
    /// <returns>True when a quest id was resolved or already present.</returns>
    public static bool TryPopulateQuestId(QuestPlate questPlate)
    {
        if (questPlate == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(questPlate.QuestId))
        {
            return true;
        }

        if (!TryResolveQuestId(questPlate.QuestName, out var questId))
        {
            return false;
        }

        questPlate.QuestId = questId;
        return true;
    }

    /// <summary>
    ///     Tries to resolve a quest identifier from the current client-language
    ///     Lumina quest sheet.
    /// </summary>
    /// <param name="questName">The visible quest name.</param>
    /// <param name="questId">The resolved quest id, if any.</param>
    /// <returns>True when the quest id was found.</returns>
    public static bool TryResolveQuestId(string? questName, out string questId)
    {
        questId = string.Empty;

        var normalizedQuestName = NormalizeQuestName(questName);
        if (normalizedQuestName.Length == 0)
        {
            return false;
        }

        if (QuestIdCache.TryGetValue(normalizedQuestName, out var cachedQuestId))
        {
            questId = cachedQuestId;
            return questId.Length != 0;
        }

        var dataManager = Echoglossian.DManager;
        if (dataManager == null)
        {
            return false;
        }

        var questSheet =
            dataManager.GetExcelSheet<Quest>(Echoglossian.ClientStateInterface.ClientLanguage);
        if (questSheet == null)
        {
            return false;
        }

        var questIndex = GetQuestNameIndex(questSheet);
        if (!questIndex.TryGetValue(normalizedQuestName, out questId))
        {
            return false;
        }

        QuestIdCache[normalizedQuestName] = questId;
        return questId.Length != 0;
    }

    private static Dictionary<string, string> GetQuestNameIndex(
        ExcelSheet<Quest> questSheet)
    {
        if (questNameIndex != null)
        {
            return questNameIndex;
        }

        lock (QuestIndexLock)
        {
            if (questNameIndex != null)
            {
                return questNameIndex;
            }

            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var quest in questSheet)
            {
                var questName = ReadQuestString(quest, "Name", "Text", "QuestName");
                var normalizedQuestName = NormalizeQuestName(questName);
                if (normalizedQuestName.Length == 0)
                {
                    continue;
                }

                var questId = ReadQuestString(quest, "RowId", "Id");
                if (questId.Length == 0)
                {
                    continue;
                }

                index.TryAdd(normalizedQuestName, questId);
            }

            questNameIndex = index;
            return questNameIndex;
        }
    }

    private static string ReadQuestString(object quest, params string[] propertyNames)
    {
        var questType = quest.GetType();
        foreach (var propertyName in propertyNames)
        {
            var property = questType.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);

            if (property?.GetValue(quest) is null)
            {
                continue;
            }

            var value = property.GetValue(quest)?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string NormalizeQuestName(string? questName)
    {
        return string.IsNullOrWhiteSpace(questName)
            ? string.Empty
            : questName.Trim();
    }
}
