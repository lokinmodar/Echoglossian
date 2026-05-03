// <copyright file="QuestLuminaResolver.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;
using System.Globalization;
using Echoglossian.EFCoreSqlite.Models.Journal;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Echoglossian;

/// <summary>
///     Resolves quest metadata from Lumina so quest records can be enriched
///     without changing the current UI-driven capture flow.
/// </summary>
/// <remarks>
///     The cache-and-index shape follows the general Lumina usage pattern seen
///     in several Practical Dalamud plugins, especially Critical-Impact
///     projects that reuse IDataManager and ExcelSheet access instead of
///     querying the sheet repeatedly.
/// </remarks>
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

        PluginRuntimeLog.Debug("[QuestLuminaResolver] Cleared quest Lumina caches.");
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
        if (!questIndex.TryGetValue(normalizedQuestName, out var resolvedQuestId) ||
            string.IsNullOrWhiteSpace(resolvedQuestId))
        {
            return false;
        }

        questId = resolvedQuestId;
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
                var questName = GetQuestNameText(quest);
                var normalizedQuestName = NormalizeQuestName(questName);
                if (normalizedQuestName.Length == 0)
                {
                    continue;
                }

                var questId = GetQuestRowIdText(quest);
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

    /// <summary>
    ///     Resolves the display name text from a Lumina quest row.
    /// </summary>
    /// <param name="quest">The Lumina quest row.</param>
    /// <returns>The trimmed quest name, or an empty string when unavailable.</returns>
    internal static string GetQuestNameText(Quest quest)
    {
        return quest.Name.ExtractText().Trim();
    }

    /// <summary>
    ///     Resolves the stable numeric quest row identifier as text.
    /// </summary>
    /// <param name="quest">The Lumina quest row.</param>
    /// <returns>The quest row id as invariant text.</returns>
    internal static string GetQuestRowIdText(Quest quest)
    {
        return quest.RowId.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Resolves the raw internal quest sheet identifier text.
    /// </summary>
    /// <param name="quest">The Lumina quest row.</param>
    /// <returns>The trimmed internal quest sheet id, or an empty string.</returns>
    internal static string GetQuestSheetIdText(Quest quest)
    {
        return quest.Id.ExtractText().Trim();
    }

    private static string NormalizeQuestName(string? questName)
    {
        return string.IsNullOrWhiteSpace(questName)
            ? string.Empty
            : questName.Trim();
    }
}


