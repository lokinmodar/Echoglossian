// <copyright file="QuestDialogueMetadata.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models.Journal;

/// <summary>
///     Stores derived speaker and addressee metadata for one exact quest
///     dialogue source row.
/// </summary>
[Table("questdialoguemetadata")]
public sealed class QuestDialogueMetadata
{
    /// <summary>
    ///     Gets or sets the primary key.
    /// </summary>
    [Key]
    public long Id { get; set; }

    /// <summary>
    ///     Gets or sets the canonical quest id.
    /// </summary>
    public uint QuestId { get; set; }

    /// <summary>
    ///     Gets or sets the quest sequence containing the source row.
    /// </summary>
    public ushort QuestSequence { get; set; }

    /// <summary>
    ///     Gets or sets the source language code.
    /// </summary>
    public string SourceLanguageCode { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the game version that supplied the source row.
    /// </summary>
    public string GameVersion { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the quest sheet id.
    /// </summary>
    public string QuestSheetId { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the quest text sheet name.
    /// </summary>
    public string QuestTextSheetName { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the exact quest text row key.
    /// </summary>
    public string SourceRowKey { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the hash of the exact source text.
    /// </summary>
    public string SourceTextHash { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets a diagnostic preview of the source text.
    /// </summary>
    public string SourceTextPreview { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the derived speaker hint.
    /// </summary>
    public string SpeakerHint { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the derived addressee hint.
    /// </summary>
    public string AddresseeHint { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the derived speaker role hint.
    /// </summary>
    public string SpeakerRoleHint { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the derived addressee role hint.
    /// </summary>
    public string AddresseeRoleHint { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the metadata derivation provenance.
    /// </summary>
    public string Provenance { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the confidence tier assigned by derivation.
    /// </summary>
    public int ConfidenceTier { get; set; }

    /// <summary>
    ///     Gets or sets the version of the metadata derivation algorithm.
    /// </summary>
    public string DerivationVersion { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the UTC timestamp at which the row was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    ///     Gets or sets the UTC timestamp at which the row was last updated.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }
}
