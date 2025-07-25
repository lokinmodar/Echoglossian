// <copyright file="IMultiTextEntity.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models;

/// <summary>
///     Extends <see cref="IGenericEntity" /> to support entities with two original
///     and translated text fields.
///     Useful for structures like TalkMessage or BattleTalkMessage which include
///     both a sender and message.
/// </summary>
public interface IMultiTextEntity : IGenericEntity
{
  /// <summary>
  ///     Gets the secondary original text (e.g., original message text).
  /// </summary>
  /// <returns>The secondary original text value.</returns>
  string? GetOriginalSecondaryText();

  /// <summary>
  ///     Sets the original value of the secondary text field.
  /// </summary>
  /// <param name="secondaryText">The original secondary text value.</param>
  void SetOriginalSecondaryText(string secondaryText);

  /// <summary>
  ///     Gets the secondary translated text (e.g., translated message text).
  /// </summary>
  /// <returns>The secondary translated text value.</returns>
  string? GetTranslatedSecondaryText();

  /// <summary>
  ///     Sets the translated value of the secondary text field.
  /// </summary>
  /// <param name="translated">The translated secondary text value.</param>
  void SetTranslatedSecondaryText(string translated);

  /// <summary>
  ///  Gets the RTL language translation image data.
  /// </summary>
  /// <returns>The byte array containing the RTL language translation image data.</returns>
  byte[]? GetRTLLangTranslationImageData();

  /// <summary>
  ///     Sets the RTL language translation image data.
  /// </summary>
  /// <param name="imageData">The byte array containing the image data.</param>
  void SetRTLLangTranslationImageData(byte[]? imageData);
}