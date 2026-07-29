// <copyright file="QuestPopupText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models.Journal;

/// <summary>
///     Stores dedicated translated quest-popup text for surfaces that cannot
///     yet be reconciled safely into canonical <see cref="QuestPlate" /> rows.
/// </summary>
[Table("questpopuptexts")]
public sealed class QuestPopupText : IGenericEntity
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="QuestPopupText" />
    ///     class.
    /// </summary>
    public QuestPopupText()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="QuestPopupText" />
    ///     class.
    /// </summary>
    /// <param name="surfaceName">The popup surface name.</param>
    /// <param name="questId">The optional canonical quest id.</param>
    /// <param name="originalTitle">The original popup title.</param>
    /// <param name="originalBody">The original popup body.</param>
    /// <param name="originalLang">The original source language.</param>
    /// <param name="translatedTitle">The translated popup title.</param>
    /// <param name="translatedBody">The translated popup body.</param>
    /// <param name="translationLang">The target translation language.</param>
    /// <param name="translationEngine">The translation engine id.</param>
    /// <param name="gameVersion">The stored game version.</param>
    /// <param name="sourceContentHash">The optional source content hash.</param>
    /// <param name="createdDate">The created timestamp.</param>
    /// <param name="updatedDate">The updated timestamp.</param>
    public QuestPopupText(
        string? surfaceName,
        string? questId,
        string? originalTitle,
        string? originalBody,
        string? originalLang,
        string? translatedTitle,
        string? translatedBody,
        string? translationLang,
        int? translationEngine,
        string? gameVersion,
        string? sourceContentHash,
        DateTime? createdDate,
        DateTime? updatedDate)
    {
        this.SurfaceName = surfaceName;
        this.QuestId = questId;
        this.OriginalTitle = originalTitle;
        this.OriginalBody = originalBody;
        this.OriginalLang = originalLang;
        this.TranslatedTitle = translatedTitle;
        this.TranslatedBody = translatedBody;
        this.TranslationLang = translationLang;
        this.TranslationEngine = translationEngine;
        this.GameVersion = gameVersion;
        this.SourceContentHash = sourceContentHash;
        this.CreatedDate = createdDate;
        this.UpdatedDate = updatedDate;
    }

    /// <summary>
    ///     Gets or sets the primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the popup surface name.
    /// </summary>
    public string? SurfaceName { get; set; }

    /// <summary>
    ///     Gets or sets the optional canonical quest id.
    /// </summary>
    public string? QuestId { get; set; }

    /// <summary>
    ///     Gets or sets the original popup title.
    /// </summary>
    public string? OriginalTitle { get; set; }

    /// <summary>
    ///     Gets or sets the original popup body.
    /// </summary>
    public string? OriginalBody { get; set; }

    /// <summary>
    ///     Gets or sets the original source language.
    /// </summary>
    public string? OriginalLang { get; set; }

    /// <summary>
    ///     Gets or sets the translated popup title.
    /// </summary>
    public string? TranslatedTitle { get; set; }

    /// <summary>
    ///     Gets or sets the translated popup body.
    /// </summary>
    public string? TranslatedBody { get; set; }

    /// <summary>
    ///     Gets or sets the target translation language.
    /// </summary>
    public string? TranslationLang { get; set; }

    /// <summary>
    ///     Gets or sets the translation engine id.
    /// </summary>
    public int? TranslationEngine { get; set; }

    /// <summary>
    ///     Gets or sets the stored game version.
    /// </summary>
    public string? GameVersion { get; set; }

    /// <summary>
    ///     Gets or sets the optional source content hash.
    /// </summary>
    public string? SourceContentHash { get; set; }

    /// <summary>
    ///     Gets or sets the created timestamp.
    /// </summary>
    public DateTime? CreatedDate { get; set; }

    /// <summary>
    ///     Gets or sets the updated timestamp.
    /// </summary>
    public DateTime? UpdatedDate { get; set; }

    /// <summary>
    ///     Gets or sets the optimistic concurrency token.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    /// <inheritdoc />
    public string GetOriginalText()
    {
        return $"{this.OriginalTitle}\n{this.OriginalBody}".Trim();
    }

    /// <inheritdoc />
    public void SetOriginalText(string original)
    {
        this.OriginalBody = original;
    }

    /// <inheritdoc />
    public string GetOriginalLang()
    {
        return this.OriginalLang ?? string.Empty;
    }

    /// <inheritdoc />
    public void SetOriginalLang(string lang)
    {
        this.OriginalLang = lang;
    }

    /// <inheritdoc />
    public string? GetTranslatedText()
    {
        return $"{this.TranslatedTitle}\n{this.TranslatedBody}".Trim();
    }

    /// <inheritdoc />
    public void SetTranslatedText(string translated)
    {
        this.TranslatedBody = translated;
    }

    /// <inheritdoc />
    public string? GetTranslationLang()
    {
        return this.TranslationLang;
    }

    /// <inheritdoc />
    public void SetTranslationLang(string lang)
    {
        this.TranslationLang = lang;
    }

    /// <inheritdoc />
    public int? GetTranslationEngine()
    {
        return this.TranslationEngine;
    }

    /// <inheritdoc />
    public void SetTranslationEngine(int engine)
    {
        this.TranslationEngine = engine;
    }

    /// <inheritdoc />
    public string GetEntityKey()
    {
        return $"{this.SurfaceName}|{this.QuestId}|{this.OriginalTitle}|{this.OriginalBody}";
    }

    /// <inheritdoc />
    public void SetEntityKey(string key)
    {
        this.SurfaceName = key;
    }

    /// <inheritdoc />
    public string? GetGameVersion()
    {
        return this.GameVersion;
    }

    /// <inheritdoc />
    public void SetGameVersion(string version)
    {
        this.GameVersion = version;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return
            $"Id: {this.Id}, Surface: {this.SurfaceName}, QuestId: {this.QuestId}, OriginalTitle: {this.OriginalTitle}, OriginalBody: {this.OriginalBody}, OriginalLang: {this.OriginalLang}, TranslatedTitle: {this.TranslatedTitle}, TranslatedBody: {this.TranslatedBody}, TranslationLang: {this.TranslationLang}, TranslationEngine: {this.TranslationEngine}, GameVersion: {this.GameVersion}, SourceContentHash: {this.SourceContentHash}, CreatedDate: {this.CreatedDate}, UpdatedDate: {this.UpdatedDate}";
    }
}
