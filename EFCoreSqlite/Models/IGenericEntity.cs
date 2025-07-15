// <copyright file="IGenericEntity.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Defines a contract for generic translatable entities stored in the
///     database.
/// </summary>
public interface IGenericEntity
{
    /// <summary>
    ///     Gets the original (untranslated) string(s).
    /// </summary>
    /// <returns>The original string to be translated.</returns>
    string GetOriginalText();

    /// <summary>
    ///     Gets the translated string(s).
    /// </summary>
    /// <returns>The translated string.</returns>
    string? GetTranslatedText();

    /// <summary>
    ///     Sets the translated string to persist in the database.
    /// </summary>
    /// <param name="translated">The translated string.</param>
    void SetTranslatedText(string translated);

    /// <summary>
    ///     Gets the language code of the translation (e.g., "en", "ja").
    /// </summary>
    /// <returns>The target language code.</returns>
    string? GetTranslationLang();

    /// <summary>
    ///     Gets the engine ID used for the translation.
    /// </summary>
    /// <returns>The translation engine identifier.</returns>
    int? GetTranslationEngine();

    /// <summary>
    ///     Gets a key to identify this entity uniquely (e.g., addon name, toast type,
    ///     quest name).
    /// </summary>
    /// <returns>The entity's identifying key.</returns>
    string GetEntityKey();

    /// <summary>
    ///     Gets the game version (if applicable). May return null for entities where
    ///     version is irrelevant.
    /// </summary>
    /// <returns>The game version string or null.</returns>
    string? GetGameVersion();
}