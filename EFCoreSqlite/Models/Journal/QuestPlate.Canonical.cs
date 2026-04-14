// <copyright file="QuestPlate.Canonical.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models.Journal;

/// <summary>
///     Provides canonical quest payload helpers for <see cref="QuestPlate" />.
/// </summary>
public partial class QuestPlate
{
    /// <summary>
    ///     Applies the current canonical quest payload to the quest plate,
    ///     replacing the original row sections with the latest resolved quest
    ///     data while preserving translated content whose row keys still match.
    /// </summary>
    /// <param name="questCanonicalData">The canonical quest payload.</param>
    internal void ApplyCanonicalPayload(QuestCanonicalData questCanonicalData)
    {
        ArgumentNullException.ThrowIfNull(questCanonicalData);

        var questProgressSnapshot = questCanonicalData.QuestProgressSnapshot;
        var currentSequenceRowKey =
            questCanonicalData.TryGetCurrentSequenceEntry(out var currentSequenceEntry)
                ? currentSequenceEntry.KeyText
                : string.Empty;
        var translatedRowsByKey = this.CanonicalRows
            .Where(row => !string.IsNullOrWhiteSpace(row.RowKey) &&
                          !string.IsNullOrWhiteSpace(row.TranslatedText))
            .ToDictionary(row => row.RowKey, row => row.TranslatedText!, StringComparer.Ordinal);

        this.QuestId = questProgressSnapshot.QuestId.ToString(CultureInfo.InvariantCulture);
        this.QuestName = questProgressSnapshot.QuestName;
        this.OriginalQuestMessage = questCanonicalData.CurrentSequenceText;
        this.QuestTextSheetName = questProgressSnapshot.QuestSheetName;
        this.SourceContentHash = questProgressSnapshot.ContentHash;
        this.GameVersion = questCanonicalData.GameVersion;
        this.canonicalRows = BuildCanonicalRows(
            questProgressSnapshot.QuestSeqTexts,
            QuestPlateCanonicalRowSection.Summary,
            currentSequenceRowKey,
            translatedRowsByKey)
            .Concat(BuildCanonicalRows(
                questProgressSnapshot.QuestSteps,
                QuestPlateCanonicalRowSection.Objective,
                currentSequenceRowKey: null,
                translatedRowsByKey))
            .Concat(BuildCanonicalRows(
                questProgressSnapshot.QuestSystemTexts,
                QuestPlateCanonicalRowSection.System,
                currentSequenceRowKey: null,
            translatedRowsByKey))
            .ToList();

        this.TranslatedQuestMessage = this.canonicalRows
            .FirstOrDefault(row => row.IsCurrentSequence)?.TranslatedText ?? string.Empty;
        this.SynchronizeLegacyTextProjections();
    }

    /// <summary>
    ///     Replaces the current canonical quest payload with the source quest
    ///     payload while preserving already translated row text whose row keys
    ///     still exist in the incoming canonical payload.
    /// </summary>
    /// <param name="source">The source quest plate carrying the newer payload.</param>
    internal void MergeCanonicalPayloadFrom(QuestPlate source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source.UpdateFieldsFromText();
        this.UpdateFieldsFromText();

        var existingTranslatedRows = this.CanonicalRows
            .Where(row => !string.IsNullOrWhiteSpace(row.RowKey) &&
                          !string.IsNullOrWhiteSpace(row.TranslatedText))
            .ToDictionary(row => row.RowKey, row => row.TranslatedText!, StringComparer.Ordinal);

        var mergedCanonicalRows = source.CanonicalRows
            .Select(sourceRow =>
            {
                existingTranslatedRows.TryGetValue(sourceRow.RowKey, out var existingTranslatedText);
                return new QuestPlateCanonicalRow(
                    sourceRow.Section,
                    sourceRow.RowKey,
                    sourceRow.OriginalText,
                    sourceRow.Order,
                    sourceRow.IsCurrentSequence,
                    !string.IsNullOrWhiteSpace(sourceRow.TranslatedText)
                        ? sourceRow.TranslatedText
                        : existingTranslatedText);
            })
            .ToList();

        this.canonicalRows = mergedCanonicalRows;
        this.SynchronizeLegacyTextProjections();
    }

