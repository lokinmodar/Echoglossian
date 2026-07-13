// <copyright file="ToastGuiSupportedToastRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Toasts;

/// <summary>
///     Owns the alternate callback-driven ToastGui route for supported normal
///     and error toasts. Under this path, supported normal toasts are treated
///     as one logical family and no longer rely on addon-specific native
///     mutation or overlay anchoring.
/// </summary>
internal sealed class ToastGuiSupportedToastRuntime
{
  private const string ErrorToastType = "Error";
  private const string NormalToastType = "NonError";
  private const int ErrorOverlayLifetimeMs = 5000;
  private const int FastNormalOverlayLifetimeMs = 3500;
  private const int SlowNormalOverlayLifetimeMs = 5500;

  private readonly Action clearErrorOverlay;
  private readonly Action clearNormalOverlay;
  private readonly Config config;
  private readonly Func<ToastMessage, ToastMessage?> findToastMessage;
  private readonly Func<ToastMessage, Task<string>> insertToastMessageAsync;
  private readonly Func<string, string> normalizeReplacementText;
  private readonly object stateGate = new();
  private readonly TranslationService translationService;
  private readonly Action<string, string, string> updateErrorOverlay;
  private readonly Action<string, string, string> updateNormalOverlay;

  private int activeErrorRequestId;
  private int activeNormalRequestId;
  private string currentErrorOriginalText = string.Empty;
  private string currentNormalOriginalText = string.Empty;
  private ToastPosition currentNormalToastPosition = ToastPosition.Top;
  private ToastSpeed currentNormalToastSpeed = ToastSpeed.Fast;

  /// <summary>
  ///     Initializes a new instance of the
  ///     <see cref="ToastGuiSupportedToastRuntime" /> class.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The shared translation service.</param>
  /// <param name="findToastMessage">The shared toast DB lookup delegate.</param>
  /// <param name="insertToastMessageAsync">
  ///     The shared toast persistence delegate.
  /// </param>
  /// <param name="updateNormalOverlay">
  ///     Delegate used to publish content into the unified normal-toast
  ///     overlay.
  /// </param>
  /// <param name="clearNormalOverlay">
  ///     Delegate used to clear the unified normal-toast overlay.
  /// </param>
  /// <param name="updateErrorOverlay">
  ///     Delegate used to publish content into the error-toast overlay.
  /// </param>
  /// <param name="clearErrorOverlay">
  ///     Delegate used to clear the error-toast overlay.
  /// </param>
  /// <param name="normalizeReplacementText">
  ///     Delegate used to normalize text before native replacement when the
  ///     game font requires it.
  /// </param>
  public ToastGuiSupportedToastRuntime(
      Config config,
      TranslationService translationService,
      Func<ToastMessage, ToastMessage?> findToastMessage,
      Func<ToastMessage, Task<string>> insertToastMessageAsync,
      Action<string, string, string> updateNormalOverlay,
      Action clearNormalOverlay,
      Action<string, string, string> updateErrorOverlay,
      Action clearErrorOverlay,
      Func<string, string> normalizeReplacementText)
  {
    this.config = config;
    this.translationService = translationService;
    this.findToastMessage = findToastMessage;
    this.insertToastMessageAsync = insertToastMessageAsync;
    this.updateNormalOverlay = updateNormalOverlay;
    this.clearNormalOverlay = clearNormalOverlay;
    this.updateErrorOverlay = updateErrorOverlay;
    this.clearErrorOverlay = clearErrorOverlay;
    this.normalizeReplacementText = normalizeReplacementText;
  }

  /// <summary>
  ///     Handles generic normal-toast callbacks through the alternate family
  ///     route.
  /// </summary>
  /// <param name="message">The toast payload.</param>
  /// <param name="options">The toast options supplied by Dalamud.</param>
  /// <param name="isHandled">Whether another callback consumed the toast.</param>
  public void HandleNormalToast(
      ref SeString message,
      ref ToastOptions options,
      ref bool isHandled)
  {
    if (!ToastGuiSupportedToastPolicy.UseSupportedNormalToastRuntime(
            this.config))
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

    var requestId = this.BeginNormalRequest(originalText, options);
    var storedToast = this.findToastMessage(
        this.BuildLookupMessage(
            NormalToastType,
            originalText,
            sourceLanguage));
    if (this.IsStoredTranslationUsable(
            storedToast,
            originalText,
            NormalToastType))
    {
      this.ApplyResolvedNormalToast(
          ref message,
          originalText,
          storedToast!.TranslatedToastMessage!,
          requestId);
      return;
    }

    this.PublishNormalOverlay(originalText, string.Empty, "IToastGui.Toast");
    Task.Run(() => this.ResolveNormalTranslationAsync(
        originalText,
        requestId,
        sourceLanguage));
  }

