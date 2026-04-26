// <copyright file="PvPActionText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one canonical DB-first PvPAction payload.
/// </summary>
[Table("pvpactiontexts")]
public sealed class PvPActionText : ReferenceTextRowBase
{
    /// <summary>
    ///     Gets or sets the PvPAction row identifier.
    /// </summary>
    [NotMapped]
    public uint PvPActionId
    {
        get => this.ReferenceId;
        set => this.ReferenceId = value;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return
            $"PvPActionId={this.PvPActionId}, OriginalName={this.OriginalName}, TranslationLang={this.TranslationLang}, TranslationEngine={this.TranslationEngine}, GameVersion={this.GameVersion}, SourceContentHash={this.SourceContentHash}";
    }
}
