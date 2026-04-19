// <copyright file="TranslationService.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Provides translation services using various translation engines.
/// </summary>
public class TranslationService
{
  private readonly Action<string>? debugLog;
  private readonly Func<string, string> sanitizeText;
  private readonly ITranslator translator = null!;

  /// <summary>
  ///     Initializes a new instance of the <see cref="TranslationService" /> class.
  /// </summary>
  /// <param name="config">The configuration settings for the translation service.</param>
  /// <param name="pluginLog">The plugin logger for logging purposes.</param>
  /// <param name="sanitizer">
  ///     The sanitizer used to clean input text before
  ///     translation.
  /// </param>
  public TranslationService(
      Config config,
      IPluginLog pluginLog,
      Sanitizer sanitizer)
  {
    this.debugLog = message => pluginLog.Debug(message);
    this.sanitizeText = sanitizer.Sanitize;
    var chosenEngine = (Echoglossian.TransEngines)config.ChosenTransEngine;

    if (chosenEngine == Echoglossian.TransEngines.All)
    {
      return;
    }

    this.translator = TranslatorFactory.Create(
        chosenEngine,
        config,
        pluginLog);
  }

  /// <summary>
  ///     Initializes a new instance of the <see cref="TranslationService" /> class
  ///     with test-friendly dependencies.
  /// </summary>
  /// <param name="sanitizeText">The sanitizer delegate to apply before translation.</param>
  /// <param name="translator">The translator implementation to use.</param>
  internal TranslationService(
      Func<string, string> sanitizeText,
      ITranslator translator)
  {
    this.debugLog = null;
    this.sanitizeText = sanitizeText;
    this.translator = translator;
  }

  /// <summary>
  ///     Translates the given text from the source language to the target language
  ///     synchronously.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">Source text language.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <returns>The translated text as a string.</returns>
  public string Translate(
      string text,
      string sourceLanguage,
      string targetLanguage)
  {
    this.debugLog?.Invoke(
        $"TranslationService: Translate called with text: {text}, sourceLanguage: {sourceLanguage}, targetLanguage: {targetLanguage}");

    var (sanitizedText, shouldTranslate) = this.CheckTextToTranslate(text);
    if (!shouldTranslate)
    {
      return sanitizedText;
    }

    var startingEllipsis = string.Empty;

    var parsedText = sanitizedText;
    if (text.StartsWith("..."))
    {
      startingEllipsis = "...";
      parsedText = text.Substring(3);
    }

    var finalDialogueText = this.translator.Translate(
        parsedText,
        sourceLanguage,
        targetLanguage);

    finalDialogueText = !string.IsNullOrEmpty(startingEllipsis)
        ? startingEllipsis + finalDialogueText
        : finalDialogueText;
    return finalDialogueText;
  }

  /// <summary>
  ///     Translates the given text from the source language to the target language
  ///     asynchronously.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">Source text language.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <returns>
  ///     A task that represents the asynchronous operation. The task result
  ///     contains the translated text as a string.
  /// </returns>
  public async Task<string> TranslateAsync(
      string text,
      string sourceLanguage,
      string targetLanguage)
  {
    var (sanitizedText, shouldTranslate) = this.CheckTextToTranslate(text);
    if (!shouldTranslate)
    {
      return sanitizedText;
    }

    var startingEllipsis = string.Empty;

    var parsedText = sanitizedText;
    if (text.StartsWith("..."))
    {
      startingEllipsis = "...";
      parsedText = text.Substring(3);
    }

    var finalDialogueText = await this.translator.TranslateAsync(
        parsedText,
        sourceLanguage,
        targetLanguage);

    finalDialogueText = !string.IsNullOrEmpty(startingEllipsis)
        ? startingEllipsis + finalDialogueText
        : finalDialogueText;
    return finalDialogueText;
  }

  /// <summary>
  /// Determines whether the specified text should be translated and returns a sanitized version of the text.
  /// </summary>
  /// <param name="text">The text to be checked and potentially sanitized for translation.</param>
  /// <returns>A tuple containing the sanitized text and a boolean indicating whether the text should be translated. The
  /// sanitized text is an empty string if the input text is null or empty, or if the sanitized result is equivalent to
  /// specific non-translatable patterns. The boolean is <see langword="true"/> if the text should be translated;
  /// otherwise, <see langword="false"/>.</returns>
  private (string SanitizedText, bool ShouldTranslate) CheckTextToTranslate(
      string text)
  {
    if (string.IsNullOrEmpty(text))
    {
      return (string.Empty, false);
    }

    var sanitizedString = this.sanitizeText(text);
    if (string.IsNullOrEmpty(sanitizedString))
    {
      return (string.Empty, false);
    }

    if (sanitizedString == "...")
    {
      return (sanitizedString, false);
    }

    if (sanitizedString == "???")
    {
      return (sanitizedString, false);
    }

    return (sanitizedString, true);
  }
}