    /// <summary>
    ///     Tries to resolve a translated summary row, preferring canonical
    ///     structured rows before the legacy text-keyed projection.
    /// </summary>
    /// <param name="rowKey">The canonical row key.</param>
    /// <param name="sourceText">The visible source text.</param>
    /// <param name="translatedText">The resolved translated text.</param>
    /// <returns>True when a translated summary row was found.</returns>
    public bool TryGetTranslatedSummaryText(
        string? rowKey,
        string? sourceText,
        out string translatedText)
    {
        if (TryGetTranslatedCanonicalRowText(
                this.CanonicalRows,
                QuestPlateCanonicalRowSection.Summary,
                rowKey,
                sourceText,
                out translatedText))
        {
            return true;
        }

        return TryGetTranslatedSectionText(
            this.TranslatedSummaryRowsByKey,
            this.TranslatedSummaries,
            rowKey,
            sourceText,
            out translatedText);
    }

    /// <summary>
    ///     Tries to resolve a translated objective row, preferring canonical
    ///     structured rows before the legacy text-keyed projection.
    /// </summary>
    /// <param name="rowKey">The canonical row key.</param>
    /// <param name="sourceText">The visible source text.</param>
    /// <param name="translatedText">The resolved translated text.</param>
    /// <returns>True when a translated objective row was found.</returns>
    public bool TryGetTranslatedObjectiveText(
        string? rowKey,
        string? sourceText,
        out string translatedText)
    {
        if (TryGetTranslatedCanonicalRowText(
                this.CanonicalRows,
                QuestPlateCanonicalRowSection.Objective,
                rowKey,
                sourceText,
                out translatedText))
        {
            return true;
        }

        return TryGetTranslatedSectionText(
            this.TranslatedObjectiveRowsByKey,
            this.TranslatedObjectives,
            rowKey,
            sourceText,
            out translatedText);
    }

    /// <summary>
    ///     Tries to resolve a translated system row, preferring canonical
    ///     structured rows before the legacy text-keyed projection.
    /// </summary>
    /// <param name="rowKey">The canonical row key.</param>
    /// <param name="sourceText">The visible source text.</param>
    /// <param name="translatedText">The resolved translated text.</param>
    /// <returns>True when a translated system row was found.</returns>
    public bool TryGetTranslatedSystemText(
        string? rowKey,
        string? sourceText,
        out string translatedText)
    {
        if (TryGetTranslatedCanonicalRowText(
                this.CanonicalRows,
                QuestPlateCanonicalRowSection.System,
                rowKey,
                sourceText,
                out translatedText))
        {
            return true;
        }

        return TryGetTranslatedSectionText(
            this.TranslatedSystemRowsByKey,
            this.TranslatedSystemRows,
            rowKey,
            sourceText,
            out translatedText);
    }

    /// <summary>
    ///     Stores a translated summary row in canonical structured rows and in
    ///     legacy-compatible text-keyed projections.
    /// </summary>
    /// <param name="rowKey">The canonical row key.</param>
    /// <param name="sourceText">The source text.</param>
    /// <param name="translatedText">The translated text.</param>
    public void SetTranslatedSummaryText(
        string? rowKey,
        string? sourceText,
        string translatedText)
    {
        SetTranslatedCanonicalRowText(
            this.CanonicalRows,
            QuestPlateCanonicalRowSection.Summary,
            rowKey,
            sourceText,
            translatedText);
        SetTranslatedSectionText(
            this.SummaryRowsByKey,
            this.TranslatedSummaryRowsByKey,
            this.TranslatedSummaries,
            rowKey,
            sourceText,
            translatedText);
    }

