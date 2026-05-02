// <copyright file="SelectString.partial.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Partial implementation of <see cref="SelectString" /> to support generic
///     translation access via <see cref="IGenericEntity" />.
/// </summary>
public partial class SelectString : IGenericEntity
{
    /// <inheritdoc />
    public string GetOriginalText()
    {
        return this.OriginalSelectString ?? string.Empty;
    }

    /// <inheritdoc />
    public void SetOriginalText(string original)
    {
        this.OriginalSelectString = original;
    }

    /// <inheritdoc />
    public string GetOriginalLang()
    {
        return this.OriginalSelectStringLang ?? string.Empty;
    }

    /// <inheritdoc />
    public void SetOriginalLang(string lang)
    {
        this.OriginalSelectStringLang = lang;
    }

    /// <inheritdoc />
    public string? GetTranslatedText()
    {
        return this.TranslatedSelectString;
    }

    /// <inheritdoc />
    public void SetTranslatedText(string translated)
    {
        this.TranslatedSelectString = translated;
    }

    /// <inheritdoc />
    public string? GetTranslationLang()
    {
        return this.TranslationLang;
    }

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
    public void SetEntityKey(string key)
    {
        this.RowVersion = key.Select(c => (byte)c).ToArray();
    }

    /// <inheritdoc />
    public string? GetGameVersion()
    {
        return null;
    }

    /// <inheritdoc />
    public void SetGameVersion(string version)
    {
        // No game version for SelectString entities.
    }

    /// <inheritdoc />
    public string GetEntityKey()
    {
        return this.RowVersion?.ToString() ?? string.Empty;
    }
}
