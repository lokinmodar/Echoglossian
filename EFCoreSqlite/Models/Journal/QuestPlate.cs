// <copyright file="QuestPlate.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models.Journal;

/// <summary>
///     Represents a quest plate in the database.
/// </summary>
[Table("questplates")]
public partial class QuestPlate
{
    [NotMapped] private List<QuestPlateCanonicalRow>? canonicalRows;
    [NotMapped] private Dictionary<string, string>? objectives;
    [NotMapped] private Dictionary<string, string>? objectiveRowsByKey;

    [NotMapped] private Dictionary<string, string>? summaries;
    [NotMapped] private Dictionary<string, string>? summaryRowsByKey;

    [NotMapped] private Dictionary<string, string>? translatedObjectives;
    [NotMapped] private Dictionary<string, string>? translatedObjectiveRowsByKey;

    [NotMapped] private Dictionary<string, string>? translatedSummaries;
    [NotMapped] private Dictionary<string, string>? translatedSummaryRowsByKey;

    [NotMapped] private Dictionary<string, string>? systemRows;
    [NotMapped] private Dictionary<string, string>? systemRowsByKey;

    [NotMapped] private Dictionary<string, string>? translatedSystemRows;
    [NotMapped] private Dictionary<string, string>? translatedSystemRowsByKey;

    /// <summary>
    ///     Initializes a new instance of the <see cref="QuestPlate" /> class.
    /// </summary>
    /// <param name="questName">The quest name.</param>
    /// <param name="originalQuestMessage">The original quest message.</param>
    /// <param name="originalLang">The original language code.</param>
    /// <param name="translatedQuestName">The translated quest name.</param>
    /// <param name="translatedQuestMessage">The translated quest message.</param>
    /// <param name="questId">The resolved quest identifier.</param>
    /// <param name="translationLang">The target translation language.</param>
    /// <param name="translationEngine">The translation engine id.</param>
    /// <param name="createdDate">The created date.</param>
    /// <param name="updatedDate">The updated date.</param>
    /// <param name="gameVersion">The game version snapshot.</param>
    public QuestPlate(
        string? questName,
        string? originalQuestMessage,
        string? originalLang,
        string? translatedQuestName,
        string? translatedQuestMessage,
        string? questId,
        string? translationLang,
        int? translationEngine,
        DateTime? createdDate,
        DateTime? updatedDate,
        string? gameVersion = null)
    {
        this.QuestId = questId;
        this.QuestName = questName;
        this.OriginalQuestMessage = originalQuestMessage;
        this.OriginalLang = originalLang;
        this.TranslatedQuestName = translatedQuestName;
        this.TranslatedQuestMessage = translatedQuestMessage;
        this.TranslationLang = translationLang;
        this.TranslationEngine = translationEngine;
        this.GameVersion = gameVersion;
        this.CreatedDate = createdDate;
        this.UpdatedDate = updatedDate;
        this.canonicalRows = new List<QuestPlateCanonicalRow>();
        this.objectives = new Dictionary<string, string>();
        this.objectiveRowsByKey = new Dictionary<string, string>();
        this.summaries = new Dictionary<string, string>();
        this.summaryRowsByKey = new Dictionary<string, string>();
        this.translatedObjectives = new Dictionary<string, string>();
        this.translatedObjectiveRowsByKey = new Dictionary<string, string>();
        this.translatedSummaries = new Dictionary<string, string>();
        this.translatedSummaryRowsByKey = new Dictionary<string, string>();
        this.systemRows = new Dictionary<string, string>();
        this.systemRowsByKey = new Dictionary<string, string>();
        this.translatedSystemRows = new Dictionary<string, string>();
        this.translatedSystemRowsByKey = new Dictionary<string, string>();
    }

    [Key] public int Id { get; set; }

    public string? QuestId { get; set; }

    public string? QuestName { get; set; }

    public string? OriginalQuestMessage { get; set; }

    public string? OriginalLang { get; set; }

    public string? TranslatedQuestName { get; set; }

    public string? TranslatedQuestMessage { get; set; }

    public string? TranslationLang { get; set; }

    public int? TranslationEngine { get; set; }

