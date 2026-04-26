// <copyright file="BuddyActionText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one canonical DB-first BuddyAction payload.
/// </summary>
[Table("buddyactiontexts")]
public sealed class BuddyActionText : ReferenceTextRowBase
{
    /// <summary>
    ///     Gets or sets the BuddyAction row identifier.
    /// </summary>
    [NotMapped]
    public uint BuddyActionId
    {
        get => this.ReferenceId;
        set => this.ReferenceId = value;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return
            $"BuddyActionId={this.BuddyActionId}, OriginalName={this.OriginalName}, TranslationLang={this.TranslationLang}, TranslationEngine={this.TranslationEngine}, GameVersion={this.GameVersion}, SourceContentHash={this.SourceContentHash}";
    }
}
