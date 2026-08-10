// <copyright file="TalkHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;

namespace Echoglossian.NativeUI.AddonHandlers.Talk;

/// <summary>
///     Handles the full Talk addon runtime inside the new addon-handler model.
///     This includes capture, translation lookup, async translation, overlay
///     updates, and optional native text replacement.
/// </summary>
public sealed class TalkHandler :
    IAddonTranslationHandler,
    IVisibleDialogueRetranslationHandler,
    IPluginUnloadAwareAddonHandler,
    IDisposable
{
  private static readonly TimeSpan DialogueSessionTtl = TimeSpan.FromSeconds(30);
  private const int DialogueSessionHistoryLimit = 3;
  private const string TalkAddonName = "Talk";
  private const int NameNodeId = 2;
  private const int TextNodeId = 3;
  private const int ParentNodeId = 10;

  private readonly Action clearOverlay;
  private readonly Config config;
  private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>> eventHandlers = new();
  private readonly Func<TalkMessage, CancellationToken, Task<TalkMessage?>>
      findTalkMessageAsync;
  private readonly Func<TalkMessage, CancellationToken, Task<string>> insertTalkMessageAsync;
  private readonly Func<string, string> normalizeReplacementText;
  private readonly Func<string, string, SourceClientLanguage, CancellationToken,
      Task<DialogueInterlocutorHints>> resolveInterlocutorHintsAsync;
  private readonly OwnedAsyncOperationSet ownedOperations = new(
      exception => PluginRuntimeLog.Error(
          $"[{TalkAddonName}] Unexpected Talk background operation error: {exception}"));
  private readonly Action restoreNativeMutation;
  private readonly SourcePublicationLifecycle sourceLifecycle = new();
  private readonly object stateGate = new();
  private readonly TranslationService translationService;
  private readonly Action<string, string, string> updateOverlay;

  private int activeRequestId;
  private string currentSourceLanguageCode = string.Empty;
  private string currentOriginalName = string.Empty;
  private string currentOriginalText = string.Empty;
  private string currentReplacementName = string.Empty;
  private string currentReplacementText = string.Empty;
  private string currentTranslatedName = string.Empty;
  private string currentTranslatedText = string.Empty;
  private bool nativeTalkTextNodeStateCaptured;
  private bool nativeTalkTextNodeStateDirty;
  private byte originalTalkFontSize;
  private float originalTalkTextWidth;
  private TextFlags originalTalkTextFlags;
  private string nativeTalkTextNodeStateCapturedForSourceText = string.Empty;
  private bool translationInFlight;

  /// <summary>
  ///     Initializes a new instance of the <see cref="TalkHandler" /> class.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The translation service used by the plugin.</param>
  /// <param name="findTalkMessageAsync">
  ///     Delegate used to look up previously translated Talk messages.
  /// </param>
  /// <param name="insertTalkMessageAsync">
  ///     Delegate used to persist translated Talk messages.
  /// </param>
  /// <param name="updateOverlay">
  ///     Delegate used to publish translated content to the Talk overlay state.
  /// </param>
  /// <param name="clearOverlay">
  ///     Delegate used to clear the Talk overlay state when the source text changes.
  /// </param>
  /// <param name="normalizeReplacementText">
  ///     Delegate used to normalize translated text before native replacement.
  /// </param>
  /// <param name="resolveInterlocutorHintsAsync">
  ///     Delegate used to resolve current-line interlocutor hints after a database miss.
  /// </param>
  /// <param name="restoreNativeMutation">
  ///     Optional native-free override for restoring tracked Talk mutation.
  /// </param>
  public TalkHandler(
      Config config,
      TranslationService translationService,
      Func<TalkMessage, CancellationToken, Task<TalkMessage?>>
          findTalkMessageAsync,
      Func<TalkMessage, Task<string>> insertTalkMessageAsync,
      Action<string, string, string> updateOverlay,
      Action clearOverlay,
      Func<string, string> normalizeReplacementText,
      Func<string, string, SourceClientLanguage, CancellationToken,
          Task<DialogueInterlocutorHints>> resolveInterlocutorHintsAsync,
      Action? restoreNativeMutation = null)
    : this(
        config,
        translationService,
        findTalkMessageAsync,
        (message, _) => insertTalkMessageAsync(message),
        updateOverlay,
        clearOverlay,
        normalizeReplacementText,
        resolveInterlocutorHintsAsync,
        restoreNativeMutation)
  {
  }

  /// <summary>
  ///     Initializes a Talk handler with cancellation-aware dialogue
  ///     persistence owned by the captured operation scope.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The shared translation service.</param>
  /// <param name="findTalkMessageAsync">The canonical asynchronous Talk lookup.</param>
  /// <param name="insertTalkMessageAsync">The cancellation-aware persistence delegate.</param>
  /// <param name="updateOverlay">The overlay publication callback.</param>
  /// <param name="clearOverlay">The overlay clear callback.</param>
  /// <param name="normalizeReplacementText">The native replacement normalizer.</param>
  /// <param name="resolveInterlocutorHintsAsync">
  ///     The asynchronous current-line interlocutor hint resolver.
  /// </param>
  /// <param name="restoreNativeMutation">The optional native restoration override.</param>
  internal TalkHandler(
      Config config,
      TranslationService translationService,
      Func<TalkMessage, CancellationToken, Task<TalkMessage?>>
          findTalkMessageAsync,
      Func<TalkMessage, CancellationToken, Task<string>> insertTalkMessageAsync,
      Action<string, string, string> updateOverlay,
      Action clearOverlay,
      Func<string, string> normalizeReplacementText,
      Func<string, string, SourceClientLanguage, CancellationToken,
          Task<DialogueInterlocutorHints>> resolveInterlocutorHintsAsync,
      Action? restoreNativeMutation = null)
  {
    this.config = config;
    this.translationService = translationService;
    this.findTalkMessageAsync = findTalkMessageAsync;
    this.insertTalkMessageAsync = insertTalkMessageAsync;
    this.updateOverlay = updateOverlay;
    this.clearOverlay = clearOverlay;
    this.normalizeReplacementText = normalizeReplacementText;
    this.resolveInterlocutorHintsAsync = resolveInterlocutorHintsAsync;
    this.restoreNativeMutation =
        restoreNativeMutation ?? this.RestoreTrackedNativeMutation;

    this.RegisterHandler(AddonEvent.PreRefresh, this.OnPreRefresh);
    this.RegisterHandler(AddonEvent.PostUpdate, this.OnApplyNativeTalkText);
    this.RegisterHandler(AddonEvent.PreDraw, this.OnApplyNativeTalkText);
    this.RegisterHandler(AddonEvent.PreHide, this.OnResetState);
    this.RegisterHandler(AddonEvent.PreFinalize, this.OnResetState);
  }

  /// <summary>
  ///     Returns the event handlers required to drive the Talk addon flow.
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
  public void OnPluginUnload()
  {
    this.InvalidateStateForSource(null);
    this.ownedOperations.Dispose();
  }

  /// <inheritdoc />
  public void Dispose()
  {
    this.OnPluginUnload();
  }

  /// <inheritdoc />
  public async Task<VisibleDialogueRetranslationResult> RetranslateVisibleTextAndPersistAsync()
  {
    const VisibleStorySurfaceKind surface = VisibleStorySurfaceKind.Talk;
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

    if (!this.TryCaptureCurrentTalkSource(out var originalName, out var originalText))
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
          sourceOperation.CancellationToken,
          originContext: "Talk/Text").ConfigureAwait(false);
      var translatedName = this.ShouldTranslateTalkNpcNames() &&
                           !originalName.IsNullOrEmpty()
          ? await this.translationService.TranslateAsync(
              originalName,
              sourceLanguage,
              operationScope.TargetLanguageCode,
              TranslationSurfaceGroup.Dialogue,
              translatorResolution,
              sourceOperation.CancellationToken,
              originContext: "Talk/Speaker").ConfigureAwait(false)
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
          }
        }

        return new VisibleDialogueRetranslationResult(
            true,
            false,
            surface,
            surfaceName,
            VisibleStorySurfaceText.GetNoUsableTranslationMessage(surface));
      }

      var translatedTalkData = new TalkMessage(
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

      var persistenceResult = await Echoglossian.UpsertTalkDataAsync(
          translatedTalkData,
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
      var sourceChangedBeforeApply = !publicationAccepted || !stateUpdated;

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
          $"[{TalkAddonName}] Error retranslating visible Talk dialogue: {ex}");
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
  ///     Captures Talk source text during refresh, publishes any cached translation,
  ///     and queues translation work when needed.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the handler.</param>
  /// <param name="args">The addon arguments associated with the refresh.</param>
  private unsafe void OnPreRefresh(AddonEvent type, AddonArgs args)
  {
    if (args is not AddonRefreshArgs refreshArgs ||
        args.AddonName != TalkAddonName)
    {
      return;
    }

    var atkValues = (AtkValue*)refreshArgs.AtkValues;
    if (atkValues == null || refreshArgs.AtkValueCount < 2)
    {
      return;
    }

    var originalText = this.ReadTalkAtkString(atkValues[0]);
    var originalName = this.ReadTalkAtkString(atkValues[1]);

    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      this.InvalidateStateForSource(null);
      return;
    }

    this.InvalidateStateForSource(sourceLanguage);

    this.RemapTranslatedRefreshSourceToOriginal(
        ref originalName,
        ref originalText);

    if (string.IsNullOrWhiteSpace(originalText))
    {
      return;
    }

    if (this.ShouldApplyNativeTalkText())
    {
      var addonPtr = GameGuiInterface.GetAddonByName(TalkAddonName);
      if (addonPtr.Address != IntPtr.Zero)
      {
        var talkAddon = (AtkUnitBase*)addonPtr.Address;
        if (talkAddon != null && talkAddon->IsVisible)
        {
          var textNode = talkAddon->GetTextNodeById(TextNodeId);
          if (textNode != null && !textNode->NodeText.IsEmpty)
          {
            this.CaptureOriginalTalkTextNodeState(textNode, originalText);
          }
        }
      }
    }

    if (this.TryGetCachedTranslation(
            originalName,
            originalText,
            sourceLanguage,
            out var translatedName,
            out var translatedText))
    {
      this.PublishOverlay(
          originalName,
          originalText,
          translatedName,
          translatedText);

      if (this.ShouldApplyNativeTalkText())
      {
        this.ApplyTranslatedRefreshValues(
            atkValues,
            translatedName,
            translatedText);
      }

      return;
    }

    if (this.TryQueueTranslation(
            originalName,
            originalText,
            sourceLanguage))
    {
      return;
    }
  }

  /// <summary>
  ///     Applies translated Talk text to the visible addon during lifecycle stages
  ///     where native node mutations can survive long enough to be rendered.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the handler.</param>
  /// <param name="args">The addon arguments associated with the update or draw.</param>
  private unsafe void OnApplyNativeTalkText(AddonEvent type, AddonArgs args)
  {
    if (args.AddonName != TalkAddonName)
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

    var addonPtr = GameGuiInterface.GetAddonByName(TalkAddonName);
    if (addonPtr.Address == IntPtr.Zero)
    {
      return;
    }

    var talkAddon = (AtkUnitBase*)addonPtr.Address;
    if (talkAddon == null || !talkAddon->IsVisible)
    {
      return;
    }

    var nameNode = talkAddon->GetTextNodeById(NameNodeId);
    var textNode = talkAddon->GetTextNodeById(TextNodeId);
    var parentNode = talkAddon->GetNodeById(ParentNodeId);

    if (textNode == null || textNode->NodeText.IsEmpty || parentNode == null)
    {
      return;
    }

    var shouldApplyNativeTalkText = this.ShouldApplyNativeTalkText();
    if (!shouldApplyNativeTalkText)
    {
      return;
    }

    if (!this.TryGetCurrentResolvedTranslation(
            sourceLanguage,
            out var translatedName,
            out var translatedText,
            out var replacementName,
            out var replacementText))
    {
      this.TryRestoreOriginalTalkText(
          nameNode,
          textNode);

      return;
    }

    var visibleName = this.ReadNodeText(nameNode);
    var visibleText = this.ReadNodeText(textNode);

    if (!shouldApplyNativeTalkText)
    {
      this.TryRestoreOriginalTalkText(
          nameNode,
          textNode);

      return;
    }

    if (this.IsNativeTalkAlreadyApplied(
            visibleName,
            visibleText,
            replacementName,
            replacementText))
    {
      return;
    }

    if (this.ShouldTranslateTalkNpcNames() &&
        nameNode != null &&
        !string.IsNullOrWhiteSpace(translatedName) &&
        visibleName != replacementName)
    {
      nameNode->SetText(replacementName);
    }

    this.CaptureOriginalTalkTextNodeState(textNode, visibleText);
    textNode->TextFlags = TextFlags.WordWrap
                          | TextFlags.MultiLine
                          | TextFlags.AutoAdjustNodeSize;
    textNode->FontSize = (byte)(translatedText.Length >= 350
        ? 11
        : translatedText.Length >= 256 ? 12 : 14);
    textNode->SetWidth(parentNode->GetWidth());
    textNode->SetText(replacementText);
    textNode->ResizeNodeForCurrentText();
    this.nativeTalkTextNodeStateDirty = true;
  }

  /// <summary>
  ///     Restores plugin-owned native mutation and clears resolved dialogue
  ///     state when the operation source changes or cannot be resolved.
  /// </summary>
  /// <param name="sourceLanguage">
  ///     The operation-captured source, or no value when source resolution
  ///     failed.
  /// </param>
  internal void InvalidateStateForSource(
      SourceClientLanguage? sourceLanguage)
  {
    this.sourceLifecycle.TransitionTo(
        sourceLanguage.HasValue
            ? this.CreateDialogueReuseScope(sourceLanguage.Value)
            : null,
        () =>
        {
          bool hasOwnedState;
          lock (this.stateGate)
          {
            hasOwnedState =
                !string.IsNullOrWhiteSpace(
                    this.currentSourceLanguageCode);
          }

          if (hasOwnedState)
          {
            this.restoreNativeMutation();
          }

          lock (this.stateGate)
          {
            this.activeRequestId++;
            this.currentSourceLanguageCode = string.Empty;
            this.currentOriginalName = string.Empty;
            this.currentOriginalText = string.Empty;
            this.currentReplacementName = string.Empty;
            this.currentReplacementText = string.Empty;
            this.currentTranslatedName = string.Empty;
            this.currentTranslatedText = string.Empty;
            this.translationInFlight = false;
          }

          VisibleStorySurfaceDiagnosticsStore.Clear(
              VisibleStorySurfaceKind.Talk);
          this.clearOverlay();
        });
  }

  /// <summary>
  ///     Restores the tracked native Talk mutation when the live addon exists.
  /// </summary>
  private unsafe void RestoreTrackedNativeMutation()
  {
    var addonPtr = GameGuiInterface.GetAddonByName(TalkAddonName);
    if (addonPtr.Address != IntPtr.Zero)
    {
      var talkAddon = (AtkUnitBase*)addonPtr.Address;
      if (talkAddon != null && talkAddon->IsVisible)
      {
        this.TryRestoreOriginalTalkText(
            talkAddon->GetTextNodeById(NameNodeId),
            talkAddon->GetTextNodeById(TextNodeId));
      }
    }
  }

  /// <summary>
  ///     Clears the active Talk state when the addon receives a new event or
  ///     leaves the screen.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the reset.</param>
  /// <param name="args">The addon arguments associated with the reset.</param>
  private unsafe void OnResetState(AddonEvent type, AddonArgs args)
  {
    if (args.AddonName != TalkAddonName)
    {
      return;
    }

    var addonPtr = GameGuiInterface.GetAddonByName(TalkAddonName);
    if (addonPtr.Address != IntPtr.Zero)
    {
      var talkAddon = (AtkUnitBase*)addonPtr.Address;
      if (talkAddon != null &&
          talkAddon->IsVisible &&
          this.ShouldApplyNativeTalkText())
      {
        var nameNode = talkAddon->GetTextNodeById(NameNodeId);
        var textNode = talkAddon->GetTextNodeById(TextNodeId);
        if (nameNode != null || textNode != null)
        {
          this.TryRestoreOriginalTalkText(nameNode, textNode);
        }
      }
    }

    lock (this.stateGate)
    {
      this.activeRequestId++;
      this.currentSourceLanguageCode = string.Empty;
      this.currentOriginalName = string.Empty;
      this.currentOriginalText = string.Empty;
      this.currentReplacementName = string.Empty;
      this.currentReplacementText = string.Empty;
      this.currentTranslatedName = string.Empty;
      this.currentTranslatedText = string.Empty;
      this.translationInFlight = false;
    }

    VisibleStorySurfaceDiagnosticsStore.Clear(VisibleStorySurfaceKind.Talk);
    this.nativeTalkTextNodeStateCaptured = false;
    this.nativeTalkTextNodeStateDirty = false;
    this.nativeTalkTextNodeStateCapturedForSourceText = string.Empty;
    this.clearOverlay();
  }

  /// <summary>
  ///     Captures the original Talk text-node presentation so it can be restored
  ///     after native replacement or when the handler is disabled mid-stream.
  /// </summary>
  /// <param name="textNode">The Talk text node to snapshot.</param>
  private unsafe void CaptureOriginalTalkTextNodeState(
      AtkTextNode* textNode,
      string sourceText)
  {
    if (textNode == null || string.IsNullOrWhiteSpace(sourceText))
    {
      return;
    }

    if (this.nativeTalkTextNodeStateCaptured &&
        this.nativeTalkTextNodeStateCapturedForSourceText == sourceText)
    {
      return;
    }

    this.originalTalkTextFlags = textNode->TextFlags;
    this.originalTalkFontSize = textNode->FontSize;
    this.originalTalkTextWidth = textNode->GetWidth();
    this.nativeTalkTextNodeStateCaptured = true;
    this.nativeTalkTextNodeStateCapturedForSourceText = sourceText;
  }

  /// <summary>
  ///     Restores the original Talk text node presentation for the active line.
  /// </summary>
  /// <param name="nameNode">The Talk sender-name node.</param>
  /// <param name="textNode">The Talk message text node.</param>
  /// <returns>True when at least one node was restored.</returns>
  private unsafe bool TryRestoreOriginalTalkText(
      AtkTextNode* nameNode,
      AtkTextNode* textNode)
  {
    lock (this.stateGate)
    {
      if (!this.nativeTalkTextNodeStateCaptured ||
          !this.nativeTalkTextNodeStateDirty ||
          this.nativeTalkTextNodeStateCapturedForSourceText != this.currentOriginalText ||
          string.IsNullOrWhiteSpace(this.currentOriginalText))
      {
        return false;
      }

      var originalName = this.currentOriginalName;
      var originalText = this.currentOriginalText;
      var replacementName = this.currentReplacementName;
      var replacementText = this.currentReplacementText;
      var restoredAny = false;

      if (nameNode != null)
      {
        var nameNodeAddress = (nint)nameNode;
        restoredAny |= NativeMutationOwnership.TryRestore(
            this.ReadNodeText(nameNode),
            replacementName,
            originalName,
            restoredText => ((AtkTextNode*)nameNodeAddress)->SetText(
                restoredText));
      }

      if (textNode != null)
      {
        var textNodeAddress = (nint)textNode;
        restoredAny |= NativeMutationOwnership.TryRestore(
            this.ReadNodeText(textNode),
            replacementText,
            originalText,
            restoredText =>
            {
              var liveTextNode = (AtkTextNode*)textNodeAddress;
              liveTextNode->SetWidth((ushort)Math.Max(
                  0f,
                  this.originalTalkTextWidth));
              liveTextNode->TextFlags = this.originalTalkTextFlags;
              liveTextNode->FontSize = this.originalTalkFontSize;
              liveTextNode->SetText(restoredText);
            });
      }

      this.nativeTalkTextNodeStateDirty = false;
      return restoredAny;
    }
  }

  /// <summary>
  ///     Builds a lookup entity matching the historical Talk message schema already
  ///     used in the database.
  /// </summary>
  /// <param name="originalName">The original sender name.</param>
  /// <param name="originalText">The original Talk message text.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <returns>A formatted <see cref="TalkMessage" /> suitable for DB lookup.</returns>
  private TalkMessage BuildLookupMessage(
      string originalName,
      string originalText,
      SourceClientLanguage sourceLanguage,
      TranslationReuseScope? scope = null)
  {
    var operationScope = scope ?? this.CreateDialogueReuseScope(sourceLanguage);
    return new TalkMessage(
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
  ///     Applies translated values directly to Talk refresh arguments when a cached
  ///     translation is already available.
  /// </summary>
  /// <param name="atkValues">The refresh ATK values.</param>
  /// <param name="translatedName">The translated sender name.</param>
  /// <param name="translatedText">The translated Talk text.</param>
  private unsafe void ApplyTranslatedRefreshValues(
      AtkValue* atkValues,
      string translatedName,
      string translatedText)
  {
    if (!string.IsNullOrWhiteSpace(translatedText))
    {
      atkValues[0].SetManagedString(this.NormalizeForReplacement(translatedText));
    }

    if (this.ShouldTranslateTalkNpcNames() &&
        !string.IsNullOrWhiteSpace(translatedName))
    {
      atkValues[1].SetManagedString(this.NormalizeForReplacement(translatedName));
    }
  }

  /// <summary>
  ///     Normalizes translated text for native Talk replacement when the active
  ///     config requests diacritic stripping.
  /// </summary>
  /// <param name="text">The translated text to normalize.</param>
  /// <returns>The text that should be written back into the native Talk addon.</returns>
  private string NormalizeForReplacement(string text)
  {
    return this.config.RemoveDiacriticsWhenUsingReplacementTalkBTalk
        ? this.normalizeReplacementText(text)
        : text;
  }

  /// <summary>
  ///     Determines whether Talk sender names should participate in translation,
  ///     native replacement, and overlay title resolution.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when Talk sender names are enabled for the
  ///     current config; otherwise, <see langword="false" />.
  /// </returns>
  private bool ShouldTranslateTalkNpcNames()
  {
    return this.config.TranslateTalkNpcNames;
  }

  /// <summary>
  ///     Reads the current string value from a text node.
  /// </summary>
  /// <param name="textNode">The text node to inspect.</param>
  /// <returns>The visible node text, or an empty string when unavailable.</returns>
  private unsafe string ReadNodeText(AtkTextNode* textNode)
  {
    return textNode != null && !textNode->NodeText.IsEmpty
        ? MemoryHelper.ReadSeStringAsString(
            out _,
            (nint)textNode->NodeText.StringPtr.Value)
        : string.Empty;
  }

  /// <summary>
  ///     Determines whether the native Talk addon already shows the translated
  ///     replacement text for the active line.
  /// </summary>
  /// <param name="visibleName">The sender name currently visible in the addon.</param>
  /// <param name="visibleText">The Talk text currently visible in the addon.</param>
  /// <param name="replacementName">
  ///     The pre-normalized sender name that should be written to the native UI.
  /// </param>
  /// <param name="replacementText">
  ///     The pre-normalized Talk text that should be written to the native UI.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> when the visible native UI already matches the
  ///     translated replacement state; otherwise, <see langword="false" />.
  /// </returns>
  private bool IsNativeTalkAlreadyApplied(
      string visibleName,
      string visibleText,
      string replacementName,
      string replacementText)
  {
    if (string.IsNullOrWhiteSpace(replacementText))
    {
      return false;
    }

    var textMatches = visibleText == replacementText;
    var nameMatches = !this.ShouldTranslateTalkNpcNames() ||
                      string.IsNullOrWhiteSpace(replacementName) ||
                      visibleName == replacementName;

    return textMatches && nameMatches;
  }

  /// <summary>
  ///     Maps Talk refresh values back to the original source line when the addon
  ///     refresh arrives already carrying the translated text previously written
  ///     into the native nodes.
  /// </summary>
  /// <param name="capturedName">
  ///     The captured sender name, updated in place when a translated value is
  ///     recognized.
  /// </param>
  /// <param name="capturedText">
  ///     The captured Talk text, updated in place when a translated value is
  ///     recognized.
  /// </param>
  private void RemapTranslatedRefreshSourceToOriginal(
      ref string capturedName,
      ref string capturedText)
  {
    lock (this.stateGate)
    {
      if (string.IsNullOrWhiteSpace(this.currentOriginalText) ||
          string.IsNullOrWhiteSpace(this.currentTranslatedText))
      {
        return;
      }

      var normalizedTranslatedText = this.currentReplacementText;
      var textMatchesTranslatedOutput =
          capturedText == this.currentTranslatedText ||
          capturedText == normalizedTranslatedText;

      if (!textMatchesTranslatedOutput)
      {
        return;
      }

      var nameMatchesTranslatedOutput =
          !this.ShouldTranslateTalkNpcNames() ||
          string.IsNullOrWhiteSpace(this.currentTranslatedName) ||
          capturedName == this.currentTranslatedName ||
          capturedName == this.currentReplacementName;

      if (!nameMatchesTranslatedOutput)
      {
        return;
      }

      capturedName = this.currentOriginalName;
      capturedText = this.currentOriginalText;
    }
  }

  /// <summary>
  ///     Publishes translated Talk content into the shared overlay state.
  /// </summary>
  /// <param name="originalName">The original sender name.</param>
  /// <param name="originalText">The original Talk text.</param>
  /// <param name="translatedName">The translated sender name.</param>
  /// <param name="translatedText">The translated Talk text.</param>
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
  ///     Builds the normalized runtime session key used by Talk dialogue
  ///     session history.
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
  ///     Reads a string value from a Talk ATK value.
  /// </summary>
  /// <param name="atkValue">The ATK value to inspect.</param>
  /// <returns>The extracted text, or an empty string when unavailable.</returns>
  private unsafe string ReadTalkAtkString(AtkValue atkValue)
  {
    var stringPointer = (nint)atkValue.String.Value;
    return stringPointer != 0
        ? MemoryHelper.ReadSeStringAsString(out _, stringPointer)
        : string.Empty;
  }

  /// <summary>
  ///     Tries to capture the currently visible Talk source line, mapping any
  ///     translated native state back to the original source when possible.
  /// </summary>
  /// <param name="originalName">Receives the original visible sender name.</param>
  /// <param name="originalText">Receives the original visible Talk text.</param>
  /// <returns>
  ///     <see langword="true" /> when a visible Talk line could be resolved;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private unsafe bool TryCaptureCurrentTalkSource(
      out string originalName,
      out string originalText)
  {
    var addonPtr = GameGuiInterface.GetAddonByName(TalkAddonName);
    if (addonPtr.Address != IntPtr.Zero)
    {
      var talkAddon = (AtkUnitBase*)addonPtr.Address;
      if (talkAddon != null && talkAddon->IsVisible)
      {
        var nameNode = talkAddon->GetTextNodeById(NameNodeId);
        var textNode = talkAddon->GetTextNodeById(TextNodeId);
        originalName = this.ReadNodeText(nameNode);
        originalText = this.ReadNodeText(textNode);
        this.RemapTranslatedRefreshSourceToOriginal(
            ref originalName,
            ref originalText);
        if (!string.IsNullOrWhiteSpace(originalText))
        {
          return true;
        }
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
  ///     Resolves a Talk translation from cache or external translation and stores
  ///     the result as the current translation state.
  /// </summary>
  /// <param name="originalName">The original sender name.</param>
  /// <param name="originalText">The original Talk text.</param>
  /// <param name="requestId">The request identifier used to reject stale results.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <param name="sourceOperation">The source generation captured when queued.</param>
  /// <param name="operationToken">The token owned by the queued operation.</param>
  /// <returns>A task that completes when the translation state has been updated.</returns>
  internal async Task ResolveTranslationAsync(
      string originalName,
      string originalText,
      int requestId,
      SourceClientLanguage sourceLanguage,
      SourcePublicationOperation sourceOperation,
      CancellationToken operationToken)
  {
    try
    {
      operationToken.ThrowIfCancellationRequested();
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
      var foundTalkMessage = await this.findTalkMessageAsync(
          lookup,
          operationToken).ConfigureAwait(false);

      if (!this.sourceLifecycle.IsCurrent(sourceOperation) ||
          !this.IsCurrentRequest(requestId, sourceLanguage))
      {
        return;
      }

      string translatedName;
      string translatedText;
      VisibleStorySurfaceProvenanceKind provenance;
      bool usedRuntimeOnlyDialogueContext = false;
      var dialogueTranslationEngine = operationScope.TranslationEngine
                                      .GetValueOrDefault();

      if (foundTalkMessage != null)
      {
        translatedName = this.ShouldTranslateTalkNpcNames()
            ? foundTalkMessage.TranslatedSenderName ?? string.Empty
            : string.Empty;
        translatedText = foundTalkMessage.TranslatedTalkMessage ?? string.Empty;
        provenance = VisibleStorySurfaceProvenanceKind.DbReuse;
      }
      else
      {
        var interlocutorHints = await this.resolveInterlocutorHintsAsync(
            originalName,
            originalText,
            sourceLanguage,
            operationToken).ConfigureAwait(false);
        if (!this.sourceLifecycle.IsCurrent(sourceOperation) ||
            !this.IsCurrentRequest(requestId, sourceLanguage))
        {
          return;
        }

        var dialogueContext = DialogueTranslationSessionStore.BuildContext(
            TalkAddonName,
            this.BuildDialogueSessionKey(
                originalName,
                sourceLanguage,
                operationScope),
            originalName,
            originalText,
            DialogueSessionHistoryLimit,
            DialogueSessionTtl,
            interlocutorHints: interlocutorHints);
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
            operationToken,
            originContext: "Talk/Text").ConfigureAwait(false);

        if (!this.sourceLifecycle.IsCurrent(sourceOperation) ||
            !this.IsCurrentRequest(requestId, sourceLanguage))
        {
          return;
        }

        translatedName = this.ShouldTranslateTalkNpcNames() && !originalName.IsNullOrEmpty()
            ? await this.translationService.TranslateAsync(
                originalName,
                sourceLanguage,
                operationScope.TargetLanguageCode,
                TranslationSurfaceGroup.Dialogue,
                translatorResolution,
                operationToken,
                originContext: "Talk/Speaker").ConfigureAwait(false)
            : string.Empty;

        if (!this.sourceLifecycle.IsCurrent(sourceOperation) ||
            !this.IsCurrentRequest(requestId, sourceLanguage))
        {
          return;
        }

        var existingTranslatedTalkMessage = await this.findTalkMessageAsync(
            lookup,
            operationToken).ConfigureAwait(false);
        if (!this.sourceLifecycle.IsCurrent(sourceOperation) ||
            !this.IsCurrentRequest(requestId, sourceLanguage))
        {
          return;
        }

        if (!string.IsNullOrWhiteSpace(
                existingTranslatedTalkMessage?.TranslatedTalkMessage))
        {
          translatedName = this.ShouldTranslateTalkNpcNames()
              ? existingTranslatedTalkMessage.TranslatedSenderName ?? string.Empty
              : string.Empty;
          translatedText =
              existingTranslatedTalkMessage.TranslatedTalkMessage ?? string.Empty;
          provenance = VisibleStorySurfaceProvenanceKind.DbReuse;
        }
        else if (!usesRuntimeOnlyDialogueContext &&
                 this.sourceLifecycle.IsCurrent(sourceOperation))
        {
          var translatedTalkData = new TalkMessage(
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

          await this.insertTalkMessageAsync(
              translatedTalkData,
              operationToken).ConfigureAwait(false);
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

      if (!this.sourceLifecycle.IsCurrent(sourceOperation))
      {
        return;
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
    catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
    {
      lock (this.stateGate)
      {
        if (requestId == this.activeRequestId)
        {
          this.translationInFlight = false;
        }
      }
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

      PluginRuntimeLog.Error($"[{TalkAddonName}] Error resolving Talk translation: {ex}");
    }
  }

  /// <summary>
  ///     Records the latest visible Talk provenance snapshot for the debugger.
  /// </summary>
  /// <param name="provenance">The provenance label kind to expose.</param>
  /// <param name="originalName">The original speaker name.</param>
  /// <param name="originalText">The original Talk text.</param>
  /// <param name="translatedName">The translated speaker name.</param>
  /// <param name="translatedText">The translated Talk text.</param>
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
            VisibleStorySurfaceKind.Talk,
            provenance,
            VisibleStorySurfaceTableMap.Resolve(VisibleStorySurfaceKind.Talk),
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
  ///     Resolves the effective dialogue translation engine identifier.
  /// </summary>
  /// <returns>The effective dialogue translation engine identifier.</returns>
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
  ///     Determines whether native Talk text should be replaced instead of leaving
  ///     the original addon text untouched.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when the Talk addon should receive translated
  ///     native text; otherwise, <see langword="false" />.
  /// </returns>
  private bool ShouldApplyNativeTalkText()
  {
    return TranslationDisplayModeHelper.WritesNativeTranslation(
        this.config.TalkTranslationDisplayMode,
        this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Determines whether the Talk overlay should show the original line while
  ///     the native addon receives the translation.
  /// </summary>
  /// <returns>
  ///     <see langword="true" /> when Talk swap mode is active; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool ShouldSwapTexts()
  {
    return TranslationDisplayModeHelper.ShowsOriginalOverlayText(
        this.config.TalkTranslationDisplayMode,
        this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Gets a value indicating whether the current Talk line has unresolved
  ///     background translation work.
  /// </summary>
  internal bool IsTranslationInFlight
  {
    get
    {
      lock (this.stateGate)
      {
        return this.translationInFlight;
      }
    }
  }

  /// <summary>
  ///     Returns the active resolved Talk translation currently held by the
  ///     handler state.
  /// </summary>
  /// <param name="translatedName">Receives the translated sender name.</param>
  /// <param name="translatedText">Receives the translated Talk text.</param>
  /// <param name="replacementName">
  ///     Receives the sender name already normalized for native replacement.
  /// </param>
  /// <param name="replacementText">
  ///     Receives the Talk text already normalized for native replacement.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> when the handler already has a translated Talk
  ///     line ready for native replacement; otherwise, <see langword="false" />.
  /// </returns>
  internal bool TryGetCurrentResolvedTranslation(
      SourceClientLanguage sourceLanguage,
      out string translatedName,
      out string translatedText,
      out string replacementName,
      out string replacementText)
  {
    lock (this.stateGate)
    {
      if (NativeRuntimeSourceScope.MatchesSource(
              this.currentSourceLanguageCode,
              sourceLanguage) &&
          !string.IsNullOrWhiteSpace(this.currentTranslatedText))
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
  ///     Returns the active cached translation when it still matches the supplied
  ///     source Talk content.
  /// </summary>
  /// <param name="originalName">The source sender name.</param>
  /// <param name="originalText">The source Talk text.</param>
  /// <param name="translatedName">Receives the translated sender name.</param>
  /// <param name="translatedText">Receives the translated Talk text.</param>
  /// <returns>
  ///     <see langword="true" /> when a matching cached translation exists;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryGetCachedTranslation(
      string originalName,
      string originalText,
      SourceClientLanguage sourceLanguage,
      out string translatedName,
      out string translatedText)
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
        return true;
      }
    }

    translatedName = string.Empty;
    translatedText = string.Empty;
    return false;
  }

  /// <summary>
  ///     Captures one Talk line and starts its handler-owned asynchronous
  ///     resolution without waiting for persistent lookup to complete.
  /// </summary>
  /// <param name="originalName">The source sender name.</param>
  /// <param name="originalText">The source Talk text.</param>
  /// <param name="sourceLanguage">The resolved source language.</param>
  /// <returns>
  ///     <see langword="true" /> when the operation was queued; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  internal bool TryQueueTranslation(
      string originalName,
      string originalText,
      SourceClientLanguage sourceLanguage)
  {
    if (!this.TryQueueTranslation(
            originalName,
            originalText,
            sourceLanguage,
            out var requestId,
            out var sourceOperation))
    {
      return false;
    }

    this.clearOverlay();
    var accepted = this.ownedOperations.Run(
        operationToken => this.ResolveTranslationAsync(
            originalName,
            originalText,
            requestId,
            sourceLanguage,
            sourceOperation,
            operationToken),
        sourceOperation.CancellationToken);
    if (!accepted)
    {
      lock (this.stateGate)
      {
        if (requestId == this.activeRequestId)
        {
          this.translationInFlight = false;
        }
      }
    }

    return accepted;
  }

  /// <summary>
  ///     Starts a new translation request when the Talk source changes or when the
  ///     current source still has no cached translation available.
  /// </summary>
  /// <param name="originalName">The source sender name.</param>
  /// <param name="originalText">The source Talk text.</param>
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
  internal bool TryQueueTranslation(
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

      if (!sourceChanged && (this.translationInFlight || hasTranslation))
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
      this.translationInFlight = true;
      this.activeRequestId++;
      requestId = this.activeRequestId;
      sourceOperation = this.sourceLifecycle.Capture(
          this.CreateDialogueReuseScope(sourceLanguage));
      return true;
    }
  }

  /// <summary>
  ///     Determines whether one request still owns the active Talk managed
  ///     state for the supplied source language.
  /// </summary>
  /// <param name="requestId">The captured request identifier.</param>
  /// <param name="sourceLanguage">The captured source language.</param>
  /// <returns>
  ///     <see langword="true" /> when the request remains current; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  private bool IsCurrentRequest(
      int requestId,
      SourceClientLanguage sourceLanguage)
  {
    lock (this.stateGate)
    {
      return requestId == this.activeRequestId &&
             NativeRuntimeSourceScope.MatchesSource(
                 this.currentSourceLanguageCode,
                 sourceLanguage);
    }
  }
}


