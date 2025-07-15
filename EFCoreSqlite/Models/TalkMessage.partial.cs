// <copyright file="TalkMessage.partial.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Adapter to enable <see cref="TalkMessage" /> to be handled via
///     <see cref="IMultiTextEntity" />.
/// </summary>
public partial class TalkMessage : IMultiTextEntity
{
    /// <inheritdoc />
    public string GetOriginalText()
    {
        return this.SenderName;
    }

    /// <inheritdoc />
    public string? GetTranslatedText()
    {
        return this.TranslatedSenderName;
    }

    /// <inheritdoc />
    public void SetTranslatedText(string translated)
    {
        this.TranslatedSenderName = translated;
    }

    /// <inheritdoc />
    public string GetOriginalSecondaryText()
    {
        return this.OriginalTalkMessage;
    }

    /// <inheritdoc />
    public string? GetTranslatedSecondaryText()
    {
        return this.TranslatedTalkMessage;
    }

    /// <inheritdoc />
    public void SetTranslatedSecondaryText(string translated)
    {
        this.TranslatedTalkMessage = translated;
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
    public string GetEntityKey()
    {
        return this.SenderName;
    }

    /// <inheritdoc />
    public string? GetGameVersion()
    {
        return null;

        // Not applicable for TalkMessage
    }
}