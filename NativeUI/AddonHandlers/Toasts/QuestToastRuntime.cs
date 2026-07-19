// <copyright file="QuestToastRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Toasts;

/// <summary>
///     Handles quest toasts through the new toast runtime model while keeping the
///     game UI responsive. Quest toasts are still exposed by Dalamud through
///     <see cref="IToastGui.QuestToast" />, so this runtime uses cache-first
///     lookup, asynchronous translation on cache misses, and per-request overlay
///     lifetime tracking instead of relying on the legacy toast handler partial.
/// </summary>
internal sealed class QuestToastRuntime
{
  private const string QuestToastType = "NonError";
  private const int OverlayLifetimeMs = 5000;

  private readonly Action clearOverlay;
  private readonly Config config;
  private readonly Func<ToastMessage, ToastMessage?> findToastMessage;
  private readonly Func<ToastMessage, Task<string>> insertToastMessageAsync;
  private readonly Func<string, string> normalizeReplacementText;
  private readonly object stateGate = new();
  private readonly TranslationService translationService;
  private readonly Action<string, string, string> updateOverlay;

  private int activeRequestId;
  private string currentOriginalText = string.Empty;
  private QuestToastPosition currentQuestToastPosition =
      QuestToastPosition.Centre;

  /// <summary>
  ///     Initializes a new instance of the <see cref="QuestToastRuntime" />
  ///     class.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The translation service used by the plugin.</param>
  /// <param name="findToastMessage">
  ///     Delegate used to look up previously translated quest toast rows.
  /// </param>
  /// <param name="insertToastMessageAsync">
  ///     Delegate used to persist translated quest toast rows.
  /// </param>
  /// <param name="updateOverlay">
  ///     Delegate used to publish translated content to the quest toast overlay.
  /// </param>
  /// <param name="clearOverlay">
  ///     Delegate used to clear the quest toast overlay state.
  /// </param>
  /// <param name="normalizeReplacementText">
  ///     Delegate used to normalize translated text before native replacement.
  /// </param>
  public QuestToastRuntime(
      Config config,
      TranslationService translationService,
      Func<ToastMessage, ToastMessage?> findToastMessage,
      Func<ToastMessage, Task<string>> insertToastMessageAsync,
      Action<string, string, string> updateOverlay,
      Action clearOverlay,
      Func<string, string> normalizeReplacementText)
  {
    this.config = config;
    this.translationService = translationService;
    this.findToastMessage = findToastMessage;
    this.insertToastMessageAsync = insertToastMessageAsync;
    this.updateOverlay = updateOverlay;
    this.clearOverlay = clearOverlay;
    this.normalizeReplacementText = normalizeReplacementText;
  }

  /// <summary>
  ///     Handles quest-toast callbacks using the new toast runtime. Cache hits
  ///     apply immediately, while cache misses queue translation work in the
  ///     background so the callback never blocks the game UI.
  /// </summary>
  /// <param name="message">The quest toast text payload.</param>
  /// <param name="options">The quest toast options provided by Dalamud.</param>
  /// <param name="isHandled">Whether another handler already consumed the toast.</param>
  public void HandleQuestToast(
      ref SeString message,
      ref QuestToastOptions options,
      ref bool isHandled)
  {
    if (!this.config.TranslateToast || !this.config.TranslateQuestToast)
    {
      return;
    }

    var originalText = message.TextValue;
    if (string.IsNullOrWhiteSpace(originalText))
    {
      return;
    }

    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      return;
    }

    var requestId = this.BeginRequest(originalText, options);
    ToastTranslationDebugLog.Request(
        this.GetCurrentSurfaceIdentity(),
        "IToastGui.QuestToast",
        originalText,
        this.ShouldUseOverlay(),
        this.ShouldApplyNativeText(),
        this.ShouldSwapTexts());
    var lookupToast = this.BuildLookupMessage(originalText, sourceLanguage);
    var storedToast = this.findToastMessage(lookupToast);
    if (storedToast != null &&
        !string.IsNullOrWhiteSpace(storedToast.TranslatedToastMessage))
    {
      ToastTranslationDebugLog.Reuse(
          this.GetCurrentSurfaceIdentity(),
          "IToastGui.QuestToast",
          "db");
      this.ApplyResolvedToast(
          ref message,
          originalText,
          storedToast.TranslatedToastMessage!,
          requestId);
      return;
    }

    this.PublishOverlay(originalText, string.Empty, "IToastGui.QuestToast");
    ToastTranslationDebugLog.Queued(
        this.GetCurrentSurfaceIdentity(),
        "IToastGui.QuestToast",
        requestId);

