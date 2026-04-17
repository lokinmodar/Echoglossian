// <copyright file="ActionTooltip.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one canonical DB-first action tooltip payload.
/// </summary>
[Table("actiontooltips")]
public class ActionTooltip
{
    /// <summary>
    ///     Gets or sets the primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the action row identifier.
    /// </summary>
    public uint ActionId { get; set; }

    /// <summary>
    ///     Gets or sets the action icon identifier.
    /// </summary>
    public uint IconId { get; set; }

    /// <summary>
    ///     Gets or sets the action-category row identifier.
    /// </summary>
    public uint ActionCategoryId { get; set; }

    /// <summary>
    ///     Gets or sets the owning class-job row identifier.
    /// </summary>
    public uint ClassJobId { get; set; }

    /// <summary>
    ///     Gets or sets the class-job-category row identifier.
    /// </summary>
    public uint ClassJobCategoryId { get; set; }

    /// <summary>
    ///     Gets or sets the canonical original action name.
    /// </summary>
    public string? ActionName { get; set; }

    /// <summary>
    ///     Gets or sets the canonical original action description.
    /// </summary>
    public string? ActionDescription { get; set; }

    /// <summary>
    ///     Gets or sets the fully assembled original tooltip text.
    /// </summary>
    public string? OriginalTooltipText { get; set; }

    /// <summary>
    ///     Gets or sets the language of the original source payload.
    /// </summary>
    public string? OriginalLang { get; set; }

    /// <summary>
    ///     Gets or sets the translated action name.
    /// </summary>
    public string? TranslatedActionName { get; set; }

    /// <summary>
    ///     Gets or sets the translated action description.
    /// </summary>
    public string? TranslatedActionDescription { get; set; }

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
            $"ActionId={this.ActionId}, ActionName={this.ActionName}, TranslationLang={this.TranslationLang}, TranslationEngine={this.TranslationEngine}, GameVersion={this.GameVersion}, SourceContentHash={this.SourceContentHash}";
    }
}
