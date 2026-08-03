// <copyright file="ToastGuiCaptureRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Toasts;

/// <summary>
///     Uses Dalamud's <see cref="IToastGui" /> callbacks to capture supported
///     normal and error toast source payloads before the addon-node handlers
///     read the live UI.
/// </summary>
/// <remarks>
///     This runtime is intentionally conservative. It does not replace the
///     addon-handler presentation path, and it does not publish overlays or
///     mutate native nodes. It only prefetches translations into the existing
///     toast persistence path so the addon handlers can later reuse the same
///     cache/DB semantics with lower latency and less dependence on reading a
///     live node that may already be mutated.
/// </remarks>
internal sealed class ToastGuiCaptureRuntime
{
  private const string ErrorToastType = "Error";
  private const string NonErrorToastType = "NonError";

  private readonly Config config;
  private readonly Func<ToastMessage, ToastMessage?> findToastMessage;
  private readonly object stateGate = new();
  private readonly Func<ToastMessage, Task<string>> insertToastMessageAsync;
  private readonly HashSet<string> inFlightKeys = [];
  private readonly TranslationService translationService;

  /// <summary>
  ///     Initializes a new instance of the
  ///     <see cref="ToastGuiCaptureRuntime" /> class.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The shared translation service.</param>
  /// <param name="findToastMessage">
  ///     Delegate used to look up previously translated toast rows.
  /// </param>
  /// <param name="insertToastMessageAsync">
  ///     Delegate used to persist translated toast rows.
  /// </param>
  public ToastGuiCaptureRuntime(
      Config config,
      TranslationService translationService,
      Func<ToastMessage, ToastMessage?> findToastMessage,
      Func<ToastMessage, Task<string>> insertToastMessageAsync)
  {
    this.config = config;
    this.translationService = translationService;
    this.findToastMessage = findToastMessage;
    this.insertToastMessageAsync = insertToastMessageAsync;
  }

  /// <summary>
  ///     Handles generic normal-toast callbacks from Dalamud and opportunistically
  ///     prefetches translations for supported addon-backed toast surfaces.
  /// </summary>
  /// <param name="message">The toast payload.</param>
  /// <param name="options">The generic toast options.</param>
  /// <param name="isHandled">
  ///     Whether another callback already consumed the toast.
  /// </param>
  public void HandleNormalToast(
      ref SeString message,
      ref ToastOptions options,
      ref bool isHandled)
  {
    if (!this.ShouldCaptureNormalToasts())
    {
      return;
    }

    this.TryPrefetchToast(
        NonErrorToastType,
        message.TextValue);
  }

  /// <summary>
  ///     Handles error-toast callbacks from Dalamud and opportunistically
  ///     prefetches translations for the native error-toast addon.
  /// </summary>
  /// <param name="message">The toast payload.</param>
  /// <param name="isHandled">
  ///     Whether another callback already consumed the toast.
  /// </param>
  public void HandleErrorToast(
      ref SeString message,
      ref bool isHandled)
  {
    if (!this.ShouldCaptureErrorToasts())
    {
      return;
    }

    this.TryPrefetchToast(
        ErrorToastType,
        message.TextValue);
  }

  /// <summary>
  ///     Determines whether the experimental ToastGui-assisted capture path
  ///     should run for normal toasts.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when supported normal toasts should be
  ///     prefetched from ToastGui; otherwise, <see langword="false" />.
  /// </returns>
  private bool ShouldCaptureNormalToasts()
  {
    return ToastGuiSupportedToastPolicy.UseLegacyNormalToastCapturePrefetch(
        this.config);
  }

  /// <summary>
  ///     Determines whether the experimental ToastGui-assisted capture path
  ///     should run for error toasts.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when error toasts should be prefetched from
  ///     ToastGui; otherwise, <see langword="false" />.
  /// </returns>
  private bool ShouldCaptureErrorToasts()
  {
    return ToastGuiSupportedToastPolicy.UseLegacyErrorToastCapturePrefetch(
        this.config);
  }

  /// <summary>
  ///     Prefetches one toast source line into the existing translation
  ///     persistence path when no usable translated row already exists.
  /// </summary>
  /// <param name="toastType">The persisted toast type.</param>
  /// <param name="originalText">The original toast text.</param>
  private void TryPrefetchToast(
      string toastType,
      string originalText)
  {
    if (string.IsNullOrWhiteSpace(originalText))
    {
      return;
    }

    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      return;
    }

    var lookupToast = this.BuildLookupMessage(
        toastType,
        originalText,
        sourceLanguage);
    var storedToast = this.findToastMessage(lookupToast);
    if (this.IsStoredTranslationUsable(
            storedToast,
            originalText,
            toastType))
    {
      return;
    }

