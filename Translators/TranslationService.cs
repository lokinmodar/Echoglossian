// <copyright file="TranslationService.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators
{
  /// <summary>
  /// Provides translation services using various translation engines.
  /// </summary>
  public class TranslationService
  {
    private readonly ITranslator translator;

    private readonly Sanitizer sanitizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TranslationService"/> class.
    /// </summary>
    /// <param name="config">The configuration settings for the translation service.</param>
    /// <param name="pluginLog">The plugin logger for logging purposes.</param>
    /// <param name="sanitizer">The sanitizer used to clean input text before translation.</param>
    public TranslationService(Config config, IPluginLog pluginLog, Sanitizer sanitizer)
    {
      this.sanitizer = sanitizer;
      TransEngines chosenEngine = (TransEngines)config.ChosenTransEngine;

      switch (chosenEngine)
      {
        case TransEngines.Google: // validated!
          this.translator = new GoogleTranslator(pluginLog, config);
          break;
        case TransEngines.Deepl:
          this.translator = new DeepLTranslator(pluginLog, config.DeeplTranslatorUsingApiKey, config.DeeplTranslatorApiKey);
          break;
        case TransEngines.ChatGPT: // validated!
          this.translator = new ChatGPTTranslator(pluginLog, config.ChatGPTBaseUrl, config.ChatGptApiKey, config.OpenAILlmModel, config.ChatGptTemperature);
          break;
        case TransEngines.YandexCloud:
          this.translator = new YandexTranslator(pluginLog, config);
          break;
        case TransEngines.GTranslate:
          this.translator = new GTranslateTranslator(pluginLog, config);
          break;
        case TransEngines.Amazon:
          this.translator = new AmazonTranslateTranslator(pluginLog, config);
          break;
        case TransEngines.Microsoft:
          this.translator = new MicrosoftTranslator(pluginLog, config);
          break;
        case TransEngines.Gemini:
          this.translator = new GeminiTranslator(pluginLog, config);
          break;
        case TransEngines.DeepSeek:
          this.translator = new DeepSeekTranslator(pluginLog, config);
          break;
        case TransEngines.Ollama:
          break;
        case TransEngines.LibreTranslate:
          this.translator = new LibreTranslateTranslator(pluginLog, config);
          break;
        case TransEngines.YandexPublic:
          this.translator = new YandexPublicTranslator(pluginLog, config);
          break;
        case TransEngines.OpenRouter:
          this.translator = new OpenRouterTranslator(pluginLog, config);
          break;
        case TransEngines.All:
          break;
        default:
          throw new NotSupportedException($"Translation engine {chosenEngine} is not supported.");
      }
    }

    /// <summary>
    /// Translates the given text from the source language to the target language synchronously.
    /// </summary>
    /// <param name="text">Text to translate.</param>
    /// <param name="sourceLanguage">Source text language.</param>
    /// <param name="targetLanguage">Target translation language.</param>
    /// <returns>The translated text as a string.</returns>
    public string Translate(string text, string sourceLanguage, string targetLanguage)
    {
      var (sanitizedText, shouldTranslate) = this.CheckTextToTranslate(text);
      if (!shouldTranslate)
      {
        return sanitizedText;
      }

      string startingEllipsis = string.Empty;

      string parsedText = sanitizedText;
      if (text.StartsWith("..."))
      {
        startingEllipsis = "...";
        parsedText = text.Substring(3);
      }

      string finalDialogueText = this.translator.Translate(parsedText, sourceLanguage, targetLanguage);

      finalDialogueText = !string.IsNullOrEmpty(startingEllipsis)
          ? startingEllipsis + finalDialogueText
          : finalDialogueText;
      return finalDialogueText;
    }

    /// <summary>
    /// Translates the given text from the source language to the target language asynchronously.
    /// </summary>
    /// <param name="text">Text to translate.</param>
    /// <param name="sourceLanguage">Source text language.</param>
    /// <param name="targetLanguage">Target translation language.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the translated text as a string.</returns>
    public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
      var (sanitizedText, shouldTranslate) = this.CheckTextToTranslate(text);
      if (!shouldTranslate)
      {
        return sanitizedText;
      }

      string startingEllipsis = string.Empty;

      string parsedText = sanitizedText;
      if (text.StartsWith("..."))
      {
        startingEllipsis = "...";
        parsedText = text.Substring(3);
      }

      string finalDialogueText = await this.translator.TranslateAsync(parsedText, sourceLanguage, targetLanguage);

      finalDialogueText = !string.IsNullOrEmpty(startingEllipsis)
          ? startingEllipsis + finalDialogueText
          : finalDialogueText;
      return finalDialogueText;
    }

    private (string SanitizedText, bool ShouldTranslate) CheckTextToTranslate(string text)
    {
      if (string.IsNullOrEmpty(text))
      {
        return (string.Empty, false);
      }

      string sanitizedString = this.sanitizer.Sanitize(text);
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
}