  /// <summary>
  ///     Handles error-toast callbacks through the alternate callback-owned
  ///     route.
  /// </summary>
  /// <param name="message">The error-toast payload.</param>
  /// <param name="isHandled">Whether another callback consumed the toast.</param>
  public void HandleErrorToast(
      ref SeString message,
      ref bool isHandled)
  {
    if (!ToastGuiSupportedToastPolicy.UseSupportedErrorToastRuntime(
            this.config))
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

    var requestId = this.BeginErrorRequest(originalText);
    var storedToast = this.findToastMessage(
        this.BuildLookupMessage(
            ErrorToastType,
            originalText,
            sourceLanguage));
    if (this.IsStoredTranslationUsable(
            storedToast,
            originalText,
            ErrorToastType))
    {
      this.ApplyResolvedErrorToast(
          ref message,
          originalText,
          storedToast!.TranslatedToastMessage!,
          requestId);
      return;
    }

    this.PublishErrorOverlay(originalText, string.Empty, "IToastGui.ErrorToast");
    Task.Run(() => this.ResolveErrorTranslationAsync(
        originalText,
        requestId,
        sourceLanguage));
  }

  /// <summary>
  ///     Synchronizes the unified normal-toast overlay to a stable viewport
  ///     anchor based on the last callback-reported toast position.
  /// </summary>
  /// <param name="overlay">The overlay to synchronize.</param>
  /// <returns>
  ///     <see langword="true" /> when the overlay currently has visible
  ///     content; otherwise, <see langword="false" />.
  /// </returns>
  public bool TrySyncNormalToastOverlayToViewport(TranslationOverlay overlay)
  {
    overlay.Semaphore.Wait();
    var shouldDisplay = overlay.Display;
    overlay.Semaphore.Release();
    if (!shouldDisplay)
    {
      return false;
    }

    ToastPosition toastPosition;
    lock (this.stateGate)
    {
      toastPosition = this.currentNormalToastPosition;
    }

    var viewport = ImGui.GetMainViewport();
    var width = viewport.Size.X * 0.35f;
    var height = 56f;
    var positionY = toastPosition == ToastPosition.Bottom
        ? viewport.Pos.Y + (viewport.Size.Y * 0.84f)
        : viewport.Pos.Y + (viewport.Size.Y * 0.14f);

    overlay.Position = new Vector2(
        viewport.Pos.X + ((viewport.Size.X - width) * 0.5f),
        positionY);
    overlay.Dimensions = new Vector2(width, height);
    return true;
  }

  /// <summary>
  ///     Synchronizes the error-toast overlay to a stable top-center viewport
  ///     anchor.
  /// </summary>
  /// <param name="overlay">The overlay to synchronize.</param>
  /// <returns>
  ///     <see langword="true" /> when the overlay currently has visible
  ///     content; otherwise, <see langword="false" />.
  /// </returns>
  public bool TrySyncErrorToastOverlayToViewport(TranslationOverlay overlay)
  {
    overlay.Semaphore.Wait();
    var shouldDisplay = overlay.Display;
    overlay.Semaphore.Release();
    if (!shouldDisplay)
    {
      return false;
    }

    var viewport = ImGui.GetMainViewport();
    overlay.Position = new Vector2(
        viewport.Pos.X + (viewport.Size.X * 0.325f),
        viewport.Pos.Y + (viewport.Size.Y * 0.18f));
    overlay.Dimensions = new Vector2(
        viewport.Size.X * 0.35f,
        56f);
    return true;
  }

  /// <summary>
  ///     Applies one cache-hit normal toast immediately to the current callback.
  /// </summary>
  /// <param name="message">The live callback payload.</param>
  /// <param name="originalText">The original toast text.</param>
  /// <param name="translatedText">The translated toast text.</param>
  /// <param name="requestId">The active request identifier.</param>
  private void ApplyResolvedNormalToast(
      ref SeString message,
      string originalText,
      string translatedText,
      int requestId)
  {
    if (this.ShouldUseNormalOverlay())
    {
      this.PublishNormalOverlay(originalText, translatedText, "IToastGui.Toast");
      _ = this.ScheduleNormalOverlayClearAsync(requestId);
      if (!this.ShouldSwapNormalTexts())
      {
        return;
      }
    }

    if (!this.ShouldApplyNativeNormalText())
    {
      return;
    }

    if (!this.ShouldUseNormalOverlay())
    {
      this.clearNormalOverlay();
    }

    message = this.NormalizeForReplacement(translatedText);
  }