    Task.Run(() => this.ResolveTranslationAsync(
        originalText,
        requestId,
        sourceLanguage));
  }

  /// <summary>
  ///     Applies a resolved quest toast either to the native toast callback or to
  ///     the overlay path according to the current config.
  /// </summary>
  /// <param name="message">The live toast payload to mutate when applicable.</param>
  /// <param name="originalText">The original quest toast text.</param>
  /// <param name="translatedText">The resolved translated text.</param>
  /// <param name="requestId">The active request identifier.</param>
  private void ApplyResolvedToast(
      ref SeString message,
      string originalText,
      string translatedText,
      int requestId)
  {
    if (this.ShouldUseOverlay())
    {
      this.PublishOverlay(originalText, translatedText, "IToastGui.QuestToast");
      _ = this.ScheduleOverlayClearAsync(requestId);
      if (!this.ShouldSwapTexts())
      {
        return;
      }
    }

    if (!this.ShouldApplyNativeText())
    {
      ToastTranslationDebugLog.Skip(
          this.GetCurrentSurfaceIdentity(),
          "IToastGui.QuestToast",
          "native-disabled");
      return;
    }

    if (!this.ShouldUseOverlay())
    {
      this.clearOverlay();
    }

    var replacementText = this.config.RemoveDiacriticsWhenUsingReplacementQuest
        ? this.normalizeReplacementText(translatedText)
        : translatedText;

    ToastTranslationDebugLog.Apply(
        this.GetCurrentSurfaceIdentity(),
        "IToastGui.QuestToast",
        "native",
        replacementText);
    message = replacementText;
  }

  /// <summary>
  ///     Resolves a quest toast translation asynchronously and publishes or caches
  ///     the result when it still belongs to the latest toast request.
  /// </summary>
  /// <param name="originalText">The original quest toast text.</param>
  /// <param name="requestId">The request identifier used to reject stale updates.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <returns>A task that completes when the translation attempt finishes.</returns>
  private async Task ResolveTranslationAsync(
      string originalText,
      int requestId,
      SourceClientLanguage sourceLanguage)
  {
    string translatedText;
    try
    {
      translatedText = await this.translationService.TranslateAsync(
          originalText,
          sourceLanguage,
          LangDict[LanguageInt].Code,
          originContext: this.GetCurrentSurfaceIdentity()) ?? string.Empty;
    }
    catch (Exception ex)
    {
      ToastTranslationDebugLog.Failure(
          this.GetCurrentSurfaceIdentity(),
          "async-resolve",
          ex.Message);
      return;
    }

    if (string.IsNullOrWhiteSpace(translatedText))
    {
      ToastTranslationDebugLog.Failure(
          this.GetCurrentSurfaceIdentity(),
          "async-resolve",
          "empty-translation");
      return;
    }

    await this.insertToastMessageAsync(
        new ToastMessage(
            QuestToastType,
            originalText,
            sourceLanguage.PersistenceCode,
            translatedText,
            LangDict[LanguageInt].Code,
            this.config.ChosenTransEngine,
            DateTime.Now,
            DateTime.Now));

    if (!this.IsCurrentRequest(requestId, originalText))
    {
      return;
    }

    if (!this.ShouldUseOverlay())
    {
      return;
    }

    this.PublishOverlay(originalText, translatedText, "async-resolve");
    _ = this.ScheduleOverlayClearAsync(requestId);
  }

  /// <summary>
  ///     Delays quest-toast overlay cleanup so the overlay lifetime roughly
  ///     matches the transient nature of native toast messages without depending
  ///     on a legacy polling path.
  /// </summary>
  /// <param name="requestId">
  ///     The request identifier that owns the current overlay contents.
  /// </param>
  /// <returns>A task that completes after the overlay has either been cleared or skipped.</returns>
  private async Task ScheduleOverlayClearAsync(int requestId)
  {
    await Task.Delay(OverlayLifetimeMs);

    lock (this.stateGate)
    {
      if (requestId != this.activeRequestId)
      {
        return;
      }

      this.currentOriginalText = string.Empty;
    }

    this.clearOverlay();
  }

  /// <summary>
  ///     Starts a new quest-toast request and returns the active request ID.
  /// </summary>
  /// <param name="originalText">The original quest toast text.</param>
  /// <returns>The request identifier associated with the quest toast.</returns>
  private int BeginRequest(
      string originalText,
      QuestToastOptions options)
  {
    lock (this.stateGate)
    {
      this.activeRequestId++;
      this.currentOriginalText = originalText;
      this.currentQuestToastPosition = options.Position;
      return this.activeRequestId;
    }
  }

  /// <summary>
  ///     Gets the current quest-toast placement reported by the latest active
  ///     callback-owned quest toast.
  /// </summary>
  /// <returns>The latest quest-toast placement bucket.</returns>
  internal QuestToastPosition GetCurrentQuestToastPosition()
  {
    lock (this.stateGate)
    {
      return this.currentQuestToastPosition;
    }
  }

  /// <summary>
  ///     Gets the placement-specific overlay configuration for the latest
  ///     callback-owned quest toast.
  /// </summary>
  /// <returns>The placement-specific overlay configuration.</returns>
  internal UIOverlays.TranslationOverlay.TranslationWindowConfig
      GetCurrentOverlayConfig()
  {
    return UIOverlays.TranslationOverlay.TranslationWindowConfig
        .FromConfigForQuestToastPlacement(
            this.config,
            this.GetCurrentQuestToastPosition());
  }

  /// <summary>
  ///     Determines whether the request still corresponds to the latest quest
  ///     toast line processed by this runtime.
  /// </summary>
  /// <param name="requestId">The request identifier to validate.</param>
  /// <param name="originalText">The original quest toast text.</param>
  /// <returns>
  ///     <see langword="true" /> when the request is still current; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool IsCurrentRequest(int requestId, string originalText)
  {
    lock (this.stateGate)
    {
      return requestId == this.activeRequestId &&
             string.Equals(
                 this.currentOriginalText,
                 originalText,
                 StringComparison.Ordinal);
    }
  }

  /// <summary>
  ///     Builds the historical quest toast lookup entity that matches the toast DB
  ///     schema already used by the plugin.
  /// </summary>
  /// <param name="originalText">The original quest toast text.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <returns>A lookup <see cref="ToastMessage" /> for quest toast history.</returns>
  private ToastMessage BuildLookupMessage(
      string originalText,
      SourceClientLanguage sourceLanguage)
  {
    return new ToastMessage(
        QuestToastType,
        originalText,
        sourceLanguage.PersistenceCode,
        string.Empty,
        LangDict[LanguageInt].Code,
        this.config.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now);
  }

  /// <summary>
  ///     Determines whether quest toasts should currently use their overlay path.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when quest toasts should render in an overlay;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool ShouldUseOverlay()
  {
    return this.config.TranslateToast &&
           this.config.TranslateQuestToast &&
           TranslationDisplayModeHelper.UsesOverlayPresentation(
               this.GetCurrentDisplayMode(),
               this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Determines whether quest toasts should also replace the native game UI
  ///     while still rendering through the overlay.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when swap mode is active; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool ShouldSwapTexts()
  {
    return this.config.TranslateToast &&
           this.config.TranslateQuestToast &&
           TranslationDisplayModeHelper.ShowsOriginalOverlayText(
               this.GetCurrentDisplayMode(),
               this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Determines whether quest toasts should currently apply translated text
  ///     back into the native game UI.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when native replacement is active; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool ShouldApplyNativeText()
  {
    return this.config.TranslateToast &&
           this.config.TranslateQuestToast &&
           TranslationDisplayModeHelper.WritesNativeTranslation(
               this.GetCurrentDisplayMode(),
               this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Gets the placement-specific display mode for the latest callback-owned
  ///     quest toast.
  /// </summary>
  /// <returns>The placement-specific display mode.</returns>
  private JournalTranslationDisplayMode GetCurrentDisplayMode()
  {
    return this.GetCurrentQuestToastPosition() switch
    {
      QuestToastPosition.Left =>
          this.config.QuestToastLeftTranslationDisplayMode,
      QuestToastPosition.Right =>
          this.config.QuestToastRightTranslationDisplayMode,
      _ => this.config.QuestToastCentreTranslationDisplayMode,
    };
  }

  /// <summary>
  ///     Selects the overlay text for the current quest toast state.
  /// </summary>
  /// <param name="originalText">The original quest toast text.</param>
  /// <param name="translatedText">The translated quest toast text.</param>
  /// <returns>The text that should be shown in the overlay.</returns>
  private string SelectOverlayText(
      string originalText,
      string translatedText)
  {
    if (this.ShouldSwapTexts() &&
        !string.IsNullOrWhiteSpace(originalText))
    {
      return originalText;
    }

    return translatedText;
  }

  /// <summary>
  ///     Publishes quest-toast content to the configured overlay when overlay
  ///     mode is enabled.
  /// </summary>
  /// <param name="originalText">The original quest toast text.</param>
  /// <param name="translatedText">The translated quest toast text.</param>
  /// <param name="trigger">The log trigger label associated with the call.</param>
  private void PublishOverlay(
      string originalText,
      string translatedText,
      string trigger)
  {
    if (!this.ShouldUseOverlay())
    {
      ToastTranslationDebugLog.Skip(
          this.GetCurrentSurfaceIdentity(),
          trigger,
          "overlay-disabled");
      this.clearOverlay();
      return;
    }

    var overlayText = this.SelectOverlayText(originalText, translatedText);
    if (string.IsNullOrWhiteSpace(overlayText))
    {
      ToastTranslationDebugLog.Skip(
          this.GetCurrentSurfaceIdentity(),
          trigger,
          "overlay-text-unavailable");
      this.clearOverlay();
      return;
    }

    ToastTranslationDebugLog.Apply(
        this.GetCurrentSurfaceIdentity(),
        trigger,
        "overlay",
        overlayText);
    this.updateOverlay(string.Empty, overlayText, string.Empty);
  }

  /// <summary>
  ///     Builds the current quest-toast surface identity including placement.
  /// </summary>
  /// <returns>The current surface identity.</returns>
  private string GetCurrentSurfaceIdentity()
  {
    return $"QuestToast/{this.GetCurrentQuestToastPosition()}";
  }
}


