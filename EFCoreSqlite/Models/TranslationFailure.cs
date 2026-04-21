// <copyright file="TranslationFailure.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents one exact translation request that previously returned no
///     usable translated text for a specific source/target language pair and
///     translation engine.
/// </summary>
[Table("translationfailures")]
public class TranslationFailure
{
    /// <summary>
    ///     Gets or sets the primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the exact sanitized text that was sent to the
    ///     translation engine.
    /// </summary>
    public string? SourceText { get; set; }

    /// <summary>
    ///     Gets or sets the stable hash of <see cref="SourceText" /> used for
    ///     lookup and indexing.
    /// </summary>
    public string? SourceTextHash { get; set; }

    /// <summary>
    ///     Gets or sets the normalized source language code.
    /// </summary>
    public string? SourceLanguage { get; set; }

    /// <summary>
    ///     Gets or sets the normalized target language code.
    /// </summary>
    public string? TargetLanguage { get; set; }

    /// <summary>
    ///     Gets or sets the translation-engine identifier.
    /// </summary>
    public int TranslationEngine { get; set; }

    /// <summary>
    ///     Gets or sets the last recorded failure reason.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    ///     Gets or sets the origin context that first produced this exact
    ///     failed translation request.
    /// </summary>
    public string? FirstSeenOrigin { get; set; }

    /// <summary>
    ///     Gets or sets the most recent origin context that produced this exact
    ///     failed translation request.
    /// </summary>
    public string? LastSeenOrigin { get; set; }

    /// <summary>
    ///     Gets or sets how many times this exact request failed.
    /// </summary>
    public int FailureCount { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the row creation time in UTC.
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets the last update time in UTC.
    /// </summary>
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets the optimistic-concurrency token.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        return
            $"SourceTextHash={this.SourceTextHash}, SourceLanguage={this.SourceLanguage}, TargetLanguage={this.TargetLanguage}, TranslationEngine={this.TranslationEngine}, FailureReason={this.FailureReason}, FirstSeenOrigin={this.FirstSeenOrigin}, LastSeenOrigin={this.LastSeenOrigin}, FailureCount={this.FailureCount}";
    }
}
