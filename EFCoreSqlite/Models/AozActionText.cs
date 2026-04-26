// <copyright file="AozActionText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one canonical DB-first AozAction payload.
/// </summary>
[Table("aozactiontexts")]
public sealed class AozActionText : ReferenceTextRowBase
{
    /// <summary>
    ///     Gets or sets the AozAction row identifier.
    /// </summary>
    [NotMapped]
    public uint AozActionId
    {
        get => this.ReferenceId;
        set => this.ReferenceId = value;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return
            $"AozActionId={this.AozActionId}, OriginalName={this.OriginalName}, TranslationLang={this.TranslationLang}, TranslationEngine={this.TranslationEngine}, GameVersion={this.GameVersion}, SourceContentHash={this.SourceContentHash}";
    }
}
