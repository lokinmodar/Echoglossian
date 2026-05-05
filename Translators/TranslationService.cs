// <copyright file="TranslationService.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.DBHelpers;
using System.Runtime.CompilerServices;

namespace Echoglossian.Translators;

/// <summary>
///     Provides translation services using various translation engines.
/// </summary>
public class TranslationService
{
  private const string EmptyResultFailureReason = "empty-result";
  private readonly Action<string>? debugLog;
  private readonly Func<string, string, string, int, bool>? isKnownFailedTranslation;
  private readonly Action<string, string, string, int, string, string?>? recordFailedTranslation;
  private readonly Func<string, string> sanitizeText;
  private readonly int translationEngineId = -1;
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
    this.debugLog = message => PluginRuntimeLog.Debug(pluginLog, message);
    this.sanitizeText = sanitizer.Sanitize;
    var chosenEngine = (Echoglossian.TransEngines)config.ChosenTransEngine;
    this.translationEngineId = (int)chosenEngine;
    this.isKnownFailedTranslation =
        TranslationFailureCacheManager.Contains;
    this.recordFailedTranslation =
        (sourceText, sourceLanguage, targetLanguage, translationEngine, failureReason, originContext) =>
            TranslationFailurePersistenceHelper.RecordFailure(
                ConfigDirectory,
                sourceText,
                sourceLanguage,
                targetLanguage,
                translationEngine,
                failureReason,
                originContext,
                TranslationFailureCacheManager.Update);

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
      ITranslator translator,
      int translationEngine = (int)Echoglossian.TransEngines.Google,
      Func<string, string, string, int, bool>? isKnownFailedTranslation = null,
      Action<string, string, string, int, string, string?>? recordFailedTranslation = null)
  {
    this.debugLog = null;
    this.sanitizeText = sanitizeText;
    this.translator = translator;
    this.translationEngineId = translationEngine;
    this.isKnownFailedTranslation = isKnownFailedTranslation;
    this.recordFailedTranslation = recordFailedTranslation;
  }

  /// <summary>
  ///     Translates the given text from the source language to the target language
  ///     synchronously.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">Source text language.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="originContext">Optional explicit origin context for diagnostics and persistence.</param>
  /// <param name="callerMemberName">The caller member name when no explicit origin context is provided.</param>
  /// <param name="callerFilePath">The caller file path when no explicit origin context is provided.</param>
  /// <returns>The translated text as a string.</returns>
  public string Translate(
      string text,
      string sourceLanguage,
      string targetLanguage,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    this.debugLog?.Invoke(
        $"TranslationService: Translate called with text: {text}, sourceLanguage: {sourceLanguage}, targetLanguage: {targetLanguage}");

    var (sanitizedText, shouldTranslate) = this.CheckTextToTranslate(text);
    if (!shouldTranslate)
    {
      return sanitizedText;
    }

    if (this.ShouldBypassTranslationDueToMissingLanguageAssets())
    {
      return sanitizedText;
    }

    var startingEllipsis = string.Empty;

    var parsedText = sanitizedText;
    if (sanitizedText.StartsWith("...", StringComparison.Ordinal))
    {
      startingEllipsis = "...";
      parsedText = sanitizedText.Substring(3);
    }

    var normalizedSourceLanguage =
        RuntimeLanguageHelper.NormalizeLanguage(sourceLanguage);
    var normalizedTargetLanguage =
        RuntimeLanguageHelper.NormalizeLanguage(targetLanguage);
    var resolvedOriginContext = ResolveOriginContext(
        originContext,
        callerMemberName,
        callerFilePath);
    if (this.IsKnownFailedTranslation(
            parsedText,
            normalizedSourceLanguage,
            normalizedTargetLanguage))
    {
      return sanitizedText;
    }

    var finalDialogueText = this.translator.Translate(
        parsedText,
        sourceLanguage,
        targetLanguage);
    finalDialogueText = this.AcceptTranslatedResultOrFallback(
        finalDialogueText,
        parsedText,
        sanitizedText,
        normalizedSourceLanguage,
        normalizedTargetLanguage,
        resolvedOriginContext);

    return string.IsNullOrEmpty(startingEllipsis) ||
           string.Equals(finalDialogueText, sanitizedText, StringComparison.Ordinal)
        ? finalDialogueText
        : startingEllipsis + finalDialogueText;
  }

  /// <summary>
  ///     Translates the given text from the source language to the target language
  ///     asynchronously.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">Source text language.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="originContext">Optional explicit origin context for diagnostics and persistence.</param>
  /// <param name="callerMemberName">The caller member name when no explicit origin context is provided.</param>
  /// <param name="callerFilePath">The caller file path when no explicit origin context is provided.</param>
  /// <returns>
  ///     A task that represents the asynchronous operation. The task result
  ///     contains the translated text as a string.
  /// </returns>
  public async Task<string> TranslateAsync(
      string text,
      string sourceLanguage,
      string targetLanguage,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    var (sanitizedText, shouldTranslate) = this.CheckTextToTranslate(text);
    if (!shouldTranslate)
    {
      return sanitizedText;
    }

    if (this.ShouldBypassTranslationDueToMissingLanguageAssets())
    {
      return sanitizedText;
    }

    var startingEllipsis = string.Empty;

    var parsedText = sanitizedText;
    if (sanitizedText.StartsWith("...", StringComparison.Ordinal))
    {
      startingEllipsis = "...";
      parsedText = sanitizedText.Substring(3);
    }

    var normalizedSourceLanguage =
        RuntimeLanguageHelper.NormalizeLanguage(sourceLanguage);
    var normalizedTargetLanguage =
        RuntimeLanguageHelper.NormalizeLanguage(targetLanguage);
    var resolvedOriginContext = ResolveOriginContext(
        originContext,
        callerMemberName,
        callerFilePath);
    if (this.IsKnownFailedTranslation(
            parsedText,
            normalizedSourceLanguage,
            normalizedTargetLanguage))
    {
      return sanitizedText;
    }

    var finalDialogueText = await this.translator.TranslateAsync(
        parsedText,
        sourceLanguage,
        targetLanguage);
    finalDialogueText = this.AcceptTranslatedResultOrFallback(
        finalDialogueText,
        parsedText,
        sanitizedText,
        normalizedSourceLanguage,
        normalizedTargetLanguage,
        resolvedOriginContext);

    return string.IsNullOrEmpty(startingEllipsis) ||
           string.Equals(finalDialogueText, sanitizedText, StringComparison.Ordinal)
        ? finalDialogueText
        : startingEllipsis + finalDialogueText;
  }

  /// <summary>
  /// Accepts a translated result only when it is safe to treat as a real
  /// translation; otherwise records the failure and falls back to the
  /// sanitized source text.
  /// </summary>
  /// <param name="translatedText">The translated text candidate.</param>
  /// <param name="parsedText">The parsed source text sent to the translator.</param>
  /// <param name="sanitizedText">The sanitized source text shown on fallback.</param>
  /// <param name="normalizedSourceLanguage">The normalized source language code.</param>
  /// <param name="normalizedTargetLanguage">The normalized target language code.</param>
  /// <param name="resolvedOriginContext">The resolved origin context for diagnostics.</param>
  /// <returns>
  /// The accepted translated text, or <paramref name="sanitizedText" /> when
  /// the result is empty or synthetic.
  /// </returns>
  private string AcceptTranslatedResultOrFallback(
      string? translatedText,
      string parsedText,
      string sanitizedText,
      string normalizedSourceLanguage,
      string normalizedTargetLanguage,
      string? resolvedOriginContext)
  {
    if (TranslationResultGuard.IsPersistableTranslation(translatedText))
    {
      return translatedText!;
    }

    this.RecordFailedTranslation(
        parsedText,
        normalizedSourceLanguage,
        normalizedTargetLanguage,
        string.IsNullOrWhiteSpace(translatedText)
            ? EmptyResultFailureReason
            : TranslationResultGuard.SyntheticErrorFailureReason,
        resolvedOriginContext);

    return sanitizedText;
  }

  /// <summary>
  /// Determines whether the current selected language depends on missing
  /// downloaded font assets and should therefore bypass translation work until
  /// those assets are available.
  /// </summary>
  /// <returns>
  /// <c>true</c> when translation should be bypassed because required language
  /// assets are missing; otherwise, <c>false</c>.
  /// </returns>
  private bool ShouldBypassTranslationDueToMissingLanguageAssets()
  {
    if (!AssetsManager.HasMissingRequiredAssets(SelectedLanguage))
    {
      return false;
    }

    this.debugLog?.Invoke(
        "TranslationService: bypassing translation because the selected language requires missing downloaded font assets.");
    return true;
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

  /// <summary>
  ///     Determines whether the given exact translation request is already
  ///     known to fail for the current engine and language pair.
  /// </summary>
  /// <param name="sourceText">The exact sanitized source text.</param>
  /// <param name="sourceLanguage">The normalized source language code.</param>
  /// <param name="targetLanguage">The normalized target language code.</param>
  /// <returns>
  ///     <see langword="true" /> when the request should be skipped because it
  ///     is already cached as a known failure; otherwise <see langword="false" />.
  /// </returns>
  private bool IsKnownFailedTranslation(
      string sourceText,
      string sourceLanguage,
      string targetLanguage)
  {
    if (this.translationEngineId < 0 ||
        this.isKnownFailedTranslation == null ||
        string.IsNullOrWhiteSpace(sourceText))
    {
      return false;
    }

    return this.isKnownFailedTranslation(
        sourceText,
        sourceLanguage,
        targetLanguage,
        this.translationEngineId);
  }

  /// <summary>
  ///     Records one exact translation request as a known failure for the
  ///     current engine and language pair.
  /// </summary>
  /// <param name="sourceText">The exact sanitized source text.</param>
  /// <param name="sourceLanguage">The normalized source language code.</param>
  /// <param name="targetLanguage">The normalized target language code.</param>
  /// <param name="originContext">The origin context associated with the request.</param>
  private void RecordFailedTranslation(
      string sourceText,
      string sourceLanguage,
      string targetLanguage,
      string failureReason,
      string? originContext)
  {
    if (this.translationEngineId < 0 ||
        this.recordFailedTranslation == null ||
        string.IsNullOrWhiteSpace(sourceText))
    {
      return;
    }

    this.recordFailedTranslation(
        sourceText,
        sourceLanguage,
        targetLanguage,
        this.translationEngineId,
        failureReason,
        originContext);
  }

  /// <summary>
  ///     Resolves the origin context that should be persisted for one failed
  ///     translation request.
  /// </summary>
  /// <param name="originContext">The explicit origin context, if any.</param>
  /// <param name="callerMemberName">The caller member name.</param>
  /// <param name="callerFilePath">The caller file path.</param>
  /// <returns>The best available origin context string.</returns>
  private static string? ResolveOriginContext(
      string? originContext,
      string callerMemberName,
      string callerFilePath)
  {
    if (!string.IsNullOrWhiteSpace(originContext))
    {
      return originContext;
    }

    var callerFileName = Path.GetFileNameWithoutExtension(callerFilePath);
    if (string.IsNullOrWhiteSpace(callerFileName) &&
        string.IsNullOrWhiteSpace(callerMemberName))
    {
      return null;
    }

    if (string.IsNullOrWhiteSpace(callerFileName))
    {
      return callerMemberName;
    }

    if (string.IsNullOrWhiteSpace(callerMemberName))
    {
      return callerFileName;
    }

    return $"{callerFileName}.{callerMemberName}";
  }
}
