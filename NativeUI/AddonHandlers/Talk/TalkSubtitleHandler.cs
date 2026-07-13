// <copyright file="TalkSubtitleHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

using Echoglossian.NativeUI.AddonHandlers.Common;

namespace Echoglossian.NativeUI.AddonHandlers.Talk;

/// <summary>
///     Handles the TalkSubtitle addon runtime inside the new addon-handler model.
///     TalkSubtitle is driven from addon lifecycle events rather than the legacy
///     handler so it can reuse the same cache-first, async translation pipeline as
///     Talk and BattleTalk while keeping the subtitle overlay independent.
/// </summary>
public sealed class TalkSubtitleHandler :
    IAddonTranslationHandler,
    IVisibleDialogueRetranslationHandler
{
  private const string TalkSubtitleAddonName = "TalkSubtitle";
  private const int TextNodeId = 2;
  private const int AltTextNodeId = 3;
  private const int AltTextNodeId2 = 4;

  private readonly Action clearOverlay;
  private readonly Config config;
  private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>> eventHandlers = new();
  private readonly Func<TalkSubtitleMessage, TalkSubtitleMessage?> findTalkSubtitleMessage;
  private readonly Func<TalkSubtitleMessage, Task<string>> insertTalkSubtitleMessageAsync;
  private readonly Func<string, string> normalizeReplacementText;
  private readonly object stateGate = new();
  private readonly TranslationService translationService;
  private readonly Action<string, string, string> updateOverlay;

  private int activeRequestId;
  private string currentSourceLanguageCode = string.Empty;
  private string currentOriginalText = string.Empty;
  private string currentReplacementText = string.Empty;
  private string currentTranslatedText = string.Empty;
  private string lastFailedOriginalText = string.Empty;
  private string lastFailedSourceLanguageCode = string.Empty;
  private bool translationInFlight;

  /// <summary>
  ///     Initializes a new instance of the <see cref="TalkSubtitleHandler" /> class.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The translation service used by the plugin.</param>
  /// <param name="findTalkSubtitleMessage">
  ///     Delegate used to look up previously translated TalkSubtitle messages.
  /// </param>
  /// <param name="insertTalkSubtitleMessageAsync">
  ///     Delegate used to persist translated TalkSubtitle messages.
  /// </param>
  /// <param name="updateOverlay">
  ///     Delegate used to publish translated content to the TalkSubtitle overlay.
  /// </param>
  /// <param name="clearOverlay">
  ///     Delegate used to clear the TalkSubtitle overlay state.
  /// </param>
  /// <param name="normalizeReplacementText">
  ///     Delegate used to normalize translated text before native replacement.
  /// </param>
  public TalkSubtitleHandler(
      Config config,
      TranslationService translationService,
      Func<TalkSubtitleMessage, TalkSubtitleMessage?> findTalkSubtitleMessage,
      Func<TalkSubtitleMessage, Task<string>> insertTalkSubtitleMessageAsync,
      Action<string, string, string> updateOverlay,
      Action clearOverlay,
      Func<string, string> normalizeReplacementText)
  {
    this.config = config;
    this.translationService = translationService;
    this.findTalkSubtitleMessage = findTalkSubtitleMessage;
    this.insertTalkSubtitleMessageAsync = insertTalkSubtitleMessageAsync;
    this.updateOverlay = updateOverlay;
    this.clearOverlay = clearOverlay;
    this.normalizeReplacementText = normalizeReplacementText;

    this.RegisterHandler(AddonEvent.PreSetup, this.OnCaptureTalkSubtitle);
    this.RegisterHandler(AddonEvent.PreRefresh, this.OnCaptureTalkSubtitle);
    this.RegisterHandler(AddonEvent.PostUpdate, this.OnApplyNativeTalkSubtitleText);
    this.RegisterHandler(AddonEvent.PreDraw, this.OnApplyNativeTalkSubtitleText);
    this.RegisterHandler(AddonEvent.PreHide, this.OnResetState);
    this.RegisterHandler(AddonEvent.PreFinalize, this.OnResetState);
  }

  /// <summary>
  ///     Returns the event handlers required to drive the TalkSubtitle addon flow.
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
    const VisibleStorySurfaceKind surface = VisibleStorySurfaceKind.TalkSubtitle;
    var surfaceName = VisibleStorySurfaceText.ResolveSurfaceName(surface);
    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      this.InvalidateStateForSource(null);
      return new VisibleDialogueRetranslationResult(
          false,
          false,
          surface,
          surfaceName,
          VisibleStorySurfaceText.GetNoVisibleTextMessage(surface));
    }

    this.InvalidateStateForSource(sourceLanguage);

    string originalText;
    int requestId;

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
      this.currentSourceLanguageCode = sourceLanguage.PersistenceCode;
      this.currentTranslatedText = string.Empty;
      this.currentReplacementText = string.Empty;
      this.translationInFlight = true;
    }

    try
    {
      var translatedText = await this.translationService.TranslateAsync(
          originalText,
          sourceLanguage.ProviderCode,
          LangDict[LanguageInt].Code,
          TranslationSurfaceGroup.Dialogue).ConfigureAwait(false) ?? string.Empty;

      if (!TranslationPersistenceGuard.IsUsableDialogueTranslation(
              originalText,
              translatedText,
              sourceLanguage.PersistenceCode,
              LangDict[LanguageInt].Code))
      {
        lock (this.stateGate)
        {
          if (requestId == this.activeRequestId)
          {
            this.translationInFlight = false;
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
      var translatedTalkSubtitle = new TalkSubtitleMessage(
          originalText,
          sourceLanguage.PersistenceCode,
          translatedText,
          LangDict[LanguageInt].Code,
          dialogueTranslationEngine,
          DateTime.Now,
          DateTime.Now);
      var persistenceResult = await this.insertTalkSubtitleMessageAsync(
          translatedTalkSubtitle).ConfigureAwait(false);
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
        sourceChangedBeforeApply =
            requestId != this.activeRequestId ||
            !NativeRuntimeSourceScope.MatchesSource(
                this.currentSourceLanguageCode,
                sourceLanguage);
        if (!sourceChangedBeforeApply)
        {
          this.translationInFlight = false;
          this.currentTranslatedText = translatedText;
          this.currentReplacementText = replacementText;
        }
      }

      if (!sourceChangedBeforeApply)
      {
        this.RecordDiagnosticsSnapshot(
            VisibleStorySurfaceProvenanceKind.FreshLiveTranslation,
            originalText,
            translatedText,
            effectiveTranslationEngineId: dialogueTranslationEngine);
        this.PublishOverlay(originalText, translatedText);
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
        }
      }

      PluginRuntimeLog.Error(
          $"[{TalkSubtitleAddonName}] Error retranslating visible TalkSubtitle text: {ex}");
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
  ///     Captures TalkSubtitle source text early in the lifecycle so a translation
  ///     can already be queued before the first draw pass completes.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the handler.</param>
  /// <param name="args">The addon arguments associated with the event.</param>
  private unsafe void OnCaptureTalkSubtitle(AddonEvent type, AddonArgs args)
  {
    if (args.AddonName != TalkSubtitleAddonName)
    {
      return;
    }

    AtkValue* atkValues;
    if (args is AddonSetupArgs setupArgs)
    {
      atkValues = (AtkValue*)setupArgs.AtkValues;
    }
    else if (args is AddonRefreshArgs refreshArgs)
    {
      atkValues = (AtkValue*)refreshArgs.AtkValues;
    }
    else
    {
      return;
    }

    if (atkValues == null)
    {
      return;
    }

    if (atkValues[0].Type != ValueType.String ||
        !atkValues[0].String.HasValue)
    {
      return;
    }

    var originalText = MemoryHelper.ReadSeStringAsString(
        out _,
        (nint)atkValues[0].String.Value);

    if (string.IsNullOrWhiteSpace(originalText))
    {
      return;
    }

    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      this.InvalidateStateForSource(null);
      return;
    }

    this.InvalidateStateForSource(sourceLanguage);

    if (this.TryGetCachedTranslation(
            originalText,
            sourceLanguage,
            out var translatedText,
            out var replacementText))
    {
      this.SetResolvedState(
          originalText,
          translatedText,
          replacementText,
          sourceLanguage);
      this.PublishOverlay(originalText, translatedText);

      if (this.ShouldApplyNativeTalkSubtitleText())
      {
        atkValues[0].SetManagedString(replacementText);
      }

      return;
    }

    if (this.TryLoadStoredTranslation(
            originalText,
            sourceLanguage,
            out var storedTranslatedText,
            out var storedReplacementText))
    {
      this.SetResolvedState(
          originalText,
          storedTranslatedText,
          storedReplacementText,
          sourceLanguage);
      this.PublishOverlay(originalText, storedTranslatedText);

      if (this.ShouldApplyNativeTalkSubtitleText())
      {
        atkValues[0].SetManagedString(storedReplacementText);
      }

      return;
    }

    if (this.ShouldApplyNativeTalkSubtitleText())
    {
      atkValues[0].SetManagedString(string.Empty);
    }

    if (this.TryQueueTranslation(
            originalText,
            sourceLanguage,
            out var requestId))
    {
      this.ClearOverlayForPendingState(originalText);
      Task.Run(() => this.ResolveTranslationAsync(
          originalText,
          requestId,
          sourceLanguage));
    }
  }

  /// <summary>
  ///     Applies translated TalkSubtitle text to the visible addon when the native
  ///     replacement path is active.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the handler.</param>
  /// <param name="args">The addon arguments associated with the event.</param>
  private unsafe void OnApplyNativeTalkSubtitleText(
      AddonEvent type,
      AddonArgs args)
  {
    if (args.AddonName != TalkSubtitleAddonName ||
        !this.ShouldApplyNativeTalkSubtitleText())
    {
      return;
    }

    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      this.InvalidateStateForSource(null);
      return;
    }

    this.InvalidateStateForSource(sourceLanguage);

    var addonPtr = GameGuiInterface.GetAddonByName(TalkSubtitleAddonName);
    if (addonPtr.Address == IntPtr.Zero)
    {
      return;
    }

    var talkSubtitleAddon = (AtkUnitBase*)addonPtr.Address;
    if (talkSubtitleAddon == null || !talkSubtitleAddon->IsVisible)
    {
      return;
    }

    if (!this.TryGetCurrentResolvedTranslation(
            sourceLanguage,
            out var translatedText,
            out var replacementText))
    {
      return;
    }

    var textNode = talkSubtitleAddon->GetTextNodeById(TextNodeId);
    var altTextNode = talkSubtitleAddon->GetTextNodeById(AltTextNodeId);
    var altTextNode2 = talkSubtitleAddon->GetTextNodeById(AltTextNodeId2);

    if (textNode == null || textNode->NodeText.IsEmpty)
    {
      return;
    }

    var visibleText = this.ReadNodeText(textNode);
    if (this.TextMatches(visibleText, replacementText))
    {
      return;
    }

    textNode->SetText(replacementText);
    if (altTextNode != null)
    {
      altTextNode->SetText(replacementText);
    }

    if (altTextNode2 != null)
    {
      altTextNode2->SetText(replacementText);
    }

    if (this.ShouldSwapTexts())
    {
      this.PublishOverlay(this.currentOriginalText, translatedText);
    }
  }

  /// <summary>
  ///     Restores plugin-owned subtitle text and clears resolved state when the
  ///     operation source changes or cannot be resolved.
  /// </summary>
  /// <param name="sourceLanguage">
  ///     The operation-captured source, or no value when source resolution
  ///     failed.
  /// </param>
  private unsafe void InvalidateStateForSource(
      SourceClientLanguage? sourceLanguage)
  {
    var originalText = string.Empty;
    var replacementText = string.Empty;
    var shouldInvalidate = false;
    lock (this.stateGate)
    {
      shouldInvalidate =
          !string.IsNullOrWhiteSpace(this.currentSourceLanguageCode) &&
          !NativeRuntimeSourceScope.MatchesSource(
              this.currentSourceLanguageCode,
              sourceLanguage);
      if (shouldInvalidate)
      {
        originalText = this.currentOriginalText;
        replacementText = this.currentReplacementText;
      }
    }

    if (!shouldInvalidate)
    {
      if (!sourceLanguage.HasValue)
      {
        this.clearOverlay();
      }

      return;
    }

    var addonPtr = GameGuiInterface.GetAddonByName(TalkSubtitleAddonName);
    if (addonPtr.Address != IntPtr.Zero)
    {
      var addon = (AtkUnitBase*)addonPtr.Address;
      if (addon != null && addon->IsVisible)
      {
        foreach (var nodeId in new[]
                 {
                   TextNodeId,
                   AltTextNodeId,
                   AltTextNodeId2,
                 })
        {
          var textNode = addon->GetTextNodeById((uint)nodeId);
          if (textNode != null &&
              this.TextMatches(this.ReadNodeText(textNode), replacementText))
          {
            textNode->SetText(originalText);
          }
        }
      }
    }

    lock (this.stateGate)
    {
      this.activeRequestId++;
      this.currentSourceLanguageCode = string.Empty;
      this.currentOriginalText = string.Empty;
      this.currentTranslatedText = string.Empty;
      this.currentReplacementText = string.Empty;
      this.translationInFlight = false;
      this.lastFailedOriginalText = string.Empty;
      this.lastFailedSourceLanguageCode = string.Empty;
    }

    VisibleStorySurfaceDiagnosticsStore.Clear(
        VisibleStorySurfaceKind.TalkSubtitle);
    this.clearOverlay();
  }

  /// <summary>
  ///     Clears the in-memory TalkSubtitle state when the addon hides or is finalized.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the reset.</param>
  /// <param name="args">The addon arguments associated with the reset.</param>
  private void OnResetState(AddonEvent type, AddonArgs args)
  {
    lock (this.stateGate)
    {
      this.activeRequestId++;
      this.currentSourceLanguageCode = string.Empty;
      this.currentOriginalText = string.Empty;
      this.currentTranslatedText = string.Empty;
      this.currentReplacementText = string.Empty;
      this.translationInFlight = false;
      this.lastFailedOriginalText = string.Empty;
      this.lastFailedSourceLanguageCode = string.Empty;
    }

    VisibleStorySurfaceDiagnosticsStore.Clear(VisibleStorySurfaceKind.TalkSubtitle);
    this.clearOverlay();
  }

  /// <summary>
  ///     Resolves a TalkSubtitle translation without blocking the game UI and
  ///     persists the result for future cache hits.
  /// </summary>
  /// <param name="originalText">The original TalkSubtitle text.</param>
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
          sourceLanguage.ProviderCode,
          LangDict[LanguageInt].Code,
          TranslationSurfaceGroup.Dialogue) ?? string.Empty;
    }
    catch (Exception ex)
    {
      PluginRuntimeLog.Debug(
          $"{this.GetType().Name}.ResolveTranslationAsync exception {ex}");
      translatedText = string.Empty;
    }

    if (string.IsNullOrWhiteSpace(translatedText))
    {
      lock (this.stateGate)
      {
        if (requestId == this.activeRequestId &&
            NativeRuntimeSourceScope.MatchesSource(
                this.currentSourceLanguageCode,
                sourceLanguage) &&
            this.TextMatches(this.currentOriginalText, originalText))
        {
          this.translationInFlight = false;
          this.lastFailedOriginalText = originalText;
          this.lastFailedSourceLanguageCode =
              sourceLanguage.PersistenceCode;
        }
      }

      return;
    }

    var replacementText = this.NormalizeForReplacement(translatedText);
    var translatedTalkSubtitle = new TalkSubtitleMessage(
        originalText,
        sourceLanguage.PersistenceCode,
        translatedText,
        LangDict[LanguageInt].Code,
        this.GetDialogueTranslationEngineId(),
        DateTime.Now,
        DateTime.Now);

    await this.insertTalkSubtitleMessageAsync(translatedTalkSubtitle);

    lock (this.stateGate)
    {
      if (requestId != this.activeRequestId ||
          !NativeRuntimeSourceScope.MatchesSource(
              this.currentSourceLanguageCode,
              sourceLanguage))
      {
        return;
      }

      this.currentTranslatedText = translatedText;
      this.currentReplacementText = replacementText;
      this.translationInFlight = false;
      this.lastFailedOriginalText = string.Empty;
      this.lastFailedSourceLanguageCode = string.Empty;
    }

    this.RecordDiagnosticsSnapshot(
        VisibleStorySurfaceProvenanceKind.FreshLiveTranslation,
        originalText,
        translatedText,
        effectiveTranslationEngineId: this.GetDialogueTranslationEngineId());
    this.PublishOverlay(originalText, translatedText);
  }

  /// <summary>
  ///     Determines whether the current captured source still has a cached
  ///     translation.
  /// </summary>
  /// <param name="originalText">The source TalkSubtitle text.</param>
  /// <param name="translatedText">Receives the translated text.</param>
  /// <param name="replacementText">Receives the normalized replacement text.</param>
  /// <returns>
  ///     <see langword="true" /> when a matching cached translation exists;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryGetCachedTranslation(
      string originalText,
      SourceClientLanguage sourceLanguage,
      out string translatedText,
      out string replacementText)
  {
    lock (this.stateGate)
    {
      if (NativeRuntimeSourceScope.MatchesSource(
              this.currentSourceLanguageCode,
              sourceLanguage) &&
          this.TextMatches(this.currentOriginalText, originalText) &&
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
  ///     Attempts to load a stored TalkSubtitle translation from the database.
  /// </summary>
  /// <param name="originalText">The source TalkSubtitle text.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <param name="translatedText">Receives the translated text.</param>
  /// <param name="replacementText">Receives the normalized replacement text.</param>
  /// <returns>
  ///     <see langword="true" /> when a stored translation exists;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryLoadStoredTranslation(
      string originalText,
      SourceClientLanguage sourceLanguage,
      out string translatedText,
      out string replacementText)
  {
    var lookup = this.findTalkSubtitleMessage(
        this.BuildLookupMessage(originalText, sourceLanguage));
    if (lookup == null ||
        !string.Equals(
            lookup.OriginalTalkSubtitleMessage,
            originalText,
            StringComparison.Ordinal) ||
        string.IsNullOrWhiteSpace(lookup.TranslatedTalkSubtitleMessage))
    {
      translatedText = string.Empty;
      replacementText = string.Empty;
      return false;
    }

    translatedText = lookup.TranslatedTalkSubtitleMessage!;
    replacementText = this.NormalizeForReplacement(translatedText);
    this.RecordDiagnosticsSnapshot(
        VisibleStorySurfaceProvenanceKind.DbReuse,
        originalText,
        translatedText,
        effectiveTranslationEngineId: this.GetDialogueTranslationEngineId());
    return true;
  }

  /// <summary>
  ///     Returns the active resolved translation currently held by the handler
  ///     state.
  /// </summary>
  /// <param name="translatedText">Receives the translated TalkSubtitle text.</param>
  /// <param name="replacementText">Receives the normalized replacement text.</param>
  /// <returns>
  ///     <see langword="true" /> when the handler already has a translated line;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryGetCurrentResolvedTranslation(
      SourceClientLanguage sourceLanguage,
      out string translatedText,
      out string replacementText)
  {
    lock (this.stateGate)
    {
      if (NativeRuntimeSourceScope.MatchesSource(
              this.currentSourceLanguageCode,
              sourceLanguage) &&
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
  ///     Starts a new translation request when the source line changes or when
  ///     the current line still has no cached translation available.
  /// </summary>
  /// <param name="originalText">The source TalkSubtitle text.</param>
  /// <param name="requestId">Receives the active request identifier.</param>
  /// <returns>
  ///     <see langword="true" /> when a new translation task should be queued;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryQueueTranslation(
      string originalText,
      SourceClientLanguage sourceLanguage,
      out int requestId)
  {
    lock (this.stateGate)
    {
      if (NativeRuntimeSourceScope.MatchesSource(
              this.currentSourceLanguageCode,
              sourceLanguage) &&
          this.TextMatches(this.currentOriginalText, originalText))
      {
        if (this.translationInFlight ||
            !string.IsNullOrWhiteSpace(this.currentTranslatedText) ||
            (NativeRuntimeSourceScope.MatchesSource(
                 this.lastFailedSourceLanguageCode,
                 sourceLanguage) &&
             this.TextMatches(this.lastFailedOriginalText, originalText)))
        {
          requestId = this.activeRequestId;
          return false;
        }
      }

      this.activeRequestId++;
      this.currentSourceLanguageCode = sourceLanguage.PersistenceCode;
      this.currentOriginalText = originalText;
      this.currentTranslatedText = string.Empty;
      this.currentReplacementText = string.Empty;
      this.translationInFlight = true;
      requestId = this.activeRequestId;
      return true;
    }
  }

  /// <summary>
  ///     Sets the resolved in-memory state for the current TalkSubtitle line.
  /// </summary>
  /// <param name="originalText">The original TalkSubtitle text.</param>
  /// <param name="translatedText">The translated TalkSubtitle text.</param>
  /// <param name="replacementText">The translated text normalized for native replacement.</param>
  private void SetResolvedState(
      string originalText,
      string translatedText,
      string replacementText,
      SourceClientLanguage sourceLanguage)
  {
    lock (this.stateGate)
    {
      this.currentSourceLanguageCode = sourceLanguage.PersistenceCode;
      this.currentOriginalText = originalText;
      this.currentTranslatedText = translatedText;
      this.currentReplacementText = replacementText;
      this.translationInFlight = false;
      this.lastFailedOriginalText = string.Empty;
      this.lastFailedSourceLanguageCode = string.Empty;
    }
  }

  /// <summary>
  ///     Publishes translated TalkSubtitle content to the configured overlay when
  ///     overlay mode is enabled.
  /// </summary>
  /// <param name="originalText">The original TalkSubtitle text.</param>
  /// <param name="translatedText">The translated TalkSubtitle text.</param>
  private void PublishOverlay(string originalText, string translatedText)
  {
    if (!this.ShouldUseOverlay())
    {
      this.clearOverlay();
      return;
    }

    var overlayText = this.ShouldSwapTexts()
        ? originalText
        : translatedText;

    this.updateOverlay(string.Empty, overlayText, string.Empty);
  }

  /// <summary>
  ///     Determines whether this TalkSubtitle request should render through the
  ///     overlay path.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when the overlay path is active; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool ShouldUseOverlay()
  {
    return this.config.TranslateTalkSubtitle &&
           TranslationDisplayModeHelper.UsesOverlayPresentation(
               this.config.TalkSubtitleTranslationDisplayMode,
               this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Determines whether the TalkSubtitle addon should receive translated text
  ///     directly in the game UI.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when the addon should be replaced natively;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool ShouldApplyNativeTalkSubtitleText()
  {
    return TranslationDisplayModeHelper.WritesNativeTranslation(
        this.config.TalkSubtitleTranslationDisplayMode,
        this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Determines whether the TalkSubtitle overlay should show the original
  ///     text while the native addon receives the translation.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when TalkSubtitle swap mode is active;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool ShouldSwapTexts()
  {
    return TranslationDisplayModeHelper.ShowsOriginalOverlayText(
        this.config.TalkSubtitleTranslationDisplayMode,
        this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Builds a lookup entity matching the historical TalkSubtitle schema
  ///     already used in the database.
  /// </summary>
  /// <param name="originalText">The original TalkSubtitle text.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <returns>
  ///     A formatted <see cref="TalkSubtitleMessage" /> suitable for DB lookup.
  /// </returns>
  private TalkSubtitleMessage BuildLookupMessage(
      string originalText,
      SourceClientLanguage sourceLanguage)
  {
    return new TalkSubtitleMessage(
        originalText,
        sourceLanguage.PersistenceCode,
        string.Empty,
        LangDict[LanguageInt].Code,
        this.GetDialogueTranslationEngineId(),
        DateTime.Now,
        DateTime.Now);
  }

  /// <summary>
  ///     Resolves the effective translation engine identifier for the current
  ///     dialogue-family routing path.
  /// </summary>
  /// <returns>The effective dialogue-family translation engine identifier.</returns>
  private int GetDialogueTranslationEngineId()
  {
    return this.translationService.GetEffectiveTranslationEngineId(
        TranslationSurfaceGroup.Dialogue);
  }

  /// <summary>
  ///     Records the latest visible TalkSubtitle provenance snapshot for the
  ///     debugger.
  /// </summary>
  /// <param name="provenance">The provenance label kind to expose.</param>
  /// <param name="originalText">The original subtitle text.</param>
  /// <param name="translatedText">The translated subtitle text.</param>
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
            VisibleStorySurfaceKind.TalkSubtitle,
            provenance,
            VisibleStorySurfaceTableMap.Resolve(
                VisibleStorySurfaceKind.TalkSubtitle),
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
  ///     Normalizes translated TalkSubtitle text before native replacement when
  ///     the active config requests diacritic stripping.
  /// </summary>
  /// <param name="text">The translated TalkSubtitle text to normalize.</param>
  /// <returns>The text that should be written back into the native addon.</returns>
  private string NormalizeForReplacement(string text)
  {
    return this.config.RemoveDiacriticsWhenUsingReplacementTalkBTalk
        ? this.normalizeReplacementText(text)
        : text;
  }

  /// <summary>
  ///     Reads the current string value from a TalkSubtitle text node.
  /// </summary>
  /// <param name="textNode">The text node to inspect.</param>
  /// <returns>The visible node text, or an empty string when unavailable.</returns>
  private unsafe string ReadNodeText(AtkTextNode* textNode)
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
  ///     Compares two rendered text values after normalizing spacing and the
  ///     optional diacritic removal rules used for native replacement.
  /// </summary>
  /// <param name="left">The first text value.</param>
  /// <param name="right">The second text value.</param>
  /// <returns><see langword="true" /> when the texts should be considered equal.</returns>
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
  ///     Publishes the original TalkSubtitle line while a translation is still in
  ///     flight so swap mode does not leave the overlay blank.
  /// </summary>
  /// <param name="originalText">The original TalkSubtitle text.</param>
  private void ClearOverlayForPendingState(string originalText)
  {
    if (this.ShouldSwapTexts())
    {
      this.updateOverlay(string.Empty, originalText, string.Empty);
      return;
    }

    this.clearOverlay();
  }
}


