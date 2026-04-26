// <copyright file="CompanyActionText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one canonical DB-first CompanyAction payload.
/// </summary>
[Table("companyactiontexts")]
public sealed class CompanyActionText : ReferenceTextRowBase
{
    /// <summary>
    ///     Gets or sets the CompanyAction row identifier.
    /// </summary>
    [NotMapped]
    public uint CompanyActionId
    {
        get => this.ReferenceId;
        set => this.ReferenceId = value;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return
            $"CompanyActionId={this.CompanyActionId}, OriginalName={this.OriginalName}, TranslationLang={this.TranslationLang}, TranslationEngine={this.TranslationEngine}, GameVersion={this.GameVersion}, SourceContentHash={this.SourceContentHash}";
    }
}
