// <copyright file="MiniTalkMessage.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

[Table("minitalkmessages")]
public partial class MiniTalkMessage
{
  /// <summary>
  ///     Initializes a new instance of the <see cref="MiniTalkMessage" /> class.
  /// </summary>
  /// <param name="originalMiniTalkMessage">The original MiniTalk message.</param>
  /// <param name="originalMiniTalkMessageLang">The language of the original MiniTalk message.</param>
  /// <param name="translatedMiniTalkMessage">The translated MiniTalk message.</param>
  /// <param name="translationLang">The language of the translated MiniTalk message.</param>
  /// <param name="translationEngine">The translation engine used.</param>
  /// <param name="createdDate">The date the message was created.</param>
  /// <param name="updatedDate">The date the message was last updated.</param>
  public MiniTalkMessage(
      string? originalMiniTalkMessage,
      string? originalMiniTalkMessageLang,
      string? translatedMiniTalkMessage,
      string? translationLang,
      int? translationEngine,
      DateTime? createdDate,
      DateTime? updatedDate)
  {
    this.OriginalMiniTalkMessage = originalMiniTalkMessage;
    this.OriginalMiniTalkMessageLang = originalMiniTalkMessageLang;
    this.TranslatedMiniTalkMessage = translatedMiniTalkMessage;
    this.TranslationLang = translationLang;
    this.TranslationEngine = translationEngine;
    this.CreatedDate = createdDate;
    this.UpdatedDate = updatedDate;
  }

  [Key] public int Id { get; set; }

  public string? OriginalMiniTalkMessage { get; set; }

  public string? OriginalMiniTalkMessageLang { get; set; }

  public string? TranslatedMiniTalkMessage { get; set; }

  public string? TranslationLang { get; set; }

  public int? TranslationEngine { get; set; }

  public DateTime? CreatedDate { get; set; }

  public DateTime? UpdatedDate { get; set; }

  [Timestamp] public byte[]? RowVersion { get; set; }

  public override string? ToString()
  {
    return
        $"Id: {this.Id}, OriginalMsg: {this.OriginalMiniTalkMessage}, OriginalLang: {this.OriginalMiniTalkMessageLang}, TranslMsg: {this.TranslatedMiniTalkMessage}, TransLang: {this.TranslationLang}, TranEngine: {this.TranslationEngine}, CreatedAt: {this.CreatedDate}, UpdatedAt: {this.UpdatedDate}";
  }
}
