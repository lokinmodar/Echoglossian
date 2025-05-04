// <copyright file="ITranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators
{
  /// <summary>
  /// Interface for translating text between different languages.
  /// </summary>
  public interface ITranslator
  {
    /// <summary>
    /// Translates the specified text from the source language to the target language.
    /// </summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="sourceLanguage">The language of the text to translate.</param>
    /// <param name="targetLanguage">The language to translate the text into.</param>
    /// <returns>The translated text.</returns>
    string? Translate(string text, string sourceLanguage, string targetLanguage);

    /// <summary>
    /// Asynchronously translates the specified text from the source language to the target language.
    /// </summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="sourceLanguage">The language of the text to translate.</param>
    /// <param name="targetLanguage">The language to translate the text into.</param>
    /// <returns>A task that represents the asynchronous translation operation. The task result contains the translated text.</returns>
    Task<string?> TranslateAsync(string text, string sourceLanguage, string targetLanguage);
  }
}
