// <copyright file="ActionTooltip.partial.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Partial implementation of <see cref="ActionTooltip" /> to support generic
///     translation access via <see cref="IGenericEntity" />.
/// </summary>
public partial class ActionTooltip : IGenericEntity
{
    /// <inheritdoc />
    public string GetOriginalText()
    {
        return this.OriginalActionTooltip;
    }

    /// <inheritdoc />
    public string? GetTranslatedText()
    {
        return this.TranslatedActionTooltip;
    }

    /// <inheritdoc />
    public void SetTranslatedText(string translated)
    {
        this.TranslatedActionTooltip = translated;
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
        return this.RowVersion?.ToString();
    }
}