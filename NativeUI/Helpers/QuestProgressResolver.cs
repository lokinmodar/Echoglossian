// <copyright file="QuestProgressResolver.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;

using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

using QuestManager = FFXIVClientStructs.FFXIV.Client.Game.QuestManager;

namespace Echoglossian;

/// <summary>
///     Resolves quest progression data from Lumina and the live quest manager
///     so quest windows can key their hover and translation behavior from a
///     stable quest identity instead of raw UI text alone.
/// </summary>
internal static class QuestProgressResolver
{
    private static readonly ConcurrentDictionary<string, QuestProgressSnapshot> QuestProgressCache =
        new(StringComparer.Ordinal);

    /// <summary>
    ///     Clears cached quest progression lookups.
    /// </summary>
    public static void Clear()
    {
        QuestProgressCache.Clear();
        PluginRuntimeLog.Debug("[QuestProgressResolver] Cleared quest progress cache.");
    }

    /// <summary>
    ///     Tries to resolve the current quest progression snapshot for the given
    ///     quest plate.
    /// </summary>
    /// <param name="questPlate">The quest plate to enrich with progression data.</param>
    /// <param name="snapshot">The resolved progression snapshot, if any.</param>
    /// <returns>True when progression data could be resolved.</returns>
    public static bool TryResolveQuestProgress(
        QuestPlate? questPlate,
        out QuestProgressSnapshot snapshot)
    {
        snapshot = default;

        if (questPlate == null)
        {
            return false;
        }

        if (!QuestLuminaResolver.TryPopulateQuestId(questPlate))
        {
            return false;
        }

        return TryResolveQuestProgress(questPlate.QuestId, out snapshot);
    }

    /// <summary>
    ///     Tries to resolve the current quest progression snapshot for the
    ///     supplied quest id.
    /// </summary>
    /// <param name="questIdText">The quest id resolved from Lumina.</param>
    /// <param name="snapshot">The resolved progression snapshot, if any.</param>
    /// <returns>True when progression data could be resolved.</returns>
    public static bool TryResolveQuestProgress(
        string? questIdText,
        out QuestProgressSnapshot snapshot)
    {
        snapshot = default;

        if (string.IsNullOrWhiteSpace(questIdText) ||
            !uint.TryParse(
                questIdText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var questId))
        {
            return false;
        }

        var dataManager = Echoglossian.DManager;
        if (dataManager == null)
        {
            return false;
        }

        var questSheet =
            dataManager.GetExcelSheet<Quest>(Echoglossian.ClientStateInterface.ClientLanguage);
        if (questSheet == null ||
            !TryResolveQuestRow(
                questSheet,
                questId,
                out var resolvedQuestRowId,
                out var questRow))
        {
            return false;
        }

        var runtimeQuestId = (ushort)(resolvedQuestRowId & 0xFFFF);
        var questSequence = QuestManager.GetQuestSequence(runtimeQuestId);
        var cacheKey = $"{resolvedQuestRowId}:{questSequence}";
        if (QuestProgressCache.TryGetValue(cacheKey, out snapshot))
        {
            return true;
        }

        var questSheetName = BuildQuestTextSheetName(questRow);
        if (questSheetName.Length == 0)
        {
            return false;
        }

        var questTextSheet =
            dataManager.GameData.GetExcelSheet<RawRow>(name: questSheetName);
        if (questTextSheet == null || questTextSheet.Count == 0)
        {
            return false;
        }

        var questName = QuestLuminaResolver.GetQuestNameText(questRow);
        var (questSteps, questSeqs, questSystemTexts) = ReadQuestTextRows(questTextSheet);
        if (questSteps.Count == 0 && questSeqs.Count == 0)
        {
            return false;
        }

        var contentHash = QuestContentHash.Compute(questSeqs, questSteps, questSystemTexts);

        snapshot = new QuestProgressSnapshot(
            resolvedQuestRowId,
            questSequence,
            questName,
            questSheetName,
            questSteps,
            questSeqs,
            questSystemTexts,
            contentHash);

        QuestProgressCache[cacheKey] = snapshot;
        return true;
    }

    private static bool TryResolveQuestRow(
        ExcelSheet<Quest> questSheet,
        uint questId,
        out uint resolvedQuestRowId,
        out Quest questRow)
    {
        resolvedQuestRowId = 0;
        questRow = default;

        if (questId < 0x10000)
        {
            var promotedQuestRowId = questId + 0x10000;
            if (questSheet.TryGetRow(promotedQuestRowId, out questRow))
            {
                resolvedQuestRowId = promotedQuestRowId;
                return true;
            }
        }

        if (questSheet.TryGetRow(questId, out questRow))
        {
            resolvedQuestRowId = questId;
            return true;
        }

        return false;
    }

