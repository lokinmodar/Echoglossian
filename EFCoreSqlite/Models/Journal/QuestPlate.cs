// <copyright file="QuestPlate.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Echoglossian.EFCoreSqlite.Models.Journal
{
  [Table("questplates")]
  public class QuestPlate
  {
    [Key]
    public int Id { get; set; }

    [Required]

    public string QuestId { get; set; }

    [Required]

    public string QuestName { get; set; }

    [Required]
    public string OriginalQuestMessage { get; set; }

    [Required]
    public string OriginalLang { get; set; }

    [Required]
    public string TranslatedQuestName { get; set; }

    [Required]
    public string TranslatedQuestMessage { get; set; }

    [Required]
    public string TranslationLang { get; set; }

    [Required]
    public int TranslationEngine { get; set; }

    [Required]
    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [NotMapped]
    public Dictionary<string, string> Objectives { get; set; }

    public string ObjectivesAsText { get; set; }

    [NotMapped]
    public Dictionary<string, string> Summaries { get; set; }

    public string SummariesAsText { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }

    [NotMapped]
    public Dictionary<string, string> Texts { get; set; }

    public string SerializedTexts { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestPlate"/> class.
    /// </summary>
    /// <param name="questName"></param>
    /// <param name="originalQuestMessage"></param>
    /// <param name="originalLang"></param>
    /// <param name="translatedQuestName"></param>
    /// <param name="translatedQuestMessage"></param>
    /// <param name="questId"></param>
    /// <param name="translationLang"></param>
    /// <param name="translationEngine"></param>
    /// <param name="createdDate"></param>
    /// <param name="updatedDate"></param>
    public QuestPlate(
      string questName, string originalQuestMessage,
      string originalLang,
      string translatedQuestName, string translatedQuestMessage,
      string questId, string translationLang, int translationEngine,
      DateTime createdDate, DateTime? updatedDate)
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
      this.Objectives = new();
      this.Summaries = new();
      this.Texts = new();
    }

    public void UpdateFieldsAsText()
    {
      this.ObjectivesAsText = string.Empty;
      this.SummariesAsText = string.Empty;
      if (this.Objectives != null && this.Objectives.Count != 0)
      {
        this.ObjectivesAsText = JsonSerializer.Serialize(this.Objectives);
      }

      if (this.Summaries != null && this.Summaries.Count != 0)
      {
        this.SummariesAsText = JsonSerializer.Serialize(this.Summaries);
      }

      if (this.Texts != null && this.Texts.Count != 0)
      {
        this.SerializedTexts = JsonSerializer.Serialize(this.Texts);
      }
    }

    public void UpdateFieldsFromText()
    {
      if (this.ObjectivesAsText != null && this.ObjectivesAsText != string.Empty)
      {
        this.Objectives = JsonSerializer.Deserialize<Dictionary<string, string>>(this.ObjectivesAsText);
      }

      if (this.SummariesAsText != null && this.SummariesAsText != string.Empty)
      {
        this.Summaries = JsonSerializer.Deserialize<Dictionary<string, string>>(this.SummariesAsText);
      }

      if (this.SerializedTexts != null && this.SerializedTexts != string.Empty)
      {
        this.Texts = JsonSerializer.Deserialize<Dictionary<string, string>>(this.SerializedTexts);
      }
    }

    public override string ToString()
    {
      return
        $"Id: {this.Id}, QuestName: {this.QuestName}, QuestID: {this.QuestId}, OriginalMsg: {this.OriginalQuestMessage}, OriginalLang: {this.OriginalLang}, TranslQuestName: {this.TranslatedQuestName}, TranslMsg: {this.TranslatedQuestMessage}, TransLang: {this.TranslationLang}, TranEngine: {this.TranslationEngine}, CreatedAt: {this.CreatedDate}, UpdatedAt: {this.UpdatedDate}, Objectives: {this.Objectives}, Summaries: {this.Summaries}, Texts: {this.Texts}";
    }
  }
}
