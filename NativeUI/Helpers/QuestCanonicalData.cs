// <copyright file="QuestCanonicalData.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Represents the current canonical quest payload resolved from Lumina and
///     live quest progress before it is projected into legacy-compatible
///     persistence and addon display shapes.
/// </summary>
internal sealed class QuestCanonicalData
{
    private QuestCanonicalData(
        QuestProgressSnapshot questProgressSnapshot,
        string gameVersion)
    {
        this.QuestProgressSnapshot = questProgressSnapshot;
        this.GameVersion = gameVersion;
        this.CurrentSequenceText = ResolveCurrentSequenceText(questProgressSnapshot);
        this.SummaryRowsByKey = BuildRowMapByKey(questProgressSnapshot.QuestSeqTexts);
        this.SummaryRowsByText = BuildRowMapByText(questProgressSnapshot.QuestSeqTexts);
        this.ObjectiveRowsByKey = BuildRowMapByKey(questProgressSnapshot.QuestSteps);
        this.ObjectiveRowsByText = BuildRowMapByText(questProgressSnapshot.QuestSteps);
        this.SystemRowsByKey = BuildRowMapByKey(questProgressSnapshot.QuestSystemTexts);
        this.SystemRowsByText = BuildRowMapByText(questProgressSnapshot.QuestSystemTexts);
    }

    /// <summary>
    ///     Gets the backing progress snapshot that produced this canonical
    ///     quest payload.
    /// </summary>
    public QuestProgressSnapshot QuestProgressSnapshot { get; }

    /// <summary>
    ///     Gets the game version associated with this canonical quest payload.
    /// </summary>
    public string GameVersion { get; }

    /// <summary>
    ///     Gets the current SEQ text for the quest's live sequence.
    /// </summary>
    public string CurrentSequenceText { get; }

    /// <summary>
    ///     Gets the canonical SEQ rows keyed by quest row key.
    /// </summary>
    public IReadOnlyDictionary<string, string> SummaryRowsByKey { get; }

    /// <summary>
    ///     Gets the legacy-compatible SEQ projection keyed by source text.
    ///     Duplicate source texts collapse in this view.
    /// </summary>
    public IReadOnlyDictionary<string, string> SummaryRowsByText { get; }

    /// <summary>
    ///     Gets the canonical TODO rows keyed by quest row key.
    /// </summary>
    public IReadOnlyDictionary<string, string> ObjectiveRowsByKey { get; }

    /// <summary>
    ///     Gets the legacy-compatible TODO projection keyed by source text.
    ///     Duplicate source texts collapse in this view.
    /// </summary>
    public IReadOnlyDictionary<string, string> ObjectiveRowsByText { get; }

    /// <summary>
    ///     Gets the canonical SYSTEM rows keyed by quest row key.
    /// </summary>
    public IReadOnlyDictionary<string, string> SystemRowsByKey { get; }

    /// <summary>
    ///     Gets the legacy-compatible SYSTEM projection keyed by source text.
    ///     Duplicate source texts collapse in this view.
    /// </summary>
    public IReadOnlyDictionary<string, string> SystemRowsByText { get; }

