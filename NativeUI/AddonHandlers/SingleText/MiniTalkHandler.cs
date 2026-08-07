// <copyright file="MiniTalkHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;

namespace Echoglossian.NativeUI.AddonHandlers.SingleText;

/// <summary>
///     Resolves the live MiniTalk bubble text-node instances that are currently
///     readable from the addon tree.
/// </summary>
/// <param name="addon">The visible "_MiniTalk" addon instance.</param>
/// <returns>The bubble text-node addresses in tree order.</returns>
internal unsafe delegate List<nint> ResolveMiniTalkBubbleNodesDelegate(
    AtkUnitBase* addon);

/// <summary>
///     Synchronizes the overlay bounds for a single MiniTalk bubble instance.
/// </summary>
/// <param name="bubbleKey">The stable bubble key.</param>
/// <param name="addon">The visible "_MiniTalk" addon instance.</param>
/// <param name="textNode">The visible bubble text node.</param>
internal unsafe delegate void SyncMiniTalkBubbleOverlayBoundsDelegate(
    nint bubbleKey,
    AtkUnitBase* addon,
    AtkTextNode* textNode);

/// <summary>
///     Updates the overlay content for a single MiniTalk bubble instance.
/// </summary>
/// <param name="bubbleKey">The stable bubble key.</param>
/// <param name="translatedName">Translated speaker/title.</param>
/// <param name="translatedText">Translated content.</param>
/// <param name="originalName">Original speaker/title.</param>
internal delegate void UpdateMiniTalkBubbleOverlayDelegate(
    nint bubbleKey,
    string translatedName,
    string translatedText,
    string originalName);

/// <summary>
///     Clears the overlay state for a single MiniTalk bubble instance.
/// </summary>
/// <param name="bubbleKey">The stable bubble key.</param>
/// <param name="clearText">Whether to clear the translated text.</param>
internal delegate void ClearMiniTalkBubbleOverlayDelegate(
    nint bubbleKey,
    bool clearText);

/// <summary>
///     Handles the "MiniTalk" addon runtime inside the new addon-handler model.
///     MiniTalk behaves like a lightweight single-text addon, so it reuses the
///     same cache-first async translation flow used by the other single-text
///     handlers while keeping its overlay titleless.
/// </summary>
internal sealed class MiniTalkHandler : IAddonTranslationHandler
{
  private const string MiniTalkAddonName = "_MiniTalk";
  private const string MiniTalkType = "MiniTalk";

  private readonly Config config;
  private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>> eventHandlers = new();
  private readonly Func<MiniTalkMessage, MiniTalkMessage?> findMiniTalkMessage;
  private readonly Func<MiniTalkMessage, CancellationToken, Task<string>> insertMiniTalkMessageAsync;
  private readonly Func<string, string> normalizeReplacementText;
  private readonly SourcePublicationLifecycle sourceLifecycle = new();
  private readonly object stateGate = new();
  private readonly TranslationService translationService;
  private readonly ResolveMiniTalkBubbleNodesDelegate resolveMiniTalkBubbleTextNodes;
  private readonly UpdateMiniTalkBubbleOverlayDelegate updateOverlay;
  private readonly ClearMiniTalkBubbleOverlayDelegate clearOverlay;
  private readonly SyncMiniTalkBubbleOverlayBoundsDelegate updateOverlayBounds;
  private readonly Dictionary<nint, BubbleState> bubbleStates = new();

  /// <summary>
  ///     In-memory state for a single MiniTalk bubble instance.
  /// </summary>
  private sealed class BubbleState
  {
    public int ActiveRequestId;

    public NativeTextNodeLayoutSnapshot? NativeLayoutSnapshot { get; set; }

    public string NativeLayoutOriginalText { get; set; } = string.Empty;

    public string NativeLayoutReplacementText { get; set; } = string.Empty;

    public string CurrentOriginalText { get; set; } = string.Empty;

    public string CurrentSourceLanguageCode { get; set; } = string.Empty;

    public string CurrentReplacementText { get; set; } = string.Empty;

    public string CurrentTranslatedText { get; set; } = string.Empty;

    public string LastFailedOriginalText { get; set; } = string.Empty;

    public string LastFailedSourceLanguageCode { get; set; } = string.Empty;

    public bool TranslationInFlight { get; set; }
  }

  /// <summary>
  ///     Initializes a new instance of the <see cref="MiniTalkHandler" /> class.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The translation service used by the plugin.</param>
  /// <param name="findMiniTalkMessage">
  ///     Delegate used to look up previously translated MiniTalk messages.
  /// </param>
  /// <param name="insertMiniTalkMessageAsync">
  ///     Delegate used to persist translated MiniTalk messages.
  /// </param>
  /// <param name="updateOverlay">
  ///     Delegate used to publish translated content to the MiniTalk overlay.
  /// </param>
  /// <param name="clearOverlay">
  ///     Delegate used to clear the MiniTalk overlay state.
  /// </param>
  /// <param name="normalizeReplacementText">
  ///     Delegate used to normalize translated text before native replacement.
  /// </param>
  public unsafe MiniTalkHandler(
      Config config,
      TranslationService translationService,
      Func<MiniTalkMessage, MiniTalkMessage?> findMiniTalkMessage,
      Func<MiniTalkMessage, Task<string>> insertMiniTalkMessageAsync,
      UpdateMiniTalkBubbleOverlayDelegate updateOverlay,
      ClearMiniTalkBubbleOverlayDelegate clearOverlay,
      SyncMiniTalkBubbleOverlayBoundsDelegate updateOverlayBounds,
      ResolveMiniTalkBubbleNodesDelegate resolveMiniTalkBubbleTextNodes,
      Func<string, string> normalizeReplacementText)
    : this(
        config,
        translationService,
        findMiniTalkMessage,
        (message, _) => insertMiniTalkMessageAsync(message),
        updateOverlay,
        clearOverlay,
        updateOverlayBounds,
        resolveMiniTalkBubbleTextNodes,
        normalizeReplacementText)
  {
  }