    /// <summary>
    ///     Stores a translated objective row in canonical structured rows and
    ///     in legacy-compatible text-keyed projections.
    /// </summary>
    /// <param name="rowKey">The canonical row key.</param>
    /// <param name="sourceText">The source text.</param>
    /// <param name="translatedText">The translated text.</param>
    public void SetTranslatedObjectiveText(
        string? rowKey,
        string? sourceText,
        string translatedText)
    {
        SetTranslatedCanonicalRowText(
            this.CanonicalRows,
            QuestPlateCanonicalRowSection.Objective,
            rowKey,
            sourceText,
            translatedText);
        SetTranslatedSectionText(
            this.ObjectiveRowsByKey,
            this.TranslatedObjectiveRowsByKey,
            this.TranslatedObjectives,
            rowKey,
            sourceText,
            translatedText);
    }

    /// <summary>
    ///     Stores a translated system row in canonical structured rows and in
    ///     legacy-compatible text-keyed projections.
    /// </summary>
    /// <param name="rowKey">The canonical row key.</param>
    /// <param name="sourceText">The source text.</param>
    /// <param name="translatedText">The translated text.</param>
    public void SetTranslatedSystemText(
        string? rowKey,
        string? sourceText,
        string translatedText)
    {
        SetTranslatedCanonicalRowText(
            this.CanonicalRows,
            QuestPlateCanonicalRowSection.System,
            rowKey,
            sourceText,
            translatedText);
        SetTranslatedSectionText(
            this.SystemRowsByKey,
            this.TranslatedSystemRowsByKey,
            this.TranslatedSystemRows,
            rowKey,
            sourceText,
            translatedText);
    }

    /// <summary>
    ///     Rebuilds the legacy text-keyed projections from the canonical quest
    ///     payload when available. Legacy projections remain as a compatibility
    ///     surface for handlers that have not yet been migrated.
    /// </summary>
    public void SynchronizeLegacyTextProjections()
    {
        var canonicalRows = this.CanonicalRows;
        if (canonicalRows.Count != 0)
        {
            this.Objectives.Clear();
            this.ObjectiveRowsByKey.Clear();
            this.Summaries.Clear();
            this.SummaryRowsByKey.Clear();
            this.SystemRows.Clear();
            this.SystemRowsByKey.Clear();
            this.TranslatedObjectives.Clear();
            this.TranslatedObjectiveRowsByKey.Clear();
            this.TranslatedSummaries.Clear();
            this.TranslatedSummaryRowsByKey.Clear();
            this.TranslatedSystemRows.Clear();
            this.TranslatedSystemRowsByKey.Clear();

            foreach (var canonicalRow in canonicalRows
                         .OrderBy(row => row.Section)
                         .ThenBy(row => row.Order))
            {
                if (string.IsNullOrWhiteSpace(canonicalRow.OriginalText))
                {
                    continue;
                }

                var sourceRowsByText = GetSectionRowsByText(this, canonicalRow.Section);
                var sourceRowsByKey = GetSectionRowsByKey(this, canonicalRow.Section);
                var translatedRowsByText = GetTranslatedSectionRowsByText(this, canonicalRow.Section);
                var translatedRowsByKey = GetTranslatedSectionRowsByKey(this, canonicalRow.Section);

                sourceRowsByText[canonicalRow.OriginalText] = canonicalRow.OriginalText;
                if (!string.IsNullOrWhiteSpace(canonicalRow.RowKey))
                {
                    sourceRowsByKey[canonicalRow.RowKey] = canonicalRow.OriginalText;
                }

                if (!string.IsNullOrWhiteSpace(canonicalRow.TranslatedText))
                {
                    translatedRowsByText[canonicalRow.OriginalText] = canonicalRow.TranslatedText!;
                    if (!string.IsNullOrWhiteSpace(canonicalRow.RowKey))
                    {
                        translatedRowsByKey[canonicalRow.RowKey] = canonicalRow.TranslatedText!;
                    }
                }
            }

            var currentSequenceRow = canonicalRows.FirstOrDefault(row =>
                row.Section == QuestPlateCanonicalRowSection.Summary &&
                row.IsCurrentSequence &&
                !string.IsNullOrWhiteSpace(row.OriginalText));
            if (currentSequenceRow != null)
            {
                this.OriginalQuestMessage = currentSequenceRow.OriginalText;
                this.TranslatedQuestMessage = currentSequenceRow.TranslatedText ?? string.Empty;
            }

            return;
        }

        SynchronizeSection(
            this.ObjectiveRowsByKey,
            this.TranslatedObjectiveRowsByKey,
            this.Objectives,
            this.TranslatedObjectives);
        SynchronizeSection(
            this.SummaryRowsByKey,
            this.TranslatedSummaryRowsByKey,
            this.Summaries,
            this.TranslatedSummaries);
        SynchronizeSection(
            this.SystemRowsByKey,
            this.TranslatedSystemRowsByKey,
            this.SystemRows,
            this.TranslatedSystemRows);
    }

