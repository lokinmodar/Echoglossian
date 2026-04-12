// <copyright file="QuestPlate.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models.Journal;

/// <summary>
///     Represents a quest plate in the database.
/// </summary>
[Table("questplates")]
public class QuestPlate
{
    [NotMapped] private Dictionary<string, string>? objectives;

    [NotMapped] private Dictionary<string, string>? summaries;

    [NotMapped] private Dictionary<string, string>? translatedObjectives;

    [NotMapped] private Dictionary<string, string>? translatedSummaries;

    [NotMapped] private Dictionary<string, string>? systemRows;

    [NotMapped] private Dictionary<string, string>? translatedSystemRows;

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
        this.objectives = new Dictionary<string, string>();
        this.summaries = new Dictionary<string, string>();
        this.translatedObjectives = new Dictionary<string, string>();
        this.translatedSummaries = new Dictionary<string, string>();
        this.systemRows = new Dictionary<string, string>();
        this.translatedSystemRows = new Dictionary<string, string>();
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
    ///     Gets or sets the canonical quest text sheet path (e.g. "quest/043/AktKmb114_04393").
    /// </summary>
    public string? QuestTextSheetName { get; set; }

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

    public string? SummariesAsText { get; set; }

    /// <summary>
    ///     Gets or sets the translated TODO rows serialized as JSON.
    /// </summary>
    public string? TranslatedObjectivesAsText { get; set; }

    /// <summary>
    ///     Gets or sets the translated SEQ rows serialized as JSON.
    /// </summary>
    public string? TranslatedSummariesAsText { get; set; }

    /// <summary>
    ///     Gets or sets the original SYSTEM (cinematic caption) rows serialized as JSON.
    /// </summary>
    public string? SystemRowsAsText { get; set; }

    /// <summary>
    ///     Gets or sets the translated SYSTEM rows serialized as JSON.
    /// </summary>
    public string? TranslatedSystemRowsAsText { get; set; }

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
    ///     Updates the serialized text fields from their in-memory dictionaries.
    /// </summary>
    /// <param name="prettyPrint">Should it be pretty printed?.</param>
    public void UpdateFieldsAsText(bool prettyPrint = false)
    {
        this.ObjectivesAsText = string.Empty;
        this.SummariesAsText = string.Empty;
        this.TranslatedObjectivesAsText = string.Empty;
        this.TranslatedSummariesAsText = string.Empty;
        this.SystemRowsAsText = string.Empty;
        this.TranslatedSystemRowsAsText = string.Empty;

        if (this.objectives != null && this.objectives.Count != 0)
        {
            this.ObjectivesAsText = JsonConvert.SerializeObject(
                this.objectives,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.summaries != null && this.summaries.Count != 0)
        {
            this.SummariesAsText = JsonConvert.SerializeObject(
                this.summaries,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.translatedObjectives != null && this.translatedObjectives.Count != 0)
        {
            this.TranslatedObjectivesAsText = JsonConvert.SerializeObject(
                this.translatedObjectives,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.translatedSummaries != null && this.translatedSummaries.Count != 0)
        {
            this.TranslatedSummariesAsText = JsonConvert.SerializeObject(
                this.translatedSummaries,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.systemRows != null && this.systemRows.Count != 0)
        {
            this.SystemRowsAsText = JsonConvert.SerializeObject(
                this.systemRows,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.translatedSystemRows != null && this.translatedSystemRows.Count != 0)
        {
            this.TranslatedSystemRowsAsText = JsonConvert.SerializeObject(
                this.translatedSystemRows,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }
    }

    /// <summary>
    ///     Forces loading the Objectives and Summaries from the stored text fields.
    /// </summary>
    public void UpdateFieldsFromText()
    {
        // Parentheses needed here as well
        this.objectives = !string.IsNullOrEmpty(this.ObjectivesAsText)
            ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                this.ObjectivesAsText) ?? new Dictionary<string, string>()
            : new Dictionary<string, string>();

        this.summaries = !string.IsNullOrEmpty(this.SummariesAsText)
            ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                this.SummariesAsText) ?? new Dictionary<string, string>()
            : new Dictionary<string, string>();

        this.translatedObjectives = !string.IsNullOrEmpty(this.TranslatedObjectivesAsText)
            ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                this.TranslatedObjectivesAsText) ?? new Dictionary<string, string>()
            : new Dictionary<string, string>();

        this.translatedSummaries = !string.IsNullOrEmpty(this.TranslatedSummariesAsText)
            ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                this.TranslatedSummariesAsText) ?? new Dictionary<string, string>()
            : new Dictionary<string, string>();

        this.systemRows = !string.IsNullOrEmpty(this.SystemRowsAsText)
            ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                this.SystemRowsAsText) ?? new Dictionary<string, string>()
            : new Dictionary<string, string>();

        this.translatedSystemRows = !string.IsNullOrEmpty(this.TranslatedSystemRowsAsText)
            ? JsonConvert.DeserializeObject<Dictionary<string, string>>(
                this.TranslatedSystemRowsAsText) ?? new Dictionary<string, string>()
            : new Dictionary<string, string>();
    }

    /// <inheritdoc />
    public override string? ToString()
    {
        return
            $"Id: {this.Id}, QuestName: {this.QuestName}, QuestID: {this.QuestId}, Sheet: {this.QuestTextSheetName}, OriginalMsg: {this.OriginalQuestMessage}, OriginalLang: {this.OriginalLang}, TranslQuestName: {this.TranslatedQuestName}, TranslMsg: {this.TranslatedQuestMessage}, TransLang: {this.TranslationLang}, TranEngine: {this.TranslationEngine}, GameVersion: {this.GameVersion}, CreatedAt: {this.CreatedDate}, UpdatedAt: {this.UpdatedDate}, Objectives: {this.ObjectivesAsText}, Summaries: {this.SummariesAsText}, SystemRows: {this.SystemRowsAsText}";
    }
}
