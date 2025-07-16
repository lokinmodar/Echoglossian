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
    ///     Sets the original (untranslated) string(s).
    /// </summary>
    /// <param name="original">The original text string.</param>
    void SetOriginalText(string original);

    /// <summary>
    ///     Gets the language code of the original text (e.g., "en", "ja").
    /// </summary>
    /// <returns>Returns originalLang for the text.</returns>
    string GetOriginalLang();

    /// <summary>
    ///     Sets the language code of the original text.
    /// </summary>
    /// <param name="lang">Language of the original text</param>
    void SetOriginalLang(string lang);

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
    ///     Sets the language code of the translation.
    /// </summary>
    /// <param name="lang">The language code (e.g., "en").</param>
    void SetTranslationLang(string lang);

    /// <summary>
    ///     Gets the engine ID used for the translation.
    /// </summary>
    /// <returns>The translation engine identifier.</returns>
    int? GetTranslationEngine();

    /// <summary>
    ///     Sets the engine ID used for the translation.
    /// </summary>
    /// <param name="engine">The translation engine ID.</param>
    void SetTranslationEngine(int engine);

    /// <summary>
    ///     Gets a key to identify this entity uniquely (e.g., addon name, toast type,
    ///     quest name).
    /// </summary>
    /// <returns>The entity's identifying key.</returns>
    string GetEntityKey();

    /// <summary>
    ///     Sets a key to identify this entity uniquely.
    /// </summary>
    /// <param name="key">The identifying key (e.g., addon name).</param>
    void SetEntityKey(string key);

    /// <summary>
    ///     Gets the game version (if applicable). May return null for entities where
    ///     the version is irrelevant.
    /// </summary>
    /// <returns>The game version string or null.</returns>
    string? GetGameVersion();

    /// <summary>
    ///     Sets the game version associated with this entity.
    /// </summary>
    /// <param name="version">The game version string.</param>
    void SetGameVersion(string version);
}