    /// <summary>
    ///     Prunes translated quest-row state so only rows still present in the
    ///     canonical payload remain.
    /// </summary>
    public void PruneTranslatedRowsToCanonicalPayload()
    {
        if (this.CanonicalRows.Count != 0)
        {
            this.canonicalRows = this.CanonicalRows
                .Where(row => !string.IsNullOrWhiteSpace(row.OriginalText))
                .ToList();
            this.SynchronizeLegacyTextProjections();
            return;
        }

        PruneTranslatedRows(
            this.ObjectiveRowsByKey,
            this.TranslatedObjectiveRowsByKey);
        PruneTranslatedRows(
            this.SummaryRowsByKey,
            this.TranslatedSummaryRowsByKey);
        PruneTranslatedRows(
            this.SystemRowsByKey,
            this.TranslatedSystemRowsByKey);
    }

    private List<QuestPlateCanonicalRow> LoadCanonicalRows()
    {
        var canonicalRows = this.LoadCanonicalRowsFromText();
        if (canonicalRows.Count != 0)
        {
            return canonicalRows;
        }

        return this.BuildCanonicalRowsFromProjections();
    }

    private List<QuestPlateCanonicalRow> LoadCanonicalRowsFromText()
    {
        if (string.IsNullOrWhiteSpace(this.CanonicalRowsAsText))
        {
            return [];
        }

        return JsonConvert.DeserializeObject<List<QuestPlateCanonicalRow>>(
                   this.CanonicalRowsAsText) ??
               [];
    }

    private List<QuestPlateCanonicalRow> BuildCanonicalRowsFromProjections()
    {
        List<QuestPlateCanonicalRow> canonicalRows = [];

        canonicalRows.AddRange(BuildLegacyRows(
            this.SummaryRowsByKey,
            this.TranslatedSummaryRowsByKey,
            this.TranslatedSummaries,
            QuestPlateCanonicalRowSection.Summary,
            this.OriginalQuestMessage));
        canonicalRows.AddRange(BuildLegacyRows(
            this.ObjectiveRowsByKey,
            this.TranslatedObjectiveRowsByKey,
            this.TranslatedObjectives,
            QuestPlateCanonicalRowSection.Objective,
            currentSequenceText: null));
        canonicalRows.AddRange(BuildLegacyRows(
            this.SystemRowsByKey,
            this.TranslatedSystemRowsByKey,
            this.TranslatedSystemRows,
            QuestPlateCanonicalRowSection.System,
            currentSequenceText: null));

        if (canonicalRows.Count != 0)
        {
            return canonicalRows;
        }

        canonicalRows.AddRange(BuildLegacyTextOnlyRows(
            this.Summaries,
            this.TranslatedSummaries,
            QuestPlateCanonicalRowSection.Summary,
            this.OriginalQuestMessage));
        canonicalRows.AddRange(BuildLegacyTextOnlyRows(
            this.Objectives,
            this.TranslatedObjectives,
            QuestPlateCanonicalRowSection.Objective,
            currentSequenceText: null));
        canonicalRows.AddRange(BuildLegacyTextOnlyRows(
            this.SystemRows,
            this.TranslatedSystemRows,
            QuestPlateCanonicalRowSection.System,
            currentSequenceText: null));

        return canonicalRows;
    }

