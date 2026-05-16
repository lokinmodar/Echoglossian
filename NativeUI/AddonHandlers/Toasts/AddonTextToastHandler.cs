// <copyright file="AddonTextToastHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Toasts;

/// <summary>
///     Delegate used to update toast overlay bounds from a live addon instance.
/// </summary>
/// <param name="addon">The live addon instance.</param>
internal unsafe delegate void UpdateToastOverlayBoundsDelegate(
    AtkUnitBase* addon,
    AtkTextNode* textNode);

/// <summary>
///     Delegate used to locate the text node that should be read and optionally
///     replaced inside a toast addon.
/// </summary>
/// <param name="addon">The live addon instance.</param>
/// <returns>The text node to read/replace, or <see langword="null" />.</returns>
internal unsafe delegate AtkTextNode* ResolveToastTextNodeDelegate(
    AtkUnitBase* addon);

/// <summary>
///     Shared runtime for toast addons whose content is ultimately represented by
///     a single text node inside an addon managed through <see cref="IAddonLifecycle" />.
///     Cache hits are used synchronously while network translation is queued in
///     the background to avoid blocking the game UI.
/// </summary>
internal class AddonTextToastHandler : IAddonTranslationHandler
{
  private readonly string addonName;
  private readonly Action clearOverlay;
  private readonly Config config;
  private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>> eventHandlers = new();
  private readonly Func<ToastMessage, ToastMessage?> findToastMessage;
  private readonly Func<Config, bool> isTypeEnabled;
  private readonly Func<ToastMessage, Task<string>> insertToastMessageAsync;
  private readonly Func<Config, JournalTranslationDisplayMode> modeSelector;
  private readonly Func<string, string> normalizeReplacementText;
  private readonly ResolveToastTextNodeDelegate resolveToastTextNode;
  private readonly object stateGate = new();
  private readonly string toastType;
  private readonly TranslationService translationService;
  private readonly Action<string, string, string> updateOverlay;
  private readonly UpdateToastOverlayBoundsDelegate updateOverlayBounds;

  private int activeRequestId;
  private string currentOriginalText = string.Empty;
  private string currentReplacementText = string.Empty;
  private string currentTranslatedText = string.Empty;
  private string lastFailedOriginalText = string.Empty;
  private int overlayPublicationVersion;
  private bool translationInFlight;

  /// <summary>
  ///     Initializes a new instance of the <see cref="AddonTextToastHandler" />
  ///     class.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="addonName">The addon name handled by this runtime.</param>
  /// <param name="toastType">The toast type persisted in the historical DB.</param>
  /// <param name="translationService">The translation service used by the plugin.</param>
  /// <param name="findToastMessage">
  ///     Delegate used to look up previously translated toast messages.
  /// </param>
  /// <param name="insertToastMessageAsync">
  ///     Delegate used to persist translated toast messages.
  /// </param>
  /// <param name="updateOverlay">
  ///     Delegate used to publish translated content to a toast overlay.
  /// </param>
  /// <param name="clearOverlay">
  ///     Delegate used to clear the toast overlay state.
  /// </param>
  /// <param name="updateOverlayBounds">
  ///     Delegate used to update the toast overlay bounds from a live addon.
  /// </param>
  /// <param name="resolveToastTextNode">
  ///     Delegate used to locate the text node that holds the toast message.
  /// </param>
  /// <param name="normalizeReplacementText">
  ///     Delegate used to normalize translated text before native replacement.
  /// </param>
  /// <param name="isTypeEnabled">
  ///     Delegate used to determine whether the concrete toast type is enabled in
  ///     the current config.
  /// </param>
  /// <param name="modeSelector">
  ///     Delegate used to determine how the concrete toast type should present
  ///     translated text.
  /// </param>
  protected AddonTextToastHandler(
      Config config,
      string addonName,
      string toastType,
      TranslationService translationService,
      Func<ToastMessage, ToastMessage?> findToastMessage,
      Func<ToastMessage, Task<string>> insertToastMessageAsync,
      Action<string, string, string> updateOverlay,
      Action clearOverlay,
      UpdateToastOverlayBoundsDelegate updateOverlayBounds,
      ResolveToastTextNodeDelegate resolveToastTextNode,
      Func<string, string> normalizeReplacementText,
      Func<Config, bool> isTypeEnabled,
      Func<Config, JournalTranslationDisplayMode> modeSelector)
  {
    this.config = config;
    this.addonName = addonName;
    this.toastType = toastType;
    this.translationService = translationService;
    this.findToastMessage = findToastMessage;
    this.insertToastMessageAsync = insertToastMessageAsync;
    this.updateOverlay = updateOverlay;
    this.clearOverlay = clearOverlay;
    this.updateOverlayBounds = updateOverlayBounds;
    this.resolveToastTextNode = resolveToastTextNode;
    this.normalizeReplacementText = normalizeReplacementText;
    this.isTypeEnabled = isTypeEnabled;
    this.modeSelector = modeSelector;

    this.RegisterHandler(AddonEvent.PreUpdate, this.OnPreUpdate);
    this.RegisterHandler(AddonEvent.PostUpdate, this.OnUpdateVisibleAddon);
    this.RegisterHandler(AddonEvent.PreDraw, this.OnUpdateVisibleAddon);
    this.RegisterHandler(AddonEvent.PreHide, this.OnResetState);
    this.RegisterHandler(AddonEvent.PreFinalize, this.OnResetState);
  }

