// <copyright file="BgcArmyActionText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one canonical DB-first BgcArmyAction payload.
/// </summary>
[Table("bgcarmyactiontexts")]
public sealed class BgcArmyActionText : ReferenceTextRowBase
{
    /// <summary>
    ///     Gets or sets the BgcArmyAction row identifier.
    /// </summary>
    [NotMapped]
    public uint BgcArmyActionId
    {
        get => this.ReferenceId;
        set => this.ReferenceId = value;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return
            $"BgcArmyActionId={this.BgcArmyActionId}, OriginalName={this.OriginalName}, TranslationLang={this.TranslationLang}, TranslationEngine={this.TranslationEngine}, GameVersion={this.GameVersion}, SourceContentHash={this.SourceContentHash}";
    }
}
