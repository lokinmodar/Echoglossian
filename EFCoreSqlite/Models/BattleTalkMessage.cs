// <copyright file="BattleTalkMessage.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Echoglossian.EFCoreSqlite.Models
{
  [Table("battletalkmessages")]
  public class BattleTalkMessage
  {
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string SenderName { get; set; }

    [Required]
    [MaxLength(400)]
    public string OriginalBattleTalkMessage { get; set; }

    [Required]
    public string OriginalSenderNameLang { get; set; }

    [Required]
    public string OriginalBattleTalkMessageLang { get; set; }

    [MaxLength(100)]
    public string TranslatedSenderName { get; set; }

    [MaxLength(400)]
    public string TranslatedBattleTalkMessage { get; set; }

    [Required]
    public string TranslationLang { get; set; }

    [Required]
    public int TranslationEngine { get; set; }

    [Required]
    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }

    public BattleTalkMessage(string senderName, string originalBattleTalkMessage, string originalBattleTalkMessageLang,
      string originalSenderNameLang, string translatedSenderName, string translatedBattleTalkMessage,
      string translationLang, int translationEngine, DateTime createdDate, DateTime? updatedDate)
    {
      this.SenderName = senderName;
      this.OriginalBattleTalkMessage = originalBattleTalkMessage;
      this.OriginalSenderNameLang = originalSenderNameLang;
      this.OriginalBattleTalkMessageLang = originalBattleTalkMessageLang;
      this.TranslatedSenderName = translatedSenderName;
      this.TranslatedBattleTalkMessage = translatedBattleTalkMessage;
      this.TranslationLang = translationLang;
      this.TranslationEngine = translationEngine;
      this.TranslationEngine = translationEngine;
      this.CreatedDate = createdDate;
      this.UpdatedDate = updatedDate;
    }

    public override string ToString()
    {
      return
        $"Id: {this.Id}, Sender: {this.SenderName}, OriginalMsg: {this.OriginalBattleTalkMessage}, OriginalLang: {this.OriginalBattleTalkMessageLang}, OriginalSenderNameLang: {this.OriginalSenderNameLang}, TranslatedName: {this.TranslatedSenderName}, TranslMsg: {this.TranslatedBattleTalkMessage}, TransLang: {this.TranslationLang}, TranEngine: {this.TranslationEngine}, CreatedAt: {this.CreatedDate}, UpdatedAt: {this.UpdatedDate}";
    }
  }
}
