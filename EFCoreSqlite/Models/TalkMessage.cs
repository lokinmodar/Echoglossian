// <copyright file="TalkMessage.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Echoglossian.EFCoreSqlite.Models
{
  [Table("talkmessages")]
  public class TalkMessage
  {
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string SenderName { get; set; }

    [Required]
    [MaxLength(400)]
    public string OriginalTalkMessage { get; set; }

    [Required]
    public string OriginalSenderNameLang { get; set; }

    [Required]
    public string OriginalTalkMessageLang { get; set; }

    [MaxLength(100)]
    public string TranslatedSenderName { get; set; }

    [MaxLength(400)]
    public string TranslatedTalkMessage { get; set; }

    [Required]
    public string TranslationLang { get; set; }

    [Required]
    public int TranslationEngine { get; set; }

    [Required]
    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }

    public TalkMessage(string senderName, string originalTalkMessage, string originalTalkMessageLang, string originalSenderNameLang, string translatedSenderName, string translatedTalkMessage, string translationLang, int translationEngine, DateTime createdDate, DateTime? updatedDate)
    {
      this.SenderName = senderName;
      this.OriginalTalkMessage = originalTalkMessage;
      this.OriginalSenderNameLang = originalSenderNameLang;
      this.OriginalTalkMessageLang = originalTalkMessageLang;
      this.TranslatedSenderName = translatedSenderName;
      this.TranslatedTalkMessage = translatedTalkMessage;
      this.TranslationLang = translationLang;
      this.TranslationEngine = translationEngine;
      this.TranslationEngine = translationEngine;
      this.CreatedDate = createdDate;
      this.UpdatedDate = updatedDate;
    }

    public override string ToString()
    {
      return
        $"Id: {this.Id}, Sender: {this.SenderName}, OriginalMsg: {this.OriginalTalkMessage}, OriginalLang: {this.OriginalTalkMessageLang}, OriginalSenderNameLang: {this.OriginalSenderNameLang}, TranslatedName: {this.TranslatedSenderName}, TranslMsg: {this.TranslatedTalkMessage}, TransLang: {this.TranslationLang}, TranEngine: {this.TranslationEngine}, CreatedAt: {this.CreatedDate}, UpdatedAt: {this.UpdatedDate}";
    }
  }
}