  /// <summary>
  ///     Returns the event handlers required to drive the toast addon flow.
  /// </summary>
  /// <returns>
  ///     A dictionary mapping addon events to combined delegates.
  /// </returns>
  public Dictionary<AddonEvent, IAddonLifecycle.AddonEventDelegate> GetEventHandlers()
  {
    return this.eventHandlers.ToDictionary(
        kvp => kvp.Key,
        kvp => new IAddonLifecycle.AddonEventDelegate((evt, args) =>
        {
          foreach (var handler in kvp.Value)
          {
            handler(evt, args);
          }
        }));
  }

  /// <summary>
  ///     Registers a local delegate for the specified addon event.
  /// </summary>
  /// <param name="evt">The lifecycle event to handle.</param>
  /// <param name="handler">The delegate invoked for that event.</param>
  protected void RegisterHandler(
      AddonEvent evt,
      LocalAddonHandlerDelegate handler)
  {
    if (!this.eventHandlers.TryGetValue(evt, out var handlers))
    {
      handlers = [];
      this.eventHandlers[evt] = handlers;
    }

    handlers.Add(handler);
  }

  /// <summary>
  ///     Captures the visible toast source line before the addon finishes its
  ///     update, publishes any cached translation, and queues background work on
  ///     cache misses without blocking the game UI.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the handler.</param>
  /// <param name="args">The addon arguments associated with the update.</param>
  protected unsafe void OnPreUpdate(AddonEvent type, AddonArgs args)
  {
    if (!this.ShouldHandleToast(args, out var addon))
    {
      return;
    }

    var textNode = this.resolveToastTextNode(addon);
    if (!this.TryReadCurrentSource(textNode, out var originalText))
    {
      return;
    }

    this.updateOverlayBounds(addon, textNode);
    // PluginRuntimeLog.Debug(
    //     $"[{this.addonName}] trigger={type} captured source='{originalText}' " +
    //     $"overlay={this.ShouldUseOverlay()} native={this.ShouldApplyNativeToastText()} " +
    //     $"swap={this.ShouldSwapTexts()}");
    this.TryCaptureOrQueueToastSource(originalText, type.ToString());
  }