    private static (List<QuestProgressEntry> Steps, List<QuestProgressEntry> Seqs, List<QuestProgressEntry> SystemTexts) ReadQuestTextRows(
        ExcelSheet<RawRow> questTextSheet)
    {
        var steps = new List<QuestProgressEntry>();
        var seqs = new List<QuestProgressEntry>();
        var systemTexts = new List<QuestProgressEntry>();
        var evaluator = Echoglossian.SeStringEvaluator;

        var rowCount = Convert.ToInt32(questTextSheet.Count, CultureInfo.InvariantCulture);
        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var row = questTextSheet.GetRow((uint)rowIndex);

            ReadOnlySeString rawKey = row.ReadStringColumn(0);
            ReadOnlySeString rawValue = row.ReadStringColumn(1);
            var keyText = EvaluateQuestText(rawKey, evaluator);
            var valueText = EvaluateQuestText(rawValue, evaluator);

            if (keyText.Length == 0 || valueText.Length == 0)
            {
                continue;
            }

            var entry = new QuestProgressEntry(rawKey, rawValue, keyText, valueText);

            if (keyText.Contains("_TODO_", StringComparison.Ordinal))
            {
                steps.Add(entry);
            }
            else if (keyText.Contains("_SEQ_", StringComparison.Ordinal))
            {
                seqs.Add(entry);
            }
            else if (keyText.Contains("_SYSTEM_", StringComparison.Ordinal))
            {
                systemTexts.Add(entry);
            }
        }

        return (steps, seqs, systemTexts);
    }

    private static string EvaluateQuestText(
        ReadOnlySeString text,
        ISeStringEvaluator? evaluator)
    {
        if (evaluator == null)
        {
            return text.ExtractText();
        }

        try
        {
            return evaluator.Evaluate(
                    text,
                    language: Echoglossian.ClientStateInterface.ClientLanguage)
                .ExtractText();
        }
        catch (Exception)
        {
            return text.ExtractText();
        }
    }

    private static string BuildQuestTextSheetName(Quest questRow)
    {
        var questId = QuestLuminaResolver.GetQuestSheetIdText(questRow);
        if (questId.Length < 5)
        {
            return string.Empty;
        }

        var dir = questId.Substring(questId.Length - 5, 3);
        return $"quest/{dir}/{questId}";
    }
}

/// <summary>
///     Represents a quest progression snapshot derived from Lumina and the
///     live quest manager.
/// </summary>
/// <param name="QuestId">The quest identifier.</param>
/// <param name="QuestSequence">The live quest sequence.</param>
/// <param name="QuestName">The quest name resolved from Lumina.</param>
/// <param name="QuestSheetName">The text sheet name used for the quest.</param>
/// <param name="QuestSteps">The TODO row texts (active objectives).</param>
/// <param name="QuestSeqTexts">The SEQ row texts (journal summaries per phase).</param>
/// <param name="QuestSystemTexts">The SYSTEM row texts (cinematic captions).</param>
/// <param name="ContentHash">A stable content fingerprint of all translatable row texts.</param>
internal readonly record struct QuestProgressSnapshot(
    uint QuestId,
    byte QuestSequence,
    string QuestName,
    string QuestSheetName,
    IReadOnlyList<QuestProgressEntry> QuestSteps,
    IReadOnlyList<QuestProgressEntry> QuestSeqTexts,
    IReadOnlyList<QuestProgressEntry> QuestSystemTexts,
    string ContentHash)
{
    /// <summary>
    /// Gets a stable cache key for the quest progress snapshot.
    /// </summary>
    public string CacheKey => $"{this.QuestId}:{this.QuestSequence}";
}

/// <summary>
///     Represents a quest step entry preserving the original structured text.
/// </summary>
/// <param name="OriginalKey">The original structured key payload.</param>
/// <param name="OriginalText">The original structured text payload.</param>
/// <param name="KeyText">The evaluated text for the key.</param>
/// <param name="Text">The evaluated quest step text.</param>
internal readonly record struct QuestProgressEntry(
    ReadOnlySeString OriginalKey,
    ReadOnlySeString OriginalText,
    string KeyText,
    string Text);


