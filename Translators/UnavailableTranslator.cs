// <copyright file="UnavailableTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Safe fallback translator used when the configured engine cannot be
///     instantiated during bootstrap.
/// </summary>
internal sealed class UnavailableTranslator : ITranslator
{
  /// <summary>
  ///     Returns the original input text unchanged.
  /// </summary>
  /// <param name="text">The source text.</param>
  /// <param name="sourceLanguage">The source language code.</param>
  /// <param name="targetLanguage">The target language code.</param>
  /// <returns>The original input text.</returns>
  public string? Translate(
      string text,
      string sourceLanguage,
      string targetLanguage)
  {
    return text;
  }

  /// <summary>
  ///     Returns the original input text unchanged.
  /// </summary>
  /// <param name="text">The source text.</param>
  /// <param name="sourceLanguage">The source language code.</param>
  /// <param name="targetLanguage">The target language code.</param>
  /// <returns>The original input text.</returns>
  public Task<string?> TranslateAsync(
      string text,
      string sourceLanguage,
      string targetLanguage)
  {
    return Task.FromResult<string?>(text);
  }
}
