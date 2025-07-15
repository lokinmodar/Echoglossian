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
    /// <inheritdoc />
    public string GetOriginalText()
    {
        return this.OriginalWindowStrings;
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
    public int? GetTranslationEngine()
    {
        return this.TranslationEngine;
    }

    /// <inheritdoc />
    public string? GetGameVersion()
    {
        return this.GameVersion;
    }

    /// <inheritdoc />
    public string GetEntityKey()
    {
        return this.WindowAddonName;
    }
}