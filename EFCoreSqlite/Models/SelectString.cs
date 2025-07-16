// <copyright file="SelectString.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents a select string in the database.
/// </summary>
[Table("selectstrings")]
public partial class SelectString
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SelectString" /> class.
    /// </summary>
    /// <param name="originalSelectString">The original select string.</param>
    /// <param name="originalSelectStringLang">
    ///     The language of the original select
    ///     string.
    /// </param>
    /// <param name="translatedSelectString">The translated select string.</param>
    /// <param name="translationLang">The language of the translated select string.</param>
    /// <param name="translationEngine">The translation engine used.</param>
    /// <param name="createdDate">The date the record was created.</param>
    /// <param name="updatedDate">The date the record was last updated.</param>
    public SelectString(
        string? originalSelectString,
        string? originalSelectStringLang,
        string? translatedSelectString,
        string? translationLang,
        int? translationEngine,
        DateTime? createdDate,
        DateTime? updatedDate)
    {
        this.OriginalSelectString = originalSelectString;
        this.OriginalSelectStringLang = originalSelectStringLang;
        this.TranslatedSelectString = translatedSelectString;
        this.TranslationLang = translationLang;
        this.TranslationEngine = translationEngine;
        this.CreatedDate = createdDate;
        this.UpdatedDate = updatedDate;
    }

    [Key] public int Id { get; set; }

    public string? OriginalSelectString { get; set; }

    public string? OriginalSelectStringLang { get; set; }

    public string? TranslatedSelectString { get; set; }

    public string? TranslationLang { get; set; }

    public int? TranslationEngine { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [Timestamp] public byte[]? RowVersion { get; set; }

    public override string? ToString()
    {
        return
            $"Id: {this.Id}, OriginalSelectString: {this.OriginalSelectString}, OriginalSelectStringLang: {this.OriginalSelectStringLang}, TranslatedSelectString: {this.TranslatedSelectString}, TranslationLang: {this.TranslationLang}, TranslationEngine: {this.TranslationEngine}, CreatedDate: {this.CreatedDate}, UpdatedDate: {this.UpdatedDate}";
    }
}