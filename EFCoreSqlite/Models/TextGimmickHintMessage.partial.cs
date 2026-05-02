// <copyright file="TextGimmickHintMessage.partial.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Adapter to enable <see cref="TextGimmickHintMessage" /> to be handled via
///     <see cref="IGenericEntity" />.
/// </summary>
public partial class TextGimmickHintMessage : IGenericEntity
{
  /// <summary>
  ///     Initializes a new instance of the <see cref="TextGimmickHintMessage" /> class.
  /// </summary>
  public TextGimmickHintMessage() { }

  /// <inheritdoc />
  public string GetOriginalText()
  {
    return this.OriginalText ?? string.Empty;
  }

  /// <inheritdoc />
  public void SetOriginalText(string original)
  {
    this.OriginalText = original;
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
    return this.TranslatedText;
  }

  /// <inheritdoc />
  public void SetTranslatedText(string translated)
  {
    this.TranslatedText = translated;
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
    return this.OriginalText ?? string.Empty;
  }

  /// <inheritdoc />
  public void SetEntityKey(string key)
  {
    this.OriginalText = key;
  }

  /// <inheritdoc />
  public string? GetGameVersion()
  {
    return null;
  }

  /// <inheritdoc />
  public void SetGameVersion(string version)
  {
    /* Ignored — TextGimmickHintMessage does not use game version */
  }
}