  /// <summary>
  ///     Initializes a MiniTalk handler with cancellation-aware dialogue
  ///     persistence owned by the captured operation scope.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The shared translation service.</param>
  /// <param name="findMiniTalkMessage">The canonical MiniTalk lookup.</param>
  /// <param name="insertMiniTalkMessageAsync">The cancellation-aware persistence delegate.</param>
  /// <param name="updateOverlay">The overlay publication callback.</param>
  /// <param name="clearOverlay">The overlay clear callback.</param>
  /// <param name="updateOverlayBounds">The overlay-bounds synchronization callback.</param>
  /// <param name="resolveMiniTalkBubbleTextNodes">The native bubble resolver.</param>
  /// <param name="normalizeReplacementText">The native replacement normalizer.</param>
  internal unsafe MiniTalkHandler(
      Config config,
      TranslationService translationService,
      Func<MiniTalkMessage, MiniTalkMessage?> findMiniTalkMessage,
      Func<MiniTalkMessage, CancellationToken, Task<string>> insertMiniTalkMessageAsync,
      UpdateMiniTalkBubbleOverlayDelegate updateOverlay,
      ClearMiniTalkBubbleOverlayDelegate clearOverlay,
      SyncMiniTalkBubbleOverlayBoundsDelegate updateOverlayBounds,
      ResolveMiniTalkBubbleNodesDelegate resolveMiniTalkBubbleTextNodes,
      Func<string, string> normalizeReplacementText)
  {
    this.config = config;
    this.translationService = translationService;
    this.findMiniTalkMessage = findMiniTalkMessage;
    this.insertMiniTalkMessageAsync = insertMiniTalkMessageAsync;
    this.updateOverlay = updateOverlay;
    this.clearOverlay = clearOverlay;
    this.updateOverlayBounds = updateOverlayBounds;
    this.resolveMiniTalkBubbleTextNodes = resolveMiniTalkBubbleTextNodes;
    this.normalizeReplacementText = normalizeReplacementText;

    this.RegisterHandler(AddonEvent.PreUpdate, this.OnPreUpdate);
    this.RegisterHandler(AddonEvent.PostUpdate, this.OnUpdateVisibleAddon);
    this.RegisterHandler(AddonEvent.PreDraw, this.OnUpdateVisibleAddon);
    this.RegisterHandler(AddonEvent.PreHide, this.OnResetState);
    this.RegisterHandler(AddonEvent.PreFinalize, this.OnResetState);
  }

  /// <summary>
  ///     Returns the event handlers required to drive the MiniTalk addon flow.
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
  ///     Gets or creates the state container for a MiniTalk bubble instance.
  /// </summary>
  /// <param name="bubbleKey">The stable bubble key.</param>
  /// <returns>The tracked state for the bubble.</returns>
  private BubbleState GetOrCreateBubbleState(nint bubbleKey)
  {
    lock (this.stateGate)
    {
      if (!this.bubbleStates.TryGetValue(bubbleKey, out var state))
      {
        state = new BubbleState();
        this.bubbleStates[bubbleKey] = state;
      }

      return state;
    }
  }

  /// <summary>
  ///     Tries to get the current state for a MiniTalk bubble instance.
  /// </summary>
  /// <param name="bubbleKey">The stable bubble key.</param>
  /// <param name="state">Receives the tracked state for the bubble.</param>
  /// <returns>
  ///     <see langword="true" /> when state exists; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool TryGetBubbleState(nint bubbleKey, out BubbleState state)
  {
    lock (this.stateGate)
    {
      return this.bubbleStates.TryGetValue(bubbleKey, out state!);
    }
  }

  /// <summary>
  ///     Removes stale MiniTalk overlays that no longer correspond to visible
  ///     bubble nodes in the current frame.
  /// </summary>
  /// <param name="activeBubbleKeys">The bubble keys seen in this frame.</param>
  private void PruneHiddenMiniTalkBubbles(HashSet<nint> activeBubbleKeys)
  {
    List<KeyValuePair<nint, BubbleState>> hiddenBubbleStates;
    lock (this.stateGate)
    {
      hiddenBubbleStates = this.bubbleStates
          .Where(kvp => !activeBubbleKeys.Contains(kvp.Key))
          .ToList();

      foreach (var hiddenBubbleState in hiddenBubbleStates)
      {
        this.bubbleStates.Remove(hiddenBubbleState.Key);
      }
    }

    foreach (var hiddenBubbleState in hiddenBubbleStates)
    {
      this.TryRestoreNativeLayout(
          hiddenBubbleState.Value.NativeLayoutSnapshot,
          hiddenBubbleState.Value.NativeLayoutOriginalText,
          hiddenBubbleState.Value.NativeLayoutReplacementText);
      this.clearOverlay(hiddenBubbleState.Key, false);
    }
  }

  /// <summary>
  ///     Restores any native MiniTalk mutation currently tracked for the bubble
  ///     when the source text changes or native mode is left.
  /// </summary>
  /// <param name="bubbleKey">The stable bubble key.</param>
  /// <param name="nextOriginalText">
  ///     The next original source text expected for the bubble, or an empty value
  ///     when the current mutation should always be restored.
  /// </param>
  private void RestoreTrackedBubbleLayoutIfNeeded(
      nint bubbleKey,
      string? nextOriginalText = null)
  {
    NativeTextNodeLayoutSnapshot? snapshot = null;
    string originalText = string.Empty;
    string replacementText = string.Empty;
    var restoreText = string.IsNullOrWhiteSpace(nextOriginalText);

    lock (this.stateGate)
    {
      if (!this.bubbleStates.TryGetValue(bubbleKey, out var state) ||
          state.NativeLayoutSnapshot == null)
      {
        return;
      }

      if (!string.IsNullOrWhiteSpace(nextOriginalText) &&
          this.TextMatches(state.NativeLayoutOriginalText, nextOriginalText))
      {
        return;
      }

      snapshot = state.NativeLayoutSnapshot;
      originalText = state.NativeLayoutOriginalText;
      replacementText = state.NativeLayoutReplacementText;
      state.NativeLayoutSnapshot = null;
      state.NativeLayoutOriginalText = string.Empty;
      state.NativeLayoutReplacementText = string.Empty;
    }

    this.TryRestoreNativeLayout(
        snapshot,
        originalText,
        replacementText,
        restoreText);
  }

  /// <summary>
  ///     Restores the tracked native MiniTalk layout for the current bubble even
  ///     when the source line has not changed, so repeated native apply passes
  ///     always restart from the original bubble geometry instead of stacking
  ///     width and height growth from the prior frame.
  /// </summary>
  /// <param name="bubbleKey">The stable bubble key.</param>
  /// <param name="originalText">The original source line currently assigned to the bubble.</param>
  private void PrepareTrackedBubbleLayoutForReapply(
      nint bubbleKey,
      string originalText)
  {
    NativeTextNodeLayoutSnapshot? snapshot = null;
    string replacementText = string.Empty;

    lock (this.stateGate)
    {
      if (!this.bubbleStates.TryGetValue(bubbleKey, out var state) ||
          state.NativeLayoutSnapshot == null ||
          !this.TextMatches(state.NativeLayoutOriginalText, originalText))
      {
        return;
      }

      snapshot = state.NativeLayoutSnapshot;
      replacementText = state.NativeLayoutReplacementText;
      state.NativeLayoutSnapshot = null;
      state.NativeLayoutOriginalText = string.Empty;
      state.NativeLayoutReplacementText = string.Empty;
    }

    this.TryRestoreNativeLayout(
        snapshot,
        originalText,
        replacementText,
        restoreText: false);
  }

