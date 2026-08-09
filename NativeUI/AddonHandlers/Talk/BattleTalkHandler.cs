// <copyright file="BattleTalkHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;

namespace Echoglossian.NativeUI.AddonHandlers.Talk;

/// <summary>
///     Handles the full BattleTalk runtime inside the new addon-handler model.
///     This includes live text capture from the visible addon, translation lookup,
///     async translation, overlay updates, and optional native text replacement.
/// </summary>
public sealed class BattleTalkHandler : IAddonTranslationHandler, IVisibleDialogueRetranslationHandler
{
  private static readonly TimeSpan DialogueSessionTtl = TimeSpan.FromSeconds(30);
  private const string BattleTalkAddonName = "_BattleTalk";
  private const int DialogueSessionHistoryLimit = 3;
  private const int HideResetDelayMilliseconds = 5000;
  private const int NameNodeId = 4;
  private const int TextNodeId = 6;
  private const int ParentNodeId = 1;
  private const int TimerNodeId = 2;
  private const int NineGridNodeId = 7;

  private readonly Action clearOverlay;
  private readonly Config config;
  private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>> eventHandlers = new();
  private readonly Func<BattleTalkMessage, BattleTalkMessage?> findBattleTalkMessage;
  private readonly Func<BattleTalkMessage, CancellationToken, Task<string>> insertBattleTalkMessageAsync;
  private readonly Func<string, string> normalizeReplacementText;
  private readonly SourcePublicationLifecycle sourceLifecycle = new();
  private readonly object stateGate = new();
  private readonly TranslationService translationService;
  private readonly Action<string, string, string> updateOverlay;

  private int activeRequestId;
  private int hideResetGeneration;
  private string currentSourceLanguageCode = string.Empty;
  private string currentOriginalName = string.Empty;
  private string currentOriginalText = string.Empty;
  private string currentReplacementName = string.Empty;
  private string currentReplacementText = string.Empty;
  private string currentTranslatedName = string.Empty;
  private string currentTranslatedText = string.Empty;
  private string failedOriginalName = string.Empty;
  private string failedOriginalText = string.Empty;
  private string failedSourceLanguageCode = string.Empty;
  private string lastResolvedOriginalName = string.Empty;
  private string lastResolvedOriginalText = string.Empty;
  private string lastResolvedReplacementName = string.Empty;
  private string lastResolvedReplacementText = string.Empty;
  private string lastResolvedSourceLanguageCode = string.Empty;
  private NativeTextNodeLayoutSnapshot? nativeLayoutSnapshot;
  private string nativeLayoutOriginalName = string.Empty;
  private string nativeLayoutOriginalText = string.Empty;
  private string nativeLayoutReplacementName = string.Empty;
  private string nativeLayoutReplacementText = string.Empty;
  private bool translationInFlight;

  /// <summary>
  ///     Initializes a new instance of the <see cref="BattleTalkHandler" /> class.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The translation service used by the plugin.</param>
  /// <param name="findBattleTalkMessage">
  ///     Delegate used to look up previously translated BattleTalk messages.
  /// </param>
  /// <param name="insertBattleTalkMessageAsync">
  ///     Delegate used to persist translated BattleTalk messages.
  /// </param>
  /// <param name="updateOverlay">
  ///     Delegate used to publish translated content to the BattleTalk overlay state.
  /// </param>
  /// <param name="clearOverlay">
  ///     Delegate used to clear the BattleTalk overlay state when the source text
  ///     changes or the addon hides.
  /// </param>
  /// <param name="normalizeReplacementText">
  ///     Delegate used to normalize translated text before native replacement.
  /// </param>
  public BattleTalkHandler(
      Config config,
      TranslationService translationService,
      Func<BattleTalkMessage, BattleTalkMessage?> findBattleTalkMessage,
      Func<BattleTalkMessage, Task<string>> insertBattleTalkMessageAsync,
      Action<string, string, string> updateOverlay,
      Action clearOverlay,
      Func<string, string> normalizeReplacementText)
    : this(
        config,
        translationService,
        findBattleTalkMessage,
        (message, _) => insertBattleTalkMessageAsync(message),
        updateOverlay,
        clearOverlay,
        normalizeReplacementText)
  {
  }