    if (!this.TryBeginPrefetch(
            toastType,
            originalText,
            sourceLanguage,
            out var inFlightKey))
    {
      return;
    }

    Task.Run(() => this.ResolveTranslationAsync(
        toastType,
        originalText,
        inFlightKey,
        sourceLanguage));
  }

  /// <summary>
  ///     Starts one background prefetch request when the same toast line is not
  ///     already being resolved.
  /// </summary>
  /// <param name="toastType">The persisted toast type.</param>
  /// <param name="originalText">The original toast text.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <param name="inFlightKey">
  ///     Receives the unique in-flight key for the request.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> when a new request should be queued;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryBeginPrefetch(
      string toastType,
      string originalText,
      SourceClientLanguage sourceLanguage,
      out string inFlightKey)
  {
    inFlightKey = this.BuildInFlightKey(
        toastType,
        originalText,
        sourceLanguage);

    lock (this.stateGate)
    {
      return this.inFlightKeys.Add(inFlightKey);
    }
  }

  /// <summary>
  ///     Resolves one toast translation asynchronously and persists it into the
  ///     existing toast history tables when successful.
  /// </summary>
  /// <param name="toastType">The persisted toast type.</param>
  /// <param name="originalText">The original toast text.</param>
  /// <param name="inFlightKey">The key representing the queued request.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <returns>A task that completes when the translation attempt finishes.</returns>
  private async Task ResolveTranslationAsync(
      string toastType,
      string originalText,
      string inFlightKey,
      SourceClientLanguage sourceLanguage)
  {
    try
    {
      var translatedText = await this.translationService.TranslateAsync(
          originalText,
          sourceLanguage,
          LangDict[LanguageInt].Code,
          originContext: $"ToastGuiCapture/{toastType}") ?? string.Empty;
      if (string.IsNullOrWhiteSpace(translatedText))
      {
        return;
      }

      await this.insertToastMessageAsync(
          new ToastMessage(
              toastType,
              originalText,
              sourceLanguage.PersistenceCode,
              translatedText,
              LangDict[LanguageInt].Code,
              this.config.ChosenTransEngine,
              DateTime.Now,
              DateTime.Now));
    }
    finally
    {
      lock (this.stateGate)
      {
        this.inFlightKeys.Remove(inFlightKey);
      }
    }
  }

  /// <summary>
  ///     Builds a lookup entity matching the historical toast schema already
  ///     used by the plugin database.
  /// </summary>
  /// <param name="toastType">The persisted toast type.</param>
  /// <param name="originalText">The original toast text.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <returns>A formatted <see cref="ToastMessage" /> for DB lookup.</returns>
  private ToastMessage BuildLookupMessage(
      string toastType,
      string originalText,
      SourceClientLanguage sourceLanguage)
  {
    return new ToastMessage(
        toastType,
        originalText,
        sourceLanguage.PersistenceCode,
        string.Empty,
        LangDict[LanguageInt].Code,
        this.config.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now);
  }

  /// <summary>
  ///     Determines whether a stored toast row already contains a usable
  ///     translation for the observed callback payload.
  /// </summary>
  /// <param name="toastMessage">The stored row to validate.</param>
  /// <param name="originalText">The expected original toast text.</param>
  /// <param name="toastType">The persisted toast type.</param>
  /// <returns>
  ///     <see langword="true" /> when the stored row can be reused; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool IsStoredTranslationUsable(
      ToastMessage? toastMessage,
      string originalText,
      string toastType)
  {
    return toastMessage != null &&
           string.Equals(
               toastMessage.ToastType,
               toastType,
               StringComparison.OrdinalIgnoreCase) &&
           string.Equals(
               toastMessage.OriginalToastMessage,
               originalText,
               StringComparison.Ordinal) &&
           !string.IsNullOrWhiteSpace(
               toastMessage.TranslatedToastMessage);
  }

  /// <summary>
  ///     Builds the in-flight key used to deduplicate concurrent callback
  ///     prefetch requests.
  /// </summary>
  /// <param name="toastType">The persisted toast type.</param>
  /// <param name="originalText">The original toast text.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <returns>The stable in-flight dedupe key.</returns>
  private string BuildInFlightKey(
      string toastType,
      string originalText,
      SourceClientLanguage sourceLanguage)
  {
    return
        $"{toastType}|{sourceLanguage.PersistenceCode}|{LangDict[LanguageInt].Code}|{this.config.ChosenTransEngine}|{originalText}";
  }
}
