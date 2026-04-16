// <copyright file="StringArrayDatas.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
/// Represents a record of a translated string array, including raw data and translation metadata.
/// </summary>
[Table("stringarraydatas")]
public partial class StringArrayDatas
{

  /// <summary>
  ///  Initializes a new instance of the <see cref="StringArrayDatas"/> class with specified parameters.
  /// </summary>
  /// <param name="type">The type of addon or structure this array belongs to.</param>
  /// <param name="size">The size of the array.</param>
  /// <param name="rawData">The raw binary data of the string array.</param>
  /// <param name="formattedRawData">The raw data formatted with payloads.</param>
  /// <param name="originalLang">The Lang of the original content.</param>
  /// <param name="originalStrings">The original string array in serialized JSON format.</param>
  /// <param name="translationLang">The Lang used for the translated content.</param>
  /// <param name="translatedStrings">The translated string array in serialized JSON format.</param>
  /// <param name="translatedStringsWithPayloads">The translated strings with associated payloads.</param>
  /// <param name="translationEngine">The translation engine used to generate the translations.</param>
  /// <param name="gameVersion">The game version this data was captured from.</param>
  /// <param name="createdAt">The creation timestamp.</param>
  /// <param name="updatedAt">The last updated timestamp.</param>
  public StringArrayDatas(string? type, int size, byte[]? rawData, string? formattedRawData, string? originalLang, string? originalStrings, string? translationLang, string? translatedStrings, string? translatedStringsWithPayloads, int? translationEngine, string? gameVersion, DateTime createdAt, DateTime updatedAt)
  {
    this.Type = type;
    this.Size = size;
    this.RawData = rawData;
    this.FormattedRawData = formattedRawData;
    this.OriginalLang = originalLang;
    this.OriginalStrings = originalStrings;
    this.TranslationLang = translationLang;
    this.TranslatedStrings = translatedStrings;
    this.TranslatedStringsWithPayloads = translatedStringsWithPayloads;
    this.TranslationEngine = translationEngine;
    this.GameVersion = gameVersion;
    this.CreatedAt = createdAt;
    this.UpdatedAt = updatedAt;
  }

  /// <summary>
  /// Gets or sets the array index as primary key.
  /// </summary>
  [Key]
  public int Id { get; set; }

  /// <summary>
  /// Gets or sets the type of addon or structure this array belongs to.
  /// </summary>
  public string? Type { get; set; }

  /// <summary>
  /// Gets or sets the size of the array.
  /// </summary>
  public int Size { get; set; }

  /// <summary>
  /// Gets or sets the raw binary data of the string array.
  /// </summary>
  public byte[]? RawData { get; set; }

  /// <summary>
  /// Gets or sets the raw data formatted with payloads.
  /// </summary>
  public string? FormattedRawData { get; set; }

  /// <summary>
  /// Gets or sets the Lang of the original content.
  /// </summary>
  public string? OriginalLang { get; set; }

  /// <summary>
  /// Gets or sets the original string array in serialized JSON format.
  /// </summary>
  public string? OriginalStrings { get; set; }

  /// <summary>
  /// Gets or sets the Lang used for the translated content.
  /// </summary>
  public string? TranslationLang { get; set; }

  /// <summary>
  /// Gets or sets the translated string array in serialized JSON format.
  /// </summary>
  public string? TranslatedStrings { get; set; }

  /// <summary>
  /// Gets or sets the translated strings with associated payloads.
  /// </summary>
  public string? TranslatedStringsWithPayloads { get; set; }

  /// <summary>
  /// Gets or sets the translation engine used to generate the translations.
  /// </summary>
  public int? TranslationEngine { get; set; }

  /// <summary>
  /// Gets or sets the game version this data was captured from.
  /// </summary>
  public string? GameVersion { get; set; }

  /// <summary>
  /// Gets or sets the semantic context key for this string-array surface.
  /// </summary>
  public string? ContextKey { get; set; }

  /// <summary>
  /// Gets or sets the schema version used to serialize the structured payload.
  /// </summary>
  public int? SchemaVersion { get; set; }

  /// <summary>
  /// Gets or sets the stable source-content hash for the canonical payload.
  /// </summary>
  public string? SourceContentHash { get; set; }

  /// <summary>
  /// Gets or sets the structured original payload for this surface.
  /// </summary>
  public string? OriginalStructuredPayload { get; set; }

  /// <summary>
  /// Gets or sets the structured translated payload for this surface.
  /// </summary>
  public string? TranslatedStructuredPayload { get; set; }

  /// <summary>
  /// Gets or sets the creation timestamp.
  /// </summary>
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// Gets or sets the last updated timestamp.
  /// </summary>
  public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// Gets or sets the row version for concurrency handling.
  /// </summary>
  [Timestamp]
  public byte[]? RowVersion { get; set; }

  public override bool Equals(object? obj)
  {
    return base.Equals(obj);
  }

  public override int GetHashCode()
  {
    return base.GetHashCode();
  }

  public override string? ToString()
  {
    return $"Id: {this.Id}, Type: {this.Type}, Size: {this.Size}, OriginalLang: {this.OriginalLang}, TranslationLang: {this.TranslationLang}, OriginalStrings: {this.OriginalStrings}, TranslatedStrings: {this.TranslatedStrings}, TranslationEngine: {this.TranslationEngine}, GameVersion: {this.GameVersion}, CreatedAt: {this.CreatedAt}, UpdatedAt: {this.UpdatedAt}";
  }
}