  /// <summary>
  ///     Applies one cache-hit error toast immediately to the current callback.
  /// </summary>
  /// <param name="message">The live callback payload.</param>
  /// <param name="originalText">The original toast text.</param>
  /// <param name="translatedText">The translated toast text.</param>
  /// <param name="requestId">The active request identifier.</param>
  private void ApplyResolvedErrorToast(
      ref SeString message,
      string originalText,
      string translatedText,
      int requestId)
  {
    if (this.ShouldUseErrorOverlay())
    {
      this.PublishErrorOverlay(originalText, translatedText, "IToastGui.ErrorToast");
      _ = this.ScheduleErrorOverlayClearAsync(requestId);
      if (!this.ShouldSwapErrorTexts())
      {
        return;
      }
    }

    if (!this.ShouldApplyNativeErrorText())
    {
      return;
    }

    if (!this.ShouldUseErrorOverlay())
    {
      this.clearErrorOverlay();
    }

    message = this.NormalizeForReplacement(translatedText);
  }

  /// <summary>
  ///     Resolves one supported normal-toast line asynchronously.
  /// </summary>
  /// <param name="originalText">The original toast text.</param>
  /// <param name="requestId">The active request identifier.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <returns>A task that completes after the translation attempt.</returns>
  private async Task ResolveNormalTranslationAsync(
      string originalText,
      int requestId,
      SourceClientLanguage sourceLanguage)
  {
    string translatedText;
    try
    {
      translatedText = await this.translationService.TranslateAsync(
          originalText,
          sourceLanguage.ProviderCode,
          LangDict[LanguageInt].Code) ?? string.Empty;
    }
    catch
    {
      return;
    }

    if (string.IsNullOrWhiteSpace(translatedText))
    {
      return;
    }

    await this.insertToastMessageAsync(
        new ToastMessage(
            NormalToastType,
            originalText,
            sourceLanguage.PersistenceCode,
            translatedText,
            LangDict[LanguageInt].Code,
            this.config.ChosenTransEngine,
            DateTime.Now,
            DateTime.Now));

    if (!this.IsCurrentNormalRequest(requestId, originalText))
    {
      return;
    }

    if (!this.ShouldUseNormalOverlay())
    {
      return;
    }

    this.PublishNormalOverlay(originalText, translatedText, "async-resolve");
    _ = this.ScheduleNormalOverlayClearAsync(requestId);
  }

  /// <summary>
  ///     Resolves one supported error-toast line asynchronously.
  /// </summary>
  /// <param name="originalText">The original toast text.</param>
  /// <param name="requestId">The active request identifier.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <returns>A task that completes after the translation attempt.</returns>
  private async Task ResolveErrorTranslationAsync(
      string originalText,
      int requestId,
      SourceClientLanguage sourceLanguage)
  {
    string translatedText;
    try
    {
      translatedText = await this.translationService.TranslateAsync(
          originalText,
          sourceLanguage.ProviderCode,
          LangDict[LanguageInt].Code) ?? string.Empty;
    }
    catch
    {
      return;
    }

    if (string.IsNullOrWhiteSpace(translatedText))
    {
      return;
    }

    await this.insertToastMessageAsync(
        new ToastMessage(
            ErrorToastType,
            originalText,
            sourceLanguage.PersistenceCode,
            translatedText,
            LangDict[LanguageInt].Code,
            this.config.ChosenTransEngine,
            DateTime.Now,
            DateTime.Now));

    if (!this.IsCurrentErrorRequest(requestId, originalText))
    {
      return;
    }

    if (!this.ShouldUseErrorOverlay())
    {
      return;
    }

    this.PublishErrorOverlay(originalText, translatedText, "async-resolve");
    _ = this.ScheduleErrorOverlayClearAsync(requestId);
  }

  /// <summary>
  ///     Starts one supported normal-toast request.
  /// </summary>
  /// <param name="originalText">The original toast text.</param>
  /// <param name="options">The current toast options.</param>
  /// <returns>The active request identifier.</returns>
  private int BeginNormalRequest(
      string originalText,
      ToastOptions options)
  {
    lock (this.stateGate)
    {
      this.activeNormalRequestId++;
      this.currentNormalOriginalText = originalText;
      this.currentNormalToastPosition = options.Position;
      this.currentNormalToastSpeed = options.Speed;
      return this.activeNormalRequestId;
    }
  }

