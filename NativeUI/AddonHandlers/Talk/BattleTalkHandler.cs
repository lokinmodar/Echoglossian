// <copyright file="BattleTalkHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Talk;

/// <summary>
///     Handles the full BattleTalk runtime inside the new addon-handler model.
///     This includes live text capture from the visible addon, translation lookup,
///     async translation, overlay updates, and optional native text replacement.
/// </summary>
public sealed class BattleTalkHandler : IAddonTranslationHandler
{
  private const string BattleTalkAddonName = "_BattleTalk";
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
  private readonly Func<BattleTalkMessage, Task<string>> insertBattleTalkMessageAsync;
  private readonly Func<string, string> normalizeReplacementText;
  private readonly object stateGate = new();
  private readonly TranslationService translationService;
  private readonly Action<string, string, string> updateOverlay;

  private int activeRequestId;
  private int hideResetGeneration;
  private string currentOriginalName = string.Empty;
  private string currentOriginalText = string.Empty;
  private string currentReplacementName = string.Empty;
  private string currentReplacementText = string.Empty;
  private string currentTranslatedName = string.Empty;
  private string currentTranslatedText = string.Empty;
  private string failedOriginalName = string.Empty;
  private string failedOriginalText = string.Empty;
  private string lastResolvedOriginalName = string.Empty;
  private string lastResolvedOriginalText = string.Empty;
  private string lastResolvedReplacementName = string.Empty;
  private string lastResolvedReplacementText = string.Empty;
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
        this.ApplyTranslatedNodes(
            battleTalkAddon,
            translatedName,
            replacementName,
            replacementText);
      }
    }
    else
    {
      this.ShowPendingSwapOverlayIfNeeded(originalName, originalText);
    }

    if (this.TryQueueTranslation(originalName, originalText, out var requestId))
    {
      Task.Run(() => this.ResolveTranslationAsync(
          originalName,
          originalText,
          requestId));
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

    lock (this.stateGate)
    {
      this.hideResetGeneration++;
      this.ResetStateLocked();
    }

    this.clearOverlay();
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
    this.currentOriginalName = string.Empty;
    this.currentOriginalText = string.Empty;
    this.currentReplacementName = string.Empty;
    this.currentReplacementText = string.Empty;
    this.currentTranslatedName = string.Empty;
    this.currentTranslatedText = string.Empty;
    this.failedOriginalName = string.Empty;
    this.failedOriginalText = string.Empty;
    this.translationInFlight = false;
  }

  /// <summary>
  ///     Builds a lookup entity matching the historical BattleTalk message schema
  ///     already used in the database.
  /// </summary>
  /// <param name="originalName">The original sender name.</param>
  /// <param name="originalText">The original BattleTalk text.</param>
  /// <returns>
  ///     A formatted <see cref="BattleTalkMessage" /> suitable for DB lookup.
  /// </returns>
  private BattleTalkMessage BuildLookupMessage(
      string originalName,
      string originalText)
  {
    return new BattleTalkMessage(
        originalName,
        originalText,
        ClientStateInterface.ClientLanguage.Humanize(),
        ClientStateInterface.ClientLanguage.Humanize(),
        string.Empty,
        string.Empty,
        LangDict[LanguageInt].Code,
        this.config.ChosenTransEngine,
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
  ///     Resolves a BattleTalk translation from cache or external translation and
  ///     stores the result as the current translation state.
  /// </summary>
  /// <param name="originalName">The original sender name.</param>
  /// <param name="originalText">The original BattleTalk text.</param>
  /// <param name="requestId">
  ///     The request identifier used to reject stale results.
  /// </param>
  /// <returns>A task that completes when the translation state has been updated.</returns>
  private async Task ResolveTranslationAsync(
      string originalName,
      string originalText,
      int requestId)
  {
    try
    {
      var lookup = this.BuildLookupMessage(originalName, originalText);
      var foundBattleTalkMessage = this.findBattleTalkMessage(lookup);

      string translatedName;
      string translatedText;

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
      }
      else
      {
        translatedText = await this.translationService.TranslateAsync(
            originalText,
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code);

        translatedName = string.Empty;
        if (this.ShouldTranslateBattleTalkNpcNames() && !originalName.IsNullOrEmpty())
        {
          try
          {
            translatedName = await this.translationService.TranslateAsync(
                originalName,
                ClientStateInterface.ClientLanguage.Humanize(),
                LangDict[LanguageInt].Code);
          }
          catch (Exception ex)
          {
            PluginRuntimeLog.Warning(
                $"[{BattleTalkAddonName}] Speaker name translation failed; continuing with translated text. {ex.Message}");
          }
        }

        var translatedBattleTalkData = new BattleTalkMessage(
            originalName,
            originalText,
            ClientStateInterface.ClientLanguage.Humanize(),
            ClientStateInterface.ClientLanguage.Humanize(),
            translatedName,
            translatedText,
            LangDict[LanguageInt].Code,
            this.config.ChosenTransEngine,
            rtlLangTranslationImageData: null,
            DateTime.Now,
            DateTime.Now);

        await this.insertBattleTalkMessageAsync(translatedBattleTalkData);
      }

      lock (this.stateGate)
      {
        if (requestId != this.activeRequestId)
        {
          return;
        }

        this.translationInFlight = false;
        this.currentReplacementName = this.NormalizeForReplacement(translatedName);
        this.currentReplacementText = this.NormalizeForReplacement(translatedText);
        this.currentTranslatedName = translatedName;
        this.currentTranslatedText = translatedText;
        if (string.IsNullOrWhiteSpace(translatedText))
        {
          this.failedOriginalName = originalName;
          this.failedOriginalText = originalText;
        }
        else
        {
          this.failedOriginalName = string.Empty;
          this.failedOriginalText = string.Empty;
        }
        this.lastResolvedOriginalName = originalName;
        this.lastResolvedOriginalText = originalText;
        this.lastResolvedReplacementName = this.currentReplacementName;
        this.lastResolvedReplacementText = this.currentReplacementText;
      }

      if (!string.IsNullOrWhiteSpace(translatedText))
      {
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
      out string translatedName,
      out string translatedText,
      out string replacementName,
      out string replacementText)
  {
    lock (this.stateGate)
    {
      var matchesCurrentSource =
          this.currentOriginalName == originalName &&
          this.currentOriginalText == originalText;
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
  /// <returns>
  ///     <see langword="true" /> when a new translation task should be queued;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  private bool TryQueueTranslation(
      string originalName,
      string originalText,
      out int requestId)
  {
    lock (this.stateGate)
    {
      var sourceChanged =
          this.currentOriginalName != originalName ||
          this.currentOriginalText != originalText;
      var hasTranslation = !string.IsNullOrWhiteSpace(this.currentTranslatedText);
      var isKnownFailedSource =
          this.failedOriginalName == originalName &&
          this.failedOriginalText == originalText;

      if (!sourceChanged && (this.translationInFlight || hasTranslation))
      {
        requestId = this.activeRequestId;
        return false;
      }

      if (!sourceChanged && isKnownFailedSource)
      {
        requestId = this.activeRequestId;
        return false;
      }

      this.currentOriginalName = originalName;
      this.currentOriginalText = originalText;
      this.currentReplacementName = string.Empty;
      this.currentReplacementText = string.Empty;
      this.currentTranslatedName = string.Empty;
      this.currentTranslatedText = string.Empty;
      this.failedOriginalName = string.Empty;
      this.failedOriginalText = string.Empty;
      this.translationInFlight = true;
      this.activeRequestId++;
      requestId = this.activeRequestId;
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

    if (this.TryQueueTranslation(originalName, originalText, out var requestId))
    {
      Task.Run(() => this.ResolveTranslationAsync(
          originalName,
          originalText,
          requestId));
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

      if (this.TryMapVisibleSourceToOriginal(
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

        if (this.TryMapVisibleSourceToOriginal(
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
        visibleText == replacementText &&
        (!this.ShouldTranslateBattleTalkNpcNames() ||
         string.IsNullOrWhiteSpace(replacementName) ||
         visibleName == replacementName))
    {
      return;
    }

    if (this.ShouldTranslateBattleTalkNpcNames() &&
        nameNode != null &&
        !string.IsNullOrWhiteSpace(translatedName) &&
        visibleName != replacementName)
    {
      nameNode->SetText(replacementName);
    }

    var backgroundNode = nineGridNode != null ? (AtkResNode*)nineGridNode : null;
    var layoutSnapshot = NativeTextNodeLayoutHelper.CaptureLayoutSnapshot(
        textNode,
        parentNode,
        backgroundNode,
        timerNode);
    var preferredWrapWidth = NativeTextNodeLayoutHelper.ResolvePreferredWrapWidth(
        textNode,
        parentNode);
    var resizeResult = NativeTextNodeLayoutHelper.ApplyWrappedTextAndMeasure(
        textNode,
        replacementText,
        preferredWrapWidth);
    NativeTextNodeLayoutHelper.ResizeFromSnapshot(
        layoutSnapshot,
        resizeResult,
        parentNode,
        backgroundNode,
        timerNode);
  }
}


