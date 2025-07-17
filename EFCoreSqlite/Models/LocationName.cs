// <copyright file="LocationName.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

[Table("locationnames")]
public class LocationName
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="LocationName" /> class.
    /// </summary>
    /// <param name="id">The identifier of the location name.</param>
    /// <param name="originalLocationName">The original name of the location.</param>
    /// <param name="originalLocationNameLang">
    ///     The language of the original location
    ///     name.
    /// </param>
    /// <param name="translatedLocationName">The translated name of the location.</param>
    /// <param name="translationLang">The language of the translated location name.</param>
    /// <param name="translationEngine">The translation engine used.</param>
    /// <param name="createdDate">The date the location name was created.</param>
    /// <param name="updatedDate">The date the location name was last updated.</param>
    public LocationName(
        int id,
        string? originalLocationName,
        string? originalLocationNameLang,
        string? translatedLocationName,
        string? translationLang,
        int? translationEngine,
        DateTime? createdDate,
        DateTime? updatedDate)
    {
        this.Id = id;
        this.OriginalLocationName = originalLocationName;
        this.OriginalLocationNameLang = originalLocationNameLang;
        this.TranslatedLocationName = translatedLocationName;
        this.TranslationLang = translationLang;
        this.TranslationEngine = translationEngine;
        this.CreatedDate = createdDate;
        this.UpdatedDate = updatedDate;
    }

    [Key] public int Id { get; set; }

    public string? OriginalLocationName { get; set; }

    public string? OriginalLocationNameLang { get; set; }

    public string? TranslatedLocationName { get; set; }

    public string? TranslationLang { get; set; }

    public int? TranslationEngine { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [Timestamp] public byte[]? RowVersion { get; set; }

    public override string? ToString()
    {
        return $"Id: {this.Id}, " +
               $"OriginalLocationName: {this.OriginalLocationName}, " +
               $"OriginalLocationNameLang: {this.OriginalLocationNameLang}, " +
               $"TranslatedLocationName: {this.TranslatedLocationName}, " +
               $"TranslationLang: {this.TranslationLang}, " +
               $"TranslationEngine: {this.TranslationEngine}, " +
               $"CreatedDate: {this.CreatedDate}, " +
               $"UpdatedDate: {this.UpdatedDate}";
    }
}