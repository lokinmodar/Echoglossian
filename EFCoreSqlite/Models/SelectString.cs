// <copyright file="SelectString.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents a cutscene select-string dialog in the database.
/// </summary>
[Table("selectstrings")]
public partial class SelectString
{
    [NotMapped] private List<string>? originalOptions;

    [NotMapped] private List<string>? translatedOptions;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SelectString" /> class.
    /// </summary>
    /// <param name="originalSelectString">The original question/title.</param>
    /// <param name="originalSelectStringLang">
    ///     The language of the original question/title.
    /// </param>
    /// <param name="originalOptionsAsText">The original option list serialized as JSON.</param>
    /// <param name="translatedSelectString">The translated question/title.</param>
    /// <param name="translatedOptionsAsText">The translated option list serialized as JSON.</param>
    /// <param name="translationLang">The language of the translated dialog.</param>
    /// <param name="translationEngine">The translation engine used.</param>
    /// <param name="createdDate">The date the record was created.</param>
    /// <param name="updatedDate">The date the record was last updated.</param>
    public SelectString(
        string? originalSelectString,
        string? originalSelectStringLang,
        string? originalOptionsAsText,
        string? translatedSelectString,
        string? translatedOptionsAsText,
        string? translationLang,
        int? translationEngine,
        DateTime? createdDate,
        DateTime? updatedDate)
    {
        this.OriginalSelectString = originalSelectString;
        this.OriginalSelectStringLang = originalSelectStringLang;
        this.OriginalOptionsAsText = originalOptionsAsText;
        this.TranslatedSelectString = translatedSelectString;
        this.TranslatedOptionsAsText = translatedOptionsAsText;
        this.TranslationLang = translationLang;
        this.TranslationEngine = translationEngine;
        this.CreatedDate = createdDate;
        this.UpdatedDate = updatedDate;
    }

    [Key] public int Id { get; set; }

    public string? OriginalSelectString { get; set; }

    public string? OriginalSelectStringLang { get; set; }

    public string? OriginalOptionsAsText { get; set; }

    public string? TranslatedSelectString { get; set; }

    public string? TranslatedOptionsAsText { get; set; }

    public string? TranslationLang { get; set; }

    public int? TranslationEngine { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [Timestamp] public byte[]? RowVersion { get; set; }

    /// <summary>
    ///     Gets or sets the original options as a list of strings.
    /// </summary>
    [NotMapped]
    public List<string> OriginalOptions
    {
        get
        {
            return this.originalOptions ??=
                !string.IsNullOrEmpty(this.OriginalOptionsAsText)
                    ? JsonConvert.DeserializeObject<List<string>>(
                          this.OriginalOptionsAsText) ??
                      new List<string>()
                    : new List<string>();
        }

        init => this.originalOptions = value;
    }

    /// <summary>
    ///     Gets or sets the translated options as a list of strings.
    /// </summary>
    [NotMapped]
    public List<string> TranslatedOptions
    {
        get
        {
            return this.translatedOptions ??=
                !string.IsNullOrEmpty(this.TranslatedOptionsAsText)
                    ? JsonConvert.DeserializeObject<List<string>>(
                          this.TranslatedOptionsAsText) ??
                      new List<string>()
                    : new List<string>();
        }

        init => this.translatedOptions = value;
    }

    /// <summary>
    ///     Updates the serialized text fields from the current option lists.
    /// </summary>
    /// <param name="prettyPrint">Should the JSON be pretty printed?.</param>
    public void UpdateFieldsAsText(bool prettyPrint = false)
    {
        this.OriginalOptionsAsText = string.Empty;
        this.TranslatedOptionsAsText = string.Empty;

        if (this.originalOptions != null && this.originalOptions.Count != 0)
        {
            this.OriginalOptionsAsText = JsonConvert.SerializeObject(
                this.originalOptions,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }

        if (this.translatedOptions != null && this.translatedOptions.Count != 0)
        {
            this.TranslatedOptionsAsText = JsonConvert.SerializeObject(
                this.translatedOptions,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }
    }

    /// <summary>
    ///     Forces loading the option lists from the stored text fields.
    /// </summary>
    public void UpdateFieldsFromText()
    {
        this.originalOptions = !string.IsNullOrEmpty(this.OriginalOptionsAsText)
            ? JsonConvert.DeserializeObject<List<string>>(
                this.OriginalOptionsAsText) ?? new List<string>()
            : new List<string>();

        this.translatedOptions = !string.IsNullOrEmpty(this.TranslatedOptionsAsText)
            ? JsonConvert.DeserializeObject<List<string>>(
                this.TranslatedOptionsAsText) ?? new List<string>()
            : new List<string>();
    }

    public override string? ToString()
    {
        return
            $"Id: {this.Id}, OriginalSelectString: {this.OriginalSelectString}, OriginalSelectStringLang: {this.OriginalSelectStringLang}, OriginalOptions: {this.OriginalOptionsAsText}, TranslatedSelectString: {this.TranslatedSelectString}, TranslatedOptions: {this.TranslatedOptionsAsText}, TranslationLang: {this.TranslationLang}, TranslationEngine: {this.TranslationEngine}, CreatedDate: {this.CreatedDate}, UpdatedDate: {this.UpdatedDate}";
    }
}