  /// <summary>
  ///     Restores one tracked native MiniTalk layout snapshot back to the
  ///     original game state.
  /// </summary>
  /// <param name="snapshot">The captured layout snapshot.</param>
  /// <param name="originalText">The original text to write back.</param>
  /// <param name="replacementText">The exact plugin-applied replacement.</param>
  private unsafe void TryRestoreNativeLayout(
      NativeTextNodeLayoutSnapshot? snapshot,
      string originalText,
      string replacementText,
      bool restoreText = true)
  {
    if (snapshot == null)
    {
      return;
    }

    var textNode = (AtkTextNode*)snapshot.TextNodeAddress;
    if (textNode == null)
    {
      return;
    }

    NativeMutationOwnership.TryRestoreWithLayoutFallback(
        this.ReadTextNode(textNode),
        replacementText,
        originalText,
        restoreText,
        restoredText => NativeTextNodeLayoutHelper.RestoreLayoutSnapshot(
            snapshot,
            restoredText,
            restoreText,
            restorePositions: false),
        () => NativeTextNodeLayoutHelper.RestoreLayoutSnapshot(
            snapshot,
            originalText,
            restoreText: false,
            restorePositions: false));
  }

  /// <summary>
  ///     Captures MiniTalk source text early in the lifecycle so a translation
  ///     can already be queued before the first draw pass completes.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the handler.</param>
  /// <param name="args">The addon arguments associated with the event.</param>
  private unsafe void OnPreUpdate(AddonEvent type, AddonArgs args)
  {
    if (!this.ShouldHandleMiniTalk(args, out var addon))
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

    var bubbleTextNodes = this.resolveMiniTalkBubbleTextNodes(addon);
    if (bubbleTextNodes == null || bubbleTextNodes.Count == 0)
    {
      // PluginRuntimeLog.Debug(
      //     $"[MiniTalk] trigger={type} addon=0x{((nint)addon):X} text-node unavailable");
      return;
    }

    var activeBubbleKeys = new HashSet<nint>();
    foreach (var bubbleNodeAddress in bubbleTextNodes)
    {
      var textNode = (AtkTextNode*)bubbleNodeAddress;
      if (!this.IsMiniTalkBubbleVisible(textNode))
      {
        this.clearOverlay(bubbleNodeAddress, false);
        continue;
      }

      activeBubbleKeys.Add(bubbleNodeAddress);

      if (!this.TryReadCurrentSource(bubbleNodeAddress, textNode, out var originalText))
      {
        // PluginRuntimeLog.Debug(
        //     $"[MiniTalk] trigger={type} addon=0x{((nint)addon):X} readable text unavailable {this.DescribeMiniTalkTextNode(textNode)}");
        continue;
      }

      this.updateOverlayBounds(bubbleNodeAddress, addon, textNode);

      // PluginRuntimeLog.Debug(
      //     $"[MiniTalk] trigger={type} addon=0x{((nint)addon):X} bubble=0x{bubbleNodeAddress:X} visible-capture overlay={this.ShouldUseOverlay()} native={this.ShouldApplyNativeText()} swap={this.ShouldSwapTexts()}");

      if (this.TryCaptureOrQueueMiniTalkSource(
              bubbleNodeAddress,
              originalText,
              type.ToString(),
              sourceLanguage))
      {
        continue;
      }
    }

    this.PruneHiddenMiniTalkBubbles(activeBubbleKeys);
  }

  /// <summary>
  ///     Updates overlay bounds for the visible MiniTalk addon and applies
  ///     translated text to the native addon when replacement mode is enabled.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the handler.</param>
  /// <param name="args">The addon arguments associated with the update or draw.</param>
  private unsafe void OnUpdateVisibleAddon(AddonEvent type, AddonArgs args)
  {
    if (!this.ShouldHandleMiniTalk(args, out var addon))
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

    var bubbleTextNodes = this.resolveMiniTalkBubbleTextNodes(addon);
    if (bubbleTextNodes == null || bubbleTextNodes.Count == 0)
    {
      // PluginRuntimeLog.Debug(
      //     $"[MiniTalk] trigger={type} addon=0x{((nint)addon):X} text-node unavailable");
      return;
    }

    var activeBubbleKeys = new HashSet<nint>();
    foreach (var bubbleNodeAddress in bubbleTextNodes)
    {
      var textNode = (AtkTextNode*)bubbleNodeAddress;
      if (!this.IsMiniTalkBubbleVisible(textNode))
      {
        this.clearOverlay(bubbleNodeAddress, false);
        continue;
      }

      activeBubbleKeys.Add(bubbleNodeAddress);
      if (!this.TryReadCurrentSource(bubbleNodeAddress, textNode, out var visibleOriginalText))
      {
        continue;
      }

      this.updateOverlayBounds(bubbleNodeAddress, addon, textNode);

      if (this.TryHandleVisibleSourceChange(
              bubbleNodeAddress,
              visibleOriginalText,
              $"{type}-visible-reconcile",
              sourceLanguage))
      {
        continue;
      }

      // PluginRuntimeLog.Debug(
      //     $"[MiniTalk] trigger={type} addon=0x{((nint)addon):X} bubble=0x{bubbleNodeAddress:X} visible-update overlay={this.ShouldUseOverlay()} native={this.ShouldApplyNativeText()} swap={this.ShouldSwapTexts()}");

      if (!this.TryGetCurrentResolvedTranslation(
              bubbleNodeAddress,
              sourceLanguage,
              out var resolvedOriginalText,
              out var translatedText,
              out var replacementText))
      {
        if (this.TryCaptureOrQueueMiniTalkSource(
                bubbleNodeAddress,
                visibleOriginalText,
                $"{type}-visible-fallback",
                sourceLanguage))
        {
          continue;
        }

        continue;
      }

      if (this.ShouldUseOverlay())
      {
        this.PublishOverlay(
            bubbleNodeAddress,
            resolvedOriginalText,
            translatedText,
            type.ToString());
        if (!this.ShouldSwapTexts())
        {
          continue;
        }
      }

      if (!this.ShouldApplyNativeText())
      {
        this.RestoreTrackedBubbleLayoutIfNeeded(bubbleNodeAddress);
        continue;
      }

      this.RestoreTrackedBubbleLayoutIfNeeded(
          bubbleNodeAddress,
          resolvedOriginalText);

      if (textNode == null)
      {
        // PluginRuntimeLog.Debug(
        //     $"[MiniTalk] trigger={type} addon=0x{((nint)addon):X} native text unavailable");
        continue;
      }

      var visibleText = this.ReadTextNode(textNode);
      if (this.TextMatches(visibleText, replacementText))
      {
        continue;
      }

      this.PrepareTrackedBubbleLayoutForReapply(
          bubbleNodeAddress,
          resolvedOriginalText);

      var layoutSnapshot = NativeTextNodeLayoutHelper.ApplyTextReplacementWithInferredReflow(
          addon,
          textNode,
          replacementText,
          allowWidthGrowth: true,
          preferCompactDetachedBaselineForHeight: true,
          additionalWrapWidth: this.ResolveMiniTalkAdditionalWrapWidth(
              addon,
              textNode));
      if (layoutSnapshot != null)
      {
        lock (this.stateGate)
        {
          if (this.bubbleStates.TryGetValue(bubbleNodeAddress, out var state))
          {
            state.NativeLayoutSnapshot = layoutSnapshot;
            state.NativeLayoutOriginalText = resolvedOriginalText;
            state.NativeLayoutReplacementText = replacementText;
          }
        }
      }

      this.updateOverlayBounds(bubbleNodeAddress, addon, textNode);
    }

    this.PruneHiddenMiniTalkBubbles(activeBubbleKeys);
  }

