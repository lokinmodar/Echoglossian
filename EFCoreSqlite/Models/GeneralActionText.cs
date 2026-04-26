// <copyright file="GeneralActionText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one canonical DB-first GeneralAction payload.
/// </summary>
[Table("generalactiontexts")]
public sealed class GeneralActionText : ReferenceTextRowBase
{
    /// <summary>
    ///     Gets or sets the GeneralAction row identifier.
    /// </summary>
    [NotMapped]
    public uint GeneralActionId
    {
        get => this.ReferenceId;
        set => this.ReferenceId = value;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return
            $"GeneralActionId={this.GeneralActionId}, OriginalName={this.OriginalName}, TranslationLang={this.TranslationLang}, TranslationEngine={this.TranslationEngine}, GameVersion={this.GameVersion}, SourceContentHash={this.SourceContentHash}";
    }
}
