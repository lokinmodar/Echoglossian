// <copyright file="ToastMessage.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Echoglossian.EFCoreSqlite.Models
{
  [Table("toastmessages")]
  public class ToastMessage
  {
    /// <summary>
    ///   Initializes a new instance of the <see cref="ToastMessage" /> class.
    /// </summary>
    /// <param name="toastType"></param>
    /// <param name="originalToastMessage"></param>
    /// <param name="originalLang"></param>
    /// <param name="translatedToastMessage"></param>
    /// <param name="translationLang"></param>
    /// <param name="translationEngine"></param>
    /// <param name="createdDate"></param>
    /// <param name="updatedDate"></param>
    public ToastMessage(
      string toastType,
      string originalToastMessage,
      string originalLang,
      string translatedToastMessage,
      string translationLang,
      int translationEngine,
      DateTime createdDate,
      DateTime? updatedDate)
    {
      this.ToastType = toastType;
      this.OriginalToastMessage = originalToastMessage;
      this.OriginalLang = originalLang;
      this.TranslatedToastMessage = translatedToastMessage;
      this.TranslationLang = translationLang;
      this.TranslationEngine = translationEngine;
      this.CreatedDate = createdDate;
      this.UpdatedDate = updatedDate;
    }

    [Key] public int Id { get; set; }

    [Required] [MaxLength(40)] public string ToastType { get; set; }

    [Required] [MaxLength(200)] public string OriginalToastMessage { get; set; }

    [Required] public string OriginalLang { get; set; }

    [MaxLength(200)] public string TranslatedToastMessage { get; set; }

    [Required] public string TranslationLang { get; set; }

    [Required] public int TranslationEngine { get; set; }

    [Required] public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [Timestamp] public byte[] RowVersion { get; set; }

    public override string ToString()
    {
      return
        $"Id: {this.Id}, ToastType: {this.ToastType}, OriginalMsg: {this.OriginalToastMessage}, OriginalLang: {this.OriginalLang}, TranslMsg: {this.TranslatedToastMessage}, TransLang: {this.TranslationLang}, TranEngine: {this.TranslationEngine}, CreatedAt: {this.CreatedDate}, UpdatedAt: {this.UpdatedDate}";
    }
  }
}