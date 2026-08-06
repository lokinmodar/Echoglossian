// <copyright file="TranslationService.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.DBHelpers;
using Echoglossian.Translators.OpenAI;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Echoglossian.Translators;

/// <summary>
///     Provides translation services using various translation engines.
/// </summary>
public class TranslationService
{
  private const string EmptyResultFailureReason = "empty-result";
  private static readonly TimeSpan TransientFailureTtl = TimeSpan.FromSeconds(30);
  private readonly ConcurrentDictionary<int, byte> describedMetricEngines = new();
  private readonly ConcurrentDictionary<int, ITranslator> translatorsByEngine = new();
  private readonly Action<string>? debugLog;
  private readonly Func<string, string, string, int, bool>? isKnownFailedTranslation;
  private readonly Action<string, string, string, int, string, string?>? recordFailedTranslation;
  private readonly Action<int, TranslationRequestMetricOutcome, TimeSpan, string?, bool>? recordTranslationMetric;
  private readonly Action<string, string, string, int, string, TimeSpan>? recordTransientFailedTranslation;
  private readonly Action<int, TranslationFailureClassification>? reportTranslationFailure;
  private readonly Config? runtimeConfig;
  private readonly IPluginLog? runtimePluginLog;
  private readonly Func<string, string> sanitizeText;
  private readonly Func<string, SourceClientLanguage?> sourceLanguageResolver;
  private readonly int translationEngineId = -1;
  private readonly Func<TranslationSurfaceGroup, TranslatorResolution> translatorResolver;

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
    this.sourceLanguageResolver = ResolveCurrentSourceLanguage;
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
    this.recordTransientFailedTranslation =
        (sourceText, sourceLanguage, targetLanguage, translationEngine, failureReason, ttl) =>
            TranslationFailureCacheManager.RememberTransientFailure(
                sourceText,
                sourceLanguage,
                targetLanguage,
                translationEngine,
                failureReason,
                ttl);
    this.recordTranslationMetric =
        (translationEngine, outcome, latency, failureReason, usedDialogueContext) =>
            TranslatorMetricsCollector.Record(
                translationEngine,
                outcome,
                latency,
                failureReason,
                usedDialogueContext);
    this.reportTranslationFailure =
        Echoglossian.ReportRuntimeTranslationFailure;

    if (chosenEngine == Echoglossian.TransEngines.All)
    {
      this.translatorResolver = _ => new TranslatorResolution(
          this.translationEngineId,
          new UnavailableTranslator());
      return;
    }

    TranslatorMetricsCollector.DescribeEngine(
        this.translationEngineId,
        ResolveMetricsProviderName(chosenEngine, config),
        ResolveMetricsModelName(chosenEngine, config));

