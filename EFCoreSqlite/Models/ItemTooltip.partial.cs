// <copyright file="ItemTooltip.partial.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Partial implementation of <see cref="ItemTooltip" /> to support generic
///     translation access via <see cref="IGenericEntity" />.
/// </summary>
public partial class ItemTooltip : IGenericEntity
{
    /// <inheritdoc />
    public string GetOriginalText()
    {
        return this.OriginalItemTooltip;
    }

    /// <inheritdoc />
    public void SetOriginalText(string original)
    {
        this.OriginalItemTooltip = original;
    }

    /// <inheritdoc />
    public string GetOriginalLang()
    {
        return this.OriginalItemTooltipLang;
    }

    /// <inheritdoc />
    public void SetOriginalLang(string lang)
    {
        this.OriginalItemTooltipLang = lang;
    }

    /// <inheritdoc />
    public string? GetTranslatedText()
    {
        return this.TranslatedItemTooltip;
    }

    /// <inheritdoc />
    public void SetTranslatedText(string translated)
    {
        this.TranslatedItemTooltip = translated;
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
        return this.GameVersion;
    }

    /// <inheritdoc />
    public void SetGameVersion(string version)
    {
        this.GameVersion = version;
    }

    /// <inheritdoc />
    public string GetEntityKey()
    {
        return this.RowVersion?.ToString();
    }
}