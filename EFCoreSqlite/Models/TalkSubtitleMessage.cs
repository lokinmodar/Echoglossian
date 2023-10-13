// <copyright file="TalkSubtitleMessage.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Echoglossian.EFCoreSqlite.Models
{
  [Table("TalkSubtitleMessages")]
  public class TalkSubtitleMessage
  {
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(400)]
    public string OriginalTalkSubtitleMessage { get; set; }

    [Required]
    public string OriginalTalkSubtitleMessageLang { get; set; }

    [MaxLength(400)]
    public string TranslatedTalkSubtitleMessage { get; set; }

    [Required]
    public string TranslationLang { get; set; }

    [Required]
    public int TranslationEngine { get; set; }

    [Required]
    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TalkSubtitleMessage"/> class.
    /// </summary>
    /// <param name="originalTalkSubtitleMessage"></param>
    /// <param name="originalTalkSubtitleMessageLang"></param>
    /// <param name="translatedTalkSubtitleMessage"></param>
    /// <param name="translationLang"></param>
    /// <param name="translationEngine"></param>
    /// <param name="createdDate"></param>
    /// <param name="updatedDate"></param>
    public TalkSubtitleMessage(string originalTalkSubtitleMessage, string originalTalkSubtitleMessageLang, string translatedTalkSubtitleMessage, string translationLang, int translationEngine, DateTime createdDate, DateTime? updatedDate)
    {

      this.OriginalTalkSubtitleMessage = originalTalkSubtitleMessage;
      this.OriginalTalkSubtitleMessageLang = originalTalkSubtitleMessageLang;
      this.TranslatedTalkSubtitleMessage = translatedTalkSubtitleMessage;
      this.TranslationLang = translationLang;
      this.TranslationEngine = translationEngine;
      this.TranslationEngine = translationEngine;
      this.CreatedDate = createdDate;
      this.UpdatedDate = updatedDate;
    }

    public override string ToString()
    {
      return
        $"Id: {this.Id}, OriginalMsg: {this.OriginalTalkSubtitleMessage}, OriginalLang: {this.OriginalTalkSubtitleMessageLang}, TranslMsg: {this.TranslatedTalkSubtitleMessage}, TransLang: {this.TranslationLang}, TranEngine: {this.TranslationEngine}, CreatedAt: {this.CreatedDate}, UpdatedAt: {this.UpdatedDate}";
    }
  }
}