    /// <summary>
    ///     Gets or sets the game version the quest plate was captured from.
    /// </summary>
    public string? GameVersion { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    /// <summary>
    ///     Gets or sets the canonical structured quest payload serialized as
    ///     JSON rows.
    /// </summary>
    public string? CanonicalRowsAsText { get; set; }

    /// <summary>
    ///     Gets or sets the canonical quest text sheet path (e.g. "quest/043/AktKmb114_04393").
    /// </summary>
    public string? QuestTextSheetName { get; set; }

    /// <summary>
    ///     Gets the canonical structured quest rows.
    /// </summary>
    [NotMapped]
    public List<QuestPlateCanonicalRow> CanonicalRows
    {
        get => this.canonicalRows ??= this.LoadCanonicalRows();
        init => this.canonicalRows = value;
    }

    /// <summary>
    ///     Gets or sets the content fingerprint of the quest's translatable rows
    ///     (SEQ + TODO + SYSTEM) at the time the snapshot was last saved.
    ///     Used to detect whether quest text actually changed between game patches
    ///     so that translations can be reused without retranslating.
    ///     A null or empty value means the row predates content-hash tracking and
    ///     should be retranslated conservatively.
    /// </summary>
    public string? SourceContentHash { get; set; }

    public string? ObjectivesAsText { get; set; }

    /// <summary>
    ///     Gets or sets the canonical TODO rows serialized as JSON and keyed by row id text.
    /// </summary>
    public string? ObjectiveRowsByKeyAsText { get; set; }

    public string? SummariesAsText { get; set; }

    /// <summary>
    ///     Gets or sets the canonical SEQ rows serialized as JSON and keyed by row id text.
    /// </summary>
    public string? SummaryRowsByKeyAsText { get; set; }

    /// <summary>
    ///     Gets or sets the translated TODO rows serialized as JSON.
    /// </summary>
    public string? TranslatedObjectivesAsText { get; set; }

    /// <summary>
    ///     Gets or sets the translated TODO rows serialized as JSON and keyed by row id text.
    /// </summary>
    public string? TranslatedObjectiveRowsByKeyAsText { get; set; }

    /// <summary>
    ///     Gets or sets the translated SEQ rows serialized as JSON.
    /// </summary>
    public string? TranslatedSummariesAsText { get; set; }

    /// <summary>
    ///     Gets or sets the translated SEQ rows serialized as JSON and keyed by row id text.
    /// </summary>
    public string? TranslatedSummaryRowsByKeyAsText { get; set; }

    /// <summary>
    ///     Gets or sets the original SYSTEM (cinematic caption) rows serialized as JSON.
    /// </summary>
    public string? SystemRowsAsText { get; set; }

    /// <summary>
    ///     Gets or sets the canonical SYSTEM rows serialized as JSON and keyed by row id text.
    /// </summary>
    public string? SystemRowsByKeyAsText { get; set; }

    /// <summary>
    ///     Gets or sets the translated SYSTEM rows serialized as JSON.
    /// </summary>
    public string? TranslatedSystemRowsAsText { get; set; }

    /// <summary>
    ///     Gets or sets the translated SYSTEM rows serialized as JSON and keyed by row id text.
    /// </summary>
    public string? TranslatedSystemRowsByKeyAsText { get; set; }

    /// <summary>
    ///     Gets lazily loads Objectives from text if needed.
    /// </summary>
    [NotMapped]
    public Dictionary<string, string> Objectives
    {
        get
        {
            return this.objectives ??=
                !string.IsNullOrEmpty(this.ObjectivesAsText)
                    ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                          this.ObjectivesAsText) ??
                      new Dictionary<string, string>()
                    : new Dictionary<string, string>();
        }
        init => this.objectives = value;
    }