  /// <summary>
  ///     Updates overlay bounds for the visible toast addon and applies translated
  ///     text to the native addon when replacement mode is enabled.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the handler.</param>
  /// <param name="args">The addon arguments associated with the update or draw.</param>
  protected unsafe void OnUpdateVisibleAddon(AddonEvent type, AddonArgs args)
  {
    if (!this.ShouldHandleToast(args, out var addon))
    {
      return;
    }

    var textNode = this.resolveToastTextNode(addon);
    this.updateOverlayBounds(addon, textNode);
    // PluginRuntimeLog.Debug(
    //     $"[{this.addonName}] trigger={type} visible-update " +
    //     $"overlay={this.ShouldUseOverlay()} native={this.ShouldApplyNativeToastText()} " +
    //     $"swap={this.ShouldSwapTexts()}");

    if (!this.TryGetCurrentResolvedTranslation(
            out var resolvedOriginalText,
            out var translatedText,
            out var replacementText))
    {
      if (this.TryReadCurrentSource(textNode, out var visibleOriginalText))
      {
        this.updateOverlayBounds(addon, textNode);
        // PluginRuntimeLog.Debug(
        //     $"[{this.addonName}] trigger={type} visible-capture source='{visibleOriginalText}' " +
        //     $"overlay={this.ShouldUseOverlay()} native={this.ShouldApplyNativeToastText()} " +
        //     $"swap={this.ShouldSwapTexts()}");
        if (this.TryCaptureOrQueueToastSource(
                visibleOriginalText,
                $"{type}-visible-fallback"))
        {
          return;
        }
      }

      return;
    }

    if (this.ShouldUseOverlay())
    {
      // PluginRuntimeLog.Debug(
      //     $"[{this.addonName}] trigger={type} republishing overlay from resolved state");
      this.PublishOverlay(resolvedOriginalText, translatedText, type.ToString());
      if (!this.ShouldSwapTexts())
      {
        return;
      }
    }

    if (!this.ShouldApplyNativeToastText())
    {
      return;
    }

    if (textNode == null || textNode->NodeText.IsEmpty)
    {
      return;
    }

    var visibleText = this.ReadTextNode(textNode);
    if (this.TextMatches(visibleText, replacementText))
    {
      return;
    }

    // PluginRuntimeLog.Debug(
    //     $"[{this.addonName}] trigger={type} applying native replacement");
    NativeTextNodeLayoutHelper.ApplyTextReplacementWithInferredReflow(
        addon,
        textNode,
        replacementText);
    this.updateOverlayBounds(addon, textNode);
  }

