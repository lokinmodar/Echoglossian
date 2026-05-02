// <copyright file="MiniTalkMessage.partial.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Adapter to enable <see cref="MiniTalkMessage" /> to be handled via
///     <see cref="IGenericEntity" />.
/// </summary>
public partial class MiniTalkMessage : IGenericEntity
{
  /// <inheritdoc />
  public string GetOriginalText()
  {
    return this.OriginalMiniTalkMessage ?? string.Empty;
  }

  /// <inheritdoc />
  public void SetOriginalText(string original)
  {
    this.OriginalMiniTalkMessage = original;
  }

  /// <inheritdoc />
  public string GetOriginalLang()
  {
    return this.OriginalMiniTalkMessageLang ?? string.Empty;
  }

  /// <inheritdoc />
  public void SetOriginalLang(string lang)
  {
    this.OriginalMiniTalkMessageLang = lang;
  }

  /// <inheritdoc />
  public string? GetTranslatedText()
  {
    return this.TranslatedMiniTalkMessage;
  }

  /// <inheritdoc />
  public void SetTranslatedText(string translated)
  {
    this.TranslatedMiniTalkMessage = translated;
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
    return this.OriginalMiniTalkMessage ?? string.Empty;
  }

  /// <inheritdoc />
  public void SetEntityKey(string entityKey)
  {
    this.OriginalMiniTalkMessage = entityKey;
  }

  /// <inheritdoc />
  public string? GetGameVersion()
  {
    return null;
  }

  /// <inheritdoc />
  public void SetGameVersion(string? gameVersion)
  {
    // No implementation needed for MiniTalkMessage
  }
}
