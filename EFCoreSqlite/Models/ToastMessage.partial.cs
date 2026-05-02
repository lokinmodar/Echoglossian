// <copyright file="ToastMessage.partial.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Adapter to enable <see cref="ToastMessage" /> to be handled via
///     <see cref="IGenericEntity" />.
/// </summary>
public partial class ToastMessage : IGenericEntity
{
    /// <inheritdoc />
    public string GetOriginalText()
    {
        return this.OriginalToastMessage ?? string.Empty;
    }

    /// <inheritdoc />
    public void SetOriginalText(string original)
    {
        this.OriginalToastMessage = original;
    }

    /// <inheritdoc />
    public string GetOriginalLang()
    {
        return this.OriginalLang ?? string.Empty;
    }

    /// <inheritdoc />
    public void SetOriginalLang(string lang)
    {
        this.OriginalLang = lang;
    }

    /// <inheritdoc />
    public string? GetTranslatedText()
    {
        return this.TranslatedToastMessage;
    }

    /// <inheritdoc />
    public void SetTranslatedText(string translated)
    {
        this.TranslatedToastMessage = translated;
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
        return this.OriginalToastMessage ?? string.Empty;
    }

    /// <inheritdoc />
    public void SetEntityKey(string key)
    {
        this.OriginalToastMessage = key;
    }

    /// <inheritdoc />
    public string? GetGameVersion()
    {
        return null;
    }

    /// <inheritdoc />
    public void SetGameVersion(string version)
    {
        /* Ignored — ToastMessage does not use game version */
    }
}
