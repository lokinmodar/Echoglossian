// <copyright file="CraftActionText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one canonical DB-first CraftAction payload.
/// </summary>
[Table("craftactiontexts")]
public sealed class CraftActionText : ReferenceTextRowBase
{
    /// <summary>
    ///     Gets or sets the CraftAction row identifier.
    /// </summary>
    [NotMapped]
    public uint CraftActionId
    {
        get => this.ReferenceId;
        set => this.ReferenceId = value;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return
            $"CraftActionId={this.CraftActionId}, OriginalName={this.OriginalName}, TranslationLang={this.TranslationLang}, TranslationEngine={this.TranslationEngine}, GameVersion={this.GameVersion}, SourceContentHash={this.SourceContentHash}";
    }
}
