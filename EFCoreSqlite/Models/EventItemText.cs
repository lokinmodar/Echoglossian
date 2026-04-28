// <copyright file="EventItemText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one canonical DB-first EventItem payload.
/// </summary>
[Table("eventitemtexts")]
public sealed class EventItemText : ReferenceTextRowBase
{
    /// <summary>
    ///     Gets or sets the EventItem row identifier.
    /// </summary>
    [NotMapped]
    public uint EventItemId
    {
        get => this.ReferenceId;
        set => this.ReferenceId = value;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return
            $"EventItemId={this.EventItemId}, OriginalName={this.OriginalName}, TranslationLang={this.TranslationLang}, TranslationEngine={this.TranslationEngine}, GameVersion={this.GameVersion}, SourceContentHash={this.SourceContentHash}";
    }
}
