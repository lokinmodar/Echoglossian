// <copyright file="ToDoText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Stores translated ToDo payloads independently from generic game-window,
///     quest, and selection-dialog rows.
/// </summary>
[Table("todotexts")]
public sealed class ToDoText
{
    /// <summary>
    ///     Gets or sets the primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Gets or sets the addon name.</summary>
    public string? AddonName { get; set; }

    /// <summary>Gets or sets the serialized original payload.</summary>
    public string? OriginalTextsAsText { get; set; }

    /// <summary>Gets or sets the original source language.</summary>
    public string? OriginalLang { get; set; }

    /// <summary>Gets or sets the serialized translated payload.</summary>
    public string? TranslatedTextsAsText { get; set; }

    /// <summary>Gets or sets the target translation language.</summary>
    public string? TranslationLang { get; set; }

    /// <summary>Gets or sets the translation engine id.</summary>
    public int? TranslationEngine { get; set; }

    /// <summary>Gets or sets the stored game version.</summary>
    public string? GameVersion { get; set; }

    /// <summary>Gets or sets the source payload hash.</summary>
    public string? SourceContentHash { get; set; }

    /// <summary>Gets or sets the created timestamp.</summary>
    public DateTime? CreatedDate { get; set; }

    /// <summary>Gets or sets the updated timestamp.</summary>
    public DateTime? UpdatedDate { get; set; }

    /// <summary>Gets or sets the optimistic concurrency token.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
