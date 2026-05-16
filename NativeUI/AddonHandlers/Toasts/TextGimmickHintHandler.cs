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
internal sealed class TextGimmickHintHandler : IAddonTranslationHandler
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

    if (this.TryGetCachedTranslation(
            originalText,
            out var translatedText,
            out _))
    {
      // PluginRuntimeLog.Debug(
      //     $"[{TextGimmickHintAddonName}] trigger={type} cache-hit -> overlay publish");
      this.SetResolvedState(
          originalText,
          translatedText,
          this.NormalizeForReplacement(translatedText));
      this.PublishOverlay(originalText, translatedText, type.ToString());
      return;
    }

    var lookupToast = this.BuildLookupMessage(originalText);
    var storedToast = this.findTextGimmickHintMessage(lookupToast);
    if (this.IsStoredTranslationUsable(storedToast, originalText))
    {
      // PluginRuntimeLog.Debug(
      //     $"[{TextGimmickHintAddonName}] trigger={type} db-hit -> overlay publish");
      this.SetResolvedState(
          originalText,
          storedToast!.TranslatedText!,
          this.NormalizeForReplacement(storedToast.TranslatedText!));
      this.PublishOverlay(
          originalText,
          storedToast.TranslatedText!,
          type.ToString());
      return;
    }

    if (this.TryQueueTranslation(originalText, out var requestId))
    {
      // PluginRuntimeLog.Debug(
      //     $"[{TextGimmickHintAddonName}] trigger={type} cache-miss -> queued translation request #{requestId}");
      this.PublishOverlay(originalText, string.Empty, type.ToString());
      Task.Run(() => this.ResolveTranslationAsync(originalText, requestId));
    }
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
    this.updateOverlayBounds(addon, textNode);
    // PluginRuntimeLog.Debug(
    //     $"[{TextGimmickHintAddonName}] trigger={type} visible-update " +
    //     $"overlay={this.ShouldUseOverlay()} native={this.ShouldApplyNativeText()} " +
    //     $"swap={this.ShouldSwapTexts()}");

    if (!this.TryGetCurrentResolvedTranslation(
            out var resolvedOriginalText,
            out var translatedText,
            out var replacementText))
    {
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
    //     $"[{TextGimmickHintAddonName}] trigger={type} applying native replacement");
    NativeTextNodeLayoutHelper.ApplyTextReplacementWithInferredReflow(
        addon,
        textNode,
        replacementText);
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
    lock (this.stateGate)
    {
      this.activeRequestId++;
      this.currentOriginalText = string.Empty;
      this.currentTranslatedText = string.Empty;
      this.currentReplacementText = string.Empty;
      this.translationInFlight = false;
      this.lastFailedOriginalText = string.Empty;
    }

    this.clearOverlay();
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
    // PluginRuntimeLog.Debug(
    //     $"[{TextGimmickHintAddonName}] trigger=async-resolve translation ready for source='{originalText}'");
    var translatedGimmickHint = new TextGimmickHintMessage(
        originalText,
        ClientStateInterface.ClientLanguage.Humanize(),
        translatedText,
        LangDict[LanguageInt].Code,
        this.config.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now);

    await this.insertTextGimmickHintMessageAsync(translatedGimmickHint);

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
    return true;
  }

  /// <summary>
  ///     Determines whether a stored gimmick-hint row still represents a usable
  ///     translation for the current source line.
  /// </summary>
  /// <param name="textGimmickHintMessage">The stored gimmick-hint row to validate.</param>
  /// <param name="originalText">The expected original gimmick-hint text.</param>
  /// <returns>
  ///     <see langword="true" /> when the stored row contains a non-empty
  ///     translation for the same source line; otherwise, <see langword="false" />.
  /// </returns>
  private bool IsStoredTranslationUsable(
      TextGimmickHintMessage? textGimmickHintMessage,
      string originalText)
  {
    return textGimmickHintMessage != null &&
           string.Equals(
               textGimmickHintMessage.OriginalText,
               originalText,
               StringComparison.Ordinal) &&
           !string.IsNullOrWhiteSpace(textGimmickHintMessage.TranslatedText);
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

    originalText = visibleText;
    return true;
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


