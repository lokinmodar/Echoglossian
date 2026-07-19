// <copyright file="NamePlateMessage.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one persisted world-object nameplate translation.
/// </summary>
[Table("nameplatemessages")]
public partial class NamePlateMessage
{
  /// <summary>
  ///     Initializes a new instance of the <see cref="NamePlateMessage" /> class.
  /// </summary>
  /// <param name="namePlateKind">The Dalamud nameplate kind.</param>
  /// <param name="originalNamePlateText">The original nameplate text.</param>
  /// <param name="originalLang">The original language identity.</param>
  /// <param name="translatedNamePlateText">The translated nameplate text.</param>
  /// <param name="translationLang">The translated language identity.</param>
  /// <param name="translationEngine">The translation engine used.</param>
  /// <param name="createdDate">The date the row was created.</param>
  /// <param name="updatedDate">The date the row was last updated.</param>
  public NamePlateMessage(
      int? namePlateKind,
      string? originalNamePlateText,
      string? originalLang,
      string? translatedNamePlateText,
      string? translationLang,
      int? translationEngine,
      DateTime? createdDate,
      DateTime? updatedDate)
  {
    this.NamePlateKind = namePlateKind;
    this.OriginalNamePlateText = originalNamePlateText;
    this.OriginalLang = originalLang;
    this.TranslatedNamePlateText = translatedNamePlateText;
    this.TranslationLang = translationLang;
    this.TranslationEngine = translationEngine;
    this.CreatedDate = createdDate;
    this.UpdatedDate = updatedDate;
  }

  /// <summary>
  ///     Gets or sets the primary key.
  /// </summary>
  [Key] public int Id { get; set; }

  /// <summary>
  ///     Gets or sets the Dalamud nameplate kind.
  /// </summary>
  public int? NamePlateKind { get; set; }

  /// <summary>
  ///     Gets or sets the original nameplate text.
  /// </summary>
  public string? OriginalNamePlateText { get; set; }

  /// <summary>
  ///     Gets or sets the source language identity.
  /// </summary>
  public string? OriginalLang { get; set; }

  /// <summary>
  ///     Gets or sets the translated nameplate text.
  /// </summary>
  public string? TranslatedNamePlateText { get; set; }

  /// <summary>
  ///     Gets or sets the target language identity.
  /// </summary>
  public string? TranslationLang { get; set; }

  /// <summary>
  ///     Gets or sets the translation engine identifier.
  /// </summary>
  public int? TranslationEngine { get; set; }

  /// <summary>
  ///     Gets or sets the creation timestamp.
  /// </summary>
  public DateTime? CreatedDate { get; set; }

  /// <summary>
  ///     Gets or sets the last update timestamp.
  /// </summary>
  public DateTime? UpdatedDate { get; set; }

  /// <summary>
  ///     Gets or sets the optimistic concurrency token.
  /// </summary>
  [Timestamp] public byte[]? RowVersion { get; set; }

  /// <inheritdoc />
  public override string? ToString()
  {
    return
        $"Id: {this.Id}, NamePlateKind: {this.NamePlateKind}, OriginalText: {this.OriginalNamePlateText}, OriginalLang: {this.OriginalLang}, TranslatedText: {this.TranslatedNamePlateText}, TransLang: {this.TranslationLang}, TranEngine: {this.TranslationEngine}, CreatedAt: {this.CreatedDate}, UpdatedAt: {this.UpdatedDate}";
  }
}
