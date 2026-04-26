// <copyright file="Trait.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one canonical DB-first trait tooltip payload.
/// </summary>
[Table("Traits")]
public class Trait
{
    /// <summary>
    ///     Gets or sets the primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the trait row identifier.
    /// </summary>
    public uint TraitId { get; set; }

    /// <summary>
    ///     Gets or sets the trait icon identifier.
    /// </summary>
    public uint IconId { get; set; }

    /// <summary>
    ///     Gets or sets the active class-job identifier used to scope the trait.
    /// </summary>
    public uint ClassJobId { get; set; }

    /// <summary>
    ///     Gets or sets the class-job-category row identifier.
    /// </summary>
    public uint ClassJobCategoryId { get; set; }

    /// <summary>
    ///     Gets or sets the canonical original trait name.
    /// </summary>
    public string? TraitName { get; set; }

    /// <summary>
    ///     Gets or sets the canonical original trait description.
    /// </summary>
    public string? TraitDescription { get; set; }

    /// <summary>
    ///     Gets or sets the fully assembled original tooltip text.
    /// </summary>
    public string? OriginalTooltipText { get; set; }

    /// <summary>
    ///     Gets or sets the language of the original source payload.
    /// </summary>
    public string? OriginalLang { get; set; }

    /// <summary>
    ///     Gets or sets the translated trait name.
    /// </summary>
    public string? TranslatedTraitName { get; set; }

    /// <summary>
    ///     Gets or sets the translated trait description.
    /// </summary>
    public string? TranslatedTraitDescription { get; set; }

    /// <summary>
    ///     Gets or sets the fully assembled translated tooltip text.
    /// </summary>
    public string? TranslatedTooltipText { get; set; }

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
    ///     Gets or sets the serialized canonical payload, including translated fields when available.
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

    /// <inheritdoc />
    public override string ToString()
    {
        return
            $"TraitId={this.TraitId}, TraitName={this.TraitName}, TranslationLang={this.TranslationLang}, TranslationEngine={this.TranslationEngine}, GameVersion={this.GameVersion}, SourceContentHash={this.SourceContentHash}";
    }
}