  /// <summary>
  ///     Starts one error-toast request.
  /// </summary>
  /// <param name="originalText">The original toast text.</param>
  /// <returns>The active request identifier.</returns>
  private int BeginErrorRequest(string originalText)
  {
    lock (this.stateGate)
    {
      this.activeErrorRequestId++;
      this.currentErrorOriginalText = originalText;
      return this.activeErrorRequestId;
    }
  }

  /// <summary>
  ///     Delays clearing the unified normal-toast overlay so the overlay
  ///     lifetime roughly matches the transient toast lifetime.
  /// </summary>
  /// <param name="requestId">The owning request identifier.</param>
  /// <returns>A task that completes after the clear delay.</returns>
  private async Task ScheduleNormalOverlayClearAsync(int requestId)
  {
    await Task.Delay(this.GetNormalOverlayLifetimeMs());

    lock (this.stateGate)
    {
      if (requestId != this.activeNormalRequestId)
      {
        return;
      }

      this.currentNormalOriginalText = string.Empty;
    }

    this.clearNormalOverlay();
  }

  /// <summary>
  ///     Delays clearing the error-toast overlay so the overlay lifetime roughly
  ///     matches the transient toast lifetime.
  /// </summary>
  /// <param name="requestId">The owning request identifier.</param>
  /// <returns>A task that completes after the clear delay.</returns>
  private async Task ScheduleErrorOverlayClearAsync(int requestId)
  {
    await Task.Delay(ErrorOverlayLifetimeMs);

    lock (this.stateGate)
    {
      if (requestId != this.activeErrorRequestId)
      {
        return;
      }

      this.currentErrorOriginalText = string.Empty;
    }

    this.clearErrorOverlay();
  }

  /// <summary>
  ///     Gets the overlay lifetime for the current supported normal-toast
  ///     request.
  /// </summary>
  /// <returns>The overlay clear delay in milliseconds.</returns>
  private int GetNormalOverlayLifetimeMs()
  {
    lock (this.stateGate)
    {
      return this.currentNormalToastSpeed == ToastSpeed.Fast
          ? FastNormalOverlayLifetimeMs
          : SlowNormalOverlayLifetimeMs;
    }
  }

