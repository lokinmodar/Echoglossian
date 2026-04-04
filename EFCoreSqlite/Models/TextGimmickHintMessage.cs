// <copyright file="TextGimmickHintMessage.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

[Table("textgimmickhintmessages")]
public partial class TextGimmickHintMessage
{
  /// <summary>
  ///     Initializes a new instance of the <see cref="TextGimmickHintMessage" /> class.
  /// </summary>
  /// <param name="originalText">The original text gimmick hint message.</param>
  /// <param name="originalLang">The language of the original message.</param>
  /// <param name="translatedText">The translated gimmick hint message.</param>
  /// <param name="translationLang">The language of the translated message.</param>
  /// <param name="translationEngine">The translation engine used.</param>
  /// <param name="createdDate">The date the message was created.</param>
  /// <param name="updatedDate">The date the message was last updated.</param>
  public TextGimmickHintMessage(
      string? originalText,
      string? originalLang,
      string? translatedText,
      string? translationLang,
      int? translationEngine,
      DateTime? createdDate,
      DateTime? updatedDate)
  {
    this.OriginalText = originalText;
    this.OriginalLang = originalLang;
    this.TranslatedText = translatedText;
    this.TranslationLang = translationLang;
    this.TranslationEngine = translationEngine;
    this.CreatedDate = createdDate;
    this.UpdatedDate = updatedDate;
  }

  [Key] public int Id { get; set; }

  public string? OriginalText { get; set; }

  public string? OriginalLang { get; set; }

  public string? TranslatedText { get; set; }

  public string? TranslationLang { get; set; }

  public int? TranslationEngine { get; set; }

  public DateTime? CreatedDate { get; set; }

  public DateTime? UpdatedDate { get; set; }

  [Timestamp] public byte[]? RowVersion { get; set; }

  public override string? ToString()
  {
    return
        $"Id: {this.Id}, OriginalMsg: {this.OriginalText}, OriginalLang: {this.OriginalLang}, TranslMsg: {this.TranslatedText}, TransLang: {this.TranslationLang}, TranEngine: {this.TranslationEngine}, CreatedAt: {this.CreatedDate}, UpdatedAt: {this.UpdatedDate}";
  }
}
