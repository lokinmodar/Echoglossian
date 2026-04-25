// <copyright file="GameWindow.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Represents a game window translation entity in the database.
/// </summary>
[Table("gamewindows")]
public partial class GameWindow
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="GameWindow" /> class.
    /// </summary>
    /// <param name="windowAddonName">The name of the window addon.</param>
    /// <param name="originalWindowStrings">The original window strings.</param>
    /// <param name="originalWindowStringsLang">
    ///     The language of the original window
    ///     strings.
    /// </param>
    /// <param name="translatedWindowStrings">The translated window strings.</param>
    /// <param name="translationLang">The language of the translation.</param>
    /// <param name="translationEngine">The translation engine used.</param>
    /// <param name="gameVersion">The version of the game.</param>
    /// <param name="createdDate">The date the record was created.</param>
    /// <param name="updatedDate">The date the record was last updated.</param>
    /// <param name="classJobId">
    ///     The class/job identifier associated with this window payload when the
    ///     addon content varies by the active job.
    /// </param>
    public GameWindow(
        string? windowAddonName,
        string? originalWindowStrings,
        string? originalWindowStringsLang,
        string? translatedWindowStrings,
        string? translationLang,
        int? translationEngine,
        string? gameVersion,
        DateTime? createdDate,
        DateTime? updatedDate,
        uint? classJobId = null)
    {
        this.WindowAddonName = windowAddonName;
        this.OriginalWindowStrings = originalWindowStrings;
        this.OriginalWindowStringsLang = originalWindowStringsLang;
        this.TranslatedWindowStrings = translatedWindowStrings;
        this.TranslationLang = translationLang;
        this.TranslationEngine = translationEngine;
        this.GameVersion = gameVersion;
        this.ClassJobId = classJobId;
        this.CreatedDate = createdDate;
        this.UpdatedDate = updatedDate;
    }

    [Key] public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the name of the window addon or unique key that identifies
    ///     this entity context.
    /// </summary>
    public string? WindowAddonName { get; set; }

    public string? OriginalWindowStrings { get; set; }

    public string? OriginalWindowStringsLang { get; set; }

    public string? TranslatedWindowStrings { get; set; }

    public string? TranslationLang { get; set; }

    public int? TranslationEngine { get; set; }

    public string? GameVersion { get; set; }

    /// <summary>
    ///     Gets or sets the class/job identifier associated with this window
    ///     payload when the addon content varies by the active job.
    /// </summary>
    public uint? ClassJobId { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [Timestamp] public byte[]? RowVersion { get; set; }

    public override string? ToString()
    {
        return
            $"GameWindow: Id={this.Id}, WindowAddonName={this.WindowAddonName}, OriginalWindowStrings={this.OriginalWindowStrings}, OriginalWindowStringsLang={this.OriginalWindowStringsLang}, TranslatedWindowStrings={this.TranslatedWindowStrings}, TranslationLang={this.TranslationLang}, TranslationEngine={this.TranslationEngine}, GameVersion={this.GameVersion}, ClassJobId={this.ClassJobId}, CreatedDate={this.CreatedDate}, UpdatedDate={this.UpdatedDate}";
    }
}