  /// <summary>
  ///     Determines whether the specified request still belongs to the current
  ///     supported normal-toast line.
  /// </summary>
  /// <param name="requestId">The request identifier to validate.</param>
  /// <param name="originalText">The original toast text.</param>
  /// <returns>
  ///     <see langword="true" /> when the request is still current; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool IsCurrentNormalRequest(
      int requestId,
      string originalText)
  {
    lock (this.stateGate)
    {
      return requestId == this.activeNormalRequestId &&
             string.Equals(
                 this.currentNormalOriginalText,
                 originalText,
                 StringComparison.Ordinal);
    }
  }

  /// <summary>
  ///     Determines whether the specified request still belongs to the current
  ///     error-toast line.
  /// </summary>
  /// <param name="requestId">The request identifier to validate.</param>
  /// <param name="originalText">The original toast text.</param>
  /// <returns>
  ///     <see langword="true" /> when the request is still current; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool IsCurrentErrorRequest(
      int requestId,
      string originalText)
  {
    lock (this.stateGate)
    {
      return requestId == this.activeErrorRequestId &&
             string.Equals(
                 this.currentErrorOriginalText,
                 originalText,
                 StringComparison.Ordinal);
    }
  }

  /// <summary>
  ///     Gets whether the unified normal-toast family should publish overlay
  ///     text.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when the unified normal-toast overlay is
  ///     active; otherwise, <see langword="false" />.
  /// </returns>
  private bool ShouldUseNormalOverlay()
  {
    return TranslationDisplayModeHelper.UsesOverlayPresentation(
        ToastGuiSupportedToastPolicy.GetNormalToastDisplayMode(this.config),
        this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Gets whether the unified normal-toast family should write translated
  ///     text into the native toast callback payload.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when native replacement is active; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool ShouldApplyNativeNormalText()
  {
    return TranslationDisplayModeHelper.WritesNativeTranslation(
        ToastGuiSupportedToastPolicy.GetNormalToastDisplayMode(this.config),
        this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Gets whether the unified normal-toast overlay should show original text
  ///     while the native toast displays the translation.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when swap mode is active; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool ShouldSwapNormalTexts()
  {
    return TranslationDisplayModeHelper.ShowsOriginalOverlayText(
        ToastGuiSupportedToastPolicy.GetNormalToastDisplayMode(this.config),
        this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Gets whether error toasts should publish overlay text.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when the error-toast overlay is active;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool ShouldUseErrorOverlay()
  {
    return TranslationDisplayModeHelper.UsesOverlayPresentation(
        this.config.ErrorToastTranslationDisplayMode,
        this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Gets whether error toasts should write translated text into the native
  ///     callback payload.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when native replacement is active; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool ShouldApplyNativeErrorText()
  {
    return TranslationDisplayModeHelper.WritesNativeTranslation(
        this.config.ErrorToastTranslationDisplayMode,
        this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Gets whether the error-toast overlay should show the original text
  ///     while the native error toast displays the translation.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when swap mode is active; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool ShouldSwapErrorTexts()
  {
    return TranslationDisplayModeHelper.ShowsOriginalOverlayText(
        this.config.ErrorToastTranslationDisplayMode,
        this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Publishes content into the unified normal-toast overlay.
  /// </summary>
  /// <param name="originalText">The original toast text.</param>
  /// <param name="translatedText">The translated toast text.</param>
  /// <param name="trigger">The trigger label associated with the call.</param>
  private void PublishNormalOverlay(
      string originalText,
      string translatedText,
      string trigger)
  {
    if (!this.ShouldUseNormalOverlay())
    {
      this.clearNormalOverlay();
      return;
    }

    var overlayText = this.SelectOverlayText(
        originalText,
        translatedText,
        this.ShouldSwapNormalTexts());
    if (string.IsNullOrWhiteSpace(overlayText))
    {
      this.clearNormalOverlay();
      return;
    }

    this.updateNormalOverlay(string.Empty, overlayText, string.Empty);
  }

  /// <summary>
  ///     Publishes content into the error-toast overlay.
  /// </summary>
  /// <param name="originalText">The original toast text.</param>
  /// <param name="translatedText">The translated toast text.</param>
  /// <param name="trigger">The trigger label associated with the call.</param>
  private void PublishErrorOverlay(
      string originalText,
      string translatedText,
      string trigger)
  {
    if (!this.ShouldUseErrorOverlay())
    {
      this.clearErrorOverlay();
      return;
    }

    var overlayText = this.SelectOverlayText(
        originalText,
        translatedText,
        this.ShouldSwapErrorTexts());
    if (string.IsNullOrWhiteSpace(overlayText))
    {
      this.clearErrorOverlay();
      return;
    }

    this.updateErrorOverlay(string.Empty, overlayText, string.Empty);
  }

  /// <summary>
  ///     Selects the overlay text for one callback-owned toast route.
  /// </summary>
  /// <param name="originalText">The original toast text.</param>
  /// <param name="translatedText">The translated toast text.</param>
  /// <param name="showOriginalOverlayText">
  ///     Whether the overlay should show the original text.
  /// </param>
  /// <returns>The overlay text to display.</returns>
  private string SelectOverlayText(
      string originalText,
      string translatedText,
      bool showOriginalOverlayText)
  {
    if (showOriginalOverlayText &&
        !string.IsNullOrWhiteSpace(originalText))
    {
      return originalText;
    }

    return translatedText;
  }

  /// <summary>
  ///     Builds one toast DB lookup entity using the plugin's existing toast
  ///     persistence schema.
  /// </summary>
  /// <param name="toastType">The persisted toast type.</param>
  /// <param name="originalText">The original toast text.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <returns>A lookup <see cref="ToastMessage" />.</returns>
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
  ///     Determines whether one stored toast row contains a usable translation
  ///     for the requested source line and toast type.
  /// </summary>
  /// <param name="toastMessage">The stored row to validate.</param>
  /// <param name="originalText">The expected original text.</param>
  /// <param name="toastType">The expected toast type.</param>
  /// <returns>
  ///     <see langword="true" /> when the stored row is usable; otherwise,
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
               StringComparison.Ordinal) &&
           string.Equals(
               toastMessage.OriginalToastMessage,
               originalText,
               StringComparison.Ordinal) &&
           !string.IsNullOrWhiteSpace(toastMessage.TranslatedToastMessage);
  }

  /// <summary>
  ///     Normalizes translated text before native replacement when the game font
  ///     requires it.
  /// </summary>
  /// <param name="translatedText">The translated text to normalize.</param>
  /// <returns>The text that should be written back into the callback payload.</returns>
  private string NormalizeForReplacement(string translatedText)
  {
    return this.config.RemoveDiacriticsWhenUsingReplacementTalkBTalk
        ? this.normalizeReplacementText(translatedText)
        : translatedText;
  }
}