  /// <summary>
  ///     Resolves the source toast line against cache, database, or background
  ///     translation work and publishes the overlay when a translation becomes
  ///     available.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the handler.</param>
  /// <param name="originalText">The original toast text to resolve.</param>
  /// <param name="trigger">The log trigger label associated with the call.</param>
  /// <returns>
  ///     <see langword="true" /> when the source was handled; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  protected bool TryCaptureOrQueueToastSource(
      string originalText,
      string trigger)
  {
    if (this.TryGetCachedTranslation(
            originalText,
            out var translatedText,
            out _))
    {
      // PluginRuntimeLog.Debug(
      //     $"[{this.addonName}] trigger={trigger} cache-hit -> overlay publish");
      this.PublishOverlay(originalText, translatedText, trigger);
      return true;
    }

    var lookupToast = this.BuildLookupMessage(originalText);
    var storedToast = this.findToastMessage(lookupToast);
    if (this.IsStoredTranslationUsable(storedToast, originalText))
    {
      this.SetResolvedState(
          originalText,
          storedToast!.TranslatedToastMessage!,
          this.NormalizeForReplacement(storedToast.TranslatedToastMessage!));
      // PluginRuntimeLog.Debug(
      //     $"[{this.addonName}] trigger={trigger} db-hit -> overlay publish");
      this.PublishOverlay(
          originalText,
          storedToast.TranslatedToastMessage!,
          trigger);
      return true;
    }

    if (this.TryQueueTranslation(originalText, out var requestId))
    {
      // PluginRuntimeLog.Debug(
      //     $"[{this.addonName}] trigger={trigger} cache-miss -> queued translation request #{requestId}");
      this.PublishOverlay(originalText, string.Empty, trigger);
      Task.Run(() => this.ResolveTranslationAsync(originalText, requestId));
      return true;
    }

    return false;
  }

  /// <summary>
  ///     Clears the in-memory toast state when the addon hides or is finalized.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the reset.</param>
  /// <param name="args">The addon arguments associated with the reset.</param>
  protected void OnResetState(AddonEvent type, AddonArgs args)
  {
    // PluginRuntimeLog.Debug($"[{this.addonName}] trigger={type} resetting toast state");
    var shouldDelayOverlayClear = this.ShouldUseOverlay();
    var resetRequestId = 0;
    var publicationVersion = 0;

    lock (this.stateGate)
    {
      this.activeRequestId++;
      this.currentOriginalText = string.Empty;
      this.currentTranslatedText = string.Empty;
      this.currentReplacementText = string.Empty;
      this.translationInFlight = false;
      this.lastFailedOriginalText = string.Empty;
      resetRequestId = this.activeRequestId;
      publicationVersion = this.overlayPublicationVersion;
    }

    if (shouldDelayOverlayClear)
    {
      this.ScheduleDeferredOverlayClear(resetRequestId, publicationVersion);
      return;
    }

    this.clearOverlay();
  }

  /// <summary>
  ///     Resolves a toast translation without blocking the game UI and persists the
  ///     result for future cache hits.
  /// </summary>
  /// <param name="originalText">The original toast text.</param>
  /// <param name="requestId">The request identifier used to reject stale updates.</param>
  /// <returns>A task that completes when the translation attempt finishes.</returns>
  protected async Task ResolveTranslationAsync(
      string originalText,
      int requestId)
  {
    string translatedText;
    try
    {
      translatedText = await this.translationService.TranslateAsync(
          originalText,
          ClientStateInterface.ClientLanguage.Humanize(),
          LangDict[LanguageInt].Code) ?? string.Empty;
    }
    catch (Exception ex)
    {
      // PluginRuntimeLog.Debug(
      //     $"{this.GetType().Name}.ResolveTranslationAsync exception {ex}");
      translatedText = string.Empty;
    }

    if (string.IsNullOrWhiteSpace(translatedText))
    {
      // PluginRuntimeLog.Debug(
      //     $"[{this.addonName}] trigger=async-resolve empty translation for source='{originalText}'");
      lock (this.stateGate)
      {
        if (requestId == this.activeRequestId &&
            this.TextMatches(this.currentOriginalText, originalText))
        {
          this.translationInFlight = false;
          this.lastFailedOriginalText = originalText;
        }
      }

      return;
    }

    var replacementText = this.NormalizeForReplacement(translatedText);
    // PluginRuntimeLog.Debug(
    //     $"[{this.addonName}] trigger=async-resolve translation ready for source='{originalText}'");
    var translatedToast = new ToastMessage(
        this.toastType,
        originalText,
        ClientStateInterface.ClientLanguage.Humanize(),
        translatedText,
        LangDict[LanguageInt].Code,
        this.config.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now);

    await this.insertToastMessageAsync(translatedToast);

    lock (this.stateGate)
    {
      if (requestId == this.activeRequestId &&
          this.TextMatches(this.currentOriginalText, originalText))
      {
        this.currentTranslatedText = translatedText;
        this.currentReplacementText = replacementText;
        this.translationInFlight = false;
        this.lastFailedOriginalText = string.Empty;
      }
    }

    if (this.ShouldUseOverlay())
    {
      this.PublishOverlay(originalText, translatedText, "async-resolve");
    }
  }

  /// <summary>
  ///     Builds a lookup entity matching the historical toast schema already used
  ///     by the plugin database.
  /// </summary>
  /// <param name="originalText">The original toast text.</param>
  /// <returns>A formatted <see cref="ToastMessage" /> for DB lookup.</returns>
  protected ToastMessage BuildLookupMessage(string originalText)
  {
    return new ToastMessage(
        this.toastType,
        originalText,
        ClientStateInterface.ClientLanguage.Humanize(),
        string.Empty,
        LangDict[LanguageInt].Code,
        this.config.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now);
  }

  /// <summary>
  ///     Determines whether the current callback should handle the configured toast
  ///     addon instance.
  /// </summary>
  /// <param name="args">The addon arguments associated with the callback.</param>
  /// <param name="addon">Receives the visible addon instance.</param>
  /// <returns>
  ///     <see langword="true" /> when the callback is for a visible toast addon;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  protected unsafe bool ShouldHandleToast(
      AddonArgs args,
      out AtkUnitBase* addon)
  {
    addon = null;
    if (args.AddonName != this.addonName || args.Addon.Address == IntPtr.Zero)
    {
      return false;
    }

    addon = (AtkUnitBase*)args.Addon.Address;
    return addon != null && addon->IsVisible;
  }

  /// <summary>
  ///     Reads the logical source text from the live toast addon, mapping the
  ///     visible translated replacement back to the original source line when
  ///     needed.
  /// </summary>
  /// <param name="addon">The visible toast addon.</param>
  /// <param name="originalText">Receives the logical original toast text.</param>
  /// <returns>
  ///     <see langword="true" /> when readable text is available; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  protected unsafe bool TryReadCurrentSource(
      AtkTextNode* textNode,
      out string originalText)
  {
    originalText = string.Empty;

    if (textNode == null || textNode->NodeText.IsEmpty)
    {
      return false;
    }

    var visibleText = this.ReadTextNode(textNode);
    if (string.IsNullOrWhiteSpace(visibleText))
    {
      return false;
    }

    lock (this.stateGate)
    {
      if (!string.IsNullOrWhiteSpace(this.currentOriginalText) &&
          !string.IsNullOrWhiteSpace(this.currentReplacementText) &&
          this.TextMatches(visibleText, this.currentReplacementText))
      {
        originalText = this.currentOriginalText;
        return true;
      }
    }

    originalText = visibleText;
    return true;
  }

  /// <summary>
  ///     Reads the current string value from a toast text node.
  /// </summary>
  /// <param name="textNode">The text node to inspect.</param>
  /// <returns>The visible node text, or an empty string when unavailable.</returns>
  protected unsafe string ReadTextNode(AtkTextNode* textNode)
  {
    try
    {
      return MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)textNode->NodeText.StringPtr.Value);
    }
    catch
    {
      return textNode->NodeText.ToString();
    }
  }

  /// <summary>
  ///     Determines whether a stored toast row still represents a usable
  ///     translation for the current source line.
  /// </summary>
  /// <param name="toastMessage">The stored toast row to validate.</param>
  /// <param name="originalText">The expected original toast text.</param>
  /// <returns>
  ///     <see langword="true" /> when the stored row contains a non-empty
  ///     translation for the same source line; otherwise, <see langword="false" />.
  /// </returns>
  protected bool IsStoredTranslationUsable(
      ToastMessage? toastMessage,
      string originalText)
  {
    return toastMessage != null &&
           this.TextMatches(
               toastMessage.OriginalToastMessage,
               originalText) &&
           !string.IsNullOrWhiteSpace(toastMessage.TranslatedToastMessage);
  }

  /// <summary>
  ///     Returns the active cached translation when it still matches the visible
  ///     toast source line.
  /// </summary>
  /// <param name="originalText">The source toast text.</param>
  /// <param name="translatedText">Receives the translated text.</param>
  /// <param name="replacementText">
  ///     Receives the translated text normalized for native replacement.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> when a matching cached translation exists;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  protected bool TryGetCachedTranslation(
      string originalText,
      out string translatedText,
      out string replacementText)
  {
    lock (this.stateGate)
    {
      if (this.TextMatches(this.currentOriginalText, originalText) &&
          !string.IsNullOrWhiteSpace(this.currentTranslatedText))
      {
        translatedText = this.currentTranslatedText;
        replacementText = this.currentReplacementText;
        return true;
      }
    }

    translatedText = string.Empty;
    replacementText = string.Empty;
    return false;
  }

  /// <summary>
  ///     Returns the active resolved translation currently held by the handler
  ///     state.
  /// </summary>
  /// <param name="translatedText">Receives the translated toast text.</param>
  /// <param name="replacementText">
  ///     Receives the translated toast text normalized for native replacement.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> when the handler already has a translated toast
  ///     line ready for display; otherwise, <see langword="false" />.
  /// </returns>
  protected bool TryGetCurrentResolvedTranslation(
      out string originalText,
      out string translatedText,
      out string replacementText)
  {
    lock (this.stateGate)
    {
      if (!string.IsNullOrWhiteSpace(this.currentTranslatedText))
      {
        originalText = this.currentOriginalText;
        translatedText = this.currentTranslatedText;
        replacementText = this.currentReplacementText;
        return true;
      }
    }

    originalText = string.Empty;
    translatedText = string.Empty;
    replacementText = string.Empty;
    return false;
  }

  /// <summary>
  ///     Starts a new translation request when the visible toast source line
  ///     changes or when the current line still has no cached translation.
  /// </summary>
  /// <param name="originalText">The source toast text.</param>
  /// <param name="requestId">
  ///     Receives the request identifier when a new translation job is queued.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> when a new translation task should be queued;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  protected bool TryQueueTranslation(
      string originalText,
      out int requestId)
  {
    lock (this.stateGate)
    {
      if (this.TextMatches(this.currentOriginalText, originalText))
      {
        if (this.translationInFlight ||
            !string.IsNullOrWhiteSpace(this.currentTranslatedText) ||
            this.TextMatches(this.lastFailedOriginalText, originalText))
        {
          requestId = this.activeRequestId;
          return false;
        }
      }

      this.activeRequestId++;
      this.currentOriginalText = originalText;
      this.currentTranslatedText = string.Empty;
      this.currentReplacementText = string.Empty;
      this.translationInFlight = true;
      requestId = this.activeRequestId;
      return true;
    }
  }

  /// <summary>
  ///     Sets the resolved in-memory state for the current toast line.
  /// </summary>
  /// <param name="originalText">The original toast text.</param>
  /// <param name="translatedText">The translated toast text.</param>
  /// <param name="replacementText">
  ///     The translated toast text normalized for native replacement.
  /// </param>
  protected void SetResolvedState(
      string originalText,
      string translatedText,
      string replacementText)
  {
    lock (this.stateGate)
    {
      this.currentOriginalText = originalText;
      this.currentTranslatedText = translatedText;
      this.currentReplacementText = replacementText;
      this.translationInFlight = false;
      this.lastFailedOriginalText = string.Empty;
    }
  }

  /// <summary>
  ///     Publishes toast content to the configured overlay when overlay mode is
  ///     enabled. When swap mode is active, the original text is shown in the
  ///     overlay while the translated text is kept for native replacement.
  /// </summary>
  /// <param name="originalText">The original toast text.</param>
  /// <param name="translatedText">The translated toast text.</param>
  /// <param name="trigger">The log trigger label associated with the call.</param>
  protected void PublishOverlay(
      string originalText,
      string translatedText,
      string trigger)
  {
    if (!this.ShouldUseOverlay())
    {
      // PluginRuntimeLog.Debug(
      //     $"[{this.addonName}] trigger={trigger} overlay disabled -> clear");
      this.clearOverlay();
      return;
    }

    var overlayText = this.SelectOverlayText(originalText, translatedText);
    if (string.IsNullOrWhiteSpace(overlayText))
    {
      // PluginRuntimeLog.Debug(
      //     $"[{this.addonName}] trigger={trigger} overlay text unavailable -> clear");
      this.clearOverlay();
      return;
    }

    // PluginRuntimeLog.Debug(
    //     $"[{this.addonName}] trigger={trigger} publish overlay text='{overlayText}'");
    lock (this.stateGate)
    {
      this.overlayPublicationVersion++;
    }

    this.updateOverlay(string.Empty, overlayText, string.Empty);
  }

  /// <summary>
  ///     Determines whether this toast type should currently use the overlay path.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when the overlay path is active; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  protected bool ShouldUseOverlay()
  {
    return this.config.TranslateToast &&
           this.isTypeEnabled(this.config) &&
           TranslationDisplayModeHelper.UsesOverlayPresentation(
               this.modeSelector(this.config),
               this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Determines whether this toast type should currently apply translated text
  ///     back into the native game UI.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when native replacement is active; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  protected bool ShouldApplyNativeToastText()
  {
    return this.config.TranslateToast &&
           this.isTypeEnabled(this.config) &&
           TranslationDisplayModeHelper.WritesNativeTranslation(
               this.modeSelector(this.config),
               this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Determines whether this toast type should currently swap its overlay
  ///     text with the original native text while still replacing the game UI.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when the overlay should display the original
  ///     text and the native addon should receive the translation; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  protected bool ShouldSwapTexts()
  {
    return this.config.TranslateToast &&
           this.isTypeEnabled(this.config) &&
           TranslationDisplayModeHelper.ShowsOriginalOverlayText(
               this.modeSelector(this.config),
               this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Selects the text that should be shown in the overlay for the current
  ///     toast state.
  /// </summary>
  /// <param name="originalText">The original toast text.</param>
  /// <param name="translatedText">The translated toast text.</param>
  /// <returns>The text that should be shown in the overlay.</returns>
  protected string SelectOverlayText(
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
  ///     Normalizes translated toast text for native replacement when the active
  ///     config requests diacritic stripping.
  /// </summary>
  /// <param name="text">The translated text to normalize.</param>
  /// <returns>The text that should be written back into the native toast addon.</returns>
  protected string NormalizeForReplacement(string text)
  {
    return this.config.RemoveDiacriticsWhenUsingReplacementTalkBTalk
        ? this.normalizeReplacementText(text)
        : text;
  }

  /// <summary>
  ///     Compares toast source or replacement text using a normalized whitespace
  ///     representation so line wrapping differences do not break source mapping.
  /// </summary>
  /// <param name="left">The first text to compare.</param>
  /// <param name="right">The second text to compare.</param>
  /// <returns>
  ///     <see langword="true" /> when both texts represent the same logical
  ///     content; otherwise, <see langword="false" />.
  /// </returns>
  protected bool TextMatches(
      string? left,
      string? right)
  {
    return string.Equals(
        NormalizeComparisonText(left),
        NormalizeComparisonText(right),
        StringComparison.Ordinal);
  }

  /// <summary>
  ///     Normalizes toast text for logical comparisons by collapsing whitespace.
  /// </summary>
  /// <param name="text">The text to normalize.</param>
  /// <returns>The normalized text suitable for equality checks.</returns>
  protected static string NormalizeComparisonText(string? text)
  {
    return Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
  }

  /// <summary>
  ///     Defers clearing overlay content long enough for ImGui to render the
  ///     latest toast state while preventing stale clears from removing newer
  ///     overlay content.
  /// </summary>
  /// <param name="requestId">The request identifier captured during reset.</param>
  /// <param name="publicationVersion">
  ///     The overlay publication version captured during reset.
  /// </param>
  private async void ScheduleDeferredOverlayClear(
      int requestId,
      int publicationVersion)
  {
    await Task.Delay(200).ConfigureAwait(false);

    lock (this.stateGate)
    {
      if (requestId != this.activeRequestId ||
          publicationVersion != this.overlayPublicationVersion)
      {
        return;
      }
    }

    this.clearOverlay();
  }
}


