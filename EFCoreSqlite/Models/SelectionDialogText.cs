// <copyright file="SelectionDialogText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Stores generic translated selection-dialog payloads for addon surfaces
///     that are not safely representable through the cutscene-specific
///     <see cref="SelectString" /> table.
/// </summary>
[Table("selectiondialogtexts")]
public sealed class SelectionDialogText
{
    [NotMapped]
    private List<string>? originalTexts;

    [NotMapped]
    private List<string>? translatedTexts;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SelectionDialogText" />
    ///     class.
    /// </summary>
    public SelectionDialogText()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SelectionDialogText" />
    ///     class.
    /// </summary>
    /// <param name="addonName">The addon name.</param>
    /// <param name="originalTextsAsText">The serialized original payload.</param>
    /// <param name="originalLang">The source language.</param>
    /// <param name="translatedTextsAsText">The serialized translated payload.</param>
    /// <param name="translationLang">The target language.</param>
    /// <param name="translationEngine">The translation engine id.</param>
    /// <param name="gameVersion">The stored game version.</param>
    /// <param name="sourceContentHash">The optional source content hash.</param>
    /// <param name="createdDate">The created timestamp.</param>
    /// <param name="updatedDate">The updated timestamp.</param>
    public SelectionDialogText(
        string? addonName,
        string? originalTextsAsText,
        string? originalLang,
        string? translatedTextsAsText,
        string? translationLang,
        int? translationEngine,
        string? gameVersion,
        string? sourceContentHash,
        DateTime? createdDate,
        DateTime? updatedDate)
    {
        this.AddonName = addonName;
        this.OriginalTextsAsText = originalTextsAsText;
        this.OriginalLang = originalLang;
        this.TranslatedTextsAsText = translatedTextsAsText;
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
    ///     Gets or sets the addon name.
    /// </summary>
    public string? AddonName { get; set; }

    /// <summary>
    ///     Gets or sets the serialized original payload.
    /// </summary>
    public string? OriginalTextsAsText { get; set; }

    /// <summary>
    ///     Gets or sets the original source language.
    /// </summary>
    public string? OriginalLang { get; set; }

    /// <summary>
    ///     Gets or sets the serialized translated payload.
    /// </summary>
    public string? TranslatedTextsAsText { get; set; }

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

    /// <summary>
    ///     Gets or sets the original payload as a materialized list.
    /// </summary>
    [NotMapped]
    public List<string> OriginalTexts
    {
        get
        {
            return this.originalTexts ??=
                !string.IsNullOrEmpty(this.OriginalTextsAsText)
                    ? JsonConvert.DeserializeObject<List<string>>(
                          this.OriginalTextsAsText) ??
                      []
                    : [];
        }

        init => this.originalTexts = value;
    }

    /// <summary>
    ///     Gets or sets the translated payload as a materialized list.
    /// </summary>
    [NotMapped]
    public List<string> TranslatedTexts
    {
        get
        {
            return this.translatedTexts ??=
                !string.IsNullOrEmpty(this.TranslatedTextsAsText)
                    ? JsonConvert.DeserializeObject<List<string>>(
                          this.TranslatedTextsAsText) ??
                      []
                    : [];
        }

        init => this.translatedTexts = value;
    }

    /// <summary>
    ///     Updates the serialized payload fields from the current in-memory
    ///     lists.
    /// </summary>
    /// <param name="prettyPrint">Whether JSON should be indented.</param>
    public void UpdateFieldsAsText(bool prettyPrint = false)
    {
        this.OriginalTextsAsText = string.Empty;
        this.TranslatedTextsAsText = string.Empty;

        if (this.originalTexts != null && this.originalTexts.Count != 0)
        {
            this.OriginalTextsAsText = JsonConvert.SerializeObject(
                this.originalTexts,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.translatedTexts != null && this.translatedTexts.Count != 0)
        {
            this.TranslatedTextsAsText = JsonConvert.SerializeObject(
                this.translatedTexts,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }
    }

    /// <summary>
    ///     Forces reloading the materialized lists from the stored JSON fields.
    /// </summary>
    public void UpdateFieldsFromText()
    {
        this.originalTexts = !string.IsNullOrEmpty(this.OriginalTextsAsText)
            ? JsonConvert.DeserializeObject<List<string>>(
                this.OriginalTextsAsText) ?? []
            : [];
        this.translatedTexts = !string.IsNullOrEmpty(this.TranslatedTextsAsText)
            ? JsonConvert.DeserializeObject<List<string>>(
                this.TranslatedTextsAsText) ?? []
            : [];
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return
            $"Id: {this.Id}, AddonName: {this.AddonName}, OriginalTexts: {this.OriginalTextsAsText}, OriginalLang: {this.OriginalLang}, TranslatedTexts: {this.TranslatedTextsAsText}, TranslationLang: {this.TranslationLang}, TranslationEngine: {this.TranslationEngine}, GameVersion: {this.GameVersion}, SourceContentHash: {this.SourceContentHash}, CreatedDate: {this.CreatedDate}, UpdatedDate: {this.UpdatedDate}";
    }
}
