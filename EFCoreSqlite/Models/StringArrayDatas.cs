// <copyright file="StringArrayData.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
/// Represents a record of a translated string array, including raw data and translation metadata.
/// </summary>
[Table("stringarraydatas")]
public class StringArrayData
{
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
  /// Gets or sets the language of the original content.
  /// </summary>
  public string? OriginalLanguage { get; set; }

  /// <summary>
  /// Gets or sets the original string array in serialized JSON format.
  /// </summary>
  public string? OriginalStrings { get; set; }

  /// <summary>
  /// Gets or sets the language used for the translated content.
  /// </summary>
  public string? TranslationLanguage { get; set; }

  /// <summary>
  /// Gets or sets the translated string array in serialized JSON format.
  /// </summary>
  public string? TranslatedStrings { get; set; }

  /// <summary>
  /// Gets or sets the translation engine used to generate the translations.
  /// </summary>
  public string? TranslationEngine { get; set; }

  /// <summary>
  /// Gets or sets the game version this data was captured from.
  /// </summary>
  public string? GameVersion { get; set; }

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
}
