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
  private readonly Sanitizer sanitizer;
  private readonly ITranslator translator;

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
    this.sanitizer = sanitizer;
    var chosenEngine = (Echoglossian.TransEngines)config.ChosenTransEngine;

    switch (chosenEngine)
    {
      case Echoglossian.TransEngines.Google: // validated!
        this.translator = new GoogleTranslator(pluginLog, config);
        break;
      case Echoglossian.TransEngines.Deepl:
        this.translator = new DeepLTranslator(
            pluginLog,
            config.DeeplTranslatorUsingApiKey,
            config.DeeplTranslatorApiKey);
        break;
      case Echoglossian.TransEngines.ChatGPT: // validated!
        this.translator = new ChatGPTTranslator(
            pluginLog,
            config.ChatGPTBaseUrl,
            config.ChatGptApiKey,
            config.OpenAILlmModel,
            config.ChatGptTemperature);
        break;
      case Echoglossian.TransEngines.YandexCloud:
        this.translator = new YandexTranslator(pluginLog, config);
        break;
      case Echoglossian.TransEngines.GTranslate:
        this.translator = new GTranslateTranslator(pluginLog, config);
        break;
      case Echoglossian.TransEngines.Amazon:
        this.translator =
            new AmazonTranslateTranslator(pluginLog, config);
        break;
      case Echoglossian.TransEngines.Microsoft:
        this.translator = new MicrosoftTranslator(pluginLog, config);
        break;
      case Echoglossian.TransEngines.Gemini:
        this.translator = new GeminiTranslator(pluginLog, config);
        break;
      case Echoglossian.TransEngines.DeepSeek:
        this.translator = new DeepSeekTranslator(pluginLog, config);
        break;
      case Echoglossian.TransEngines.Ollama:
        this.translator = new OllamaTranslator(pluginLog, config);
        break;
      case Echoglossian.TransEngines.LibreTranslate:
        this.translator =
            new LibreTranslateTranslator(pluginLog, config);
        break;
      case Echoglossian.TransEngines.YandexPublic:
        this.translator = new YandexPublicTranslator(pluginLog, config);
        break;
      case Echoglossian.TransEngines.OpenRouter:
        this.translator = new OpenRouterTranslator(pluginLog, config);
        break;
      case TransEngines.LmStudio:
        this.translator = new LmStudioTranslator(pluginLog, config);
        break;
      case Echoglossian.TransEngines.All:
        break;
      default:
        throw new NotSupportedException(
            $"Translation engine {chosenEngine} is not supported.");
    }
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
    PluginLog.Debug(
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

  private (string SanitizedText, bool ShouldTranslate) CheckTextToTranslate(
      string text)
  {
    if (string.IsNullOrEmpty(text))
    {
      return (string.Empty, false);
    }

    var sanitizedString = this.sanitizer.Sanitize(text);
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