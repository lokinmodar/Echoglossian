// <copyright file="TalkMessage.partial.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Adapter to enable <see cref="TalkMessage" /> to be handled via
///     <see cref="IMultiTextEntity" />.
/// </summary>
public partial class TalkMessage : IMultiTextEntity
{

  /// <summary>
  /// Initializes a new instance of the <see cref="TalkMessage"/> class.
  /// </summary>
  public TalkMessage() { }

  /// <inheritdoc />
  public string GetOriginalText()
  {
    return this.SenderName ?? string.Empty;
  }

  /// <inheritdoc />
  public void SetOriginalText(string original)
  {
    this.SenderName = original;
  }

  /// <inheritdoc />
  public string GetOriginalLang()
  {
    return this.OriginalTalkMessageLang ?? string.Empty;
  }

  /// <inheritdoc />
  public void SetOriginalLang(string lang)
  {
    this.OriginalTalkMessageLang = lang;
  }

  /// <inheritdoc />
  public string GetOriginalSecondaryText()
  {
    return this.OriginalTalkMessage ?? string.Empty;
  }

  /// <inheritdoc />
  public void SetOriginalSecondaryText(string originalSecondary)
  {
    this.OriginalTalkMessage = originalSecondary;
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
  public string? GetTranslatedSecondaryText()
  {
    return this.TranslatedTalkMessage;
  }

  /// <inheritdoc />
  public void SetTranslatedSecondaryText(string translatedSecondary)
  {
    this.TranslatedTalkMessage = translatedSecondary;
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
    return this.SenderName ?? string.Empty;
  }

  /// <inheritdoc />
  public void SetEntityKey(string key)
  {
    this.SenderName = key;
  }

  /// <inheritdoc />
  public string? GetGameVersion()
  {
    return null;
  }

  /// <inheritdoc />
  public void SetGameVersion(string version)
  {
    /* Ignored — TalkMessage does not use game version */
  }

  public byte[]? GetRTLLangTranslationImageData()
  {
    return this.RTLLangTranslationImageData;
  }

  public void SetRTLLangTranslationImageData(byte[]? imageData)
  {
    this.RTLLangTranslationImageData = imageData;
  }
}
