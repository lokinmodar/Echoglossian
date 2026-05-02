// <copyright file="TalkSubtitleMessage.partial.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Adapter to enable <see cref="TalkSubtitleMessage" /> to be handled via
///     <see cref="IGenericEntity" />.
/// </summary>
public partial class TalkSubtitleMessage : IGenericEntity
{
    /// <inheritdoc />
    public string GetOriginalText()
    {
        return this.OriginalTalkSubtitleMessage ?? string.Empty;
    }

    /// <inheritdoc />
    public void SetOriginalText(string original)
    {
        this.OriginalTalkSubtitleMessage = original;
    }

    /// <inheritdoc />
    public string GetOriginalLang()
    {
        return this.OriginalTalkSubtitleMessageLang ?? string.Empty;
    }

    /// <inheritdoc />
    public void SetOriginalLang(string lang)
    {
        this.OriginalTalkSubtitleMessageLang = lang;
    }

    /// <inheritdoc />
    public string? GetTranslatedText()
    {
        return this.TranslatedTalkSubtitleMessage;
    }

    /// <inheritdoc />
    public void SetTranslatedText(string translated)
    {
        this.TranslatedTalkSubtitleMessage = translated;
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
        return this.OriginalTalkSubtitleMessage ?? string.Empty;
    }

    /// <inheritdoc />
    public void SetEntityKey(string entityKey)
    {
        this.OriginalTalkSubtitleMessage = entityKey;
    }

    /// <inheritdoc />
    public string? GetGameVersion()
    {
        return null;
    }

    /// <inheritdoc />
    public void SetGameVersion(string? gameVersion)
    {
        // No implementation needed for TalkSubtitleMessage
    }
}
