// <copyright file="MainCommandText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one canonical DB-first MainCommand sheet payload.
/// </summary>
[Table("maincommandtexts")]
public sealed class MainCommandText : ReferenceTextRowBase
{
    /// <summary>
    ///     Gets or sets the MainCommand row identifier.
    /// </summary>
    [NotMapped]
    public uint MainCommandId
    {
        get => this.ReferenceId;
        set => this.ReferenceId = value;
    }

    /// <summary>
    ///     Gets or sets the visible icon identifier from the sheet.
    /// </summary>
    public uint? IconId { get; set; }

    /// <summary>
    ///     Gets or sets the sheet category value.
    /// </summary>
    public uint? CategoryId { get; set; }

    /// <summary>
    ///     Gets or sets the linked MainCommandCategory row identifier.
    /// </summary>
    public uint? MainCommandCategoryId { get; set; }

    /// <summary>
    ///     Gets or sets the MainCommand sheet's <c>Unknown0</c> value.
    /// </summary>
    public uint? Unknown0 { get; set; }

    /// <summary>
    ///     Gets or sets the sheet sort identifier.
    /// </summary>
    public uint? SortId { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        return
            $"MainCommandId={this.MainCommandId}, OriginalName={this.OriginalName}, TranslationLang={this.TranslationLang}, TranslationEngine={this.TranslationEngine}, GameVersion={this.GameVersion}, SourceContentHash={this.SourceContentHash}, CategoryId={this.CategoryId}, MainCommandCategoryId={this.MainCommandCategoryId}, SortId={this.SortId}";
    }
}
