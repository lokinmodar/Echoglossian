// <copyright file="PetActionText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one canonical DB-first PetAction payload.
/// </summary>
[Table("petactiontexts")]
public sealed class PetActionText : ReferenceTextRowBase
{
    /// <summary>
    ///     Gets or sets the PetAction row identifier.
    /// </summary>
    [NotMapped]
    public uint PetActionId
    {
        get => this.ReferenceId;
        set => this.ReferenceId = value;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return
            $"PetActionId={this.PetActionId}, OriginalName={this.OriginalName}, TranslationLang={this.TranslationLang}, TranslationEngine={this.TranslationEngine}, GameVersion={this.GameVersion}, SourceContentHash={this.SourceContentHash}";
    }
}
