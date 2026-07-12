// <copyright file="TextGimmickHintHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Toasts;

/// <summary>
///     Handles the "_TextGimmickHint" addon runtime inside the new
///     addon-handler model.
///     This implementation follows the same AddonLifecycle-first toast pattern
///     used by the other toast runtimes, adapted from the public
///     RaptureAtkModule.ShowTextGimmickHint signature in FFXIVClientStructs and
///     the TextGimmickHint.uld layout referenced by other open-source Dalamud
///     tooling.
/// </summary>
internal sealed class TextGimmickHintHandler :
    IAddonTranslationHandler,
    IVisibleDialogueRetranslationHandler
{
  private const string TextGimmickHintAddonName = "_TextGimmickHint";

  private readonly Action clearOverlay;
  private readonly Config config;
  private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>> eventHandlers = new();
  private readonly Func<TextGimmickHintMessage, TextGimmickHintMessage?> findTextGimmickHintMessage;
  private readonly Func<TextGimmickHintMessage, Task<string>> insertTextGimmickHintMessageAsync;
  private readonly Func<string, string> normalizeReplacementText;
  private readonly ResolveToastTextNodeDelegate resolveToastTextNode;
  private readonly object stateGate = new();
  private readonly TranslationService translationService;
  private readonly Action<string, string, string> updateOverlay;
  private readonly UpdateToastOverlayBoundsDelegate updateOverlayBounds;

  private int activeRequestId;
  private string currentOriginalText = string.Empty;
  private string currentReplacementText = string.Empty;
  private string currentTranslatedText = string.Empty;
  private string lastFailedOriginalText = string.Empty;
  private NativeTextNodeLayoutSnapshot? nativeLayoutSnapshot;
  private string nativeLayoutOriginalText = string.Empty;
  private bool translationInFlight;

  /// <summary>
  ///     Initializes a new instance of the <see cref="TextGimmickHintHandler" />
  ///     class.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The translation service used by the plugin.</param>
  /// <param name="findTextGimmickHintMessage">
  ///     Delegate used to look up previously translated gimmick-hint messages.
  /// </param>
  /// <param name="insertTextGimmickHintMessageAsync">
  ///     Delegate used to persist translated gimmick-hint messages.
  /// </param>
  /// <param name="updateOverlay">
  ///     Delegate used to publish translated content to the gimmick-hint overlay.
  /// </param>
  /// <param name="clearOverlay">
  ///     Delegate used to clear the gimmick-hint overlay state.
  /// </param>
  /// <param name="updateOverlayBounds">
  ///     Delegate used to update the gimmick-hint overlay bounds from the current
  ///     live addon instance.
  /// </param>
  /// <param name="normalizeReplacementText">
  ///     Delegate used to normalize translated text before native replacement.
  /// </param>
  public unsafe TextGimmickHintHandler(
      Config config,
      TranslationService translationService,
      Func<TextGimmickHintMessage, TextGimmickHintMessage?> findTextGimmickHintMessage,
      Func<TextGimmickHintMessage, Task<string>> insertTextGimmickHintMessageAsync,
      Action<string, string, string> updateOverlay,
      Action clearOverlay,
      UpdateToastOverlayBoundsDelegate updateOverlayBounds,
      Func<string, string> normalizeReplacementText)
  {
    this.config = config;
    this.translationService = translationService;
    this.findTextGimmickHintMessage = findTextGimmickHintMessage;
    this.insertTextGimmickHintMessageAsync = insertTextGimmickHintMessageAsync;
    this.updateOverlay = updateOverlay;
    this.clearOverlay = clearOverlay;
    this.updateOverlayBounds = updateOverlayBounds;
    this.resolveToastTextNode = AddonTextNodeResolvers.ResolveFirstTextNode;
    this.normalizeReplacementText = normalizeReplacementText;

    this.RegisterHandler(AddonEvent.PreUpdate, this.OnPreUpdate);
    this.RegisterHandler(AddonEvent.PostUpdate, this.OnUpdateVisibleAddon);
    this.RegisterHandler(AddonEvent.PreDraw, this.OnUpdateVisibleAddon);
    this.RegisterHandler(AddonEvent.PreHide, this.OnResetState);
    this.RegisterHandler(AddonEvent.PreFinalize, this.OnResetState);
  }

  /// <summary>
  ///     Returns the event handlers required to drive the gimmick-hint addon flow.
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

  /// <inheritdoc />
  public async Task<VisibleDialogueRetranslationResult>
      RetranslateVisibleTextAndPersistAsync()
  {
    const VisibleStorySurfaceKind surface = VisibleStorySurfaceKind.TextGimmickHint;
    var surfaceName = VisibleStorySurfaceText.ResolveSurfaceName(surface);
    string originalText;
    int requestId;
    var sourceLang = ClientStateInterface.ClientLanguage.Humanize();
    var targetLang = LangDict[LanguageInt].Code;

    lock (this.stateGate)
    {
      originalText = this.currentOriginalText;
      if (string.IsNullOrWhiteSpace(originalText))
      {
        return new VisibleDialogueRetranslationResult(
            false,
            false,
            surface,
            surfaceName,
            VisibleStorySurfaceText.GetNoVisibleTextMessage(surface));
      }

      this.activeRequestId++;
      requestId = this.activeRequestId;
      this.translationInFlight = true;
      this.lastFailedOriginalText = string.Empty;
    }

    try
    {
      var translatedText = await this.translationService.TranslateAsync(
              originalText,
              sourceLang,
              targetLang,
              TranslationSurfaceGroup.Dialogue)
          .ConfigureAwait(false) ?? string.Empty;

      if (!TranslationPersistenceGuard.IsUsableDialogueTranslation(
              originalText,
              translatedText,
              sourceLang,
              targetLang))
      {
        lock (this.stateGate)
        {
          if (requestId == this.activeRequestId)
          {
            this.translationInFlight = false;
            this.lastFailedOriginalText = originalText;
          }
        }

        return new VisibleDialogueRetranslationResult(
            true,
            false,
            surface,
            surfaceName,
            VisibleStorySurfaceText.GetNoUsableTranslationMessage(surface));
      }

      var dialogueTranslationEngine = this.GetDialogueTranslationEngineId();
      var translatedGimmickHint = new TextGimmickHintMessage(
          originalText,
          sourceLang,
          translatedText,
          targetLang,
          dialogueTranslationEngine,
          DateTime.Now,
          DateTime.Now);
      var persistenceResult = await this.insertTextGimmickHintMessageAsync(
              translatedGimmickHint)
          .ConfigureAwait(false);
      var persistenceSucceeded = !persistenceResult.StartsWith(
          "ErrorSavingData:",
          StringComparison.Ordinal) &&
                                 !string.Equals(
                                     persistenceResult,
                                     "No data to save.",
                                     StringComparison.Ordinal);
      var replacementText = this.NormalizeForReplacement(translatedText);
      var sourceChangedBeforeApply = false;
      lock (this.stateGate)
      {
        sourceChangedBeforeApply = requestId != this.activeRequestId;
        if (!sourceChangedBeforeApply)
        {
          this.currentTranslatedText = translatedText;
          this.currentReplacementText = replacementText;
          this.translationInFlight = false;
          this.lastFailedOriginalText = string.Empty;
        }
      }

      if (!sourceChangedBeforeApply)
      {
        this.RecordDiagnosticsSnapshot(
            VisibleStorySurfaceProvenanceKind.FreshLiveTranslation,
            originalText,
            translatedText,
            dialogueTranslationEngine);
        this.PublishOverlay(originalText, translatedText, "explicit-retranslate");
      }

      if (!persistenceSucceeded)
      {
        return new VisibleDialogueRetranslationResult(
            true,
            false,
            surface,
            surfaceName,
            VisibleStorySurfaceText.GetPersistenceFailedMessage(
                surface,
                persistenceResult));
      }

      if (sourceChangedBeforeApply)
      {
        return new VisibleDialogueRetranslationResult(
            true,
            true,
            surface,
            surfaceName,
            VisibleStorySurfaceText.GetPersistedButVisibleChangedMessage(
                surface));
      }

      return new VisibleDialogueRetranslationResult(
          true,
          true,
          surface,
          surfaceName,
          VisibleStorySurfaceText.GetRetranslatedAndPersistedMessage(surface));
    }
    catch (Exception ex)
    {
      lock (this.stateGate)
      {
        if (requestId == this.activeRequestId)
        {
          this.translationInFlight = false;
          this.lastFailedOriginalText = originalText;
        }
      }

      PluginRuntimeLog.Error(
          $"[{TextGimmickHintAddonName}] Error retranslating visible TextGimmickHint text: {ex}");
      return new VisibleDialogueRetranslationResult(
          true,
          false,
          surface,
          surfaceName,
          VisibleStorySurfaceText.GetRetranslationFailedMessage(surface));
    }
  }

  /// <summary>
  ///     Registers a local delegate for the specified addon event.
  /// </summary>
  /// <param name="evt">The lifecycle event to handle.</param>
  /// <param name="handler">The delegate invoked for that event.</param>
  private void RegisterHandler(
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
  ///     Captures TextGimmickHint source text early in the lifecycle so a
  ///     translation can already be queued before the first draw pass completes.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the handler.</param>
  /// <param name="args">The addon arguments associated with the event.</param>
  private unsafe void OnPreUpdate(AddonEvent type, AddonArgs args)
  {
    if (!this.ShouldHandleTextGimmickHint(args, out var addon))
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
    //     $"[{TextGimmickHintAddonName}] trigger={type} captured source='{originalText}' " +
    //     $"overlay={this.ShouldUseOverlay()} native={this.ShouldApplyNativeText()} " +
    //     $"swap={this.ShouldSwapTexts()}");
    this.TryCaptureOrQueueSource(originalText, type.ToString());
  }

  /// <summary>
  ///     Updates overlay bounds for the visible gimmick-hint addon and applies
  ///     translated text to the native addon when replacement mode is enabled.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the handler.</param>
  /// <param name="args">The addon arguments associated with the update or draw.</param>
  private unsafe void OnUpdateVisibleAddon(AddonEvent type, AddonArgs args)
  {
    if (!this.ShouldHandleTextGimmickHint(args, out var addon))
    {
      return;
    }

    var textNode = this.resolveToastTextNode(addon);
    if (!this.TryReadCurrentSource(textNode, out var visibleOriginalText))
    {
      return;
    }

    this.updateOverlayBounds(addon, textNode);
    // PluginRuntimeLog.Debug(
    //     $"[{TextGimmickHintAddonName}] trigger={type} visible-update " +
    //     $"overlay={this.ShouldUseOverlay()} native={this.ShouldApplyNativeText()} " +
    //     $"swap={this.ShouldSwapTexts()}");

    if (this.TryHandleVisibleSourceChange(
            visibleOriginalText,
            $"{type}-visible-reconcile"))
    {
      return;
    }

    if (!this.TryGetCurrentResolvedTranslation(
            out var resolvedOriginalText,
            out var translatedText,
            out var replacementText))
    {
      if (this.TryCaptureOrQueueSource(
              visibleOriginalText,
              $"{type}-visible-fallback"))
      {
        return;
      }

      return;
    }

    if (this.ShouldUseOverlay())
    {
      // PluginRuntimeLog.Debug(
      //     $"[{TextGimmickHintAddonName}] trigger={type} republishing overlay from resolved state");
      this.PublishOverlay(
          resolvedOriginalText,
          translatedText,
          type.ToString());
      if (!this.ShouldSwapTexts())
      {
        return;
      }
    }

    if (!this.ShouldApplyNativeText())
    {
      this.RestoreTrackedNativeLayoutIfNeeded();
      return;
    }

    this.RestoreTrackedNativeLayoutIfNeeded(resolvedOriginalText);

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
    //     $"[{TextGimmickHintAddonName}] trigger={type} applying native replacement");
    var layoutSnapshot = NativeTextNodeLayoutHelper.ApplyTextReplacementWithInferredReflow(
        addon,
        textNode,
        replacementText,
        allowWidthGrowth: true,
        restoreHorizontalCentering: false);
    if (layoutSnapshot != null)
    {
      lock (this.stateGate)
      {
        this.nativeLayoutSnapshot = layoutSnapshot;
        this.nativeLayoutOriginalText = resolvedOriginalText;
      }
    }

    this.updateOverlayBounds(addon, textNode);
  }

  /// <summary>
  ///     Clears the in-memory gimmick-hint state when the addon hides or is finalized.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the reset.</param>
  /// <param name="args">The addon arguments associated with the reset.</param>
  private void OnResetState(AddonEvent type, AddonArgs args)
  {
    // PluginRuntimeLog.Debug($"[{TextGimmickHintAddonName}] trigger={type} resetting toast state");
    NativeTextNodeLayoutSnapshot? layoutSnapshot = null;
    string layoutOriginalText = string.Empty;
    lock (this.stateGate)
    {
      this.activeRequestId++;
      this.currentOriginalText = string.Empty;
      this.currentTranslatedText = string.Empty;
      this.currentReplacementText = string.Empty;
      this.translationInFlight = false;
      this.lastFailedOriginalText = string.Empty;
      layoutSnapshot = this.nativeLayoutSnapshot;
      layoutOriginalText = this.nativeLayoutOriginalText;
      this.nativeLayoutSnapshot = null;
      this.nativeLayoutOriginalText = string.Empty;
    }

    this.TryRestoreNativeLayout(layoutSnapshot, layoutOriginalText);
    VisibleStorySurfaceDiagnosticsStore.Clear(
        VisibleStorySurfaceKind.TextGimmickHint);
    this.clearOverlay();
  }

  /// <summary>
  ///     Restores any tracked native gimmick-hint mutation when the source
  ///     changes or native mode is left.
  /// </summary>
  /// <param name="nextOriginalText">
  ///     The next original source text expected for the hint, or an empty value
  ///     when the current mutation should always be restored.
  /// </param>
  private void RestoreTrackedNativeLayoutIfNeeded(string? nextOriginalText = null)
  {
    NativeTextNodeLayoutSnapshot? layoutSnapshot = null;
    string layoutOriginalText = string.Empty;
    var restoreText = string.IsNullOrWhiteSpace(nextOriginalText);

    lock (this.stateGate)
    {
      if (this.nativeLayoutSnapshot == null)
      {
        return;
      }

      if (!string.IsNullOrWhiteSpace(nextOriginalText) &&
          this.TextMatches(this.nativeLayoutOriginalText, nextOriginalText))
      {
        return;
      }

      layoutSnapshot = this.nativeLayoutSnapshot;
      layoutOriginalText = this.nativeLayoutOriginalText;
      this.nativeLayoutSnapshot = null;
      this.nativeLayoutOriginalText = string.Empty;
    }

    this.TryRestoreNativeLayout(
        layoutSnapshot,
        layoutOriginalText,
        restoreText);
  }

  /// <summary>
  ///     Restores one tracked native gimmick-hint layout snapshot back to the
  ///     original game state.
  /// </summary>
  /// <param name="layoutSnapshot">The captured layout snapshot.</param>
  /// <param name="originalText">The original text to write back.</param>
  private void TryRestoreNativeLayout(
      NativeTextNodeLayoutSnapshot? layoutSnapshot,
      string originalText,
      bool restoreText = true)
  {
    if (layoutSnapshot == null)
    {
      return;
    }

    NativeTextNodeLayoutHelper.RestoreLayoutSnapshot(
        layoutSnapshot,
        originalText,
        restoreText);
  }

  /// <summary>
  ///     Reconciles the visible gimmick-hint source against the tracked state so
  ///     a recycled slot does not keep a stale translated line or a stale native
  ///     layout snapshot from the prior source.
  /// </summary>
  /// <param name="visibleOriginalText">The currently visible source line.</param>
  /// <param name="trigger">The trigger label used for capture or queueing.</param>
  /// <returns>
  ///     <see langword="true" /> when the source changed and the new source was
  ///     handled immediately; otherwise, <see langword="false" />.
  /// </returns>
  private bool TryHandleVisibleSourceChange(
      string visibleOriginalText,
      string trigger)
  {
    var shouldReconcile = false;

    lock (this.stateGate)
    {
      if (!string.IsNullOrWhiteSpace(this.currentOriginalText) &&
          !this.TextMatches(this.currentOriginalText, visibleOriginalText))
      {
        shouldReconcile = true;
      }
    }

    if (!shouldReconcile)
    {
      return false;
    }

    this.RestoreTrackedNativeLayoutIfNeeded(visibleOriginalText);
    return this.TryCaptureOrQueueSource(
        visibleOriginalText,
        trigger);
  }

  /// <summary>
  ///     Resolves the visible TextGimmickHint source line against cache,
  ///     database, or background translation work without blocking the game UI.
  /// </summary>
  /// <param name="originalText">The current visible source line.</param>
  /// <param name="trigger">The trigger label associated with the call.</param>
  /// <returns>
  ///     <see langword="true" /> when the source was handled immediately;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryCaptureOrQueueSource(
      string originalText,
      string trigger)
  {
    if (this.TryGetCachedTranslation(
            originalText,
            out var translatedText,
            out _))
    {
      this.SetResolvedState(
          originalText,
          translatedText,
          this.NormalizeForReplacement(translatedText));
      this.PublishOverlay(originalText, translatedText, trigger);
      return true;
    }

    if (this.TryLoadStoredTranslation(
            originalText,
            out var storedTranslatedText,
            out var storedReplacementText))
    {
      this.SetResolvedState(
          originalText,
          storedTranslatedText,
          storedReplacementText);
      this.PublishOverlay(
          originalText,
          storedTranslatedText,
          trigger);
      return true;
    }

    if (this.TryQueueTranslation(originalText, out var requestId))
    {
      this.PublishOverlay(originalText, string.Empty, trigger);
      Task.Run(() => this.ResolveTranslationAsync(originalText, requestId));
      return true;
    }

    return false;
  }

  /// <summary>
  ///     Resolves a gimmick-hint translation without blocking the game UI and
  ///     persists the result for future cache hits.
  /// </summary>
  /// <param name="originalText">The original gimmick-hint text.</param>
  /// <param name="requestId">The request identifier used to reject stale updates.</param>
  /// <returns>A task that completes when the translation attempt finishes.</returns>
  private async Task ResolveTranslationAsync(
      string originalText,
      int requestId)
  {
    var sourceLang = ClientStateInterface.ClientLanguage.Humanize();
    var targetLang = LangDict[LanguageInt].Code;
    string translatedText;
    try
    {
      translatedText = await this.translationService.TranslateAsync(
          originalText,
          sourceLang,
          targetLang,
          TranslationSurfaceGroup.Dialogue).ConfigureAwait(false) ?? string.Empty;
    }
    catch (Exception ex)
    {
      // PluginRuntimeLog.Debug(
      //     $"{this.GetType().Name}.ResolveTranslationAsync exception {ex}");
      translatedText = string.Empty;
    }

    if (!TranslationPersistenceGuard.IsUsableDialogueTranslation(
            originalText,
            translatedText,
            sourceLang,
            targetLang))
    {
      // PluginRuntimeLog.Debug(
      //     $"[{TextGimmickHintAddonName}] trigger=async-resolve empty translation for source='{originalText}'");
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
    var dialogueTranslationEngine = this.GetDialogueTranslationEngineId();
    // PluginRuntimeLog.Debug(
    //     $"[{TextGimmickHintAddonName}] trigger=async-resolve translation ready for source='{originalText}'");
    var translatedGimmickHint = new TextGimmickHintMessage(
        originalText,
        sourceLang,
        translatedText,
        targetLang,
        dialogueTranslationEngine,
        DateTime.Now,
        DateTime.Now);

    try
    {
      await this.insertTextGimmickHintMessageAsync(translatedGimmickHint)
          .ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      PluginRuntimeLog.Warning(
          ex,
          $"[{TextGimmickHintAddonName}] Failed to persist translated TextGimmickHint text.");
    }

    lock (this.stateGate)
    {
      if (requestId != this.activeRequestId)
      {
        return;
      }

      this.currentTranslatedText = translatedText;
      this.currentReplacementText = replacementText;
      this.translationInFlight = false;
      this.lastFailedOriginalText = string.Empty;
    }

    this.RecordDiagnosticsSnapshot(
        VisibleStorySurfaceProvenanceKind.FreshLiveTranslation,
        originalText,
        translatedText,
        dialogueTranslationEngine);
    this.PublishOverlay(originalText, translatedText, "async-resolve");
  }

  /// <summary>
  ///     Determines whether the current captured source still has a cached
  ///     translation.
  /// </summary>
  /// <param name="originalText">The source gimmick-hint text.</param>
  /// <param name="translatedText">Receives the translated text.</param>
  /// <param name="replacementText">Receives the normalized replacement text.</param>
  /// <returns>
  ///     <see langword="true" /> when a matching cached translation exists;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryGetCachedTranslation(
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
  ///     Attempts to load a stored gimmick-hint translation from the database.
  /// </summary>
  /// <param name="originalText">The source gimmick-hint text.</param>
  /// <param name="translatedText">Receives the translated text.</param>
  /// <param name="replacementText">Receives the normalized replacement text.</param>
  /// <returns>
  ///     <see langword="true" /> when a stored translation exists;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryLoadStoredTranslation(
      string originalText,
      out string translatedText,
      out string replacementText)
  {
    var lookup = this.findTextGimmickHintMessage(
        this.BuildLookupMessage(originalText));
    if (lookup == null ||
        !string.Equals(
            lookup.OriginalText,
            originalText,
            StringComparison.Ordinal) ||
        string.IsNullOrWhiteSpace(lookup.TranslatedText))
    {
      translatedText = string.Empty;
      replacementText = string.Empty;
      return false;
    }

    translatedText = lookup.TranslatedText!;
    replacementText = this.NormalizeForReplacement(translatedText);
    this.RecordDiagnosticsSnapshot(
        VisibleStorySurfaceProvenanceKind.DbReuse,
        originalText,
        translatedText,
        lookup.TranslationEngine ?? this.GetDialogueTranslationEngineId());
    return true;
  }

  /// <summary>
  ///     Returns the active resolved translation currently held by the handler
  ///     state.
  /// </summary>
  /// <param name="translatedText">Receives the translated gimmick-hint text.</param>
  /// <param name="replacementText">Receives the normalized replacement text.</param>
  /// <returns>
  ///     <see langword="true" /> when the handler already has a translated line;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryGetCurrentResolvedTranslation(
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
  ///     Starts a new translation request when the source line changes or when
  ///     the current line still has no cached translation available.
  /// </summary>
  /// <param name="originalText">The source gimmick-hint text.</param>
  /// <param name="requestId">Receives the active request identifier.</param>
  /// <returns>
  ///     <see langword="true" /> when a new translation task should be queued;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryQueueTranslation(
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
  ///     Sets the resolved in-memory state for the current gimmick-hint line.
  /// </summary>
  /// <param name="originalText">The original gimmick-hint text.</param>
  /// <param name="translatedText">The translated gimmick-hint text.</param>
  /// <param name="replacementText">
  ///     The translated text normalized for native replacement.
  /// </param>
  private void SetResolvedState(
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
  ///     Publishes translated gimmick-hint content to the configured overlay when
  ///     overlay mode is enabled.
  /// </summary>
  /// <param name="translatedText">The translated gimmick-hint text.</param>
  private void PublishOverlay(
      string originalText,
      string translatedText,
      string trigger)
  {
    if (!this.ShouldUseOverlay())
    {
      // PluginRuntimeLog.Debug(
      //     $"[{TextGimmickHintAddonName}] trigger={trigger} overlay disabled -> clear");
      this.clearOverlay();
      return;
    }

    var overlayText = this.SelectOverlayText(originalText, translatedText);
    if (string.IsNullOrWhiteSpace(overlayText))
    {
      // PluginRuntimeLog.Debug(
      //     $"[{TextGimmickHintAddonName}] trigger={trigger} overlay text unavailable -> clear");
      this.clearOverlay();
      return;
    }

    // PluginRuntimeLog.Debug(
    //     $"[{TextGimmickHintAddonName}] trigger={trigger} publish overlay text='{overlayText}'");
    this.updateOverlay(string.Empty, overlayText, string.Empty);
  }

  /// <summary>
  ///     Records the latest visible TextGimmickHint provenance snapshot for
  ///     the debugger.
  /// </summary>
  /// <param name="provenance">The provenance label kind to expose.</param>
  /// <param name="originalText">The original gimmick-hint text.</param>
  /// <param name="translatedText">The translated gimmick-hint text.</param>
  /// <param name="effectiveTranslationEngineId">
  /// The effective dialogue translation engine identifier.
  /// </param>
  private void RecordDiagnosticsSnapshot(
      VisibleStorySurfaceProvenanceKind provenance,
      string originalText,
      string translatedText,
      int effectiveTranslationEngineId)
  {
    VisibleStorySurfaceDiagnosticsStore.Record(
        new VisibleStorySurfaceDiagnosticsSnapshot(
            VisibleStorySurfaceKind.TextGimmickHint,
            provenance,
            VisibleStorySurfaceTableMap.Resolve(
                VisibleStorySurfaceKind.TextGimmickHint),
            string.Empty,
            originalText,
            string.Empty,
            string.Empty,
            translatedText,
            string.Empty,
            false,
            effectiveTranslationEngineId,
            DateTime.UtcNow,
            null,
            null));
  }

  /// <summary>
  ///     Resolves the effective dialogue translation engine identifier.
  /// </summary>
  /// <returns>The effective dialogue translation engine identifier.</returns>
  private int GetDialogueTranslationEngineId()
  {
    return this.translationService.GetEffectiveTranslationEngineId(
        TranslationSurfaceGroup.Dialogue);
  }

  /// <summary>
  ///     Determines whether this gimmick-hint request should render through the
  ///     overlay path.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when the overlay path is active; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool ShouldUseOverlay()
  {
    return this.config.TranslateTextGimmickHint &&
           TranslationDisplayModeHelper.UsesOverlayPresentation(
               this.config.TextGimmickHintTranslationDisplayMode,
               this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Determines whether the TextGimmickHint addon should receive translated
  ///     text directly in the game UI.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when the addon should be replaced natively;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool ShouldApplyNativeText()
  {
    return this.config.TranslateTextGimmickHint &&
           TranslationDisplayModeHelper.WritesNativeTranslation(
               this.config.TextGimmickHintTranslationDisplayMode,
               this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Determines whether the TextGimmickHint overlay should show the original
  ///     text while the native addon receives the translated replacement.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when swap mode is active; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool ShouldSwapTexts()
  {
    return this.config.TranslateTextGimmickHint &&
           TranslationDisplayModeHelper.ShowsOriginalOverlayText(
               this.config.TextGimmickHintTranslationDisplayMode,
               this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Selects the overlay text for the gimmick-hint toast state.
  /// </summary>
  /// <param name="originalText">The original gimmick-hint text.</param>
  /// <param name="translatedText">The translated gimmick-hint text.</param>
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
  ///     Determines whether the current callback should handle the configured
  ///     TextGimmickHint addon instance.
  /// </summary>
  /// <param name="args">The addon arguments associated with the callback.</param>
  /// <param name="addon">Receives the visible addon instance.</param>
  /// <returns>
  ///     <see langword="true" /> when the callback is for a visible
  ///     TextGimmickHint addon; otherwise, <see langword="false" />.
  /// </returns>
  private unsafe bool ShouldHandleTextGimmickHint(
      AddonArgs args,
      out AtkUnitBase* addon)
  {
    addon = null;
    if (args.AddonName != TextGimmickHintAddonName ||
        args.Addon.Address == IntPtr.Zero)
    {
      return false;
    }

    addon = (AtkUnitBase*)args.Addon.Address;
    return addon != null && addon->IsVisible;
  }

  /// <summary>
  ///     Builds a lookup entity matching the historical TextGimmickHint schema
  ///     already used by the plugin database.
  /// </summary>
  /// <param name="originalText">The original gimmick-hint text.</param>
  /// <returns>A formatted <see cref="TextGimmickHintMessage" /> for DB lookup.</returns>
  private TextGimmickHintMessage BuildLookupMessage(string originalText)
  {
    return new TextGimmickHintMessage(
        originalText,
        ClientStateInterface.ClientLanguage.Humanize(),
        string.Empty,
        LangDict[LanguageInt].Code,
        this.config.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now);
  }

  /// <summary>
  ///     Reads the current string value from a gimmick-hint text node.
  /// </summary>
  /// <param name="textNode">The text node to inspect.</param>
  /// <returns>The visible node text, or an empty string when unavailable.</returns>
  private unsafe string ReadTextNode(AtkTextNode* textNode)
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
  ///     Reads the logical source text from the live gadget-hint addon, mapping
  ///     the visible translated replacement back to the original source line when
  ///     needed.
  /// </summary>
  /// <param name="textNode">The text node to inspect.</param>
  /// <param name="originalText">Receives the logical original text.</param>
  /// <returns>
  ///     <see langword="true" /> when readable text is available; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private unsafe bool TryReadCurrentSource(
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

    if (this.ShouldApplyNativeText() &&
        this.TryReadOriginalPointerText(textNode, out var originalPointerText) &&
        !this.TextMatches(originalPointerText, visibleText) &&
        this.TryConfirmOriginalPointerMatchesVisible(
            originalPointerText,
            visibleText,
            out var confirmedOriginalText))
    {
      originalText = confirmedOriginalText;
      return true;
    }

    originalText = visibleText;
    return true;
  }

  /// <summary>
  ///     Confirms that a gimmick-hint node's original-text pointer really
  ///     belongs to the currently visible text before the handler trusts it as
  ///     the logical source line.
  /// </summary>
  /// <param name="originalPointerText">The text read from <c>OriginalTextPointer</c>.</param>
  /// <param name="visibleText">The text currently visible in the node.</param>
  /// <param name="originalText">Receives the confirmed original text.</param>
  /// <returns>
  ///     <see langword="true" /> when the pointer text can be proven to explain
  ///     the visible replacement inside the same gimmick-hint surface;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryConfirmOriginalPointerMatchesVisible(
      string originalPointerText,
      string visibleText,
      out string originalText)
  {
    lock (this.stateGate)
    {
      if (this.TextMatches(this.currentOriginalText, originalPointerText) &&
          this.TextMatches(visibleText, this.currentReplacementText))
      {
        originalText = originalPointerText;
        return true;
      }
    }

    if (this.TryLoadStoredTranslation(
            originalPointerText,
            out _,
            out var replacementText) &&
        this.TextMatches(visibleText, replacementText))
    {
      originalText = originalPointerText;
      return true;
    }

    originalText = string.Empty;
    return false;
  }

  /// <summary>
  ///     Tries to read the original game-provided text payload from a gimmick-hint
  ///     text node even when the visible node text has already been replaced.
  /// </summary>
  /// <param name="textNode">The text node to inspect.</param>
  /// <param name="originalText">Receives the original payload text.</param>
  /// <returns>
  ///     <see langword="true" /> when the original payload can be read;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private unsafe bool TryReadOriginalPointerText(
      AtkTextNode* textNode,
      out string originalText)
  {
    originalText = string.Empty;
    if (textNode == null)
    {
      return false;
    }

    try
    {
      originalText = textNode->OriginalTextPointer.AsReadOnlySeStringSpan().ExtractText();
      return !string.IsNullOrWhiteSpace(originalText);
    }
    catch
    {
      return false;
    }
  }

  /// <summary>
  ///     Compares text values after normalizing spacing and the optional
  ///     diacritic removal rules used for native replacement.
  /// </summary>
  /// <param name="left">The first text value.</param>
  /// <param name="right">The second text value.</param>
  /// <returns>
  ///     <see langword="true" /> when the texts should be considered equal.
  /// </returns>
  private bool TextMatches(string? left, string? right)
  {
    return string.Equals(
        this.NormalizeForComparison(left),
        this.NormalizeForComparison(right),
        StringComparison.Ordinal);
  }

  /// <summary>
  ///     Normalizes text for comparison against native node values.
  /// </summary>
  /// <param name="text">The text to normalize.</param>
  /// <returns>The normalized text value.</returns>
  private string NormalizeForComparison(string? text)
  {
    if (string.IsNullOrWhiteSpace(text))
    {
      return string.Empty;
    }

    var replacementText = this.NormalizeForReplacement(text);
    return string.Join(
        " ",
        replacementText.Split(
            ['\r', '\n', '\t', ' '],
            StringSplitOptions.RemoveEmptyEntries));
  }

  /// <summary>
  ///     Normalizes translated text before native replacement when the active
  ///     config requests diacritic stripping.
  /// </summary>
  /// <param name="text">The translated text to normalize.</param>
  /// <returns>The text that should be written back into the native addon.</returns>
  private string NormalizeForReplacement(string text)
  {
    return this.config.RemoveDiacriticsWhenUsingReplacementTalkBTalk
        ? this.normalizeReplacementText(text)
        : text;
  }
}


