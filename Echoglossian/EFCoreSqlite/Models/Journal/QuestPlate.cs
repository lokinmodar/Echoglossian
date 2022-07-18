// <copyright file="QuestPlate.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Echoglossian.EFCoreSqlite.Models.Journal
{
  [Table("questplates")]
  public class QuestPlate
  {
    /// <summary>
    ///   Initializes a new instance of the <see cref="QuestPlate" /> class.
    /// </summary>
    /// <param name="questName"></param>
    /// <param name="originalQuestMessage"></param>
    /// <param name="originalLang"></param>
    /// <param name="translatedQuestMessage"></param>
    /// <param name="translationLang"></param>
    /// <param name="translationEngine"></param>
    /// <param name="questId"></param>
    /// <param name="translatedQuestName"></param>
    /// <param name="createdDate"></param>
    /// <param name="updatedDate"></param>
    public QuestPlate(
      string questName,
      string originalQuestMessage,
      string originalLang,
      string translatedQuestMessage,
      string translationLang,
      int translationEngine,
      string questId,
      string translatedQuestName,
      DateTime createdDate,
      DateTime? updatedDate)
    {
      this.QuestId = questId;
      this.QuestName = questName;
      this.OriginalQuestMessage = originalQuestMessage;
      this.OriginalLang = originalLang;
      this.TranslatedQuestMessage = translatedQuestMessage;
      this.TranslatedQuestName = translatedQuestName;
      this.TranslationLang = translationLang;
      this.TranslationEngine = translationEngine;
      this.TranslationEngine = translationEngine;
      this.CreatedDate = createdDate;
      this.UpdatedDate = updatedDate;
    }

    [Key]
    public int Id { get; set; }

    [Required]
    public string QuestId { get; set; }

    [Required]
    [MaxLength(200)]
    public string QuestName { get; set; }

    [Required]
    [MaxLength(2500)]
    public string OriginalQuestMessage { get; set; }

    [Required]
    public string OriginalLang { get; set; }

    [Required]
    [MaxLength(200)]
    public string TranslatedQuestName { get; set; }

    [Required]
    [MaxLength(2500)]
    public string TranslatedQuestMessage { get; set; }

    [Required]
    public string TranslationLang { get; set; }

    [Required]
    public int TranslationEngine { get; set; }

    [Required]
    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }

    public override string ToString()
    {
      return
        $"Id: {this.Id}, QuestName: {this.QuestName}, QuestID: {this.QuestId} OriginalMsg: {this.OriginalQuestMessage}, OriginalLang: {this.OriginalLang}, TranslQuestName: {this.TranslatedQuestName}, TranslMsg: {this.TranslatedQuestMessage}, TransLang: {this.TranslationLang}, TranEngine: {this.TranslationEngine}, CreatedAt: {this.CreatedDate}, UpdatedAt: {this.UpdatedDate}";
    }
  }
}