  /// <summary>
  ///     Clears the in-memory MiniTalk state when the addon hides or is finalized.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the reset.</param>
  /// <param name="args">The addon arguments associated with the reset.</param>
  private void OnResetState(AddonEvent type, AddonArgs args)
  {
    // PluginRuntimeLog.Debug($"[MiniTalk] trigger={type} resetting mini talk state");

    List<KeyValuePair<nint, BubbleState>> bubbleStates;
    lock (this.stateGate)
    {
      bubbleStates = this.bubbleStates.ToList();
      this.bubbleStates.Clear();
    }

    foreach (var bubbleState in bubbleStates)
    {
      this.TryRestoreNativeLayout(
          bubbleState.Value.NativeLayoutSnapshot,
          bubbleState.Value.NativeLayoutOriginalText,
          bubbleState.Value.NativeLayoutReplacementText);
      this.clearOverlay(bubbleState.Key, true);
    }
  }

  /// <summary>
  ///     Restores plugin-owned bubble mutations and clears all resolved state
  ///     when the operation source changes or cannot be resolved.
  /// </summary>
  /// <param name="sourceLanguage">
  ///     The operation-captured source, or no value when source resolution
  ///     failed.
  /// </param>
  private void InvalidateStateForSource(
      SourceClientLanguage? sourceLanguage)
  {
    this.sourceLifecycle.TransitionTo(
        sourceLanguage.HasValue
            ? this.CreateDialogueReuseScope(sourceLanguage.Value)
            : null,
        () =>
        {
          List<KeyValuePair<nint, BubbleState>> bubbleStates;
          lock (this.stateGate)
          {
            bubbleStates = this.bubbleStates.ToList();
            this.bubbleStates.Clear();
          }

          foreach (var bubbleState in bubbleStates)
          {
            this.TryRestoreNativeLayout(
                bubbleState.Value.NativeLayoutSnapshot,
                bubbleState.Value.NativeLayoutOriginalText,
                bubbleState.Value.NativeLayoutReplacementText);
            this.clearOverlay(bubbleState.Key, true);
          }
        });
  }

  /// <summary>
  ///     Resolves a MiniTalk translation without blocking the game UI and
  ///     persists the result for future cache hits.
  /// </summary>
  /// <param name="bubbleKey">The native bubble node address.</param>
  /// <param name="originalText">The original MiniTalk text.</param>
  /// <param name="requestId">The request identifier used to reject stale updates.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <param name="sourceOperation">The source generation captured when queued.</param>
  /// <returns>A task that completes when the translation attempt finishes.</returns>
  private async Task ResolveTranslationAsync(
      nint bubbleKey,
      string originalText,
      int requestId,
      SourceClientLanguage sourceLanguage,
      SourcePublicationOperation sourceOperation)
  {
    var operationScope = sourceOperation.Scope ??
                         this.CreateDialogueReuseScope(sourceLanguage);
    var translatorResolution = this.translationService
        .CaptureTranslatorResolution(
            operationScope.TranslationEngine.GetValueOrDefault(),
            TranslationSurfaceGroup.Dialogue);
    string translatedText;
    try
    {
      translatedText = await this.translationService.TranslateAsync(
          originalText,
          sourceLanguage,
          operationScope.TargetLanguageCode,
          TranslationSurfaceGroup.Dialogue,
          translatorResolution,
          originContext: "MiniTalk/Text") ?? string.Empty;
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
      //     $"[MiniTalk] trigger=async-resolve empty translation for source='{originalText}'");

      lock (this.stateGate)
      {
        if (this.TryGetBubbleState(bubbleKey, out var state) &&
            requestId == state.ActiveRequestId &&
            NativeRuntimeSourceScope.MatchesSource(
                state.CurrentSourceLanguageCode,
                sourceLanguage) &&
            this.TextMatches(state.CurrentOriginalText, originalText))
        {
          state.TranslationInFlight = false;
          state.LastFailedOriginalText = originalText;
          state.LastFailedSourceLanguageCode =
              sourceLanguage.PersistenceCode;
        }
      }

      return;
    }

    var replacementText = this.NormalizeForReplacement(translatedText);
    // PluginRuntimeLog.Debug(
    //     $"[MiniTalk] trigger=async-resolve translation ready for source='{originalText}'");

    var translatedMiniTalk = new MiniTalkMessage(
        originalText,
        sourceLanguage.PersistenceCode,
        translatedText,
        operationScope.TargetLanguageCode,
        operationScope.TranslationEngine.GetValueOrDefault(),
        DateTime.Now,
        DateTime.Now);

    if (!this.sourceLifecycle.IsCurrent(sourceOperation))
    {
      return;
    }

    await this.insertMiniTalkMessageAsync(
        translatedMiniTalk,
        sourceOperation.CancellationToken);

    this.sourceLifecycle.TryPublish(
        sourceOperation,
        () =>
        {
          lock (this.stateGate)
          {
            if (!this.TryGetBubbleState(bubbleKey, out var state) ||
                requestId != state.ActiveRequestId ||
                !NativeRuntimeSourceScope.MatchesSource(
                    state.CurrentSourceLanguageCode,
                    sourceLanguage))
            {
              return;
            }

            state.CurrentTranslatedText = translatedText;
            state.CurrentReplacementText = replacementText;
            state.TranslationInFlight = false;
            state.LastFailedOriginalText = string.Empty;
            state.LastFailedSourceLanguageCode = string.Empty;
          }

          this.PublishOverlay(bubbleKey, originalText, translatedText);
        });
  }