    /// <summary>
    ///     Gets the canonical TODO rows keyed by quest text row key.
    /// </summary>
    [NotMapped]
    public Dictionary<string, string> ObjectiveRowsByKey
    {
        get
        {
            return this.objectiveRowsByKey ??=
                !string.IsNullOrEmpty(this.ObjectiveRowsByKeyAsText)
                    ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                          this.ObjectiveRowsByKeyAsText) ??
                      new Dictionary<string, string>()
                    : new Dictionary<string, string>();
        }
        init => this.objectiveRowsByKey = value;
    }

    /// <summary>
    ///     Gets lazily loads Summaries from text if needed.
    /// </summary>
    [NotMapped]
    public Dictionary<string, string> Summaries
    {
        get =>
            this.summaries ??= !string.IsNullOrEmpty(this.SummariesAsText)
                ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                    this.SummariesAsText) ?? new Dictionary<string, string>()
                : new Dictionary<string, string>();
        init => this.summaries = value;
    }

    /// <summary>
    ///     Gets the canonical SEQ rows keyed by quest text row key.
    /// </summary>
    [NotMapped]
    public Dictionary<string, string> SummaryRowsByKey
    {
        get
        {
            return this.summaryRowsByKey ??=
                !string.IsNullOrEmpty(this.SummaryRowsByKeyAsText)
                    ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                          this.SummaryRowsByKeyAsText) ??
                      new Dictionary<string, string>()
                    : new Dictionary<string, string>();
        }
        init => this.summaryRowsByKey = value;
    }

    /// <summary>
    ///     Gets or sets the translated TODO row dictionary.
    /// </summary>
    [NotMapped]
    public Dictionary<string, string> TranslatedObjectives
    {
        get
        {
            return this.translatedObjectives ??=
                !string.IsNullOrEmpty(this.TranslatedObjectivesAsText)
                    ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                          this.TranslatedObjectivesAsText) ??
                      new Dictionary<string, string>()
                    : new Dictionary<string, string>();
        }
        init => this.translatedObjectives = value;
    }

    /// <summary>
    ///     Gets or sets the translated TODO rows keyed by quest text row key.
    /// </summary>
    [NotMapped]
    public Dictionary<string, string> TranslatedObjectiveRowsByKey
    {
        get
        {
            return this.translatedObjectiveRowsByKey ??=
                !string.IsNullOrEmpty(this.TranslatedObjectiveRowsByKeyAsText)
                    ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                          this.TranslatedObjectiveRowsByKeyAsText) ??
                      new Dictionary<string, string>()
                    : new Dictionary<string, string>();
        }
        init => this.translatedObjectiveRowsByKey = value;
    }

    /// <summary>
    ///     Gets or sets the translated SEQ row dictionary.
    /// </summary>
    [NotMapped]
    public Dictionary<string, string> TranslatedSummaries
    {
        get
        {
            return this.translatedSummaries ??=
                !string.IsNullOrEmpty(this.TranslatedSummariesAsText)
                    ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                          this.TranslatedSummariesAsText) ??
                      new Dictionary<string, string>()
                    : new Dictionary<string, string>();
        }
        init => this.translatedSummaries = value;
    }

    /// <summary>
    ///     Gets or sets the translated SEQ rows keyed by quest text row key.
    /// </summary>
    [NotMapped]
    public Dictionary<string, string> TranslatedSummaryRowsByKey
    {
        get
        {
            return this.translatedSummaryRowsByKey ??=
                !string.IsNullOrEmpty(this.TranslatedSummaryRowsByKeyAsText)
                    ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                          this.TranslatedSummaryRowsByKeyAsText) ??
                      new Dictionary<string, string>()
                    : new Dictionary<string, string>();
        }
        init => this.translatedSummaryRowsByKey = value;
    }

    /// <summary>
    ///     Gets or sets the original SYSTEM row dictionary.
    /// </summary>
    [NotMapped]
    public Dictionary<string, string> SystemRows
    {
        get
        {
            return this.systemRows ??=
                !string.IsNullOrEmpty(this.SystemRowsAsText)
                    ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                          this.SystemRowsAsText) ??
                      new Dictionary<string, string>()
                    : new Dictionary<string, string>();
        }
        init => this.systemRows = value;
    }

    /// <summary>
    ///     Gets or sets the canonical SYSTEM rows keyed by quest text row key.
    /// </summary>
    [NotMapped]
    public Dictionary<string, string> SystemRowsByKey
    {
        get
        {
            return this.systemRowsByKey ??=
                !string.IsNullOrEmpty(this.SystemRowsByKeyAsText)
                    ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                          this.SystemRowsByKeyAsText) ??
                      new Dictionary<string, string>()
                    : new Dictionary<string, string>();
        }
        init => this.systemRowsByKey = value;
    }

    /// <summary>
    ///     Gets or sets the translated SYSTEM row dictionary.
    /// </summary>
    [NotMapped]
    public Dictionary<string, string> TranslatedSystemRows
    {
        get
        {
            return this.translatedSystemRows ??=
                !string.IsNullOrEmpty(this.TranslatedSystemRowsAsText)
                    ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                          this.TranslatedSystemRowsAsText) ??
                      new Dictionary<string, string>()
                    : new Dictionary<string, string>();
        }
        init => this.translatedSystemRows = value;
    }

    /// <summary>
    ///     Gets or sets the translated SYSTEM rows keyed by quest text row key.
    /// </summary>
    [NotMapped]
    public Dictionary<string, string> TranslatedSystemRowsByKey
    {
        get
        {
            return this.translatedSystemRowsByKey ??=
                !string.IsNullOrEmpty(this.TranslatedSystemRowsByKeyAsText)
                    ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                          this.TranslatedSystemRowsByKeyAsText) ??
                      new Dictionary<string, string>()
                    : new Dictionary<string, string>();
        }
        init => this.translatedSystemRowsByKey = value;
    }

    /// <summary>
    ///     Updates the serialized text fields from their in-memory dictionaries.
    /// </summary>
    /// <param name="prettyPrint">Should it be pretty printed?.</param>
    public void UpdateFieldsAsText(bool prettyPrint = false)
    {
        this.SynchronizeLegacyTextProjections();

        this.CanonicalRowsAsText = string.Empty;
        this.ObjectivesAsText = string.Empty;
        this.ObjectiveRowsByKeyAsText = string.Empty;
        this.SummariesAsText = string.Empty;
        this.SummaryRowsByKeyAsText = string.Empty;
        this.TranslatedObjectivesAsText = string.Empty;
        this.TranslatedObjectiveRowsByKeyAsText = string.Empty;
        this.TranslatedSummariesAsText = string.Empty;
        this.TranslatedSummaryRowsByKeyAsText = string.Empty;
        this.SystemRowsAsText = string.Empty;
        this.SystemRowsByKeyAsText = string.Empty;
        this.TranslatedSystemRowsAsText = string.Empty;
        this.TranslatedSystemRowsByKeyAsText = string.Empty;

        if (this.canonicalRows != null && this.canonicalRows.Count != 0)
        {
            this.CanonicalRowsAsText = JsonConvert.SerializeObject(
                this.canonicalRows,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.objectives != null && this.objectives.Count != 0)
        {
            this.ObjectivesAsText = JsonConvert.SerializeObject(
                this.objectives,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.objectiveRowsByKey != null && this.objectiveRowsByKey.Count != 0)
        {
            this.ObjectiveRowsByKeyAsText = JsonConvert.SerializeObject(
                this.objectiveRowsByKey,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.summaries != null && this.summaries.Count != 0)
        {
            this.SummariesAsText = JsonConvert.SerializeObject(
                this.summaries,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.summaryRowsByKey != null && this.summaryRowsByKey.Count != 0)
        {
            this.SummaryRowsByKeyAsText = JsonConvert.SerializeObject(
                this.summaryRowsByKey,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.translatedObjectives != null && this.translatedObjectives.Count != 0)
        {
            this.TranslatedObjectivesAsText = JsonConvert.SerializeObject(
                this.translatedObjectives,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.translatedObjectiveRowsByKey != null &&
            this.translatedObjectiveRowsByKey.Count != 0)
        {
            this.TranslatedObjectiveRowsByKeyAsText = JsonConvert.SerializeObject(
                this.translatedObjectiveRowsByKey,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.translatedSummaries != null && this.translatedSummaries.Count != 0)
        {
            this.TranslatedSummariesAsText = JsonConvert.SerializeObject(
                this.translatedSummaries,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.translatedSummaryRowsByKey != null &&
            this.translatedSummaryRowsByKey.Count != 0)
        {
            this.TranslatedSummaryRowsByKeyAsText = JsonConvert.SerializeObject(
                this.translatedSummaryRowsByKey,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.systemRows != null && this.systemRows.Count != 0)
        {
            this.SystemRowsAsText = JsonConvert.SerializeObject(
                this.systemRows,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.systemRowsByKey != null && this.systemRowsByKey.Count != 0)
        {
            this.SystemRowsByKeyAsText = JsonConvert.SerializeObject(
                this.systemRowsByKey,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.translatedSystemRows != null && this.translatedSystemRows.Count != 0)
        {
            this.TranslatedSystemRowsAsText = JsonConvert.SerializeObject(
                this.translatedSystemRows,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.translatedSystemRowsByKey != null &&
            this.translatedSystemRowsByKey.Count != 0)
        {
            this.TranslatedSystemRowsByKeyAsText = JsonConvert.SerializeObject(
                this.translatedSystemRowsByKey,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }
    }

    /// <summary>
    ///     Forces loading the Objectives and Summaries from the stored text fields.
    /// </summary>
    public void UpdateFieldsFromText()
    {
        this.canonicalRows = LoadFromTextOrKeepCurrent(
            this.CanonicalRowsAsText,
            this.canonicalRows,
            this.LoadCanonicalRowsFromText);

        this.objectives = LoadDictionaryFromTextOrKeepCurrent(
            this.ObjectivesAsText,
            this.objectives);

        this.objectiveRowsByKey = LoadDictionaryFromTextOrKeepCurrent(
            this.ObjectiveRowsByKeyAsText,
            this.objectiveRowsByKey);

        this.summaries = LoadDictionaryFromTextOrKeepCurrent(
            this.SummariesAsText,
            this.summaries);

        this.summaryRowsByKey = LoadDictionaryFromTextOrKeepCurrent(
            this.SummaryRowsByKeyAsText,
            this.summaryRowsByKey);

        this.translatedObjectives = LoadDictionaryFromTextOrKeepCurrent(
            this.TranslatedObjectivesAsText,
            this.translatedObjectives);

        this.translatedObjectiveRowsByKey = LoadDictionaryFromTextOrKeepCurrent(
            this.TranslatedObjectiveRowsByKeyAsText,
            this.translatedObjectiveRowsByKey);

        this.translatedSummaries = LoadDictionaryFromTextOrKeepCurrent(
            this.TranslatedSummariesAsText,
            this.translatedSummaries);

        this.translatedSummaryRowsByKey = LoadDictionaryFromTextOrKeepCurrent(
            this.TranslatedSummaryRowsByKeyAsText,
            this.translatedSummaryRowsByKey);

        this.systemRows = LoadDictionaryFromTextOrKeepCurrent(
            this.SystemRowsAsText,
            this.systemRows);

        this.systemRowsByKey = LoadDictionaryFromTextOrKeepCurrent(
            this.SystemRowsByKeyAsText,
            this.systemRowsByKey);

        this.translatedSystemRows = LoadDictionaryFromTextOrKeepCurrent(
            this.TranslatedSystemRowsAsText,
            this.translatedSystemRows);

        this.translatedSystemRowsByKey = LoadDictionaryFromTextOrKeepCurrent(
            this.TranslatedSystemRowsByKeyAsText,
            this.translatedSystemRowsByKey);

        if (this.canonicalRows.Count != 0)
        {
            this.SynchronizeLegacyTextProjections();
        }
    }

    /// <summary>
    ///     Loads a serialized dictionary when available and otherwise preserves
    ///     any current in-memory state that has not yet been materialized.
    /// </summary>
    /// <param name="serializedText">The serialized dictionary text.</param>
    /// <param name="currentValue">The current in-memory dictionary.</param>
    /// <returns>The resolved dictionary.</returns>
    private static Dictionary<string, string> LoadDictionaryFromTextOrKeepCurrent(
        string? serializedText,
        Dictionary<string, string>? currentValue)
    {
        if (!string.IsNullOrWhiteSpace(serializedText))
        {
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(
                       serializedText) ??
                   [];
        }

        return currentValue ?? [];
    }

    /// <summary>
    ///     Loads a serialized payload when available and otherwise preserves
    ///     any current in-memory state that has not yet been materialized.
    /// </summary>
    /// <typeparam name="T">The payload item type.</typeparam>
    /// <param name="serializedText">The serialized payload text.</param>
    /// <param name="currentValue">The current in-memory payload.</param>
    /// <param name="loadFromText">The loader to invoke when serialized text exists.</param>
    /// <returns>The resolved payload collection.</returns>
    private static List<T> LoadFromTextOrKeepCurrent<T>(
        string? serializedText,
        List<T>? currentValue,
        Func<List<T>> loadFromText)
    {
        if (!string.IsNullOrWhiteSpace(serializedText))
        {
            return loadFromText();
        }

        return currentValue ?? [];
    }

    /// <inheritdoc />
    public override string? ToString()
    {
        return
            $"Id: {this.Id}, QuestName: {this.QuestName}, QuestID: {this.QuestId}, Sheet: {this.QuestTextSheetName}, ContentHash: {this.SourceContentHash}, OriginalMsg: {this.OriginalQuestMessage}, OriginalLang: {this.OriginalLang}, TranslQuestName: {this.TranslatedQuestName}, TranslMsg: {this.TranslatedQuestMessage}, TransLang: {this.TranslationLang}, TranEngine: {this.TranslationEngine}, GameVersion: {this.GameVersion}, CreatedAt: {this.CreatedDate}, UpdatedAt: {this.UpdatedDate}, CanonicalRows: {this.CanonicalRowsAsText}, Objectives: {this.ObjectivesAsText}, ObjectiveRowsByKey: {this.ObjectiveRowsByKeyAsText}, Summaries: {this.SummariesAsText}, SummaryRowsByKey: {this.SummaryRowsByKeyAsText}, SystemRows: {this.SystemRowsAsText}, SystemRowsByKey: {this.SystemRowsByKeyAsText}, TranslatedObjectives: {this.TranslatedObjectivesAsText}, TranslatedObjectiveRowsByKey: {this.TranslatedObjectiveRowsByKeyAsText}, TranslatedSummaries: {this.TranslatedSummariesAsText}, TranslatedSummaryRowsByKey: {this.TranslatedSummaryRowsByKeyAsText}, TranslatedSystemRows: {this.TranslatedSystemRowsAsText}, TranslatedSystemRowsByKey: {this.TranslatedSystemRowsByKeyAsText}";
    }
}
