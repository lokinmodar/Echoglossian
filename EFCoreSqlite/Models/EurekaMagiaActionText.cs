// <copyright file="EurekaMagiaActionText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one canonical DB-first EurekaMagiaAction payload.
/// </summary>
[Table("eurekamagiaactiontexts")]
public sealed class EurekaMagiaActionText : ReferenceTextRowBase
{
    /// <summary>
    ///     Gets or sets the EurekaMagiaAction row identifier.
    /// </summary>
    [NotMapped]
    public uint EurekaMagiaActionId
    {
        get => this.ReferenceId;
        set => this.ReferenceId = value;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return
            $"EurekaMagiaActionId={this.EurekaMagiaActionId}, OriginalName={this.OriginalName}, TranslationLang={this.TranslationLang}, TranslationEngine={this.TranslationEngine}, GameVersion={this.GameVersion}, SourceContentHash={this.SourceContentHash}";
    }
}