  /// <summary>
  ///     Initializes a BattleTalk handler with cancellation-aware dialogue
  ///     persistence owned by the captured operation scope.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The shared translation service.</param>
  /// <param name="findBattleTalkMessage">The canonical BattleTalk lookup.</param>
  /// <param name="insertBattleTalkMessageAsync">The cancellation-aware persistence delegate.</param>
  /// <param name="updateOverlay">The overlay publication callback.</param>
  /// <param name="clearOverlay">The overlay clear callback.</param>
  /// <param name="normalizeReplacementText">The native replacement normalizer.</param>
  internal BattleTalkHandler(
      Config config,
      TranslationService translationService,
      Func<BattleTalkMessage, BattleTalkMessage?> findBattleTalkMessage,
      Func<BattleTalkMessage, CancellationToken, Task<string>> insertBattleTalkMessageAsync,
      Action<string, string, string> updateOverlay,
      Action clearOverlay,
      Func<string, string> normalizeReplacementText)
  {
    this.config = config;
    this.translationService = translationService;
    this.findBattleTalkMessage = findBattleTalkMessage;
    this.insertBattleTalkMessageAsync = insertBattleTalkMessageAsync;
    this.updateOverlay = updateOverlay;
    this.clearOverlay = clearOverlay;
    this.normalizeReplacementText = normalizeReplacementText;

    this.RegisterHandler(AddonEvent.PreShow, this.OnCaptureHint);
    this.RegisterHandler(AddonEvent.PreRefresh, this.OnCaptureHint);
    this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.OnCaptureHint);
    this.RegisterHandler(AddonEvent.PreDraw, this.OnPreDraw);
    this.RegisterHandler(AddonEvent.PreHide, this.OnScheduleResetState);
    this.RegisterHandler(AddonEvent.PreFinalize, this.OnResetState);
  }

  /// <summary>
  ///     Returns the event handlers required to drive the BattleTalk addon flow.
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
  public async Task<VisibleDialogueRetranslationResult> RetranslateVisibleTextAndPersistAsync()
  {
    const VisibleStorySurfaceKind surface = VisibleStorySurfaceKind.BattleTalk;
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
    var sourceOperation = this.sourceLifecycle.Capture(
        this.CreateDialogueReuseScope(sourceLanguage));
    var operationScope = sourceOperation.Scope ??
                         this.CreateDialogueReuseScope(sourceLanguage);
    var translatorResolution = this.translationService
        .CaptureTranslatorResolution(
            operationScope.TranslationEngine.GetValueOrDefault(),
            TranslationSurfaceGroup.Dialogue);

    if (!this.TryCaptureCurrentBattleTalkSource(
            out var originalName,
            out var originalText))
    {
      return new VisibleDialogueRetranslationResult(
          false,
          false,
          surface,
          surfaceName,
          VisibleStorySurfaceText.GetNoVisibleTextMessage(surface));
    }

    int requestId;
    lock (this.stateGate)
    {
      this.currentSourceLanguageCode = sourceLanguage.PersistenceCode;
      this.currentOriginalName = originalName;
      this.currentOriginalText = originalText;
      this.currentReplacementName = string.Empty;
      this.currentReplacementText = string.Empty;
      this.currentTranslatedName = string.Empty;
      this.currentTranslatedText = string.Empty;
      this.failedOriginalName = string.Empty;
      this.failedOriginalText = string.Empty;
      this.failedSourceLanguageCode = string.Empty;
      this.translationInFlight = true;
      this.activeRequestId++;
      requestId = this.activeRequestId;
    }

    try
    {
      var translatedText = await this.translationService.TranslateAsync(
          originalText,
          sourceLanguage,
          operationScope.TargetLanguageCode,
          TranslationSurfaceGroup.Dialogue,
          translatorResolution,
          originContext: "BattleTalk/Text").ConfigureAwait(false);
      var translatedName = this.ShouldTranslateBattleTalkNpcNames() &&
                           !originalName.IsNullOrEmpty()
          ? await this.translationService.TranslateAsync(
              originalName,
              sourceLanguage,
              operationScope.TargetLanguageCode,
              TranslationSurfaceGroup.Dialogue,
              translatorResolution,
              originContext: "BattleTalk/Speaker").ConfigureAwait(false)
          : string.Empty;
      var dialogueTranslationEngine = operationScope.TranslationEngine
                                      .GetValueOrDefault();

      if (!TranslationPersistenceGuard.IsUsableDialogueTranslation(
              originalText,
              translatedText,
              sourceLanguage.PersistenceCode,
              operationScope.TargetLanguageCode))
      {
        lock (this.stateGate)
        {
          if (requestId == this.activeRequestId)
          {
            this.translationInFlight = false;
            this.failedOriginalName = originalName;
            this.failedOriginalText = originalText;
            this.failedSourceLanguageCode = sourceLanguage.PersistenceCode;
          }
        }

        return new VisibleDialogueRetranslationResult(
            true,
            false,
            surface,
            surfaceName,
            VisibleStorySurfaceText.GetNoUsableTranslationMessage(surface));
      }

      var translatedBattleTalkData = new BattleTalkMessage(
          originalName,
          originalText,
          sourceLanguage.PersistenceCode,
          sourceLanguage.PersistenceCode,
          translatedName,
          translatedText,
          operationScope.TargetLanguageCode,
          dialogueTranslationEngine,
          rtlLangTranslationImageData: null,
          DateTime.Now,
          DateTime.Now);
      if (!this.sourceLifecycle.IsCurrent(sourceOperation))
      {
        return new VisibleDialogueRetranslationResult(
            true,
            false,
            surface,
            surfaceName,
            VisibleStorySurfaceText.GetPersistedButVisibleChangedMessage(
                surface));
      }

      var persistenceResult = await Echoglossian.UpsertBattleTalkDataAsync(
          translatedBattleTalkData,
          sourceOperation.CancellationToken).ConfigureAwait(false);
      var persistenceSucceeded = !persistenceResult.StartsWith(
          "ErrorSavingData:",
          StringComparison.Ordinal) &&
                                 !string.Equals(
                                     persistenceResult,
                                     "No data to save.",
                                     StringComparison.Ordinal);
      var replacementName = this.NormalizeForReplacement(translatedName);
      var replacementText = this.NormalizeForReplacement(translatedText);
      var stateUpdated = false;
      var publicationAccepted = this.sourceLifecycle.TryPublish(
          sourceOperation,
          () =>
          {
            lock (this.stateGate)
            {
              if (requestId != this.activeRequestId ||
                  !NativeRuntimeSourceScope.MatchesSource(
                      this.currentSourceLanguageCode,
                      sourceLanguage))
              {
                return;
              }

              this.translationInFlight = false;
              this.currentReplacementName = replacementName;
              this.currentReplacementText = replacementText;
              this.currentTranslatedName = translatedName;
              this.currentTranslatedText = translatedText;
              this.failedOriginalName = string.Empty;
              this.failedOriginalText = string.Empty;
              this.failedSourceLanguageCode = string.Empty;
              this.lastResolvedSourceLanguageCode =
                  sourceLanguage.PersistenceCode;
              this.lastResolvedOriginalName = originalName;
              this.lastResolvedOriginalText = originalText;
              this.lastResolvedReplacementName = replacementName;
              this.lastResolvedReplacementText = replacementText;
              stateUpdated = true;
            }

            this.RecordDiagnosticsSnapshot(
                VisibleStorySurfaceProvenanceKind.FreshLiveTranslation,
                originalName,
                originalText,
                translatedName,
                translatedText,
                usedRuntimeOnlyDialogueContext: false,
                dialogueTranslationEngine);
            this.PublishOverlay(
                originalName,
                originalText,
                translatedName,
                translatedText);
          });
      var sourceChangedBeforeApply =
          !publicationAccepted || !stateUpdated;

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
          this.failedOriginalName = originalName;
          this.failedOriginalText = originalText;
          this.failedSourceLanguageCode = sourceLanguage.PersistenceCode;
        }
      }

      PluginRuntimeLog.Error(
          $"[{BattleTalkAddonName}] Error retranslating visible BattleTalk dialogue: {ex}");
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
  ///     Tries to capture BattleTalk source text early in the lifecycle so a
  ///     translation can already be queued before the first draw pass completes.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the handler.</param>
  /// <param name="args">The addon arguments associated with the event.</param>
  private void OnCaptureHint(AddonEvent type, AddonArgs args)
  {
    if (args.AddonName != BattleTalkAddonName)
    {
      return;
    }

    this.CancelScheduledReset();
    this.TryCaptureAndQueueTranslation();
  }

  /// <summary>
  ///     Captures BattleTalk source text from the visible addon, publishes overlay
  ///     content when a translation is ready, and applies native replacement when
  ///     configured.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the handler.</param>
  /// <param name="args">The addon arguments associated with the draw.</param>
  private unsafe void OnPreDraw(AddonEvent type, AddonArgs args)
  {
    if (args.AddonName != BattleTalkAddonName || !this.config.TranslateBattleTalk)
    {
      return;
    }

    this.CancelScheduledReset();

    var addonPtr = GameGuiInterface.GetAddonByName(BattleTalkAddonName);
    if (addonPtr.Address == IntPtr.Zero)
    {
      return;
    }

    var battleTalkAddon = (AtkUnitBase*)addonPtr.Address;
    if (battleTalkAddon == null || !battleTalkAddon->IsVisible)
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

    if (!this.TryReadCurrentSource(
            battleTalkAddon,
            out var originalName,
            out var originalText))
    {
      return;
    }

    if (this.TryGetCachedTranslation(
            originalName,
            originalText,
            sourceLanguage,
            out var translatedName,
            out var translatedText,
            out var replacementName,
            out var replacementText))
    {
      this.PublishOverlay(
          originalName,
          originalText,
          translatedName,
          translatedText);

      if (this.ShouldApplyNativeBattleTalkText())
      {
        this.RestoreTrackedNativeLayoutIfNeeded(
            originalName,
            originalText);
        this.ApplyTranslatedNodes(
            battleTalkAddon,
            originalName,
            originalText,
            translatedName,
            replacementName,
            replacementText);
      }
      else
      {
        this.RestoreTrackedNativeLayoutIfNeeded();
      }
    }
    else
    {
      this.RestoreTrackedNativeLayoutIfNeeded();
      this.ShowPendingSwapOverlayIfNeeded(originalName, originalText);
    }

    if (this.TryQueueTranslation(
            originalName,
            originalText,
            sourceLanguage,
            out var requestId,
            out var sourceOperation))
    {
      Task.Run(() => this.ResolveTranslationAsync(
          originalName,
          originalText,
          requestId,
          sourceLanguage,
          sourceOperation));
    }
  }

  /// <summary>
  ///     Schedules a delayed BattleTalk state reset when the addon hides.
  ///     BattleTalk briefly hides between timer ticks, so clearing immediately can
  ///     make the overlay disappear before the same line is shown again. The delay
  ///     intentionally spans the observed timer cadence so transient hides do not
  ///     look like a logical end of the current line.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the delayed reset.</param>
  /// <param name="args">The addon arguments associated with the hide event.</param>
  private void OnScheduleResetState(AddonEvent type, AddonArgs args)
  {
    if (args.AddonName != BattleTalkAddonName)
    {
      return;
    }

    this.RestoreTrackedNativeLayoutIfNeeded();

    int scheduledGeneration;
    lock (this.stateGate)
    {
      scheduledGeneration = ++this.hideResetGeneration;
    }

    _ = Task.Run(async () =>
    {
      await Task.Delay(HideResetDelayMilliseconds).ConfigureAwait(false);

      lock (this.stateGate)
      {
        if (scheduledGeneration != this.hideResetGeneration)
        {
          return;
        }

        this.ResetStateLocked();
      }

      VisibleStorySurfaceDiagnosticsStore.Clear(
          VisibleStorySurfaceKind.BattleTalk);
      this.clearOverlay();
    });
  }

  /// <summary>
  ///     Clears the active BattleTalk state immediately when the addon is finalized.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the reset.</param>
  /// <param name="args">The addon arguments associated with the reset.</param>
  private void OnResetState(AddonEvent type, AddonArgs args)
  {
    if (args.AddonName != BattleTalkAddonName)
    {
      return;
    }

    this.RestoreTrackedNativeLayoutIfNeeded();

    lock (this.stateGate)
    {
      this.hideResetGeneration++;
      this.ResetStateLocked();
    }

    VisibleStorySurfaceDiagnosticsStore.Clear(VisibleStorySurfaceKind.BattleTalk);
    this.clearOverlay();
  }

  /// <summary>
  ///     Restores plugin-owned native mutation and clears resolved dialogue
  ///     state when the operation source changes or cannot be resolved.
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
          this.RestoreTrackedNativeLayoutIfNeeded();
          lock (this.stateGate)
          {
            this.ResetStateLocked();
          }

          VisibleStorySurfaceDiagnosticsStore.Clear(
              VisibleStorySurfaceKind.BattleTalk);
          this.clearOverlay();
        });
  }

  /// <summary>
  ///     Cancels any delayed reset scheduled while BattleTalk was transiently
  ///     hidden between timer ticks.
  /// </summary>
  private void CancelScheduledReset()
  {
    lock (this.stateGate)
    {
      this.hideResetGeneration++;
    }
  }

  /// <summary>
  ///     Resets the active in-memory BattleTalk state.
  /// </summary>
  private void ResetStateLocked()
  {
    this.activeRequestId++;
    this.currentSourceLanguageCode = string.Empty;
    this.currentOriginalName = string.Empty;
    this.currentOriginalText = string.Empty;
    this.currentReplacementName = string.Empty;
    this.currentReplacementText = string.Empty;
    this.currentTranslatedName = string.Empty;
    this.currentTranslatedText = string.Empty;
    this.failedOriginalName = string.Empty;
    this.failedOriginalText = string.Empty;
    this.failedSourceLanguageCode = string.Empty;
    this.lastResolvedSourceLanguageCode = string.Empty;
    this.lastResolvedOriginalName = string.Empty;
    this.lastResolvedOriginalText = string.Empty;
    this.lastResolvedReplacementName = string.Empty;
    this.lastResolvedReplacementText = string.Empty;
    this.nativeLayoutSnapshot = null;
    this.nativeLayoutOriginalName = string.Empty;
    this.nativeLayoutOriginalText = string.Empty;
    this.nativeLayoutReplacementName = string.Empty;
    this.nativeLayoutReplacementText = string.Empty;
    this.translationInFlight = false;
  }

  /// <summary>
  ///     Restores any tracked native BattleTalk mutation when the source changes
  ///     or native mode is left.
  /// </summary>
  /// <param name="nextOriginalName">
  ///     The next original sender name expected for the addon, or an empty value
  ///     when the current mutation should always be restored.
  /// </param>
  /// <param name="nextOriginalText">
  ///     The next original source text expected for the addon, or an empty value
  ///     when the current mutation should always be restored.
  /// </param>
  private void RestoreTrackedNativeLayoutIfNeeded(
      string? nextOriginalName = null,
      string? nextOriginalText = null)
  {
    NativeTextNodeLayoutSnapshot? layoutSnapshot = null;
    string layoutOriginalName = string.Empty;
    string layoutOriginalText = string.Empty;
    string layoutReplacementName = string.Empty;
    string layoutReplacementText = string.Empty;

    lock (this.stateGate)
    {
      if (this.nativeLayoutSnapshot == null)
      {
        return;
      }

      if (!string.IsNullOrWhiteSpace(nextOriginalText) &&
          this.nativeLayoutOriginalName == nextOriginalName &&
          this.nativeLayoutOriginalText == nextOriginalText)
      {
        return;
      }

      layoutSnapshot = this.nativeLayoutSnapshot;
      layoutOriginalName = this.nativeLayoutOriginalName;
      layoutOriginalText = this.nativeLayoutOriginalText;
      layoutReplacementName = this.nativeLayoutReplacementName;
      layoutReplacementText = this.nativeLayoutReplacementText;
      this.nativeLayoutSnapshot = null;
      this.nativeLayoutOriginalName = string.Empty;
      this.nativeLayoutOriginalText = string.Empty;
      this.nativeLayoutReplacementName = string.Empty;
      this.nativeLayoutReplacementText = string.Empty;
    }

    this.TryRestoreNativeLayout(
        layoutSnapshot,
        layoutOriginalName,
        layoutOriginalText,
        layoutReplacementName,
        layoutReplacementText);
  }

  /// <summary>
  ///     Restores the tracked native BattleTalk layout even when the visible
  ///     source line has not changed, so a repeat native apply pass starts from
  ///     the original addon geometry instead of stacking height and width
  ///     mutations from the previous frame.
  /// </summary>
  /// <param name="originalName">The original sender name for the active line.</param>
  /// <param name="originalText">The original source text for the active line.</param>
  private void PrepareTrackedNativeLayoutForReapply(
      string originalName,
      string originalText)
  {
    NativeTextNodeLayoutSnapshot? layoutSnapshot = null;
    string layoutOriginalName = string.Empty;
    string layoutOriginalText = string.Empty;
    string layoutReplacementName = string.Empty;
    string layoutReplacementText = string.Empty;

    lock (this.stateGate)
    {
      if (this.nativeLayoutSnapshot == null ||
          this.nativeLayoutOriginalName != originalName ||
          this.nativeLayoutOriginalText != originalText)
      {
        return;
      }

      layoutSnapshot = this.nativeLayoutSnapshot;
      layoutOriginalName = this.nativeLayoutOriginalName;
      layoutOriginalText = this.nativeLayoutOriginalText;
      layoutReplacementName = this.nativeLayoutReplacementName;
      layoutReplacementText = this.nativeLayoutReplacementText;
      this.nativeLayoutSnapshot = null;
      this.nativeLayoutOriginalName = string.Empty;
      this.nativeLayoutOriginalText = string.Empty;
      this.nativeLayoutReplacementName = string.Empty;
      this.nativeLayoutReplacementText = string.Empty;
    }

    this.TryRestoreNativeLayout(
        layoutSnapshot,
        layoutOriginalName,
        layoutOriginalText,
        layoutReplacementName,
        layoutReplacementText,
        restoreText: false);
  }

  /// <summary>
  ///     Restores one tracked native BattleTalk layout snapshot back to the
  ///     original game state.
  /// </summary>
  /// <param name="layoutSnapshot">The captured layout snapshot.</param>
  /// <param name="originalName">The original sender name to write back.</param>
  /// <param name="originalText">The original text to write back.</param>
  /// <param name="replacementName">The exact applied sender-name replacement.</param>
  /// <param name="replacementText">The exact applied text replacement.</param>
  private unsafe void TryRestoreNativeLayout(
      NativeTextNodeLayoutSnapshot? layoutSnapshot,
      string originalName,
      string originalText,
      string replacementName,
      string replacementText,
      bool restoreText = true)
  {
    if (layoutSnapshot == null)
    {
      return;
    }

    var addonPtr = GameGuiInterface.GetAddonByName(BattleTalkAddonName);
    if (addonPtr.Address != IntPtr.Zero)
    {
      var battleTalkAddon = (AtkUnitBase*)addonPtr.Address;
      if (battleTalkAddon != null)
      {
        var nameNode = battleTalkAddon->GetTextNodeById(NameNodeId);
        if (restoreText &&
            nameNode != null &&
            !string.IsNullOrWhiteSpace(originalName))
        {
          var nameNodeAddress = (nint)nameNode;
          var liveName = MemoryHelper.ReadSeStringAsString(
              out _,
              (nint)nameNode->NodeText.StringPtr.Value);
          NativeMutationOwnership.TryRestore(
              liveName,
              replacementName,
              originalName,
              restoredName => ((AtkTextNode*)nameNodeAddress)->SetText(
                  restoredName));
        }
      }
    }

    var textNode = (AtkTextNode*)layoutSnapshot.TextNodeAddress;
    if (textNode == null)
    {
      return;
    }

    var liveText = MemoryHelper.ReadSeStringAsString(
        out _,
        (nint)textNode->NodeText.StringPtr.Value);
    NativeMutationOwnership.TryRestoreWithLayoutFallback(
        liveText,
        replacementText,
        originalText,
        restoreText,
        restoredText => NativeTextNodeLayoutHelper.RestoreLayoutSnapshot(
            layoutSnapshot,
            restoredText,
            restoreText),
        () => NativeTextNodeLayoutHelper.RestoreLayoutSnapshot(
            layoutSnapshot,
            originalText,
            restoreText: false));
  }

  /// <summary>
  ///     Builds a lookup entity matching the historical BattleTalk message schema
  ///     already used in the database.
  /// </summary>
  /// <param name="originalName">The original sender name.</param>
  /// <param name="originalText">The original BattleTalk text.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <returns>
  ///     A formatted <see cref="BattleTalkMessage" /> suitable for DB lookup.
  /// </returns>
  private BattleTalkMessage BuildLookupMessage(
      string originalName,
      string originalText,
      SourceClientLanguage sourceLanguage,
      TranslationReuseScope? scope = null)
  {
    var operationScope = scope ?? this.CreateDialogueReuseScope(sourceLanguage);
    return new BattleTalkMessage(
        originalName,
        originalText,
        sourceLanguage.PersistenceCode,
        sourceLanguage.PersistenceCode,
        string.Empty,
        string.Empty,
        operationScope.TargetLanguageCode,
        operationScope.TranslationEngine.GetValueOrDefault(),
        rtlLangTranslationImageData: null,
        DateTime.Now,
        DateTime.Now);
  }

  /// <summary>
  ///     Normalizes translated text for native BattleTalk replacement when the
  ///     active config requests diacritic stripping.
  /// </summary>
  /// <param name="text">The translated text to normalize.</param>
  /// <returns>
  ///     The text that should be written back into the native BattleTalk addon.
  /// </returns>
  private string NormalizeForReplacement(string text)
  {
    return this.config.RemoveDiacriticsWhenUsingReplacementTalkBTalk
        ? this.normalizeReplacementText(text)
        : text;
  }

  /// <summary>
  ///     Determines whether BattleTalk sender names should participate in
  ///     translation, native replacement, and overlay title resolution.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when BattleTalk sender names are enabled for the
  ///     current config; otherwise, <see langword="false" />.
  /// </returns>
  private bool ShouldTranslateBattleTalkNpcNames()
  {
    return this.config.TranslateBattleTalkNpcNames;
  }

  /// <summary>
  ///     Publishes translated BattleTalk content into the shared overlay state.
  /// </summary>
  /// <param name="originalName">The original sender name.</param>
  /// <param name="originalText">The original BattleTalk text.</param>
  /// <param name="translatedName">The translated sender name.</param>
  /// <param name="translatedText">The translated BattleTalk text.</param>
  private void PublishOverlay(
      string originalName,
      string originalText,
      string translatedName,
      string translatedText)
  {
    var resolvedOverlayName = !string.IsNullOrWhiteSpace(translatedName)
        ? translatedName
        : originalName;
    var overlayName = this.ShouldSwapTexts()
        ? originalName
        : resolvedOverlayName;
    var overlayText = this.ShouldSwapTexts()
        ? originalText
        : translatedText;

    this.updateOverlay(
        overlayName,
        overlayText,
        originalName);
  }

  /// <summary>
  ///     Builds the normalized runtime session key used by BattleTalk
  ///     dialogue session history.
  /// </summary>
  /// <param name="originalName">The visible speaker name.</param>
  /// <param name="sourceLanguage">The operation-captured source identity.</param>
  /// <returns>The normalized session key.</returns>
  internal string BuildDialogueSessionKey(
      string originalName,
      SourceClientLanguage sourceLanguage,
      TranslationReuseScope? scope = null)
  {
    var normalizedSpeaker = string.IsNullOrWhiteSpace(originalName)
        ? "(anonymous)"
        : originalName.Trim();
    var operationScope = scope ?? this.CreateDialogueReuseScope(sourceLanguage);
    return
        $"{normalizedSpeaker}|source:{operationScope.SourceLanguageCode}|engine:{operationScope.TranslationEngine}|target:{operationScope.TargetLanguageCode}";
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
        this.translationService.GetEffectiveTranslationEngineId(
            TranslationSurfaceGroup.Dialogue),
        this.config.TranslateAlreadyTranslatedTexts);
  }

  /// <summary>
  ///     Resolves a BattleTalk translation from cache or external translation and
  ///     stores the result as the current translation state.
  /// </summary>
  /// <param name="originalName">The original sender name.</param>
  /// <param name="originalText">The original BattleTalk text.</param>
  /// <param name="requestId">
  ///     The request identifier used to reject stale results.
  /// </param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <param name="sourceOperation">The source generation captured when queued.</param>
  /// <returns>A task that completes when the translation state has been updated.</returns>
  private async Task ResolveTranslationAsync(
      string originalName,
      string originalText,
      int requestId,
      SourceClientLanguage sourceLanguage,
      SourcePublicationOperation sourceOperation)
  {
    try
    {
      var operationScope = sourceOperation.Scope ??
                           this.CreateDialogueReuseScope(sourceLanguage);
      var translatorResolution = this.translationService
          .CaptureTranslatorResolution(
              operationScope.TranslationEngine.GetValueOrDefault(),
              TranslationSurfaceGroup.Dialogue);
      var lookup = this.BuildLookupMessage(
          originalName,
          originalText,
          sourceLanguage,
          operationScope);
      var foundBattleTalkMessage = this.findBattleTalkMessage(lookup);

      string translatedName;
      string translatedText;
      VisibleStorySurfaceProvenanceKind provenance;
      bool usedRuntimeOnlyDialogueContext = false;
      var dialogueTranslationEngine = operationScope.TranslationEngine
                                      .GetValueOrDefault();

      if (this.IsStoredTranslationUsable(
              foundBattleTalkMessage,
              originalName,
              originalText))
      {
        translatedName = this.ShouldTranslateBattleTalkNpcNames()
            ? foundBattleTalkMessage.TranslatedSenderName ?? string.Empty
            : string.Empty;
        translatedText =
            foundBattleTalkMessage.TranslatedBattleTalkMessage ?? string.Empty;
        provenance = VisibleStorySurfaceProvenanceKind.DbReuse;
      }
      else
      {
        var dialogueContext = DialogueTranslationSessionStore.BuildContext(
            BattleTalkAddonName,
            this.BuildDialogueSessionKey(
                originalName,
                sourceLanguage,
                operationScope),
            originalName,
            originalText,
            DialogueSessionHistoryLimit,
            DialogueSessionTtl);
        var usesRuntimeOnlyDialogueContext =
            this.translationService.WillUseDialogueContext(
                dialogueContext,
                translatorResolution);
        usedRuntimeOnlyDialogueContext = usesRuntimeOnlyDialogueContext;

        translatedText = await this.translationService.TranslateAsync(
            originalText,
            sourceLanguage,
            operationScope.TargetLanguageCode,
            dialogueContext,
            TranslationSurfaceGroup.Dialogue,
            translatorResolution,
            originContext: "BattleTalk/Text").ConfigureAwait(false);

        translatedName = string.Empty;
        if (this.ShouldTranslateBattleTalkNpcNames() && !originalName.IsNullOrEmpty())
        {
          try
          {
            translatedName = await this.translationService.TranslateAsync(
                originalName,
                sourceLanguage,
                operationScope.TargetLanguageCode,
                TranslationSurfaceGroup.Dialogue,
                translatorResolution,
                originContext: "BattleTalk/Speaker").ConfigureAwait(false);
          }
          catch (Exception ex)
          {
            PluginRuntimeLog.Warning(
                $"[{BattleTalkAddonName}] Speaker name translation failed; continuing with translated text. {ex.Message}");
          }
        }

        if (!usesRuntimeOnlyDialogueContext &&
            this.sourceLifecycle.IsCurrent(sourceOperation))
        {
          var translatedBattleTalkData = new BattleTalkMessage(
              originalName,
              originalText,
              sourceLanguage.PersistenceCode,
              sourceLanguage.PersistenceCode,
              translatedName,
              translatedText,
              operationScope.TargetLanguageCode,
              dialogueTranslationEngine,
              rtlLangTranslationImageData: null,
              DateTime.Now,
              DateTime.Now);

          await this.insertBattleTalkMessageAsync(
              translatedBattleTalkData,
              sourceOperation.CancellationToken);
          provenance = VisibleStorySurfaceProvenanceKind.FreshLiveTranslation;
        }
        else if (!usesRuntimeOnlyDialogueContext)
        {
          return;
        }
        else
        {
          provenance =
              VisibleStorySurfaceProvenanceKind
                  .FreshLiveTranslationRuntimeOnlyDialogueContext;
        }
      }

      this.sourceLifecycle.TryPublish(
          sourceOperation,
          () =>
          {
            lock (this.stateGate)
            {
              if (requestId != this.activeRequestId ||
                  !NativeRuntimeSourceScope.MatchesSource(
                      this.currentSourceLanguageCode,
                      sourceLanguage))
              {
                return;
              }

              this.translationInFlight = false;
              this.currentReplacementName =
                  this.NormalizeForReplacement(translatedName);
              this.currentReplacementText =
                  this.NormalizeForReplacement(translatedText);
              this.currentTranslatedName = translatedName;
              this.currentTranslatedText = translatedText;
              if (string.IsNullOrWhiteSpace(translatedText))
              {
                this.failedOriginalName = originalName;
                this.failedOriginalText = originalText;
                this.failedSourceLanguageCode =
                    sourceLanguage.PersistenceCode;
              }
              else
              {
                this.failedOriginalName = string.Empty;
                this.failedOriginalText = string.Empty;
                this.failedSourceLanguageCode = string.Empty;
              }

              this.lastResolvedSourceLanguageCode =
                  sourceLanguage.PersistenceCode;
              this.lastResolvedOriginalName = originalName;
              this.lastResolvedOriginalText = originalText;
              this.lastResolvedReplacementName =
                  this.currentReplacementName;
              this.lastResolvedReplacementText =
                  this.currentReplacementText;
            }

            if (!string.IsNullOrWhiteSpace(translatedText))
            {
              this.RecordDiagnosticsSnapshot(
                  provenance,
                  originalName,
                  originalText,
                  translatedName,
                  translatedText,
                  usedRuntimeOnlyDialogueContext,
                  dialogueTranslationEngine);
              this.PublishOverlay(
                  originalName,
                  originalText,
                  translatedName,
                  translatedText);
            }
            else
            {
              this.clearOverlay();
            }
          });
    }
    catch (Exception ex)
    {
      lock (this.stateGate)
      {
        if (requestId == this.activeRequestId)
        {
          this.translationInFlight = false;
          this.failedOriginalName = originalName;
          this.failedOriginalText = originalText;
          this.failedSourceLanguageCode = sourceLanguage.PersistenceCode;
        }
      }

      PluginRuntimeLog.Error(
          $"[{BattleTalkAddonName}] Error resolving BattleTalk translation: {ex}");
    }
  }

  /// <summary>
  ///     Determines whether a BattleTalk row loaded from the database still
  ///     represents a usable translation for the current source line.
  /// </summary>
  /// <param name="battleTalkMessage">The stored BattleTalk row to validate.</param>
  /// <param name="originalName">The expected original sender name.</param>
  /// <param name="originalText">The expected original BattleTalk text.</param>
  /// <returns>
  ///     <see langword="true" /> when the stored row contains a non-empty
  ///     translation for the same original source line; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool IsStoredTranslationUsable(
      BattleTalkMessage? battleTalkMessage,
      string originalName,
      string originalText)
  {
    if (battleTalkMessage == null)
    {
      return false;
    }

    if (battleTalkMessage.OriginalBattleTalkMessage != originalText ||
        battleTalkMessage.SenderName != originalName)
    {
      return false;
    }

    return !string.IsNullOrWhiteSpace(
        battleTalkMessage.TranslatedBattleTalkMessage);
  }

  /// <summary>
  ///     Determines whether native BattleTalk text should be replaced instead of
  ///     leaving the original addon text untouched.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when the BattleTalk addon should receive
  ///     translated native text; otherwise, <see langword="false" />.
  /// </returns>
  private bool ShouldApplyNativeBattleTalkText()
  {
    return TranslationDisplayModeHelper.WritesNativeTranslation(
        this.config.BattleTalkTranslationDisplayMode,
        this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Determines whether the BattleTalk overlay should show the original text
  ///     while the native addon receives the translation.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when BattleTalk swap mode is active; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool ShouldSwapTexts()
  {
    return TranslationDisplayModeHelper.ShowsOriginalOverlayText(
        this.config.BattleTalkTranslationDisplayMode,
        this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Returns the active cached translation when it still matches the supplied
  ///     source BattleTalk content.
  /// </summary>
  /// <param name="originalName">The source sender name.</param>
  /// <param name="originalText">The source BattleTalk text.</param>
  /// <param name="translatedName">Receives the translated sender name.</param>
  /// <param name="translatedText">Receives the translated BattleTalk text.</param>
  /// <param name="replacementName">
  ///     Receives the sender name already normalized for native replacement.
  /// </param>
  /// <param name="replacementText">
  ///     Receives the BattleTalk text already normalized for native replacement.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> when a matching cached translation exists;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryGetCachedTranslation(
      string originalName,
      string originalText,
      SourceClientLanguage sourceLanguage,
      out string translatedName,
      out string translatedText,
      out string replacementName,
      out string replacementText)
  {
    lock (this.stateGate)
    {
      var matchesCurrentSource = NativeRuntimeSourceScope.MatchesDialogueState(
          this.currentSourceLanguageCode,
          this.currentOriginalName,
          this.currentOriginalText,
          sourceLanguage,
          originalName,
          originalText);
      var hasTranslation = !string.IsNullOrWhiteSpace(this.currentTranslatedText);

      if (matchesCurrentSource && hasTranslation)
      {
        translatedName = this.currentTranslatedName;
        translatedText = this.currentTranslatedText;
        replacementName = this.currentReplacementName;
        replacementText = this.currentReplacementText;
        return true;
      }
    }

    translatedName = string.Empty;
    translatedText = string.Empty;
    replacementName = string.Empty;
    replacementText = string.Empty;
    return false;
  }

  /// <summary>
  ///     Starts a new translation request when the BattleTalk source changes or
  ///     when the current source still has no cached translation available.
  /// </summary>
  /// <param name="originalName">The source sender name.</param>
  /// <param name="originalText">The source BattleTalk text.</param>
  /// <param name="requestId">
  ///     Receives the request identifier when a new translation job is queued.
  /// </param>
  /// <param name="sourceOperation">
  ///     Receives the source generation captured by the request.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> when a new translation task should be queued;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryQueueTranslation(
      string originalName,
      string originalText,
      SourceClientLanguage sourceLanguage,
      out int requestId,
      out SourcePublicationOperation sourceOperation)
  {
    lock (this.stateGate)
    {
      var sourceChanged = !NativeRuntimeSourceScope.MatchesDialogueState(
          this.currentSourceLanguageCode,
          this.currentOriginalName,
          this.currentOriginalText,
          sourceLanguage,
          originalName,
          originalText);
      var hasTranslation = !string.IsNullOrWhiteSpace(this.currentTranslatedText);
      var isKnownFailedSource =
          NativeRuntimeSourceScope.MatchesSource(
              this.failedSourceLanguageCode,
              sourceLanguage) &&
          this.failedOriginalName == originalName &&
          this.failedOriginalText == originalText;

      if (!sourceChanged && (this.translationInFlight || hasTranslation))
      {
        requestId = this.activeRequestId;
        sourceOperation = this.sourceLifecycle.Capture(
            this.CreateDialogueReuseScope(sourceLanguage));
        return false;
      }

      if (!sourceChanged && isKnownFailedSource)
      {
        requestId = this.activeRequestId;
        sourceOperation = this.sourceLifecycle.Capture(
            this.CreateDialogueReuseScope(sourceLanguage));
        return false;
      }

      this.currentSourceLanguageCode = sourceLanguage.PersistenceCode;
      this.currentOriginalName = originalName;
      this.currentOriginalText = originalText;
      this.currentReplacementName = string.Empty;
      this.currentReplacementText = string.Empty;
      this.currentTranslatedName = string.Empty;
      this.currentTranslatedText = string.Empty;
      this.failedOriginalName = string.Empty;
      this.failedOriginalText = string.Empty;
      this.failedSourceLanguageCode = string.Empty;
      this.translationInFlight = true;
      this.activeRequestId++;
      requestId = this.activeRequestId;
      sourceOperation = this.sourceLifecycle.Capture(
          this.CreateDialogueReuseScope(sourceLanguage));
      return true;
    }
  }

  /// <summary>
  ///     Attempts to capture currently visible BattleTalk text and queue a
  ///     translation request when the source changed.
  /// </summary>
  private unsafe void TryCaptureAndQueueTranslation()
  {
    if (!this.config.TranslateBattleTalk)
    {
      return;
    }

    var addonPtr = GameGuiInterface.GetAddonByName(BattleTalkAddonName);
    if (addonPtr.Address == IntPtr.Zero)
    {
      return;
    }

    var battleTalkAddon = (AtkUnitBase*)addonPtr.Address;
    if (battleTalkAddon == null || !battleTalkAddon->IsVisible)
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

    if (!this.TryReadCurrentSource(
            battleTalkAddon,
            out var originalName,
            out var originalText))
    {
      return;
    }

    if (this.TryGetCachedTranslation(
            originalName,
            originalText,
            sourceLanguage,
            out var translatedName,
            out var translatedText,
            out _,
            out _))
    {
      this.PublishOverlay(
          originalName,
          originalText,
          translatedName,
          translatedText);
      return;
    }

    this.ShowPendingSwapOverlayIfNeeded(originalName, originalText);

    if (this.TryQueueTranslation(
            originalName,
            originalText,
            sourceLanguage,
            out var requestId,
            out var sourceOperation))
    {
      Task.Run(() => this.ResolveTranslationAsync(
          originalName,
          originalText,
          requestId,
          sourceLanguage,
          sourceOperation));
    }
  }

  /// <summary>
  ///     Shows the original BattleTalk content in the overlay while a swap-mode
  ///     translation is still in flight, so first-seen lines do not leave the
  ///     overlay empty.
  /// </summary>
  /// <param name="originalName">The original sender name.</param>
  /// <param name="originalText">The original BattleTalk text.</param>
  private void ShowPendingSwapOverlayIfNeeded(
      string originalName,
      string originalText)
  {
    if (this.ShouldSwapTexts())
    {
      this.PublishOverlay(
          originalName,
          originalText,
          string.Empty,
          string.Empty);
      return;
    }

    this.clearOverlay();
  }

  /// <summary>
  ///     Reads the current BattleTalk source text from the live addon while mapping
  ///     already-replaced node text back to the original source content.
  /// </summary>
  /// <param name="battleTalkAddon">The visible BattleTalk addon.</param>
  /// <param name="originalName">Receives the logical source sender name.</param>
  /// <param name="originalText">Receives the logical source BattleTalk text.</param>
  /// <returns>
  ///     <see langword="true" /> when readable BattleTalk text is available;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private unsafe bool TryReadCurrentSource(
      AtkUnitBase* battleTalkAddon,
      out string originalName,
      out string originalText)
  {
    var nameNode = battleTalkAddon->GetTextNodeById(NameNodeId);
    var textNode = battleTalkAddon->GetTextNodeById(TextNodeId);

    if (textNode == null || textNode->NodeText.IsEmpty)
    {
      originalName = string.Empty;
      originalText = string.Empty;
      return false;
    }

    var visibleName = nameNode != null && !nameNode->NodeText.IsEmpty
        ? MemoryHelper.ReadSeStringAsString(
            out _,
            (nint)nameNode->NodeText.StringPtr.Value)
        : string.Empty;
    var visibleText = MemoryHelper.ReadSeStringAsString(
        out _,
        (nint)textNode->NodeText.StringPtr.Value);

    if (this.ShouldApplyNativeBattleTalkText() &&
        this.TryMapVisibleSourceToOriginal(
            visibleName,
            visibleText,
            out originalName,
            out originalText))
    {
      return !string.IsNullOrWhiteSpace(originalText);
    }

    originalName = visibleName;
    originalText = visibleText;
    return !string.IsNullOrWhiteSpace(originalText);
  }

  /// <summary>
  ///     Tries to map currently visible BattleTalk text back to the original source
  ///     line when the native UI is already showing translated replacement text.
  /// </summary>
  /// <param name="visibleName">The sender name currently visible in the addon.</param>
  /// <param name="visibleText">The BattleTalk text currently visible in the addon.</param>
  /// <param name="originalName">
  ///     Receives the original sender name when a match is found.
  /// </param>
  /// <param name="originalText">
  ///     Receives the original BattleTalk text when a match is found.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> when the visible addon text could be mapped back
  ///     to a known original source line; otherwise, <see langword="false" />.
  /// </returns>
  private bool TryMapVisibleSourceToOriginal(
      string visibleName,
      string visibleText,
      out string originalName,
      out string originalText)
  {
    lock (this.stateGate)
    {
      if (this.TryMapVisibleSourceToOriginal(
              visibleName,
              visibleText,
              this.currentOriginalName,
              this.currentOriginalText,
              this.currentReplacementName,
              this.currentReplacementText,
              allowOriginalTextMatch: true,
              requireNameMatch: true,
              out originalName,
              out originalText))
      {
        return true;
      }

      if (!string.IsNullOrWhiteSpace(this.lastResolvedSourceLanguageCode) &&
          RuntimeLanguageHelper.LanguagesMatch(
              this.lastResolvedSourceLanguageCode,
              this.currentSourceLanguageCode) &&
          this.TryMapVisibleSourceToOriginal(
              visibleName,
              visibleText,
              this.lastResolvedOriginalName,
              this.lastResolvedOriginalText,
              this.lastResolvedReplacementName,
              this.lastResolvedReplacementText,
              allowOriginalTextMatch: false,
              requireNameMatch: true,
              out originalName,
              out originalText))
      {
        return true;
      }

      if (this.ShouldSwapTexts())
      {
        if (this.TryMapVisibleSourceToOriginal(
                visibleName,
                visibleText,
                this.currentOriginalName,
                this.currentOriginalText,
                this.currentReplacementName,
                this.currentReplacementText,
                allowOriginalTextMatch: true,
                requireNameMatch: false,
                out originalName,
                out originalText))
        {
          return true;
        }

        if (!string.IsNullOrWhiteSpace(this.lastResolvedSourceLanguageCode) &&
            RuntimeLanguageHelper.LanguagesMatch(
                this.lastResolvedSourceLanguageCode,
                this.currentSourceLanguageCode) &&
            this.TryMapVisibleSourceToOriginal(
                visibleName,
                visibleText,
                this.lastResolvedOriginalName,
                this.lastResolvedOriginalText,
                this.lastResolvedReplacementName,
                this.lastResolvedReplacementText,
                allowOriginalTextMatch: false,
                requireNameMatch: false,
                out originalName,
                out originalText))
        {
          return true;
        }
      }
    }

    originalName = string.Empty;
    originalText = string.Empty;
    return false;
  }

  /// <summary>
  ///     Records the latest visible BattleTalk provenance snapshot for the
  ///     debugger.
  /// </summary>
  /// <param name="provenance">The provenance label kind to expose.</param>
  /// <param name="originalName">The original speaker name.</param>
  /// <param name="originalText">The original BattleTalk text.</param>
  /// <param name="translatedName">The translated speaker name.</param>
  /// <param name="translatedText">The translated BattleTalk text.</param>
  /// <param name="usedRuntimeOnlyDialogueContext">
  /// Whether runtime-only dialogue context influenced the live translation.
  /// </param>
  /// <param name="effectiveTranslationEngineId">
  /// The effective dialogue translation engine identifier.
  /// </param>
  private void RecordDiagnosticsSnapshot(
      VisibleStorySurfaceProvenanceKind provenance,
      string originalName,
      string originalText,
      string translatedName,
      string translatedText,
      bool usedRuntimeOnlyDialogueContext,
      int effectiveTranslationEngineId)
  {
    VisibleStorySurfaceDiagnosticsStore.Record(
        new VisibleStorySurfaceDiagnosticsSnapshot(
            VisibleStorySurfaceKind.BattleTalk,
            provenance,
            VisibleStorySurfaceTableMap.Resolve(
                VisibleStorySurfaceKind.BattleTalk),
            originalName,
            originalText,
            string.Empty,
            translatedName,
            translatedText,
            string.Empty,
            usedRuntimeOnlyDialogueContext,
            effectiveTranslationEngineId,
            DateTime.UtcNow,
            null,
            null));
  }

  /// <summary>
  ///     Tries to map visible BattleTalk text back to a specific known original and
  ///     replacement pair.
  /// </summary>
  /// <param name="visibleName">The sender name currently visible in the addon.</param>
  /// <param name="visibleText">The BattleTalk text currently visible in the addon.</param>
  /// <param name="candidateOriginalName">The candidate original sender name.</param>
  /// <param name="candidateOriginalText">The candidate original BattleTalk text.</param>
  /// <param name="candidateReplacementName">
  ///     The candidate sender name after native replacement normalization.
  /// </param>
  /// <param name="candidateReplacementText">
  ///     The candidate BattleTalk text after native replacement normalization.
  /// </param>
  /// <param name="allowOriginalTextMatch">
  ///     Whether the visible text is allowed to match the original source text in
  ///     addition to the replacement text.
  /// </param>
  /// <param name="requireNameMatch">
  ///     Whether the visible sender name must also match the candidate state.
  ///     Swap-mode BattleTalk can transiently rewrite the speaker node during timer
  ///     ticks, so text-only matching is allowed as a fallback there.
  /// </param>
  /// <param name="originalName">
  ///     Receives the original sender name when a match is found.
  /// </param>
  /// <param name="originalText">
  ///     Receives the original BattleTalk text when a match is found.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> when the visible text matches the supplied
  ///     candidate state; otherwise, <see langword="false" />.
  /// </returns>
  private bool TryMapVisibleSourceToOriginal(
      string visibleName,
      string visibleText,
      string candidateOriginalName,
      string candidateOriginalText,
      string candidateReplacementName,
      string candidateReplacementText,
      bool allowOriginalTextMatch,
      bool requireNameMatch,
      out string originalName,
      out string originalText)
  {
    if (string.IsNullOrWhiteSpace(candidateOriginalText))
    {
      originalName = string.Empty;
      originalText = string.Empty;
      return false;
    }

    var textMatches = this.TextMatchesForSourceMapping(
        visibleText,
        candidateReplacementText);
    if (!textMatches && allowOriginalTextMatch)
    {
      textMatches = this.TextMatchesForSourceMapping(
          visibleText,
          candidateOriginalText);
    }

    if (!textMatches)
    {
      originalName = string.Empty;
      originalText = string.Empty;
      return false;
    }

    var nameMatches = !requireNameMatch;
    if (requireNameMatch)
    {
      nameMatches = !this.ShouldTranslateBattleTalkNpcNames() ||
                    string.IsNullOrWhiteSpace(candidateReplacementName) ||
                    visibleName == candidateReplacementName;
      if (!nameMatches && allowOriginalTextMatch)
      {
        nameMatches = visibleName == candidateOriginalName;
      }
    }

    if (!nameMatches)
    {
      originalName = string.Empty;
      originalText = string.Empty;
      return false;
    }

    originalName = candidateOriginalName;
    originalText = candidateOriginalText;
    return true;
  }

  /// <summary>
  ///     Tries to capture the currently visible BattleTalk source line, mapping
  ///     translated native state back to the original source when possible.
  /// </summary>
  /// <param name="originalName">Receives the original visible sender name.</param>
  /// <param name="originalText">Receives the original visible BattleTalk text.</param>
  /// <returns>
  ///     <see langword="true" /> when a visible BattleTalk line could be
  ///     resolved; otherwise, <see langword="false" />.
  /// </returns>
  private unsafe bool TryCaptureCurrentBattleTalkSource(
      out string originalName,
      out string originalText)
  {
    var addonPtr = GameGuiInterface.GetAddonByName(BattleTalkAddonName);
    if (addonPtr.Address != IntPtr.Zero)
    {
      var battleTalkAddon = (AtkUnitBase*)addonPtr.Address;
      if (battleTalkAddon != null &&
          battleTalkAddon->IsVisible &&
          this.TryReadCurrentSource(
              battleTalkAddon,
              out originalName,
              out originalText))
      {
        return true;
      }
    }

    lock (this.stateGate)
    {
      if (string.IsNullOrWhiteSpace(this.currentOriginalText))
      {
        originalName = string.Empty;
        originalText = string.Empty;
        return false;
      }

      originalName = this.currentOriginalName;
      originalText = this.currentOriginalText;
      return true;
    }
  }

  /// <summary>
  ///     Determines whether the visible BattleTalk node text still matches a
  ///     candidate source or replacement string after layout-driven wrapping or
  ///     whitespace adjustments performed by the game UI.
  /// </summary>
  /// <param name="visibleText">The current text read back from the live addon.</param>
  /// <param name="candidateText">
  ///     The candidate original or replacement text stored by the handler.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> when both texts normalize to the same logical
  ///     content; otherwise, <see langword="false" />.
  /// </returns>
  private bool TextMatchesForSourceMapping(
      string visibleText,
      string candidateText)
  {
    if (string.IsNullOrWhiteSpace(visibleText) ||
        string.IsNullOrWhiteSpace(candidateText))
    {
      return false;
    }

    return this.NormalizeSourceComparisonText(visibleText) ==
           this.NormalizeSourceComparisonText(candidateText);
  }

  /// <summary>
  ///     Normalizes BattleTalk text for source matching by collapsing layout
  ///     whitespace differences introduced when the game wraps long lines.
  /// </summary>
  /// <param name="text">The text to normalize.</param>
  /// <returns>
  ///     The text collapsed to a single-space representation suitable for
  ///     logical equality checks.
  /// </returns>
  private string NormalizeSourceComparisonText(string text)
  {
    return string.Join(
        " ",
        text.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries));
  }

  /// <summary>
  ///     Determines whether the currently visible BattleTalk node text already
  ///     matches the translated text rendered by native replacement.
  /// </summary>
  /// <param name="visibleName">The currently visible sender name.</param>
  /// <param name="visibleText">The currently visible BattleTalk text.</param>
  /// <returns>
  ///     <see langword="true" /> when the visible nodes already contain the
  ///     translated output for the active source; otherwise, <see langword="false" />.
  /// </returns>
  private bool IsVisibleReplacementState(string visibleName, string visibleText)
  {
    if (string.IsNullOrWhiteSpace(this.currentOriginalText))
    {
      return false;
    }

      var normalizedTranslatedText = this.currentReplacementText;
      var normalizedTranslatedName = this.currentReplacementName;

    var textMatchesCurrentSource =
        visibleText == this.currentOriginalText ||
        (!string.IsNullOrWhiteSpace(normalizedTranslatedText) &&
         visibleText == normalizedTranslatedText);
    var nameMatchesCurrentSource =
        visibleName == this.currentOriginalName ||
        (!string.IsNullOrWhiteSpace(normalizedTranslatedName) &&
         visibleName == normalizedTranslatedName) ||
        !this.ShouldTranslateBattleTalkNpcNames();

    return textMatchesCurrentSource && nameMatchesCurrentSource;
  }

  /// <summary>
  ///     Applies translated values directly to the visible BattleTalk addon when
  ///     replacement mode is enabled.
  /// </summary>
  /// <param name="battleTalkAddon">The visible BattleTalk addon.</param>
  /// <param name="translatedName">The translated sender name.</param>
  /// <param name="replacementName">
  ///     The translated sender name already normalized for native replacement.
  /// </param>
  /// <param name="replacementText">
  ///     The translated BattleTalk text already normalized for native replacement.
  /// </param>
  private unsafe void ApplyTranslatedNodes(
      AtkUnitBase* battleTalkAddon,
      string originalName,
      string originalText,
      string translatedName,
      string replacementName,
      string replacementText)
  {
    var nameNode = battleTalkAddon->GetTextNodeById(NameNodeId);
    var textNode = battleTalkAddon->GetTextNodeById(TextNodeId);
    var parentNode = battleTalkAddon->GetNodeById(ParentNodeId);
    var nineGridNode = battleTalkAddon->GetNodeById(NineGridNodeId);
    var timerNode = battleTalkAddon->GetNodeById(TimerNodeId);

    if (textNode == null || textNode->NodeText.IsEmpty)
    {
      return;
    }

    var visibleName = nameNode != null && !nameNode->NodeText.IsEmpty
        ? MemoryHelper.ReadSeStringAsString(
            out _,
            (nint)nameNode->NodeText.StringPtr.Value)
        : string.Empty;
    var visibleText = MemoryHelper.ReadSeStringAsString(
        out _,
        (nint)textNode->NodeText.StringPtr.Value);

    if (!string.IsNullOrWhiteSpace(replacementText) &&
        this.TextMatchesForSourceMapping(visibleText, replacementText) &&
        (!this.ShouldTranslateBattleTalkNpcNames() ||
         string.IsNullOrWhiteSpace(replacementName) ||
         this.TextMatchesForSourceMapping(visibleName, replacementName)))
    {
      return;
    }

    if (this.ShouldTranslateBattleTalkNpcNames() &&
        nameNode != null &&
        !string.IsNullOrWhiteSpace(translatedName) &&
        !this.TextMatchesForSourceMapping(visibleName, replacementName))
    {
      nameNode->SetText(replacementName);
    }

    this.PrepareTrackedNativeLayoutForReapply(
        originalName,
        originalText);

    var backgroundNode = nineGridNode != null ? (AtkResNode*)nineGridNode : null;
    var layoutSnapshot = NativeTextNodeLayoutHelper.CaptureLayoutSnapshot(
        textNode,
        parentNode,
        backgroundNode,
        timerNode,
        preferCompactWrappedHeight: true);
    var preferredWrapWidth = NativeTextNodeLayoutHelper.ResolvePreferredWrapWidth(
        textNode);
    var resizeResult = NativeTextNodeLayoutHelper.ApplyWrappedTextAndMeasure(
        textNode,
        replacementText,
        preferredWrapWidth,
        preferCompactWrappedHeight: true);
    NativeTextNodeLayoutHelper.ResizeFromSnapshot(
        layoutSnapshot,
        resizeResult,
        parentNode,
        backgroundNode,
        timerNode,
        allowWidthGrowth: false,
        preserveHorizontalGeometry: true);

    lock (this.stateGate)
    {
      this.nativeLayoutSnapshot = layoutSnapshot;
      this.nativeLayoutOriginalName = originalName;
      this.nativeLayoutOriginalText = originalText;
      this.nativeLayoutReplacementName = replacementName;
      this.nativeLayoutReplacementText = replacementText;
    }
  }
}