    private static IEnumerable<QuestPlateCanonicalRow> BuildCanonicalRows(
        IEnumerable<QuestProgressEntry> questEntries,
        QuestPlateCanonicalRowSection section,
        string? currentSequenceRowKey,
        IReadOnlyDictionary<string, string> translatedRowsByKey)
    {
        var order = 0;
        foreach (var questEntry in questEntries)
        {
            if (string.IsNullOrWhiteSpace(questEntry.Text) ||
                string.IsNullOrWhiteSpace(questEntry.KeyText))
            {
                continue;
            }

            translatedRowsByKey.TryGetValue(questEntry.KeyText, out var translatedText);
            yield return new QuestPlateCanonicalRow(
                section,
                questEntry.KeyText,
                questEntry.Text,
                order++,
                string.Equals(
                    questEntry.KeyText,
                    currentSequenceRowKey,
                    StringComparison.Ordinal),
                translatedText);
        }
    }

    private static IEnumerable<QuestPlateCanonicalRow> BuildLegacyRows(
        IReadOnlyDictionary<string, string> sourceRowsByKey,
        IReadOnlyDictionary<string, string> translatedRowsByKey,
        IReadOnlyDictionary<string, string> translatedRowsByText,
        QuestPlateCanonicalRowSection section,
        string? currentSequenceText)
    {
        var fallbackOrder = 0;
        foreach (var (rowKey, originalText) in sourceRowsByKey)
        {
            if (string.IsNullOrWhiteSpace(rowKey) ||
                string.IsNullOrWhiteSpace(originalText))
            {
                continue;
            }

            translatedRowsByKey.TryGetValue(rowKey, out var translatedText);
            if (string.IsNullOrWhiteSpace(translatedText))
            {
                translatedRowsByText.TryGetValue(originalText, out translatedText);
            }

            yield return new QuestPlateCanonicalRow(
                section,
                rowKey,
                originalText,
                DetermineRowOrder(rowKey, fallbackOrder++),
                string.Equals(
                    originalText,
                    currentSequenceText,
                    StringComparison.Ordinal),
                translatedText);
        }
    }

    private static IEnumerable<QuestPlateCanonicalRow> BuildLegacyTextOnlyRows(
        IReadOnlyDictionary<string, string> sourceRowsByText,
        IReadOnlyDictionary<string, string> translatedRowsByText,
        QuestPlateCanonicalRowSection section,
        string? currentSequenceText)
    {
        var fallbackOrder = 0;
        foreach (var originalText in sourceRowsByText.Values)
        {
            if (string.IsNullOrWhiteSpace(originalText))
            {
                continue;
            }

            translatedRowsByText.TryGetValue(originalText, out var translatedText);
            var rowKey =
                $"LEGACY_{section.ToString().ToUpperInvariant()}_{fallbackOrder:D4}";
            yield return new QuestPlateCanonicalRow(
                section,
                rowKey,
                originalText,
                fallbackOrder++,
                string.Equals(
                    originalText,
                    currentSequenceText,
                    StringComparison.Ordinal),
                translatedText);
        }
    }

    private static bool TryGetTranslatedCanonicalRowText(
        IEnumerable<QuestPlateCanonicalRow> canonicalRows,
        QuestPlateCanonicalRowSection section,
        string? rowKey,
        string? sourceText,
        out string translatedText)
    {
        translatedText = string.Empty;

        QuestPlateCanonicalRow? matchingRow = null;
        if (!string.IsNullOrWhiteSpace(rowKey))
        {
            matchingRow = canonicalRows.FirstOrDefault(row =>
                row.Section == section &&
                string.Equals(row.RowKey, rowKey, StringComparison.Ordinal));
        }
        else if (!string.IsNullOrWhiteSpace(sourceText))
        {
            matchingRow = canonicalRows.FirstOrDefault(row =>
                row.Section == section &&
                string.Equals(row.OriginalText, sourceText, StringComparison.Ordinal));
        }

        if (matchingRow == null ||
            string.IsNullOrWhiteSpace(matchingRow.TranslatedText))
        {
            return false;
        }

        translatedText = matchingRow.TranslatedText;
        return true;
    }

