// <copyright file="QuestPlate.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Echoglossian.EFCoreSqlite.Models.Journal
{
  /// <summary>
  /// Represents a quest plate in the database.
  /// </summary>
  [Table("questplates")]
  public class QuestPlate
  {
    [Key]
    public int Id { get; set; }

    public string? QuestId { get; set; }

    public string? QuestName { get; set; }

    public string? OriginalQuestMessage { get; set; }

    public string? OriginalLang { get; set; }

    public string? TranslatedQuestName { get; set; }

    public string? TranslatedQuestMessage { get; set; }

    public string? TranslationLang { get; set; }

    public int? TranslationEngine { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [NotMapped]
    private Dictionary<string, string>? objectives;

    [NotMapped]
    private Dictionary<string, string>? summaries;

    public string? ObjectivesAsText { get; set; }

    public string? SummariesAsText { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestPlate"/> class.
    /// </summary>
    public QuestPlate(
      string? questName, string? originalQuestMessage,
      string? originalLang,
      string? translatedQuestName, string? translatedQuestMessage,
      string? questId, string? translationLang, int? translationEngine,
      DateTime? createdDate, DateTime? updatedDate)
    {
      this.QuestId = questId;
      this.QuestName = questName;
      this.OriginalQuestMessage = originalQuestMessage;
      this.OriginalLang = originalLang;
      this.TranslatedQuestName = translatedQuestName;
      this.TranslatedQuestMessage = translatedQuestMessage;
      this.TranslationLang = translationLang;
      this.TranslationEngine = translationEngine;
      this.CreatedDate = createdDate;
      this.UpdatedDate = updatedDate;
      this.objectives = new();
      this.summaries = new();
    }

    /// <summary>
    /// Lazily loads Objectives from text if needed.
    /// </summary>
    [NotMapped]
    public Dictionary<string, string> Objectives
    {
      get
      {
        if (this.objectives == null)
        {
          // Parentheses are necessary to group the ?? operator inside the ternary
          this.objectives = !string.IsNullOrEmpty(this.ObjectivesAsText)
            ? (JsonConvert.DeserializeObject<Dictionary<string, string>>(this.ObjectivesAsText) ?? new Dictionary<string, string>())
            : new Dictionary<string, string>();
        }

        return this.objectives;
      }
      set => this.objectives = value;
    }

    /// <summary>
    /// Lazily loads Summaries from text if needed.
    /// </summary>
    [NotMapped]
    public Dictionary<string, string> Summaries
    {
      get
      {
        if (this.summaries == null)
        {
          // Parentheses are necessary to group the ?? operator inside the ternary
          this.summaries = !string.IsNullOrEmpty(this.SummariesAsText)
            ? (JsonConvert.DeserializeObject<Dictionary<string, string>>(this.SummariesAsText) ?? new Dictionary<string, string>())
            : new Dictionary<string, string>();
        }

        return this.summaries;
      }
      set => this.summaries = value;
    }

    /// <summary>
    /// Updates the ObjectivesAsText and SummariesAsText fields based on their dictionaries.
    /// </summary>
    public void UpdateFieldsAsText(bool prettyPrint = false)
    {
      this.ObjectivesAsText = string.Empty;
      this.SummariesAsText = string.Empty;

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
    }

    /// <summary>
    /// Forces loading the Objectives and Summaries from the stored text fields.
    /// </summary>
    public void UpdateFieldsFromText()
    {
      // Parentheses needed here as well
      this.objectives = !string.IsNullOrEmpty(this.ObjectivesAsText)
        ? (JsonConvert.DeserializeObject<Dictionary<string, string>>(this.ObjectivesAsText) ?? new Dictionary<string, string>())
        : new Dictionary<string, string>();

      this.summaries = !string.IsNullOrEmpty(this.SummariesAsText)
        ? (JsonConvert.DeserializeObject<Dictionary<string, string>>(this.SummariesAsText) ?? new Dictionary<string, string>())
        : new Dictionary<string, string>();
    }

    public override string? ToString()
    {
      return
        $"Id: {this.Id}, QuestName: {this.QuestName}, QuestID: {this.QuestId}, OriginalMsg: {this.OriginalQuestMessage}, OriginalLang: {this.OriginalLang}, TranslQuestName: {this.TranslatedQuestName}, TranslMsg: {this.TranslatedQuestMessage}, TransLang: {this.TranslationLang}, TranEngine: {this.TranslationEngine}, CreatedAt: {this.CreatedDate}, UpdatedAt: {this.UpdatedDate}, Objectives: {this.Objectives}, Summaries: {this.Summaries}";
    }
  }
}