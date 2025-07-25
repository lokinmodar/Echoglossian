// <copyright file="BattleTalkMessage.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

[Table("battletalkmessages")]
public partial class BattleTalkMessage
{
  /// <summary>
  ///     Initializes a new instance of the <see cref="BattleTalkMessage" /> class.
  /// </summary>
  /// <param name="senderName">The name of the sender.</param>
  /// <param name="originalBattleTalkMessage">The original battle talk message.</param>
  /// <param name="originalBattleTalkMessageLang">The language of the original battle talk message.</param>
  /// <param name="originalSenderNameLang">The language of the original sender name.</param>
  /// <param name="translatedSenderName">The translated sender name.</param>
  /// <param name="translatedBattleTalkMessage">The translated battle talk message.</param>
  /// <param name="translationLang">The language of the translation.</param>
  /// <param name="translationEngine">The translation engine used.</param>
  /// <param name="rtlLangTranslationImageData">The RTL language translation image data.</param>
  /// <param name="createdDate">The date the message was created.</param>
  /// <param name="updatedDate">The date the message was last updated.</param>
  public BattleTalkMessage(
      string? senderName,
      string? originalBattleTalkMessage,
      string? originalBattleTalkMessageLang,
      string? originalSenderNameLang,
      string? translatedSenderName,
      string? translatedBattleTalkMessage,
      string? translationLang,
      int? translationEngine,
      byte[]? rtlLangTranslationImageData,
      DateTime? createdDate,
      DateTime? updatedDate)
  {
    this.SenderName = senderName;
    this.OriginalBattleTalkMessage = originalBattleTalkMessage;
    this.OriginalSenderNameLang = originalSenderNameLang;
    this.OriginalBattleTalkMessageLang = originalBattleTalkMessageLang;
    this.TranslatedSenderName = translatedSenderName;
    this.TranslatedBattleTalkMessage = translatedBattleTalkMessage;
    this.TranslationLang = translationLang;
    this.TranslationEngine = translationEngine;
    this.RTLLangTranslationImageData = rtlLangTranslationImageData;
    this.CreatedDate = createdDate;
    this.UpdatedDate = updatedDate;
  }

  [Key] public int Id { get; set; }

  public string? SenderName { get; set; }

  public string? OriginalBattleTalkMessage { get; set; }

  public string? OriginalSenderNameLang { get; set; }

  public string? OriginalBattleTalkMessageLang { get; set; }

  public string? TranslatedSenderName { get; set; }

  public string? TranslatedBattleTalkMessage { get; set; }

  public string? TranslationLang { get; set; }

  public int? TranslationEngine { get; set; }

  public byte[]? RTLLangTranslationImageData { get; set; }

  public DateTime? CreatedDate { get; set; }

  public DateTime? UpdatedDate { get; set; }

  [Timestamp] public byte[]? RowVersion { get; set; }

  public override string? ToString()
  {
    return
        $"Id: {this.Id}, Sender: {this.SenderName}, OriginalMsg: {this.OriginalBattleTalkMessage}, OriginalLang: {this.OriginalBattleTalkMessageLang}, OriginalSenderNameLang: {this.OriginalSenderNameLang}, TranslatedName: {this.TranslatedSenderName}, TranslMsg: {this.TranslatedBattleTalkMessage}, TransLang: {this.TranslationLang}, TranEngine: {this.TranslationEngine}, CreatedAt: {this.CreatedDate}, UpdatedAt: {this.UpdatedDate}";
  }
}