// <copyright file="ReferenceTextRowBase.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Provides the shared canonical persisted fields for action-adjacent
///     Excel-sheet reference-text rows.
/// </summary>
public abstract class ReferenceTextRowBase
{
    /// <summary>
    ///     Gets or sets the primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the stable sheet-row identifier.
    /// </summary>
    public uint ReferenceId { get; set; }

    /// <summary>
    ///     Gets or sets the canonical original name.
    /// </summary>
    public string? OriginalName { get; set; }

    /// <summary>
    ///     Gets or sets the canonical original description, when available.
    /// </summary>
    public string? OriginalDescription { get; set; }

    /// <summary>
    ///     Gets or sets the language of the original source payload.
    /// </summary>
    public string? OriginalLang { get; set; }

    /// <summary>
    ///     Gets or sets the translated name.
    /// </summary>
    public string? TranslatedName { get; set; }

    /// <summary>
    ///     Gets or sets the translated description, when available.
    /// </summary>
    public string? TranslatedDescription { get; set; }

    /// <summary>
    ///     Gets or sets the target translation language.
    /// </summary>
    public string? TranslationLang { get; set; }

    /// <summary>
    ///     Gets or sets the translation-engine identifier.
    /// </summary>
    public int? TranslationEngine { get; set; }

    /// <summary>
    ///     Gets or sets the game version associated with the source payload.
    /// </summary>
    public string? GameVersion { get; set; }

    /// <summary>
    ///     Gets or sets the stable source-content hash.
    /// </summary>
    public string? SourceContentHash { get; set; }

    /// <summary>
    ///     Gets or sets the serialized canonical payload, including translated
    ///     fields when available.
    /// </summary>
    public string? CanonicalPayloadAsText { get; set; }

    /// <summary>
    ///     Gets or sets the row creation time in UTC.
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets the row update time in UTC.
    /// </summary>
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets the optimistic-concurrency token.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
