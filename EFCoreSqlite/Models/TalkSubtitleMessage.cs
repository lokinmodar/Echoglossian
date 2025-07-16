// <copyright file="TalkSubtitleMessage.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

[Table("talksubtitlemessages")]
public partial class TalkSubtitleMessage
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TalkSubtitleMessage" /> class.
    /// </summary>
    /// <param name="originalTalkSubtitleMessage">The original talk subtitle message.</param>
    /// <param name="originalTalkSubtitleMessageLang">
    ///     The language of the original talk
    ///     subtitle message.
    /// </param>
    /// <param name="translatedTalkSubtitleMessage">
    ///     The translated talk subtitle
    ///     message.
    /// </param>
    /// <param name="translationLang">
    ///     The language of the translated talk subtitle
    ///     message.
    /// </param>
    /// <param name="translationEngine">The translation engine used.</param>
    /// <param name="createdDate">The date the message was created.</param>
    /// <param name="updatedDate">The date the message was last updated.</param>
    public TalkSubtitleMessage(
        string? originalTalkSubtitleMessage,
        string? originalTalkSubtitleMessageLang,
        string? translatedTalkSubtitleMessage,
        string? translationLang,
        int? translationEngine,
        DateTime? createdDate,
        DateTime? updatedDate)
    {
        this.OriginalTalkSubtitleMessage = originalTalkSubtitleMessage;
        this.OriginalTalkSubtitleMessageLang = originalTalkSubtitleMessageLang;
        this.TranslatedTalkSubtitleMessage = translatedTalkSubtitleMessage;
        this.TranslationLang = translationLang;
        this.TranslationEngine = translationEngine;
        this.CreatedDate = createdDate;
        this.UpdatedDate = updatedDate;
    }

    [Key] public int Id { get; set; }

    public string? OriginalTalkSubtitleMessage { get; set; }

    public string? OriginalTalkSubtitleMessageLang { get; set; }

    public string? TranslatedTalkSubtitleMessage { get; set; }

    public string? TranslationLang { get; set; }

    public int? TranslationEngine { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [Timestamp] public byte[]? RowVersion { get; set; }

    public override string? ToString()
    {
        return
            $"Id: {this.Id}, OriginalMsg: {this.OriginalTalkSubtitleMessage}, OriginalLang: {this.OriginalTalkSubtitleMessageLang}, TranslMsg: {this.TranslatedTalkSubtitleMessage}, TransLang: {this.TranslationLang}, TranEngine: {this.TranslationEngine}, CreatedAt: {this.CreatedDate}, UpdatedAt: {this.UpdatedDate}";
    }
}