// <copyright file="GameWindow.partial.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Provides <see cref="IGenericEntity" /> implementation for the
///     <see cref="GameWindow" /> entity.
/// </summary>
public partial class GameWindow : IGenericEntity
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="GameWindow" /> class for use
    ///     with the <see cref="GenericAddonHandler" />.
    /// </summary>
    public GameWindow()
    {
    }

    /// <inheritdoc />
    public string GetOriginalText()
    {
        return this.OriginalWindowStrings ?? string.Empty;
    }

    /// <inheritdoc />
    public void SetOriginalText(string original)
    {
        this.OriginalWindowStrings = original;
    }

    /// <inheritdoc />
    public string GetOriginalLang()
    {
        return this.OriginalWindowStringsLang ?? string.Empty;
    }

    /// <inheritdoc />
    public void SetOriginalLang(string lang)
    {
        this.OriginalWindowStringsLang = lang;
    }

    /// <inheritdoc />
    public string? GetTranslatedText()
    {
        return this.TranslatedWindowStrings;
    }

    /// <inheritdoc />
    public void SetTranslatedText(string translated)
    {
        this.TranslatedWindowStrings = translated;
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
        return this.WindowAddonName ?? string.Empty;
    }

    /// <inheritdoc />
    public void SetEntityKey(string key)
    {
        this.WindowAddonName = key;
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
}