    private static void SetTranslatedCanonicalRowText(
        ICollection<QuestPlateCanonicalRow> canonicalRows,
        QuestPlateCanonicalRowSection section,
        string? rowKey,
        string? sourceText,
        string translatedText)
    {
        if (string.IsNullOrWhiteSpace(translatedText))
        {
            return;
        }

        QuestPlateCanonicalRow? matchingRow = null;
        if (!string.IsNullOrWhiteSpace(rowKey))
        {
            matchingRow = canonicalRows.FirstOrDefault(row =>
                row.Section == section &&
                string.Equals(row.RowKey, rowKey, StringComparison.Ordinal));
        }
        else if (!string.IsNullOrWhiteSpace(sourceText))
        {
            matchingRow = canonicalRows.FirstOrDefault(row =>
                row.Section == section &&
                string.Equals(row.OriginalText, sourceText, StringComparison.Ordinal));
        }

        if (matchingRow == null)
        {
            var resolvedRowKey = !string.IsNullOrWhiteSpace(rowKey)
                ? rowKey
                : sourceText ?? Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            matchingRow = new QuestPlateCanonicalRow(
                section,
                resolvedRowKey,
                sourceText ?? string.Empty,
                DetermineRowOrder(resolvedRowKey, canonicalRows.Count));
            canonicalRows.Add(matchingRow);
        }

        matchingRow.TranslatedText = translatedText;
    }

    private static IDictionary<string, string> GetSectionRowsByText(
        QuestPlate questPlate,
        QuestPlateCanonicalRowSection section)
    {
        return section switch
        {
            QuestPlateCanonicalRowSection.Summary => questPlate.Summaries,
            QuestPlateCanonicalRowSection.Objective => questPlate.Objectives,
            QuestPlateCanonicalRowSection.System => questPlate.SystemRows,
            _ => questPlate.Summaries,
        };
    }

    private static IDictionary<string, string> GetSectionRowsByKey(
        QuestPlate questPlate,
        QuestPlateCanonicalRowSection section)
    {
        return section switch
        {
            QuestPlateCanonicalRowSection.Summary => questPlate.SummaryRowsByKey,
            QuestPlateCanonicalRowSection.Objective => questPlate.ObjectiveRowsByKey,
            QuestPlateCanonicalRowSection.System => questPlate.SystemRowsByKey,
            _ => questPlate.SummaryRowsByKey,
        };
    }

    private static IDictionary<string, string> GetTranslatedSectionRowsByText(
        QuestPlate questPlate,
        QuestPlateCanonicalRowSection section)
    {
        return section switch
        {
            QuestPlateCanonicalRowSection.Summary => questPlate.TranslatedSummaries,
            QuestPlateCanonicalRowSection.Objective => questPlate.TranslatedObjectives,
            QuestPlateCanonicalRowSection.System => questPlate.TranslatedSystemRows,
            _ => questPlate.TranslatedSummaries,
        };
    }

    private static IDictionary<string, string> GetTranslatedSectionRowsByKey(
        QuestPlate questPlate,
        QuestPlateCanonicalRowSection section)
    {
        return section switch
        {
            QuestPlateCanonicalRowSection.Summary => questPlate.TranslatedSummaryRowsByKey,
            QuestPlateCanonicalRowSection.Objective => questPlate.TranslatedObjectiveRowsByKey,
            QuestPlateCanonicalRowSection.System => questPlate.TranslatedSystemRowsByKey,
            _ => questPlate.TranslatedSummaryRowsByKey,
        };
    }

    private static int DetermineRowOrder(string rowKey, int fallbackOrder)
    {
        if (string.IsNullOrWhiteSpace(rowKey))
        {
            return fallbackOrder;
        }

        var parts = rowKey.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return fallbackOrder;
        }

        if (rowKey.Contains("_SYSTEM_", StringComparison.Ordinal) &&
            parts.Length >= 2 &&
            int.TryParse(parts[^2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var systemMajor) &&
            int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var systemMinor))
        {
            return (systemMajor * 1000) + systemMinor;
        }