    /// <summary>
    ///     Gets a value indicating whether the current legacy-compatible text
    ///     projection loses row identity due to repeated source text.
    /// </summary>
    public bool HasLossyTextProjection =>
        this.SummaryRowsByKey.Count != this.SummaryRowsByText.Count ||
        this.ObjectiveRowsByKey.Count != this.ObjectiveRowsByText.Count ||
        this.SystemRowsByKey.Count != this.SystemRowsByText.Count;

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.AppendLine("QuestCanonicalData");
        builder.AppendLine($"  QuestId: {this.QuestProgressSnapshot.QuestId}");
        builder.AppendLine($"  QuestSequence: {this.QuestProgressSnapshot.QuestSequence}");
        builder.AppendLine($"  CacheKey: {this.QuestProgressSnapshot.CacheKey}");
        builder.AppendLine($"  QuestName: {this.QuestProgressSnapshot.QuestName}");
        builder.AppendLine($"  QuestTextSheetName: {this.QuestProgressSnapshot.QuestSheetName}");
        builder.AppendLine($"  GameVersion: {this.GameVersion}");
        builder.AppendLine($"  ContentHash: {this.QuestProgressSnapshot.ContentHash}");
        builder.AppendLine($"  CurrentSequenceText: {this.CurrentSequenceText}");
        builder.AppendLine($"  HasLossyTextProjection: {this.HasLossyTextProjection}");
        AppendDictionary(builder, "SummaryRowsByKey", this.SummaryRowsByKey);
        AppendDictionary(builder, "SummaryRowsByText", this.SummaryRowsByText);
        AppendDictionary(builder, "ObjectiveRowsByKey", this.ObjectiveRowsByKey);
        AppendDictionary(builder, "ObjectiveRowsByText", this.ObjectiveRowsByText);
        AppendDictionary(builder, "SystemRowsByKey", this.SystemRowsByKey);
        AppendDictionary(builder, "SystemRowsByText", this.SystemRowsByText);
        return builder.ToString().TrimEnd();
    }

    /// <summary>
    ///     Creates canonical quest data from the supplied quest progress
    ///     snapshot.
    /// </summary>
    /// <param name="questProgressSnapshot">The resolved quest progress snapshot.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <returns>The canonical quest payload.</returns>
    public static QuestCanonicalData Create(
        QuestProgressSnapshot questProgressSnapshot,
        string gameVersion)
    {
        return new QuestCanonicalData(questProgressSnapshot, gameVersion);
    }

    /// <summary>
    ///     Resolves the current SEQ row text for the supplied quest progress
    ///     snapshot.
    /// </summary>
    /// <param name="questProgressSnapshot">The resolved quest progress snapshot.</param>
    /// <returns>The current SEQ row text, or the first non-empty SEQ row text.</returns>
    public static string ResolveCurrentSequenceText(
        QuestProgressSnapshot questProgressSnapshot)
    {
        if (questProgressSnapshot.QuestSeqTexts.Count == 0)
        {
            return string.Empty;
        }

        var questSequenceIndex = Math.Min(
            (int)questProgressSnapshot.QuestSequence,
            questProgressSnapshot.QuestSeqTexts.Count - 1);
        if (questSequenceIndex >= 0 &&
            questSequenceIndex < questProgressSnapshot.QuestSeqTexts.Count)
        {
            var currentQuestSequenceText =
                questProgressSnapshot.QuestSeqTexts[questSequenceIndex].Text;
            if (!string.IsNullOrWhiteSpace(currentQuestSequenceText))
            {
                return currentQuestSequenceText;
            }
        }

        foreach (var questSequenceEntry in questProgressSnapshot.QuestSeqTexts)
        {
            if (!string.IsNullOrWhiteSpace(questSequenceEntry.Text))
            {
                return questSequenceEntry.Text;
            }
        }

        return string.Empty;
    }

    /// <summary>
    ///     Tries to resolve the current SEQ entry for this canonical quest
    ///     payload.
    /// </summary>
    /// <param name="currentSequenceEntry">The current SEQ entry.</param>
    /// <returns>True when the current SEQ entry could be resolved.</returns>
    public bool TryGetCurrentSequenceEntry(out QuestProgressEntry currentSequenceEntry)
    {
        currentSequenceEntry = default;

        if (this.QuestProgressSnapshot.QuestSeqTexts.Count == 0)
        {
            return false;
        }

        var questSequenceIndex = Math.Min(
            (int)this.QuestProgressSnapshot.QuestSequence,
            this.QuestProgressSnapshot.QuestSeqTexts.Count - 1);
        if (questSequenceIndex >= 0 &&
            questSequenceIndex < this.QuestProgressSnapshot.QuestSeqTexts.Count)
        {
            var resolvedEntry = this.QuestProgressSnapshot.QuestSeqTexts[questSequenceIndex];
            if (!string.IsNullOrWhiteSpace(resolvedEntry.Text))
            {
                currentSequenceEntry = resolvedEntry;
                return true;
            }
        }

        foreach (var questSequenceEntry in this.QuestProgressSnapshot.QuestSeqTexts)
        {
            if (!string.IsNullOrWhiteSpace(questSequenceEntry.Text))
            {
                currentSequenceEntry = questSequenceEntry;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Gets the canonical SEQ entries that belong to phases completed
    ///     before the current live quest sequence.
    /// </summary>
    /// <returns>
    ///     The ordered SEQ entries whose text should populate the JournalDetail
    ///     summary block for the active quest phase.
    /// </returns>
    public IReadOnlyList<QuestProgressEntry> GetSummaryEntriesBeforeCurrentSequence()
    {
        if (this.QuestProgressSnapshot.QuestSeqTexts.Count == 0)
        {
            return [];
        }

        var currentSequenceIndex = Math.Min(
            (int)this.QuestProgressSnapshot.QuestSequence,
            this.QuestProgressSnapshot.QuestSeqTexts.Count - 1);
        if (currentSequenceIndex <= 0)
        {
            return [];
        }

        return this.QuestProgressSnapshot.QuestSeqTexts
            .Take(currentSequenceIndex)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Text))
            .ToArray();
    }

    /// <summary>
    ///     Enumerates canonical summary row keys whose source text matches the
    ///     supplied visible text.
    /// </summary>
    /// <param name="sourceText">The visible summary text.</param>
    /// <returns>The matching canonical summary row keys.</returns>
    public IEnumerable<string> EnumerateSummaryRowKeysByText(string? sourceText)
    {
        return EnumerateRowKeysByText(this.QuestProgressSnapshot.QuestSeqTexts, sourceText);
    }

    /// <summary>
    ///     Enumerates canonical objective row keys whose source text matches
    ///     the supplied visible text.
    /// </summary>
    /// <param name="sourceText">The visible objective text.</param>
    /// <returns>The matching canonical objective row keys.</returns>
    public IEnumerable<string> EnumerateObjectiveRowKeysByText(string? sourceText)
    {
        return EnumerateRowKeysByText(this.QuestProgressSnapshot.QuestSteps, sourceText);
    }

    /// <summary>
    ///     Enumerates canonical system row keys whose source text matches the
    ///     supplied visible text.
    /// </summary>
    /// <param name="sourceText">The visible system text.</param>
    /// <returns>The matching canonical system row keys.</returns>
    public IEnumerable<string> EnumerateSystemRowKeysByText(string? sourceText)
    {
        return EnumerateRowKeysByText(this.QuestProgressSnapshot.QuestSystemTexts, sourceText);
    }

    /// <summary>
    ///     Materializes the current canonical quest payload into the current
    ///     legacy-compatible <see cref="QuestPlate" /> shape.
    /// </summary>
    /// <param name="originalLanguage">The source language.</param>
    /// <param name="translationLanguage">The target translation language.</param>
    /// <param name="translationEngine">The translation engine id.</param>
    /// <param name="timestamp">The materialization timestamp.</param>
    /// <returns>The projected quest plate.</returns>
    public QuestPlate ToQuestPlate(
        string originalLanguage,
        string translationLanguage,
        int translationEngine,
        DateTime timestamp)
    {
        var questPlate = new QuestPlate(
            this.QuestProgressSnapshot.QuestName,
            this.CurrentSequenceText,
            originalLanguage,
            string.Empty,
            string.Empty,
            this.QuestProgressSnapshot.QuestId.ToString(CultureInfo.InvariantCulture),
            translationLanguage,
            translationEngine,
            timestamp,
            timestamp,
            this.GameVersion);
        questPlate.ApplyCanonicalPayload(this);

        return questPlate;
    }

    private static IReadOnlyDictionary<string, string> BuildRowMapByKey(
        IReadOnlyCollection<QuestProgressEntry> questEntries)
    {
        Dictionary<string, string> rowsByKey = new(StringComparer.Ordinal);
        foreach (var questEntry in questEntries)
        {
            if (string.IsNullOrWhiteSpace(questEntry.Text))
            {
                continue;
            }

            var rowKey = string.IsNullOrWhiteSpace(questEntry.KeyText)
                ? questEntry.Text
                : questEntry.KeyText;
            rowsByKey[rowKey] = questEntry.Text;
        }

        return rowsByKey;
    }

    private static IReadOnlyDictionary<string, string> BuildRowMapByText(
        IReadOnlyCollection<QuestProgressEntry> questEntries)
    {
        Dictionary<string, string> rowsByText = new(StringComparer.Ordinal);
        foreach (var questEntry in questEntries)
        {
            if (string.IsNullOrWhiteSpace(questEntry.Text))
            {
                continue;
            }

            rowsByText[questEntry.Text] = questEntry.Text;
        }

        return rowsByText;
    }

    private static void AppendDictionary(
        StringBuilder builder,
        string sectionName,
        IReadOnlyDictionary<string, string> rows)
    {
        builder.AppendLine($"  {sectionName}: {rows.Count}");
        foreach (var (rowKey, rowValue) in rows)
        {
            builder.AppendLine($"    [{rowKey}] = {rowValue}");
        }
    }

    private static IEnumerable<string> EnumerateRowKeysByText(
        IEnumerable<QuestProgressEntry> entries,
        string? sourceText)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            yield break;
        }

        foreach (var entry in entries)
        {
            if (!string.Equals(entry.Text, sourceText, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(entry.KeyText))
            {
                continue;
            }

            yield return entry.KeyText;
        }
    }
}