  /// <summary>
  ///     Determines whether the current captured source still has a cached
  ///     translation.
  /// </summary>
  /// <param name="originalText">The source MiniTalk text.</param>
  /// <param name="translatedText">Receives the translated text.</param>
  /// <param name="replacementText">Receives the normalized replacement text.</param>
  /// <returns>
  ///     <see langword="true" /> when a matching cached translation exists;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryGetCachedTranslation(
      nint bubbleKey,
      string originalText,
      SourceClientLanguage sourceLanguage,
      out string translatedText,
      out string replacementText)
  {
    lock (this.stateGate)
    {
      if (this.TryGetBubbleState(bubbleKey, out var state) &&
          NativeRuntimeSourceScope.MatchesSource(
              state.CurrentSourceLanguageCode,
              sourceLanguage) &&
          this.TextMatches(state.CurrentOriginalText, originalText) &&
          !string.IsNullOrWhiteSpace(state.CurrentTranslatedText))
      {
        translatedText = state.CurrentTranslatedText;
        replacementText = state.CurrentReplacementText;
        return true;
      }
    }

    translatedText = string.Empty;
    replacementText = string.Empty;
    return false;
  }

  /// <summary>
  ///     Attempts to load a stored MiniTalk translation from the database.
  /// </summary>
  /// <param name="originalText">The source MiniTalk text.</param>
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
    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      translatedText = string.Empty;
      replacementText = string.Empty;
      return false;
    }

    var lookup = this.findMiniTalkMessage(
        this.BuildLookupMessage(originalText, sourceLanguage));
    if (lookup == null ||
        !string.Equals(
            lookup.OriginalMiniTalkMessage,
            originalText,
            StringComparison.Ordinal) ||
        string.IsNullOrWhiteSpace(lookup.TranslatedMiniTalkMessage))
    {
      translatedText = string.Empty;
      replacementText = string.Empty;
      return false;
    }

    translatedText = lookup.TranslatedMiniTalkMessage!;
    replacementText = this.NormalizeForReplacement(translatedText);
    return true;
  }

  /// <summary>
  ///     Resolves a small amount of extra wrap width from the live MiniTalk node
  ///     padding so translated lines can gain a bit of horizontal room without
  ///     relying on fixed pixel constants.
  /// </summary>
  /// <param name="textNode">The live MiniTalk text node.</param>
  /// <returns>The additional wrap width suggested by the current bubble layout.</returns>
  private unsafe ushort ResolveMiniTalkAdditionalWrapWidth(
      AtkUnitBase* addon,
      AtkTextNode* textNode)
  {
    if (textNode == null)
    {
      return 0;
    }

    var parentNode = textNode->AtkResNode.ParentNode;
    var currentWidth = textNode->GetWidth();
    var preferredWrapWidth = currentWidth;
    if (NativeTextNodeLayoutHelper.TryResolveContainerNodes(
            addon,
            textNode,
            out var containerNode,
            out var backgroundNode))
    {
      preferredWrapWidth = NativeTextNodeLayoutHelper.ResolvePreferredWrapWidth(
          textNode,
          containerNode,
          backgroundNode != null
              ? &backgroundNode->AtkResNode
              : null);
    }

    var leftPadding = Math.Max(0, (int)textNode->AtkResNode.GetXShort());
    if (parentNode == null)
    {
      return MiniTalkWrapWidthHelper.ResolveAdditionalWrapWidth(
          currentWidth,
          preferredWrapWidth,
          leftPadding,
          0);
    }

    var rightPadding = Math.Max(
        0,
        parentNode->GetWidth() - leftPadding - currentWidth);
    return MiniTalkWrapWidthHelper.ResolveAdditionalWrapWidth(
        currentWidth,
        preferredWrapWidth,
        leftPadding,
        rightPadding);
  }

  /// <summary>
  ///     Returns the active resolved translation currently held by the handler
  ///     state.
  /// </summary>
  /// <param name="originalText">Receives the active original text.</param>
  /// <param name="translatedText">Receives the translated text.</param>
  /// <param name="replacementText">Receives the normalized replacement text.</param>
  /// <returns>
  ///     <see langword="true" /> when the handler already has a translated line;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryGetCurrentResolvedTranslation(
      nint bubbleKey,
      SourceClientLanguage sourceLanguage,
      out string originalText,
      out string translatedText,
      out string replacementText)
  {
    lock (this.stateGate)
    {
      if (this.TryGetBubbleState(bubbleKey, out var state) &&
          NativeRuntimeSourceScope.MatchesSource(
              state.CurrentSourceLanguageCode,
              sourceLanguage) &&
          !string.IsNullOrWhiteSpace(state.CurrentTranslatedText))
      {
        originalText = state.CurrentOriginalText;
        translatedText = state.CurrentTranslatedText;
        replacementText = state.CurrentReplacementText;
        return true;
      }
    }

    originalText = string.Empty;
    translatedText = string.Empty;
    replacementText = string.Empty;
    return false;
  }

  /// <summary>
  ///     Starts a new translation request when the visible source line changes or
  ///     when the current line still has no cached translation.
  /// </summary>
  /// <param name="originalText">The source MiniTalk text.</param>
  /// <param name="requestId">Receives the active request identifier.</param>
  /// <param name="sourceOperation">
  ///     Receives the source generation captured by the request.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> when a new translation task should be queued;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryQueueTranslation(
      nint bubbleKey,
      string originalText,
      SourceClientLanguage sourceLanguage,
      out int requestId,
      out SourcePublicationOperation sourceOperation)
  {
    lock (this.stateGate)
    {
      var state = this.GetOrCreateBubbleState(bubbleKey);
      if (NativeRuntimeSourceScope.MatchesSource(
              state.CurrentSourceLanguageCode,
              sourceLanguage) &&
          this.TextMatches(state.CurrentOriginalText, originalText))
      {
        if (state.TranslationInFlight ||
            !string.IsNullOrWhiteSpace(state.CurrentTranslatedText) ||
            (NativeRuntimeSourceScope.MatchesSource(
                 state.LastFailedSourceLanguageCode,
                 sourceLanguage) &&
             this.TextMatches(state.LastFailedOriginalText, originalText)))
        {
          requestId = state.ActiveRequestId;
          sourceOperation = this.sourceLifecycle.Capture(
              this.CreateDialogueReuseScope(sourceLanguage));
          return false;
        }
      }

      state.ActiveRequestId++;
      state.CurrentSourceLanguageCode = sourceLanguage.PersistenceCode;
      state.CurrentOriginalText = originalText;
      state.CurrentTranslatedText = string.Empty;
      state.CurrentReplacementText = string.Empty;
      state.TranslationInFlight = true;
      requestId = state.ActiveRequestId;
      sourceOperation = this.sourceLifecycle.Capture(
          this.CreateDialogueReuseScope(sourceLanguage));
      return true;
    }
  }

  /// <summary>
  ///     Sets the resolved in-memory state for the current MiniTalk line.
  /// </summary>
  /// <param name="bubbleKey">The native bubble node address.</param>
  /// <param name="originalText">The original MiniTalk text.</param>
  /// <param name="translatedText">The translated MiniTalk text.</param>
  /// <param name="replacementText">The translated text normalized for native replacement.</param>
  private void SetResolvedState(
      nint bubbleKey,
      string originalText,
      string translatedText,
      string replacementText,
      SourceClientLanguage sourceLanguage)
  {
    lock (this.stateGate)
    {
      var state = this.GetOrCreateBubbleState(bubbleKey);
      state.CurrentSourceLanguageCode = sourceLanguage.PersistenceCode;
      state.CurrentOriginalText = originalText;
      state.CurrentTranslatedText = translatedText;
      state.CurrentReplacementText = replacementText;
      state.TranslationInFlight = false;
      state.LastFailedOriginalText = string.Empty;
      state.LastFailedSourceLanguageCode = string.Empty;
    }
  }

  /// <summary>
  ///     Publishes translated MiniTalk content to the configured overlay when
  ///     overlay mode is enabled.
  /// </summary>
  /// <param name="originalText">The original MiniTalk text.</param>
  /// <param name="translatedText">The translated MiniTalk text.</param>
  /// <param name="trigger">The log trigger label associated with the call.</param>
  private void PublishOverlay(
      nint bubbleKey,
      string originalText,
      string translatedText,
      string trigger = "")
  {
    if (!this.ShouldUseOverlay())
    {
      // PluginRuntimeLog.Debug($"[MiniTalk] trigger={trigger} overlay disabled -> clear");
      this.clearOverlay(bubbleKey, true);
      return;
    }

    var overlayText = this.ShouldSwapTexts()
        ? originalText
        : translatedText;

    if (string.IsNullOrWhiteSpace(overlayText))
    {
      // PluginRuntimeLog.Debug($"[MiniTalk] trigger={trigger} overlay text unavailable -> clear");
      this.clearOverlay(bubbleKey, false);
      return;
    }

    // PluginRuntimeLog.Debug(
    //     $"[MiniTalk] trigger={trigger} publish overlay text='{overlayText}'");

    this.updateOverlay(bubbleKey, string.Empty, overlayText, string.Empty);
  }

  /// <summary>
  ///     Determines whether this MiniTalk request should render through the
  ///     overlay path.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when the overlay path is active; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool ShouldUseOverlay()
  {
    return this.config.TranslateMiniTalk &&
           TranslationDisplayModeHelper.UsesOverlayPresentation(
               this.config.MiniTalkTranslationDisplayMode,
               this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Determines whether the MiniTalk addon should receive translated text
  ///     directly in the game UI.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when the addon should be replaced natively;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool ShouldApplyNativeText()
  {
    return TranslationDisplayModeHelper.WritesNativeTranslation(
        this.config.MiniTalkTranslationDisplayMode,
        this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Determines whether the overlay should currently show the original text
  ///     while the native addon receives the translation.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when overlay swap mode is active; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool ShouldSwapTexts()
  {
    return TranslationDisplayModeHelper.ShowsOriginalOverlayText(
        this.config.MiniTalkTranslationDisplayMode,
        this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Builds a lookup entity matching the MiniTalk database schema.
  /// </summary>
  /// <param name="originalText">The original MiniTalk text.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <returns>A formatted <see cref="MiniTalkMessage" /> suitable for DB lookup.</returns>
  private MiniTalkMessage BuildLookupMessage(
      string originalText,
      SourceClientLanguage sourceLanguage,
      TranslationReuseScope? scope = null)
  {
    var operationScope = scope ?? this.CreateDialogueReuseScope(sourceLanguage);
    return new MiniTalkMessage(
        originalText,
        sourceLanguage.PersistenceCode,
        string.Empty,
        operationScope.TargetLanguageCode,
        operationScope.TranslationEngine.GetValueOrDefault(),
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
  ///     Captures the complete dialogue reuse scope before asynchronous work.
  /// </summary>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <returns>The immutable dialogue operation scope.</returns>
  private TranslationReuseScope CreateDialogueReuseScope(
      SourceClientLanguage sourceLanguage)
  {
    return new TranslationReuseScope(
        sourceLanguage.PersistenceCode,
        RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(this.config.Lang),
        this.GetDialogueTranslationEngineId(),
        this.config.TranslateAlreadyTranslatedTexts);
  }

  /// <summary>
  ///     Normalizes translated MiniTalk text before native replacement when the
  ///     active config requests diacritic stripping.
  /// </summary>
  /// <param name="text">The translated MiniTalk text to normalize.</param>
  /// <returns>The text that should be written back into the native addon.</returns>
  private string NormalizeForReplacement(string text)
  {
    return this.config.RemoveDiacriticsWhenUsingReplacementTalkBTalk
        ? this.normalizeReplacementText(text)
        : text;
  }

  /// <summary>
  ///     Reads the current string value from a MiniTalk text node.
  /// </summary>
  /// <param name="textNode">The text node to inspect.</param>
  /// <returns>The visible node text, or an empty string when unavailable.</returns>
  private unsafe string ReadTextNode(AtkTextNode* textNode)
  {
    var visibleText = textNode->NodeText.ToString();
    if (!string.IsNullOrWhiteSpace(visibleText))
    {
      return visibleText;
    }

    try
    {
      var originalText = textNode->OriginalTextPointer.AsReadOnlySeStringSpan().ExtractText();
      if (!string.IsNullOrWhiteSpace(originalText))
      {
        return originalText;
      }
    }
    catch
    {
      // Keep falling back to the legacy read path below.
    }

    try
    {
      return MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)textNode->NodeText.StringPtr.Value);
    }
    catch
    {
      return string.Empty;
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

    return NativeTextComparisonNormalizationHelper.NormalizeForComparison(
        this.NormalizeForReplacement(text));
  }

  /// <summary>
  ///     Determines whether the current callback should handle the configured
  ///     MiniTalk addon instance.
  /// </summary>
  /// <param name="args">The addon arguments associated with the callback.</param>
  /// <param name="addon">Receives the visible addon instance.</param>
  /// <returns>
  ///     <see langword="true" /> when the callback is for a visible MiniTalk
  ///     addon; otherwise, <see langword="false" />.
  /// </returns>
  private unsafe bool ShouldHandleMiniTalk(
      AddonArgs args,
      out AtkUnitBase* addon)
  {
    addon = null;
    if (args.AddonName != MiniTalkAddonName || args.Addon.Address == IntPtr.Zero)
    {
      return false;
    }

    addon = (AtkUnitBase*)args.Addon.Address;
    return addon != null && addon->IsVisible;
  }

  /// <summary>
  ///     Determines whether a MiniTalk bubble text node currently intersects the
  ///     main viewport.
  /// </summary>
  /// <param name="textNode">The visible bubble text node.</param>
  /// <returns>
  ///     <see langword="true" /> when the bubble is on screen; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private unsafe bool IsMiniTalkBubbleVisible(AtkTextNode* textNode)
  {
    if (textNode == null || !textNode->IsVisible())
    {
      return false;
    }

    if (NativeTextNodeLayoutHelper.TryResolveContainerNodes(
            addon: null,
            textNode,
            out var containerNode,
            out _) &&
        containerNode != null &&
        !containerNode->IsVisible())
    {
      return false;
    }

    var viewport = ImGui.GetMainViewport();
    var left = textNode->ScreenX;
    var top = textNode->ScreenY;
    var right = left + Math.Max(1f, textNode->GetWidth());
    var bottom = top + Math.Max(1f, textNode->GetHeight());

    return right >= viewport.Pos.X &&
           left <= viewport.Pos.X + viewport.Size.X &&
           bottom >= viewport.Pos.Y &&
           top <= viewport.Pos.Y + viewport.Size.Y;
  }

  /// <summary>
  ///     Reads the logical source text from the live MiniTalk addon, mapping the
  ///     visible translated replacement back to the original source line when
  ///     needed.
  /// </summary>
  /// <param name="bubbleKey">The stable bubble key.</param>
  /// <param name="textNode">The visible MiniTalk text node.</param>
  /// <param name="originalText">Receives the logical original MiniTalk text.</param>
  /// <returns>
  ///     <see langword="true" /> when readable text is available; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private unsafe bool TryReadCurrentSource(
      nint bubbleKey,
      AtkTextNode* textNode,
      out string originalText)
  {
    originalText = string.Empty;

    if (textNode == null)
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
      if (this.TryGetBubbleState(bubbleKey, out var state) &&
          !string.IsNullOrWhiteSpace(state.CurrentOriginalText) &&
          !string.IsNullOrWhiteSpace(state.CurrentReplacementText) &&
          this.TextMatches(visibleText, state.CurrentReplacementText))
      {
        originalText = state.CurrentOriginalText;
        return true;
      }
    }

    if (this.ShouldApplyNativeText())
    {
      if (this.TryReadOriginalPointerText(textNode, out var originalPointerText) &&
          !this.TextMatches(originalPointerText, visibleText) &&
          this.TryConfirmOriginalPointerMatchesVisible(
              bubbleKey,
              originalPointerText,
              visibleText,
              out var confirmedOriginalText))
      {
        originalText = confirmedOriginalText;
        return true;
      }

      if (this.TryMapVisibleReplacementToKnownOriginal(visibleText, out var mappedOriginalText))
      {
        originalText = mappedOriginalText;
        return true;
      }
    }

    originalText = visibleText;
    return true;
  }

  /// <summary>
  ///     Confirms that a MiniTalk node's original-text pointer really belongs to
  ///     the currently visible bubble text before the handler trusts it as the
  ///     logical source line.
  /// </summary>
  /// <param name="bubbleKey">The stable bubble key.</param>
  /// <param name="originalPointerText">The text read from <c>OriginalTextPointer</c>.</param>
  /// <param name="visibleText">The text currently visible in the node.</param>
  /// <param name="originalText">Receives the confirmed original text.</param>
  /// <returns>
  ///     <see langword="true" /> when the pointer text can be proven to explain
  ///     the visible replacement inside MiniTalk; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool TryConfirmOriginalPointerMatchesVisible(
      nint bubbleKey,
      string originalPointerText,
      string visibleText,
      out string originalText)
  {
    lock (this.stateGate)
    {
      if (this.TryGetBubbleState(bubbleKey, out var state) &&
          this.TextMatches(state.CurrentOriginalText, originalPointerText) &&
          this.TextMatches(visibleText, state.CurrentReplacementText))
      {
        originalText = originalPointerText;
        return true;
      }

      foreach (var bubbleState in this.bubbleStates.Values)
      {
        if (!this.TextMatches(
                bubbleState.CurrentOriginalText,
                originalPointerText))
        {
          continue;
        }

        if (this.TextMatches(
                visibleText,
                bubbleState.CurrentReplacementText))
        {
          originalText = originalPointerText;
          return true;
        }
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
  ///     Tries to read the original game-provided text payload from a MiniTalk
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
  ///     Tries to resolve a visible translated MiniTalk line back to an original
  ///     source line already known to another tracked bubble state.
  /// </summary>
  /// <param name="visibleText">The visible text currently rendered by the node.</param>
  /// <param name="originalText">Receives the matched original source line.</param>
  /// <returns>
  ///     <see langword="true" /> when a tracked replacement matches the visible
  ///     line; otherwise, <see langword="false" />.
  /// </returns>
  private bool TryMapVisibleReplacementToKnownOriginal(
      string visibleText,
      out string originalText)
  {
    lock (this.stateGate)
    {
      foreach (var state in this.bubbleStates.Values)
      {
        if (string.IsNullOrWhiteSpace(state.CurrentOriginalText) ||
            string.IsNullOrWhiteSpace(state.CurrentReplacementText))
        {
          continue;
        }

        if (this.TextMatches(visibleText, state.CurrentReplacementText))
        {
          originalText = state.CurrentOriginalText;
          return true;
        }
      }
    }

    originalText = string.Empty;
    return false;
  }

  /// <summary>
  ///     Reconciles a visible MiniTalk source line against the tracked bubble
  ///     state so recycled bubble slots do not keep using a stale translation or
  ///     a stale native layout snapshot from the prior occupant.
  /// </summary>
  /// <param name="bubbleKey">The stable bubble key.</param>
  /// <param name="visibleOriginalText">The current visible source line.</param>
  /// <param name="trigger">The trigger label used for capture/queue routing.</param>
  /// <returns>
  ///     <see langword="true" /> when the visible source changed and the new
  ///     source was captured, restored, or queued; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool TryHandleVisibleSourceChange(
      nint bubbleKey,
      string visibleOriginalText,
      string trigger,
      SourceClientLanguage sourceLanguage)
  {
    var shouldReconcile = false;

    lock (this.stateGate)
    {
      if (this.TryGetBubbleState(bubbleKey, out var state) &&
          !string.IsNullOrWhiteSpace(state.CurrentOriginalText) &&
          !this.TextMatches(state.CurrentOriginalText, visibleOriginalText))
      {
        shouldReconcile = true;
      }
    }

    if (!shouldReconcile)
    {
      return false;
    }

    this.RestoreTrackedBubbleLayoutIfNeeded(
        bubbleKey,
        visibleOriginalText);
    return this.TryCaptureOrQueueMiniTalkSource(
        bubbleKey,
        visibleOriginalText,
        trigger,
        sourceLanguage);
  }

  /// <summary>
  ///     Resolves the source MiniTalk line against cache, database, or background
  ///     translation work and publishes the overlay when a translation becomes
  ///     available.
  /// </summary>
  /// <param name="bubbleKey">The native bubble node address.</param>
  /// <param name="originalText">The original MiniTalk text to resolve.</param>
  /// <param name="trigger">The log trigger label associated with the call.</param>
  /// <returns>
  ///     <see langword="true" /> when the source was handled; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool TryCaptureOrQueueMiniTalkSource(
      nint bubbleKey,
      string originalText,
      string trigger,
      SourceClientLanguage sourceLanguage)
  {
    if (this.TryGetCachedTranslation(
            bubbleKey,
            originalText,
            sourceLanguage,
            out var translatedText,
            out _))
    {
      // PluginRuntimeLog.Debug(
      //     $"[MiniTalk] trigger={trigger} cache-hit -> overlay publish");

      this.SetResolvedState(
          bubbleKey,
          originalText,
          translatedText,
          this.NormalizeForReplacement(translatedText),
          sourceLanguage);
      this.PublishOverlay(bubbleKey, originalText, translatedText, trigger);
      return true;
    }

    var lookupMiniTalk = this.BuildLookupMessage(
        originalText,
        sourceLanguage);
    var storedMiniTalk = this.findMiniTalkMessage(lookupMiniTalk);
    if (this.IsStoredTranslationUsable(storedMiniTalk, originalText))
    {
      // PluginRuntimeLog.Debug(
      //     $"[MiniTalk] trigger={trigger} db-hit -> overlay publish");

      this.SetResolvedState(
          bubbleKey,
          originalText,
          storedMiniTalk!.TranslatedMiniTalkMessage!,
          this.NormalizeForReplacement(storedMiniTalk.TranslatedMiniTalkMessage!),
          sourceLanguage);
      this.PublishOverlay(
          bubbleKey,
          originalText,
          storedMiniTalk.TranslatedMiniTalkMessage!,
          trigger);
      return true;
    }

    if (this.TryQueueTranslation(
            bubbleKey,
            originalText,
            sourceLanguage,
            out var requestId,
            out var sourceOperation))
    {
      // PluginRuntimeLog.Debug(
      //     $"[MiniTalk] trigger={trigger} cache-miss -> queued translation request #{requestId}");

      this.PublishOverlay(bubbleKey, originalText, string.Empty, trigger);
      Task.Run(() => this.ResolveTranslationAsync(
          bubbleKey,
          originalText,
          requestId,
          sourceLanguage,
          sourceOperation));
      return true;
    }

    return false;
  }

  /// <summary>
  ///     Determines whether a stored MiniTalk row still represents a usable
  ///     translation for the current source line.
  /// </summary>
  /// <param name="miniTalkMessage">The stored MiniTalk row to validate.</param>
  /// <param name="originalText">The expected original MiniTalk text.</param>
  /// <returns>
  ///     <see langword="true" /> when the stored row contains a non-empty
  ///     translation for the same source line; otherwise, <see langword="false" />.
  /// </returns>
  private bool IsStoredTranslationUsable(
      MiniTalkMessage? miniTalkMessage,
      string originalText)
  {
    return miniTalkMessage != null &&
           this.TextMatches(
               miniTalkMessage.OriginalMiniTalkMessage,
               originalText) &&
           !string.IsNullOrWhiteSpace(miniTalkMessage.TranslatedMiniTalkMessage);
  }

  /// <summary>
  ///     Builds a concise debug string for the current MiniTalk text node so we
  ///     can understand why a visible line did not become readable.
  /// </summary>
  /// <param name="textNode">The text node to describe.</param>
  /// <returns>A log-friendly node description.</returns>
  private unsafe string DescribeMiniTalkTextNode(AtkTextNode* textNode)
  {
    if (textNode == null)
    {
      return "node=<null>";
    }

    var rawText = textNode->NodeText.ToString();
    var originalText = string.Empty;
    try
    {
      originalText = textNode->OriginalTextPointer.AsReadOnlySeStringSpan().ExtractText();
    }
    catch
    {
      // Leave the original text empty when the raw payload cannot be parsed.
    }

    if (string.IsNullOrWhiteSpace(rawText))
    {
      rawText = "<empty>";
    }

    if (string.IsNullOrWhiteSpace(originalText))
    {
      originalText = "<empty>";
    }

    return
        $"node=0x{(nint)textNode:X} type={textNode->Type} nodeId={textNode->NodeId} visible={textNode->IsVisible()} flags={textNode->NodeFlags} drawFlags={textNode->DrawFlags} text='{rawText}' original='{originalText}'";
  }
}


