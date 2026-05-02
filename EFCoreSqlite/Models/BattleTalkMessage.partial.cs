// <copyright file="BattleTalkMessage.partial.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Adapter to enable <see cref="BattleTalkMessage" /> to be handled via
///     <see cref="IMultiTextEntity" />.
/// </summary>
public partial class BattleTalkMessage : IMultiTextEntity
{
  /// <summary>
  ///  Initializes a new instance of the <see cref="BattleTalkMessage"/> class.
  ///  </summary>
  public BattleTalkMessage() { }


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
    return this.OriginalBattleTalkMessageLang ?? string.Empty;
  }

  /// <inheritdoc />
  public void SetOriginalLang(string lang)
  {
    this.OriginalBattleTalkMessageLang = lang;
  }

  /// <inheritdoc />
  public string GetOriginalSecondaryText()
  {
    return this.OriginalBattleTalkMessage ?? string.Empty;
  }

  /// <inheritdoc />
  public void SetOriginalSecondaryText(string originalSecondary)
  {
    this.OriginalBattleTalkMessage = originalSecondary;
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
    return this.TranslatedBattleTalkMessage;
  }

  /// <inheritdoc />
  public void SetTranslatedSecondaryText(string translatedSecondary)
  {
    this.TranslatedBattleTalkMessage = translatedSecondary;
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
    /* Ignored — BattleTalkMessage does not use game version */
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
