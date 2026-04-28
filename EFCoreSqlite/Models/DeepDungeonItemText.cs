// <copyright file="DeepDungeonItemText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one canonical DB-first DeepDungeonItem payload.
/// </summary>
[Table("deepdungeonitemtexts")]
public sealed class DeepDungeonItemText : ReferenceTextRowBase
{
    /// <summary>
    ///     Gets or sets the DeepDungeonItem row identifier.
    /// </summary>
    [NotMapped]
    public uint DeepDungeonItemId
    {
        get => this.ReferenceId;
        set => this.ReferenceId = value;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return
            $"DeepDungeonItemId={this.DeepDungeonItemId}, OriginalName={this.OriginalName}, TranslationLang={this.TranslationLang}, TranslationEngine={this.TranslationEngine}, GameVersion={this.GameVersion}, SourceContentHash={this.SourceContentHash}";
    }
}