    this.runtimeConfig = config;
    this.runtimePluginLog = pluginLog;
    this.translatorsByEngine[this.translationEngineId] =
        this.CreateTranslatorSafely(chosenEngine);
    this.describedMetricEngines.TryAdd(this.translationEngineId, 0);
    this.translatorResolver = this.ResolveConfiguredTranslator;
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
      Action<string, string, string, int, string, string?>? recordFailedTranslation = null,
      Action<int, TranslationRequestMetricOutcome, TimeSpan, string?, bool>? recordTranslationMetric = null,
      Action<string, string, string, int, string, TimeSpan>? recordTransientFailedTranslation = null,
      Action<int, TranslationFailureClassification>? reportTranslationFailure = null,
      Func<TranslationSurfaceGroup, TranslatorResolution>? translatorResolver = null,
      Func<string, SourceClientLanguage?>? sourceLanguageResolver = null)
  {
    this.debugLog = null;
    this.sanitizeText = sanitizeText;
    this.translationEngineId = translationEngine;
    this.isKnownFailedTranslation = isKnownFailedTranslation;
    this.recordFailedTranslation = recordFailedTranslation;
    this.recordTranslationMetric = recordTranslationMetric;
    this.recordTransientFailedTranslation = recordTransientFailedTranslation;
    this.reportTranslationFailure = reportTranslationFailure;
    this.sourceLanguageResolver = sourceLanguageResolver ??
                                  ResolveExplicitSourceLanguage;
    this.translatorResolver = translatorResolver ??
                              (_ => new TranslatorResolution(
                                  this.translationEngineId,
                                  translator));
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
    return this.Translate(
        text,
        sourceLanguage,
        targetLanguage,
        TranslationSurfaceGroup.Default,
        originContext,
        callerMemberName,
        callerFilePath);
  }

  /// <summary>
  ///     Translates the given text from the source language to the target
  ///     language synchronously using the specified translation surface group.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">Source text language.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
  /// <param name="originContext">Optional explicit origin context for diagnostics and persistence.</param>
  /// <param name="callerMemberName">The caller member name when no explicit origin context is provided.</param>
  /// <param name="callerFilePath">The caller file path when no explicit origin context is provided.</param>
  /// <returns>The translated text as a string.</returns>
  public string Translate(
      string text,
      string sourceLanguage,
      string targetLanguage,
      TranslationSurfaceGroup surfaceGroup,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    return this.TranslateCore(
        text,
        sourceLanguage,
        targetLanguage,
        surfaceGroup,
        capturedSourceLanguage: null,
        originContext,
        callerMemberName,
        callerFilePath);
  }

  /// <summary>
  ///     Translates text synchronously using an operation-captured source
  ///     persistence identity and provider code.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">The captured source-language contract.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="originContext">Optional explicit origin context.</param>
  /// <param name="callerMemberName">The caller member name.</param>
  /// <param name="callerFilePath">The caller file path.</param>
  /// <returns>The translated text, or the sanitized source when unresolved.</returns>
  public string Translate(
      string text,
      SourceClientLanguage sourceLanguage,
      string targetLanguage,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    return this.TranslateCore(
        text,
        sourceLanguage.ProviderCode,
        targetLanguage,
        TranslationSurfaceGroup.Default,
        sourceLanguage,
        originContext,
        callerMemberName,
        callerFilePath);
  }

  /// <summary>
  ///     Executes one synchronous translation with a resolved source contract.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">The requested provider source code.</param>
  /// <param name="targetLanguage">The requested target code.</param>
  /// <param name="surfaceGroup">The translation surface group.</param>
  /// <param name="capturedSourceLanguage">The optional captured source contract.</param>
  /// <param name="originContext">The optional origin context.</param>
  /// <param name="callerMemberName">The caller member name.</param>
  /// <param name="callerFilePath">The caller file path.</param>
  /// <returns>The translated text, or sanitized source text on failure.</returns>
  private string TranslateCore(
      string text,
      string sourceLanguage,
      string targetLanguage,
      TranslationSurfaceGroup surfaceGroup,
      SourceClientLanguage? capturedSourceLanguage,
      string? originContext,
      string callerMemberName,
      string callerFilePath)
  {
    var resolvedOriginContext = ResolveOriginContext(
        originContext,
        callerMemberName,
        callerFilePath);
    this.LogTranslationRequest(
        "Translate",
        text,
        sourceLanguage,
        targetLanguage,
        surfaceGroup,
        resolvedOriginContext);

    var (sanitizedText, shouldTranslate) = this.CheckTextToTranslate(text);
    if (!shouldTranslate)
    {
      return sanitizedText;
    }

    if (!this.TryResolveRequestSourceLanguage(
            sourceLanguage,
            capturedSourceLanguage,
            out var resolvedSourceLanguage) ||
        string.IsNullOrWhiteSpace(
            RuntimeLanguageHelper.NormalizeLanguage(targetLanguage)))
    {
      return sanitizedText;
    }

    if (this.ShouldBypassTranslationDueToMissingLanguageAssets(
            surfaceGroup,
            resolvedOriginContext))
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
        RuntimeLanguageHelper.NormalizeLanguage(
            resolvedSourceLanguage.PersistenceCode);
    var normalizedTargetLanguage =
        RuntimeLanguageHelper.NormalizeLanguage(targetLanguage);
    var translatorResolution = this.ResolveTranslator(surfaceGroup);
    if (this.IsKnownFailedTranslation(
            parsedText,
            normalizedSourceLanguage,
            normalizedTargetLanguage,
            translatorResolution.TranslationEngineId))
    {
      this.recordTranslationMetric?.Invoke(
          translatorResolution.TranslationEngineId,
          TranslationRequestMetricOutcome.ShortCircuited,
          TimeSpan.Zero,
          "known-failure-cache",
          false);
      return sanitizedText;
    }

    var stopwatch = Stopwatch.StartNew();
    var finalDialogueText = translatorResolution.Translator.Translate(
        parsedText,
        resolvedSourceLanguage.ProviderCode,
        targetLanguage);
    var acceptanceResult = this.AcceptTranslatedResultOrFallback(
        finalDialogueText,
        parsedText,
        sanitizedText,
        normalizedSourceLanguage,
        normalizedTargetLanguage,
        resolvedOriginContext,
        translatorResolution.TranslationEngineId);
    stopwatch.Stop();
    this.RecordTranslationMetric(
        acceptanceResult,
        stopwatch.Elapsed,
        false,
        translatorResolution.TranslationEngineId);
    finalDialogueText = acceptanceResult.Text;

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
    return await this.TranslateAsync(
        text,
        sourceLanguage,
        targetLanguage,
        null,
        TranslationSurfaceGroup.Default,
        CancellationToken.None,
        originContext,
        callerMemberName,
        callerFilePath).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates text asynchronously while observing cancellation.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">Source text language.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="cancellationToken">Token that cancels waiting for the provider result.</param>
  /// <param name="originContext">Optional explicit origin context for diagnostics and persistence.</param>
  /// <param name="callerMemberName">The caller member name when no explicit origin context is provided.</param>
  /// <param name="callerFilePath">The caller file path when no explicit origin context is provided.</param>
  /// <returns>A task containing the translated or sanitized source text.</returns>
  public async Task<string> TranslateAsync(
      string text,
      string sourceLanguage,
      string targetLanguage,
      CancellationToken cancellationToken,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    return await this.TranslateAsync(
        text,
        sourceLanguage,
        targetLanguage,
        null,
        TranslationSurfaceGroup.Default,
        cancellationToken,
        originContext,
        callerMemberName,
        callerFilePath).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates text asynchronously using an operation-captured source
  ///     persistence identity and provider code.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">The captured source-language contract.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="originContext">Optional explicit origin context.</param>
  /// <param name="callerMemberName">The caller member name.</param>
  /// <param name="callerFilePath">The caller file path.</param>
  /// <returns>A task containing the translated or sanitized source text.</returns>
  public async Task<string> TranslateAsync(
      string text,
      SourceClientLanguage sourceLanguage,
      string targetLanguage,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    return await this.TranslateAsyncCore(
        text,
        sourceLanguage.ProviderCode,
        targetLanguage,
        null,
        TranslationSurfaceGroup.Default,
        sourceLanguage,
        originContext,
        callerMemberName,
        callerFilePath,
        CancellationToken.None).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates text asynchronously using a captured source contract while
  ///     observing cancellation.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">The captured source-language contract.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="cancellationToken">Token that cancels waiting for the provider result.</param>
  /// <param name="originContext">Optional explicit origin context.</param>
  /// <param name="callerMemberName">The caller member name.</param>
  /// <param name="callerFilePath">The caller file path.</param>
  /// <returns>A task containing the translated or sanitized source text.</returns>
  public async Task<string> TranslateAsync(
      string text,
      SourceClientLanguage sourceLanguage,
      string targetLanguage,
      CancellationToken cancellationToken,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    return await this.TranslateAsyncCore(
        text,
        sourceLanguage.ProviderCode,
        targetLanguage,
        null,
        TranslationSurfaceGroup.Default,
        sourceLanguage,
        originContext,
        callerMemberName,
        callerFilePath,
        cancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates the given text from the source language to the target
  ///     language asynchronously using the specified translation surface group.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">Source text language.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
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
      TranslationSurfaceGroup surfaceGroup,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    return await this.TranslateAsync(
        text,
        sourceLanguage,
        targetLanguage,
        null,
        surfaceGroup,
        CancellationToken.None,
        originContext,
        callerMemberName,
        callerFilePath).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates text asynchronously for a surface group while observing cancellation.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">Source text language.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
  /// <param name="cancellationToken">Token that cancels waiting for the provider result.</param>
  /// <param name="originContext">Optional explicit origin context for diagnostics and persistence.</param>
  /// <param name="callerMemberName">The caller member name when no explicit origin context is provided.</param>
  /// <param name="callerFilePath">The caller file path when no explicit origin context is provided.</param>
  /// <returns>A task containing the translated or sanitized source text.</returns>
  public async Task<string> TranslateAsync(
      string text,
      string sourceLanguage,
      string targetLanguage,
      TranslationSurfaceGroup surfaceGroup,
      CancellationToken cancellationToken,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    return await this.TranslateAsync(
        text,
        sourceLanguage,
        targetLanguage,
        null,
        surfaceGroup,
        cancellationToken,
        originContext,
        callerMemberName,
        callerFilePath).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates text asynchronously for a surface group using an
  ///     operation-captured source contract.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">The captured source-language contract.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
  /// <param name="originContext">Optional explicit origin context.</param>
  /// <param name="callerMemberName">The caller member name.</param>
  /// <param name="callerFilePath">The caller file path.</param>
  /// <returns>A task containing the translated or sanitized source text.</returns>
  public async Task<string> TranslateAsync(
      string text,
      SourceClientLanguage sourceLanguage,
      string targetLanguage,
      TranslationSurfaceGroup surfaceGroup,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    return await this.TranslateAsyncCore(
        text,
        sourceLanguage.ProviderCode,
        targetLanguage,
        null,
        surfaceGroup,
        sourceLanguage,
        originContext,
        callerMemberName,
        callerFilePath,
        CancellationToken.None).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates text asynchronously for a surface group using a captured
  ///     source contract while observing cancellation.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">The captured source-language contract.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
  /// <param name="cancellationToken">Token that cancels waiting for the provider result.</param>
  /// <param name="originContext">Optional explicit origin context.</param>
  /// <param name="callerMemberName">The caller member name.</param>
  /// <param name="callerFilePath">The caller file path.</param>
  /// <returns>A task containing the translated or sanitized source text.</returns>
  public async Task<string> TranslateAsync(
      string text,
      SourceClientLanguage sourceLanguage,
      string targetLanguage,
      TranslationSurfaceGroup surfaceGroup,
      CancellationToken cancellationToken,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    return await this.TranslateAsyncCore(
        text,
        sourceLanguage.ProviderCode,
        targetLanguage,
        null,
        surfaceGroup,
        sourceLanguage,
        originContext,
        callerMemberName,
        callerFilePath,
        cancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates text using a translator resolution captured before the
  ///     owning asynchronous operation began.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">The captured source-language contract.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
  /// <param name="translatorResolution">
  /// The engine and translator instance captured for the operation.
  /// </param>
  /// <param name="originContext">Optional explicit origin context.</param>
  /// <returns>A task containing the translated or sanitized source text.</returns>
  internal async Task<string> TranslateAsync(
      string text,
      SourceClientLanguage sourceLanguage,
      string targetLanguage,
      TranslationSurfaceGroup surfaceGroup,
      TranslatorResolution translatorResolution,
      string? originContext = null)
  {
    return await this.TranslateAsyncCore(
        text,
        sourceLanguage.ProviderCode,
        targetLanguage,
        null,
        surfaceGroup,
        sourceLanguage,
        originContext,
        callerMemberName: string.Empty,
        callerFilePath: string.Empty,
        CancellationToken.None,
        translatorResolution).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates text using a captured translator resolution while observing cancellation.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">The captured source-language contract.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
  /// <param name="translatorResolution">The engine and translator instance captured for the operation.</param>
  /// <param name="cancellationToken">Token that cancels waiting for the provider result.</param>
  /// <param name="originContext">Optional explicit origin context.</param>
  /// <returns>A task containing the translated or sanitized source text.</returns>
  internal async Task<string> TranslateAsync(
      string text,
      SourceClientLanguage sourceLanguage,
      string targetLanguage,
      TranslationSurfaceGroup surfaceGroup,
      TranslatorResolution translatorResolution,
      CancellationToken cancellationToken,
      string? originContext = null)
  {
    return await this.TranslateAsyncCore(
        text,
        sourceLanguage.ProviderCode,
        targetLanguage,
        null,
        surfaceGroup,
        sourceLanguage,
        originContext,
        callerMemberName: string.Empty,
        callerFilePath: string.Empty,
        cancellationToken,
        translatorResolution).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates the given text from the source language to the target language
  ///     asynchronously with optional runtime-only short-lived dialogue context.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">Source text language.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="dialogueContext">Optional runtime-only short-lived dialogue context.</param>
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
      DialogueTranslationContext? dialogueContext,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    return await this.TranslateAsync(
        text,
        sourceLanguage,
        targetLanguage,
        dialogueContext,
        TranslationSurfaceGroup.Default,
        CancellationToken.None,
        originContext,
        callerMemberName,
        callerFilePath).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates text asynchronously with dialogue context while observing cancellation.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">Source text language.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="dialogueContext">Optional runtime-only short-lived dialogue context.</param>
  /// <param name="cancellationToken">Token that cancels waiting for the provider result.</param>
  /// <param name="originContext">Optional explicit origin context for diagnostics and persistence.</param>
  /// <param name="callerMemberName">The caller member name when no explicit origin context is provided.</param>
  /// <param name="callerFilePath">The caller file path when no explicit origin context is provided.</param>
  /// <returns>A task containing the translated or sanitized source text.</returns>
  public async Task<string> TranslateAsync(
      string text,
      string sourceLanguage,
      string targetLanguage,
      DialogueTranslationContext? dialogueContext,
      CancellationToken cancellationToken,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    return await this.TranslateAsync(
        text,
        sourceLanguage,
        targetLanguage,
        dialogueContext,
        TranslationSurfaceGroup.Default,
        cancellationToken,
        originContext,
        callerMemberName,
        callerFilePath).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates text asynchronously with dialogue context and an
  ///     operation-captured source contract.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">The captured source-language contract.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="dialogueContext">Optional runtime-only dialogue context.</param>
  /// <param name="originContext">Optional explicit origin context.</param>
  /// <param name="callerMemberName">The caller member name.</param>
  /// <param name="callerFilePath">The caller file path.</param>
  /// <returns>A task containing the translated or sanitized source text.</returns>
  public async Task<string> TranslateAsync(
      string text,
      SourceClientLanguage sourceLanguage,
      string targetLanguage,
      DialogueTranslationContext? dialogueContext,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    return await this.TranslateAsyncCore(
        text,
        sourceLanguage.ProviderCode,
        targetLanguage,
        dialogueContext,
        TranslationSurfaceGroup.Default,
        sourceLanguage,
        originContext,
        callerMemberName,
        callerFilePath,
        CancellationToken.None).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates text asynchronously with dialogue context and a captured
  ///     source contract while observing cancellation.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">The captured source-language contract.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="dialogueContext">Optional runtime-only dialogue context.</param>
  /// <param name="cancellationToken">Token that cancels waiting for the provider result.</param>
  /// <param name="originContext">Optional explicit origin context.</param>
  /// <param name="callerMemberName">The caller member name.</param>
  /// <param name="callerFilePath">The caller file path.</param>
  /// <returns>A task containing the translated or sanitized source text.</returns>
  public async Task<string> TranslateAsync(
      string text,
      SourceClientLanguage sourceLanguage,
      string targetLanguage,
      DialogueTranslationContext? dialogueContext,
      CancellationToken cancellationToken,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    return await this.TranslateAsyncCore(
        text,
        sourceLanguage.ProviderCode,
        targetLanguage,
        dialogueContext,
        TranslationSurfaceGroup.Default,
        sourceLanguage,
        originContext,
        callerMemberName,
        callerFilePath,
        cancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates dialogue text using a translator resolution captured before
  ///     the owning asynchronous operation began.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">The captured source-language contract.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="dialogueContext">Optional runtime-only dialogue context.</param>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
  /// <param name="translatorResolution">
  /// The engine and translator instance captured for the operation.
  /// </param>
  /// <param name="originContext">Optional explicit origin context.</param>
  /// <returns>A task containing the translated or sanitized source text.</returns>
  internal async Task<string> TranslateAsync(
      string text,
      SourceClientLanguage sourceLanguage,
      string targetLanguage,
      DialogueTranslationContext? dialogueContext,
      TranslationSurfaceGroup surfaceGroup,
      TranslatorResolution translatorResolution,
      string? originContext = null)
  {
    return await this.TranslateAsyncCore(
        text,
        sourceLanguage.ProviderCode,
        targetLanguage,
        dialogueContext,
        surfaceGroup,
        sourceLanguage,
        originContext,
        callerMemberName: string.Empty,
        callerFilePath: string.Empty,
        CancellationToken.None,
        translatorResolution).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates dialogue text using a captured translator resolution while
  ///     observing cancellation.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">The captured source-language contract.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="dialogueContext">Optional runtime-only dialogue context.</param>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
  /// <param name="translatorResolution">The engine and translator instance captured for the operation.</param>
  /// <param name="cancellationToken">Token that cancels waiting for the provider result.</param>
  /// <param name="originContext">Optional explicit origin context.</param>
  /// <returns>A task containing the translated or sanitized source text.</returns>
  internal async Task<string> TranslateAsync(
      string text,
      SourceClientLanguage sourceLanguage,
      string targetLanguage,
      DialogueTranslationContext? dialogueContext,
      TranslationSurfaceGroup surfaceGroup,
      TranslatorResolution translatorResolution,
      CancellationToken cancellationToken,
      string? originContext = null)
  {
    return await this.TranslateAsyncCore(
        text,
        sourceLanguage.ProviderCode,
        targetLanguage,
        dialogueContext,
        surfaceGroup,
        sourceLanguage,
        originContext,
        callerMemberName: string.Empty,
        callerFilePath: string.Empty,
        cancellationToken,
        translatorResolution).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates the given text from the source language to the target
  ///     language asynchronously with optional runtime-only short-lived
  ///     dialogue context and a coarse surface-group routing hint.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">Source text language.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="dialogueContext">Optional runtime-only short-lived dialogue context.</param>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
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
      DialogueTranslationContext? dialogueContext,
      TranslationSurfaceGroup surfaceGroup,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    return await this.TranslateAsyncCore(
        text,
        sourceLanguage,
        targetLanguage,
        dialogueContext,
        surfaceGroup,
        capturedSourceLanguage: null,
        originContext,
        callerMemberName,
        callerFilePath,
        CancellationToken.None).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates text asynchronously with dialogue context and surface
  ///     routing while observing cancellation.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">Source text language.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="dialogueContext">Optional runtime-only short-lived dialogue context.</param>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
  /// <param name="cancellationToken">Token that cancels waiting for the provider result.</param>
  /// <param name="originContext">Optional explicit origin context for diagnostics and persistence.</param>
  /// <param name="callerMemberName">The caller member name when no explicit origin context is provided.</param>
  /// <param name="callerFilePath">The caller file path when no explicit origin context is provided.</param>
  /// <returns>A task containing the translated or sanitized source text.</returns>
  public async Task<string> TranslateAsync(
      string text,
      string sourceLanguage,
      string targetLanguage,
      DialogueTranslationContext? dialogueContext,
      TranslationSurfaceGroup surfaceGroup,
      CancellationToken cancellationToken,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    return await this.TranslateAsyncCore(
        text,
        sourceLanguage,
        targetLanguage,
        dialogueContext,
        surfaceGroup,
        capturedSourceLanguage: null,
        originContext,
        callerMemberName,
        callerFilePath,
        cancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates text asynchronously with dialogue and surface routing while
  ///     retaining an operation-captured source contract.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">The captured source-language contract.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="dialogueContext">Optional runtime-only dialogue context.</param>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
  /// <param name="originContext">Optional explicit origin context.</param>
  /// <param name="callerMemberName">The caller member name.</param>
  /// <param name="callerFilePath">The caller file path.</param>
  /// <returns>A task containing the translated or sanitized source text.</returns>
  public async Task<string> TranslateAsync(
      string text,
      SourceClientLanguage sourceLanguage,
      string targetLanguage,
      DialogueTranslationContext? dialogueContext,
      TranslationSurfaceGroup surfaceGroup,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    return await this.TranslateAsyncCore(
        text,
        sourceLanguage.ProviderCode,
        targetLanguage,
        dialogueContext,
        surfaceGroup,
        sourceLanguage,
        originContext,
        callerMemberName,
        callerFilePath,
        CancellationToken.None).ConfigureAwait(false);
  }

  /// <summary>
  ///     Translates text asynchronously with dialogue context, surface routing,
  ///     and a captured source contract while observing cancellation.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">The captured source-language contract.</param>
  /// <param name="targetLanguage">Target translation language.</param>
  /// <param name="dialogueContext">Optional runtime-only dialogue context.</param>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
  /// <param name="cancellationToken">Token that cancels waiting for the provider result.</param>
  /// <param name="originContext">Optional explicit origin context.</param>
  /// <param name="callerMemberName">The caller member name.</param>
  /// <param name="callerFilePath">The caller file path.</param>
  /// <returns>A task containing the translated or sanitized source text.</returns>
  public async Task<string> TranslateAsync(
      string text,
      SourceClientLanguage sourceLanguage,
      string targetLanguage,
      DialogueTranslationContext? dialogueContext,
      TranslationSurfaceGroup surfaceGroup,
      CancellationToken cancellationToken,
      string? originContext = null,
      [CallerMemberName] string callerMemberName = "",
      [CallerFilePath] string callerFilePath = "")
  {
    return await this.TranslateAsyncCore(
        text,
        sourceLanguage.ProviderCode,
        targetLanguage,
        dialogueContext,
        surfaceGroup,
        sourceLanguage,
        originContext,
        callerMemberName,
        callerFilePath,
        cancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  ///     Executes one asynchronous translation with an optional captured source
  ///     contract.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">The requested provider source code.</param>
  /// <param name="targetLanguage">The requested target code.</param>
  /// <param name="dialogueContext">Optional runtime-only dialogue context.</param>
  /// <param name="surfaceGroup">The translation surface group.</param>
  /// <param name="capturedSourceLanguage">The optional captured source contract.</param>
  /// <param name="originContext">The optional origin context.</param>
  /// <param name="callerMemberName">The caller member name.</param>
  /// <param name="callerFilePath">The caller file path.</param>
  /// <returns>A task containing the translated or sanitized source text.</returns>
  private async Task<string> TranslateAsyncCore(
      string text,
      string sourceLanguage,
      string targetLanguage,
      DialogueTranslationContext? dialogueContext,
      TranslationSurfaceGroup surfaceGroup,
      SourceClientLanguage? capturedSourceLanguage,
      string? originContext,
      string callerMemberName,
      string callerFilePath,
      CancellationToken cancellationToken,
      TranslatorResolution? translatorResolution = null)
  {
    var resolvedOriginContext = ResolveOriginContext(
        originContext,
        callerMemberName,
        callerFilePath);
    this.LogTranslationRequest(
        "TranslateAsync",
        text,
        sourceLanguage,
        targetLanguage,
        surfaceGroup,
        resolvedOriginContext);

    var (sanitizedText, shouldTranslate) = this.CheckTextToTranslate(text);
    if (!shouldTranslate)
    {
      return sanitizedText;
    }

    if (!this.TryResolveRequestSourceLanguage(
            sourceLanguage,
            capturedSourceLanguage,
            out var resolvedSourceLanguage) ||
        string.IsNullOrWhiteSpace(
            RuntimeLanguageHelper.NormalizeLanguage(targetLanguage)))
    {
      return sanitizedText;
    }

    if (this.ShouldBypassTranslationDueToMissingLanguageAssets(
            surfaceGroup,
            resolvedOriginContext))
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
        RuntimeLanguageHelper.NormalizeLanguage(
            resolvedSourceLanguage.PersistenceCode);
    var normalizedTargetLanguage =
        RuntimeLanguageHelper.NormalizeLanguage(targetLanguage);
    var resolvedTranslatorResolution = translatorResolution ??
                                       this.ResolveTranslator(surfaceGroup);
    if (this.IsKnownFailedTranslation(
            parsedText,
            normalizedSourceLanguage,
            normalizedTargetLanguage,
            resolvedTranslatorResolution.TranslationEngineId))
    {
      this.recordTranslationMetric?.Invoke(
          resolvedTranslatorResolution.TranslationEngineId,
          TranslationRequestMetricOutcome.ShortCircuited,
          TimeSpan.Zero,
          "known-failure-cache",
          false);
      return sanitizedText;
    }

    var useDialogueContext = this.WillUseDialogueContext(
        dialogueContext,
        resolvedTranslatorResolution);
    var stopwatch = Stopwatch.StartNew();
    var finalDialogueText = useDialogueContext &&
                            resolvedTranslatorResolution.Translator is IDialogueContextAwareTranslator contextAwareTranslator
        ? await contextAwareTranslator.TranslateAsync(
            parsedText,
            resolvedSourceLanguage.ProviderCode,
            targetLanguage,
            dialogueContext!.Value).WaitAsync(cancellationToken).ConfigureAwait(false)
        : await resolvedTranslatorResolution.Translator.TranslateAsync(
            parsedText,
            resolvedSourceLanguage.ProviderCode,
            targetLanguage).WaitAsync(cancellationToken).ConfigureAwait(false);
    var acceptanceResult = this.AcceptTranslatedResultOrFallback(
        finalDialogueText,
        parsedText,
        sanitizedText,
        normalizedSourceLanguage,
        normalizedTargetLanguage,
        resolvedOriginContext,
        resolvedTranslatorResolution.TranslationEngineId);
    stopwatch.Stop();
    this.RecordTranslationMetric(
        acceptanceResult,
        stopwatch.Elapsed,
        useDialogueContext,
        resolvedTranslatorResolution.TranslationEngineId);
    finalDialogueText = acceptanceResult.Text;

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
  /// The accepted translated text plus the aggregated success or failure
  /// outcome used by runtime metrics.
  /// </returns>
  private TranslationAcceptanceResult AcceptTranslatedResultOrFallback(
      string? translatedText,
      string parsedText,
      string sanitizedText,
      string normalizedSourceLanguage,
      string normalizedTargetLanguage,
      string? resolvedOriginContext,
      int translationEngineId)
  {
    if (TranslationResultGuard.IsPersistableTranslation(translatedText))
    {
      return new TranslationAcceptanceResult(
          translatedText!,
          true,
          null);
    }

    if (TranslationFailureTextClassifier.TryClassify(
            translatedText,
            out var classification) &&
        classification != null)
    {
      this.reportTranslationFailure?.Invoke(
          translationEngineId,
          classification);
      if (TranslationPersistenceGuard.IsPersistentFailureReason(
              classification.FailureReason))
      {
        this.RecordFailedTranslation(
            parsedText,
            normalizedSourceLanguage,
            normalizedTargetLanguage,
            classification.FailureReason,
            resolvedOriginContext,
            translationEngineId);
      }
      else
      {
        this.RecordTransientFailedTranslation(
            parsedText,
            normalizedSourceLanguage,
            normalizedTargetLanguage,
            classification.FailureReason,
            translationEngineId);
      }

      return new TranslationAcceptanceResult(
          sanitizedText,
          false,
          classification.FailureReason);
    }

    var failureReason = string.IsNullOrWhiteSpace(translatedText)
        ? EmptyResultFailureReason
        : TranslationResultGuard.SyntheticErrorFailureReason;
    this.RecordFailedTranslation(
        parsedText,
        normalizedSourceLanguage,
        normalizedTargetLanguage,
        failureReason,
        resolvedOriginContext,
        translationEngineId);

    return new TranslationAcceptanceResult(
        sanitizedText,
        false,
        failureReason);
  }

  /// <summary>
  ///     Determines whether the active translator will actually consume the
  ///     supplied runtime-only short-lived dialogue context.
  /// </summary>
  /// <param name="dialogueContext">The optional dialogue context.</param>
  /// <returns>
  ///     <see langword="true" /> when the active translator supports dialogue
  ///     context; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  internal bool WillUseDialogueContext(
      DialogueTranslationContext? dialogueContext,
      TranslationSurfaceGroup surfaceGroup = TranslationSurfaceGroup.Default)
  {
    return this.WillUseDialogueContext(
        dialogueContext,
        this.ResolveTranslator(surfaceGroup));
  }

  /// <summary>
  ///     Determines whether a captured translator resolution will use the
  ///     supplied runtime-only dialogue context.
  /// </summary>
  /// <param name="dialogueContext">The optional dialogue context.</param>
  /// <param name="translatorResolution">
  /// The translator resolution captured for the operation.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> when the captured translator supports
  ///     dialogue context; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  internal bool WillUseDialogueContext(
      DialogueTranslationContext? dialogueContext,
      TranslatorResolution translatorResolution)
  {
    return dialogueContext.HasValue &&
           translatorResolution.Translator is IDialogueContextAwareTranslator;
  }

  /// <summary>
  ///     Captures the exact engine and translator instance for an operation
  ///     that already owns a canonical engine identifier.
  /// </summary>
  /// <param name="translationEngineId">The engine identifier captured by the operation.</param>
  /// <param name="surfaceGroup">The operation's translation surface group.</param>
  /// <returns>The immutable translator resolution for the operation.</returns>
  internal TranslatorResolution CaptureTranslatorResolution(
      int translationEngineId,
      TranslationSurfaceGroup surfaceGroup)
  {
    if (this.runtimeConfig == null)
    {
      var testResolution = this.ResolveTranslator(surfaceGroup);
      return new TranslatorResolution(
          translationEngineId,
          testResolution.Translator);
    }

    var engine = (Echoglossian.TransEngines)translationEngineId;
    if (!Enum.IsDefined(engine) ||
        engine == Echoglossian.TransEngines.All)
    {
      return new TranslatorResolution(
          translationEngineId,
          new UnavailableTranslator());
    }

    this.DescribeMetricsEngineIfNeeded(engine);
    var translator = this.translatorsByEngine.GetOrAdd(
        translationEngineId,
        _ => this.CreateTranslatorSafely(engine));
    return new TranslatorResolution(translationEngineId, translator);
  }

  /// <summary>
  ///     Resolves the effective translation engine identifier for one surface
  ///     group under the active routing policy.
  /// </summary>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
  /// <returns>The effective translation engine identifier.</returns>
  public int GetEffectiveTranslationEngineId(
      TranslationSurfaceGroup surfaceGroup = TranslationSurfaceGroup.Default)
  {
    return this.ResolveTranslator(surfaceGroup).TranslationEngineId;
  }

  /// <summary>
  /// Determines whether the current selected language depends on missing
  /// downloaded font assets and should therefore bypass translation work until
  /// those assets are available.
  /// </summary>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
  /// <param name="originContext">The resolved surface or caller context.</param>
  /// <returns>
  /// <c>true</c> when translation should be bypassed because required language
  /// assets are missing; otherwise, <c>false</c>.
  /// </returns>
  private bool ShouldBypassTranslationDueToMissingLanguageAssets(
      TranslationSurfaceGroup surfaceGroup,
      string? originContext)
  {
    if (!AssetsManager.HasMissingRequiredAssets(SelectedLanguage))
    {
      return false;
    }

    this.debugLog?.Invoke(
        $"[{GetSurfaceScope(surfaceGroup, originContext)}] TranslationService: bypassing translation because the selected language requires missing downloaded font assets.");
    return true;
  }

  /// <summary>
  ///     Writes a translation request diagnostic with the best known surface
  ///     or element identity rendered as a square-bracketed scope.
  /// </summary>
  /// <param name="operation">The translation-service operation name.</param>
  /// <param name="text">The source text requested for translation.</param>
  /// <param name="sourceLanguage">The requested provider source code.</param>
  /// <param name="targetLanguage">The requested target language code.</param>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
  /// <param name="originContext">The resolved surface or caller context.</param>
  private void LogTranslationRequest(
      string operation,
      string text,
      string sourceLanguage,
      string targetLanguage,
      TranslationSurfaceGroup surfaceGroup,
      string? originContext)
  {
    if (this.debugLog == null)
    {
      return;
    }

    var message =
        $"TranslationService: {operation} called with text: {text}, sourceLanguage: {sourceLanguage}, targetLanguage: {targetLanguage}, surfaceGroup: {surfaceGroup}";
    var surfaceScope = GetSurfaceScope(surfaceGroup, originContext);
    this.debugLog($"[{surfaceScope}] {message}");
  }

  /// <summary>
  ///     Resolves the square-bracketed diagnostic scope for one translation
  ///     request.
  /// </summary>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
  /// <param name="originContext">The resolved surface or caller context.</param>
  /// <returns>The best available diagnostic scope.</returns>
  private static string GetSurfaceScope(
      TranslationSurfaceGroup surfaceGroup,
      string? originContext)
  {
    return string.IsNullOrWhiteSpace(originContext)
        ? surfaceGroup.ToString()
        : originContext;
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
  ///     Resolves the persistence and provider identities for one request.
  /// </summary>
  /// <param name="requestedSourceCode">The source code requested by the caller.</param>
  /// <param name="capturedSourceLanguage">The optional operation-captured source.</param>
  /// <param name="sourceLanguage">The validated source contract.</param>
  /// <returns><see langword="true" /> when the source contract is complete.</returns>
  private bool TryResolveRequestSourceLanguage(
      string requestedSourceCode,
      SourceClientLanguage? capturedSourceLanguage,
      out SourceClientLanguage sourceLanguage)
  {
    SourceClientLanguage? resolved;
    if (capturedSourceLanguage.HasValue)
    {
      if (!IsKnownCapturedSourceLanguage(capturedSourceLanguage.Value))
      {
        sourceLanguage = default;
        return false;
      }

      resolved = capturedSourceLanguage;
    }
    else
    {
      resolved = this.sourceLanguageResolver(requestedSourceCode);
    }

    if (!resolved.HasValue ||
        string.IsNullOrWhiteSpace(resolved.Value.PersistenceCode) ||
        string.IsNullOrWhiteSpace(resolved.Value.ProviderCode) ||
        (!RuntimeLanguageHelper.LanguagesMatch(
             resolved.Value.ProviderCode,
             requestedSourceCode) &&
         !RuntimeLanguageHelper.LanguagesMatch(
             resolved.Value.PersistenceCode,
             requestedSourceCode)))
    {
      sourceLanguage = default;
      return false;
    }

    sourceLanguage = resolved.Value with
    {
      PersistenceCode = RuntimeLanguageHelper.NormalizeLanguage(
          resolved.Value.PersistenceCode),
    };
    return !string.IsNullOrWhiteSpace(sourceLanguage.PersistenceCode);
  }

  /// <summary>
  ///     Verifies that a captured source contract matches one host-defined
  ///     client language without consulting mutable current-client state.
  /// </summary>
  /// <param name="sourceLanguage">The captured source contract.</param>
  /// <returns><see langword="true" /> when the contract is known and consistent.</returns>
  internal static bool IsKnownCapturedSourceLanguage(
      SourceClientLanguage sourceLanguage)
  {
    for (var rawLanguage = 0; rawLanguage <= 7; rawLanguage++)
    {
      if (CapturedSourceMatchesClientLanguage(
              (ClientLanguage)rawLanguage,
              sourceLanguage))
      {
        return true;
      }
    }

    foreach (var clientLanguage in Enum.GetValues<ClientLanguage>())
    {
      if ((int)clientLanguage > 7 &&
          CapturedSourceMatchesClientLanguage(
              clientLanguage,
              sourceLanguage))
      {
        return true;
      }
    }

    return false;
  }

  /// <summary>
  ///     Compares one captured source contract with a raw client-language
  ///     identity supported by the runtime helper.
  /// </summary>
  /// <param name="clientLanguage">The raw client-language identity.</param>
  /// <param name="sourceLanguage">The captured source contract.</param>
  /// <returns><see langword="true" /> when both identities match.</returns>
  private static bool CapturedSourceMatchesClientLanguage(
      ClientLanguage clientLanguage,
      SourceClientLanguage sourceLanguage)
  {
    return RuntimeLanguageHelper.TryResolveSourceLanguage(
               clientLanguage,
               out var knownSourceLanguage) &&
           RuntimeLanguageHelper.LanguagesMatch(
               knownSourceLanguage.PersistenceCode,
               sourceLanguage.PersistenceCode) &&
           RuntimeLanguageHelper.LanguagesMatch(
               knownSourceLanguage.ProviderCode,
               sourceLanguage.ProviderCode);
  }

  /// <summary>
  ///     Resolves a legacy provider-code request against the active client
  ///     source so failure identity remains canonical in production.
  /// </summary>
  /// <param name="requestedSourceCode">The requested provider or persistence code.</param>
  /// <returns>The resolved source contract, or <see langword="null" />.</returns>
  private static SourceClientLanguage? ResolveCurrentSourceLanguage(
      string requestedSourceCode)
  {
    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage) ||
        (!RuntimeLanguageHelper.LanguagesMatch(
             sourceLanguage.ProviderCode,
             requestedSourceCode) &&
         !RuntimeLanguageHelper.LanguagesMatch(
             sourceLanguage.PersistenceCode,
             requestedSourceCode)))
    {
      return null;
    }

    return sourceLanguage;
  }

  /// <summary>
  ///     Preserves the explicit string-based source behavior for isolated
  ///     translation-service tests.
  /// </summary>
  /// <param name="requestedSourceCode">The requested source code.</param>
  /// <returns>The test source contract, or <see langword="null" />.</returns>
  private static SourceClientLanguage? ResolveExplicitSourceLanguage(
      string requestedSourceCode)
  {
    var persistenceCode =
        RuntimeLanguageHelper.NormalizeLanguage(requestedSourceCode);
    return string.IsNullOrWhiteSpace(persistenceCode)
        ? null
        : new SourceClientLanguage(
            persistenceCode,
            requestedSourceCode);
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
      string targetLanguage,
      int translationEngineId)
  {
    if (translationEngineId < 0 ||
        this.isKnownFailedTranslation == null ||
        string.IsNullOrWhiteSpace(sourceText))
    {
      return false;
    }

    return this.isKnownFailedTranslation(
        sourceText,
        sourceLanguage,
        targetLanguage,
        translationEngineId);
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
      string? originContext,
      int translationEngineId)
  {
    if (translationEngineId < 0 ||
        this.recordFailedTranslation == null ||
        string.IsNullOrWhiteSpace(sourceText))
    {
      return;
    }

    this.recordFailedTranslation(
        sourceText,
        sourceLanguage,
        targetLanguage,
        translationEngineId,
        failureReason,
        originContext);
  }

  /// <summary>
  ///     Records one exact translation request as a transient runtime-only
  ///     failure for the current engine and language pair.
  /// </summary>
  /// <param name="sourceText">The exact sanitized source text.</param>
  /// <param name="sourceLanguage">The normalized source language code.</param>
  /// <param name="targetLanguage">The normalized target language code.</param>
  /// <param name="failureReason">The normalized failure reason.</param>
  private void RecordTransientFailedTranslation(
      string sourceText,
      string sourceLanguage,
      string targetLanguage,
      string failureReason,
      int translationEngineId)
  {
    if (translationEngineId < 0 ||
        this.recordTransientFailedTranslation == null ||
        string.IsNullOrWhiteSpace(sourceText) ||
        string.IsNullOrWhiteSpace(failureReason))
    {
      return;
    }

    this.recordTransientFailedTranslation(
        sourceText,
        sourceLanguage,
        targetLanguage,
        translationEngineId,
        failureReason,
        TransientFailureTtl);
  }

  /// <summary>
  ///     Records one aggregated translation-service metrics outcome.
  /// </summary>
  /// <param name="acceptanceResult">The accepted translation outcome.</param>
  /// <param name="elapsed">The elapsed live translation latency.</param>
  /// <param name="usedDialogueContext">
  /// Whether the live request consumed runtime-only short-lived dialogue context.
  /// </param>
  private void RecordTranslationMetric(
      TranslationAcceptanceResult acceptanceResult,
      TimeSpan elapsed,
      bool usedDialogueContext,
      int translationEngineId)
  {
    this.recordTranslationMetric?.Invoke(
        translationEngineId,
        acceptanceResult.Succeeded
            ? TranslationRequestMetricOutcome.Success
            : TranslationRequestMetricOutcome.Failure,
        elapsed,
        acceptanceResult.FailureReason,
        usedDialogueContext);
  }

  /// <summary>
  ///     Resolves the provider label shown by the translator metrics debugger.
  /// </summary>
  /// <param name="engine">The configured translation engine.</param>
  /// <returns>The provider family label.</returns>
  private static string ResolveMetricsProviderName(
      Echoglossian.TransEngines engine,
      Config config)
  {
    return engine switch
    {
      Echoglossian.TransEngines.ChatGPT =>
          OpenAiProviderVariantHelper.ResolveActiveSettings(config).ProviderName,
      Echoglossian.TransEngines.Claude => "Anthropic",
      Echoglossian.TransEngines.DeepSeek => "DeepSeek",
      Echoglossian.TransEngines.Gemini => "Google Gemini",
      Echoglossian.TransEngines.OpenRouter => "OpenRouter",
      Echoglossian.TransEngines.LmStudio => "LM Studio",
      Echoglossian.TransEngines.Ollama => "Ollama",
      _ => engine.ToString(),
    };
  }

  /// <summary>
  ///     Resolves the configured model label shown by the translator metrics
  ///     debugger.
  /// </summary>
  /// <param name="engine">The configured translation engine.</param>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>The configured model label, or <see langword="null" /> when not applicable.</returns>
  private static string? ResolveMetricsModelName(
      Echoglossian.TransEngines engine,
      Config config)
  {
    return engine switch
    {
      Echoglossian.TransEngines.ChatGPT =>
          OpenAiProviderVariantHelper.ResolveActiveSettings(config).Model,
      Echoglossian.TransEngines.Claude => config.ClaudeModel,
      Echoglossian.TransEngines.DeepSeek => config.DeepSeekModel,
      Echoglossian.TransEngines.Gemini => string.IsNullOrWhiteSpace(config.GeminiModelId)
          ? config.GeminiModel
          : config.GeminiModelId,
      Echoglossian.TransEngines.OpenRouter => config.OpenRouterModel,
      Echoglossian.TransEngines.LmStudio => config.LmStudioModel,
      Echoglossian.TransEngines.Ollama => config.OllamaModel,
      Echoglossian.TransEngines.Amazon => config.AwsTranslateModel,
      Echoglossian.TransEngines.Microsoft => config.MicrosoftTranslatorModel,
      _ => null,
    };
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

  private readonly record struct TranslationAcceptanceResult(
      string Text,
      bool Succeeded,
      string? FailureReason);

  /// <summary>
  ///     Resolves the effective translator and engine id for one request.
  /// </summary>
  /// <param name="surfaceGroup">The incoming translation surface group.</param>
  /// <returns>The translator resolution for the request.</returns>
  private TranslatorResolution ResolveTranslator(TranslationSurfaceGroup surfaceGroup)
  {
    return this.translatorResolver(surfaceGroup);
  }

  /// <summary>
  ///     Resolves the configured translator path for one surface group using
  ///     the active routing policy.
  /// </summary>
  /// <param name="surfaceGroup">The incoming translation surface group.</param>
  /// <returns>The translator resolution for the request.</returns>
  private TranslatorResolution ResolveConfiguredTranslator(
      TranslationSurfaceGroup surfaceGroup)
  {
    if (this.runtimeConfig == null)
    {
      return new TranslatorResolution(
          this.translationEngineId,
          new UnavailableTranslator());
    }

    var resolvedEngine =
        LlmSurfaceGroupRoutingPolicy.ResolveEngine(
            this.runtimeConfig,
            surfaceGroup);
    var resolvedEngineId = (int)resolvedEngine;
    this.DescribeMetricsEngineIfNeeded(resolvedEngine);
    var translator = this.translatorsByEngine.GetOrAdd(
        resolvedEngineId,
        _ => this.CreateTranslatorSafely(resolvedEngine));
    return new TranslatorResolution(
        resolvedEngineId,
        translator);
  }

  /// <summary>
  ///     Describes the specified engine to the translator metrics collector one
  ///     time per runtime translation service instance.
  /// </summary>
  /// <param name="engine">The engine to describe.</param>
  private void DescribeMetricsEngineIfNeeded(Echoglossian.TransEngines engine)
  {
    if (this.runtimeConfig == null)
    {
      return;
    }

    var engineId = (int)engine;
    if (!this.describedMetricEngines.TryAdd(engineId, 0))
    {
      return;
    }

    TranslatorMetricsCollector.DescribeEngine(
        engineId,
        ResolveMetricsProviderName(engine, this.runtimeConfig),
        ResolveMetricsModelName(engine, this.runtimeConfig));
  }

  /// <summary>
  ///     Creates one translator instance for the specified engine while
  ///     keeping the shared service alive if one engine constructor fails.
  /// </summary>
  /// <param name="engine">The engine to instantiate.</param>
  /// <returns>The created translator, or a safe unavailable translator.</returns>
  private ITranslator CreateTranslatorSafely(Echoglossian.TransEngines engine)
  {
    if (this.runtimeConfig == null || this.runtimePluginLog == null)
    {
      return new UnavailableTranslator();
    }

    try
    {
      return TranslatorFactory.Create(
          engine,
          this.runtimeConfig,
          this.runtimePluginLog);
    }
    catch (Exception ex)
    {
      PluginRuntimeLog.Error(
          $"Failed to initialize translator for engine {engine}: {ex}");
      return new UnavailableTranslator();
    }
  }

  /// <summary>
  ///     Holds the per-request effective engine and translator selected by the
  ///     routing policy.
  /// </summary>
  /// <param name="TranslationEngineId">The effective translation engine identifier.</param>
  /// <param name="Translator">The effective translator instance.</param>
  internal readonly record struct TranslatorResolution(
      int TranslationEngineId,
      ITranslator Translator);
}
