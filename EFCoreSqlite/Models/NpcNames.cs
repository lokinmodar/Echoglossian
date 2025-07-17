// <copyright file="NpcNames.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

[Table("npcnames")]
public class NpcNames
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="NpcNames" /> class.
    /// </summary>
    /// <param name="id">The unique identifier for the NPC.</param>
    /// <param name="originalNpcName">The original name of the NPC.</param>
    /// <param name="originalNpcNameLang">The language of the original NPC name.</param>
    /// <param name="translatedNpcName">The translated name of the NPC.</param>
    /// <param name="translationLang">The language of the translated NPC name.</param>
    /// <param name="translationEngine">The translation engine used.</param>
    /// <param name="createdDate">The date the NPC name was created.</param>
    /// <param name="updatedDate">The date the NPC name was last updated.</param>
    public NpcNames(
        int id,
        string? originalNpcName,
        string? originalNpcNameLang,
        string? translatedNpcName,
        string? translationLang,
        int? translationEngine,
        DateTime? createdDate,
        DateTime? updatedDate)
    {
        this.Id = id;
        this.OriginalNpcName = originalNpcName;
        this.OriginalNpcNameLang = originalNpcNameLang;
        this.TranslatedNpcName = translatedNpcName;
        this.TranslationLang = translationLang;
        this.TranslationEngine = translationEngine;
        this.CreatedDate = createdDate;
        this.UpdatedDate = updatedDate;
    }

    [Key] public int Id { get; set; }

    public string? OriginalNpcName { get; set; }

    public string? OriginalNpcNameLang { get; set; }

    public string? TranslatedNpcName { get; set; }

    public string? TranslationLang { get; set; }

    public int? TranslationEngine { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [Timestamp] public byte[]? RowVersion { get; set; }

    public override string? ToString()
    {
        return $"Id: {this.Id}, " +
               $"OriginalNpcName: {this.OriginalNpcName}, " +
               $"OriginalNpcNameLang: {this.OriginalNpcNameLang}, " +
               $"TranslatedNpcName: {this.TranslatedNpcName}, " +
               $"TranslationLang: {this.TranslationLang}, " +
               $"TranslationEngine: {this.TranslationEngine}, " +
               $"CreatedDate: {this.CreatedDate}, " +
               $"UpdatedDate: {this.UpdatedDate}";
    }
}