        if (int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var order))
        {
            return order;
        }

        return fallbackOrder;
    }

    private static bool TryGetTranslatedSectionText(
        IReadOnlyDictionary<string, string> translatedRowsByKey,
        IReadOnlyDictionary<string, string> translatedRowsByText,
        string? rowKey,
        string? sourceText,
        out string translatedText)
    {
        translatedText = string.Empty;

        if (!string.IsNullOrWhiteSpace(rowKey) &&
            translatedRowsByKey.TryGetValue(rowKey, out translatedText) &&
            !string.IsNullOrWhiteSpace(translatedText))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(sourceText) &&
            translatedRowsByText.TryGetValue(sourceText, out translatedText) &&
            !string.IsNullOrWhiteSpace(translatedText))
        {
            return true;
        }

        translatedText = string.Empty;
        return false;
    }

    private static void SetTranslatedSectionText(
        IDictionary<string, string> canonicalRowsByKey,
        IDictionary<string, string> translatedRowsByKey,
        IDictionary<string, string> translatedRowsByText,
        string? rowKey,
        string? sourceText,
        string translatedText)
    {
        if (string.IsNullOrWhiteSpace(translatedText))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(rowKey) &&
            !string.IsNullOrWhiteSpace(sourceText) &&
            !canonicalRowsByKey.ContainsKey(rowKey))
        {
            canonicalRowsByKey[rowKey] = sourceText;
        }

        if (!string.IsNullOrWhiteSpace(rowKey))
        {
            translatedRowsByKey[rowKey] = translatedText;
        }

        if (!string.IsNullOrWhiteSpace(sourceText))
        {
            translatedRowsByText[sourceText] = translatedText;
        }
    }

    private static void SynchronizeSection(
        IDictionary<string, string> canonicalRowsByKey,
        IDictionary<string, string> translatedRowsByKey,
        IDictionary<string, string> legacyRowsByText,
        IDictionary<string, string> translatedRowsByText)
    {
        if (canonicalRowsByKey.Count == 0)
        {
            return;
        }

        var rebuiltRowsByText = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rowText in canonicalRowsByKey.Values)
        {
            if (string.IsNullOrWhiteSpace(rowText))
            {
                continue;
            }

            rebuiltRowsByText[rowText] = rowText;
        }

        legacyRowsByText.Clear();
        foreach (var (rowText, projectedText) in rebuiltRowsByText)
        {
            legacyRowsByText[rowText] = projectedText;
        }

        PruneTranslatedRows(canonicalRowsByKey, translatedRowsByKey);

        var rebuiltTranslatedRowsByText =
            new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (sourceText, translatedText) in translatedRowsByText)
        {
            if (string.IsNullOrWhiteSpace(sourceText) ||
                string.IsNullOrWhiteSpace(translatedText) ||
                !rebuiltRowsByText.ContainsKey(sourceText))
            {
                continue;
            }

            rebuiltTranslatedRowsByText[sourceText] = translatedText;
        }

        foreach (var (rowKey, translatedText) in translatedRowsByKey)
        {
            if (string.IsNullOrWhiteSpace(rowKey) ||
                string.IsNullOrWhiteSpace(translatedText) ||
                !canonicalRowsByKey.TryGetValue(rowKey, out var sourceText) ||
                string.IsNullOrWhiteSpace(sourceText))
            {
                continue;
            }

            rebuiltTranslatedRowsByText[sourceText] = translatedText;
        }

        translatedRowsByText.Clear();
        foreach (var (sourceText, translatedText) in rebuiltTranslatedRowsByText)
        {
            translatedRowsByText[sourceText] = translatedText;
        }
    }

    private static void PruneTranslatedRows(
        IDictionary<string, string> canonicalRowsByKey,
        IDictionary<string, string> translatedRowsByKey)
    {
        if (translatedRowsByKey.Count == 0)
        {
            return;
        }

        var staleRowKeys = translatedRowsByKey.Keys
            .Where(rowKey => !canonicalRowsByKey.ContainsKey(rowKey))
            .ToArray();
        foreach (var staleRowKey in staleRowKeys)
        {
            translatedRowsByKey.Remove(staleRowKey);
        }
    }
}
