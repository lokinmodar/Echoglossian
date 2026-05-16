// <copyright file="TalkHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Talk;

/// <summary>
///     Handles the full Talk addon runtime inside the new addon-handler model.
///     This includes capture, translation lookup, async translation, overlay
///     updates, and optional native text replacement.
/// </summary>
public sealed class TalkHandler : IAddonTranslationHandler
{
  private const string TalkAddonName = "Talk";
  private const int NameNodeId = 2;
  private const int TextNodeId = 3;
  private const int ParentNodeId = 10;

  private readonly Action clearOverlay;
  private readonly Config config;
  private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>> eventHandlers = new();
  private readonly Func<TalkMessage, TalkMessage?> findTalkMessage;
  private readonly Func<TalkMessage, Task<string>> insertTalkMessageAsync;
  private readonly Func<string, string> normalizeReplacementText;
  private readonly object stateGate = new();
  private readonly TranslationService translationService;
  private readonly Action<string, string, string> updateOverlay;

  private int activeRequestId;
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
  /// <param name="findTalkMessage">
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
  public TalkHandler(
      Config config,
      TranslationService translationService,
      Func<TalkMessage, TalkMessage?> findTalkMessage,
      Func<TalkMessage, Task<string>> insertTalkMessageAsync,
      Action<string, string, string> updateOverlay,
      Action clearOverlay,
      Func<string, string> normalizeReplacementText)
  {
    this.config = config;
    this.translationService = translationService;
    this.findTalkMessage = findTalkMessage;
    this.insertTalkMessageAsync = insertTalkMessageAsync;
    this.updateOverlay = updateOverlay;
    this.clearOverlay = clearOverlay;
    this.normalizeReplacementText = normalizeReplacementText;

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

    if (this.TryLoadStoredTranslation(
            originalName,
            originalText,
            out var storedTranslatedName,
            out var storedTranslatedText))
    {
      this.PublishOverlay(
          originalName,
          originalText,
          storedTranslatedName,
          storedTranslatedText);

      if (this.ShouldApplyNativeTalkText())
      {
        this.ApplyTranslatedRefreshValues(
            atkValues,
            storedTranslatedName,
            storedTranslatedText);
      }

      return;
    }

    if (this.TryQueueTranslation(originalName, originalText, out var requestId))
    {
      this.clearOverlay();
      Task.Run(() => this.ResolveTranslationAsync(
          originalName,
          originalText,
          requestId));
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
      this.currentOriginalName = string.Empty;
      this.currentOriginalText = string.Empty;
      this.currentReplacementName = string.Empty;
      this.currentReplacementText = string.Empty;
      this.currentTranslatedName = string.Empty;
      this.currentTranslatedText = string.Empty;
      this.translationInFlight = false;
    }

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

      if (nameNode != null && this.ReadNodeText(nameNode) != originalName)
      {
        nameNode->SetText(originalName);
      }

      if (textNode != null && this.ReadNodeText(textNode) != originalText)
      {
        textNode->SetWidth((ushort)Math.Max(0f, this.originalTalkTextWidth));
        textNode->TextFlags = this.originalTalkTextFlags;
        textNode->FontSize = this.originalTalkFontSize;
        textNode->SetText(originalText);
      }

      this.nativeTalkTextNodeStateDirty = false;
      return true;
    }
  }

  /// <summary>
  ///     Builds a lookup entity matching the historical Talk message schema already
  ///     used in the database.
  /// </summary>
  /// <param name="originalName">The original sender name.</param>
  /// <param name="originalText">The original Talk message text.</param>
  /// <returns>A formatted <see cref="TalkMessage" /> suitable for DB lookup.</returns>
  private TalkMessage BuildLookupMessage(
      string originalName,
      string originalText)
  {
    return new TalkMessage(
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
  ///     Tries to load an existing Talk translation synchronously from the
  ///     database so saved lines can still swap immediately during refresh.
  /// </summary>
  /// <param name="originalName">The original sender name.</param>
  /// <param name="originalText">The original Talk text.</param>
  /// <param name="translatedName">Receives the translated sender name.</param>
  /// <param name="translatedText">Receives the translated Talk text.</param>
  /// <returns>
  ///     <see langword="true" /> when a stored translation exists for the current
  ///     Talk line; otherwise, <see langword="false" />.
  /// </returns>
  private bool TryLoadStoredTranslation(
      string originalName,
      string originalText,
      out string translatedName,
      out string translatedText)
  {
    var foundTalkMessage = this.findTalkMessage(
        this.BuildLookupMessage(originalName, originalText));
    if (foundTalkMessage == null)
    {
      translatedName = string.Empty;
      translatedText = string.Empty;
      return false;
    }

    translatedName = this.ShouldTranslateTalkNpcNames()
        ? foundTalkMessage.TranslatedSenderName ?? string.Empty
        : string.Empty;
    translatedText = foundTalkMessage.TranslatedTalkMessage ?? string.Empty;

    lock (this.stateGate)
    {
      this.activeRequestId++;
      this.currentOriginalName = originalName;
      this.currentOriginalText = originalText;
      this.currentReplacementName = this.NormalizeForReplacement(translatedName);
      this.currentReplacementText = this.NormalizeForReplacement(translatedText);
      this.currentTranslatedName = translatedName;
      this.currentTranslatedText = translatedText;
      this.translationInFlight = false;
    }

    return !string.IsNullOrWhiteSpace(translatedText);
  }

  /// <summary>
  ///     Resolves a Talk translation from cache or external translation and stores
  ///     the result as the current translation state.
  /// </summary>
  /// <param name="originalName">The original sender name.</param>
  /// <param name="originalText">The original Talk text.</param>
  /// <param name="requestId">The request identifier used to reject stale results.</param>
  /// <returns>A task that completes when the translation state has been updated.</returns>
  private async Task ResolveTranslationAsync(
      string originalName,
      string originalText,
      int requestId)
  {
    try
    {
      var lookup = this.BuildLookupMessage(originalName, originalText);
      var foundTalkMessage = this.findTalkMessage(lookup);

      string translatedName;
      string translatedText;

      if (foundTalkMessage != null)
      {
        translatedName = this.ShouldTranslateTalkNpcNames()
            ? foundTalkMessage.TranslatedSenderName ?? string.Empty
            : string.Empty;
        translatedText = foundTalkMessage.TranslatedTalkMessage ?? string.Empty;
      }
      else
      {
        translatedText = await this.translationService.TranslateAsync(
            originalText,
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code);

        translatedName = this.ShouldTranslateTalkNpcNames() && !originalName.IsNullOrEmpty()
            ? await this.translationService.TranslateAsync(
                originalName,
                ClientStateInterface.ClientLanguage.Humanize(),
                LangDict[LanguageInt].Code)
            : string.Empty;

        var existingTranslatedTalkMessage = this.findTalkMessage(lookup);
        if (!string.IsNullOrWhiteSpace(
                existingTranslatedTalkMessage?.TranslatedTalkMessage))
        {
          translatedName = this.ShouldTranslateTalkNpcNames()
              ? existingTranslatedTalkMessage.TranslatedSenderName ?? string.Empty
              : string.Empty;
          translatedText =
              existingTranslatedTalkMessage.TranslatedTalkMessage ?? string.Empty;
        }
        else
        {
          var translatedTalkData = new TalkMessage(
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

          await this.insertTalkMessageAsync(translatedTalkData);
        }
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
        }
      }

      PluginRuntimeLog.Error($"[{TalkAddonName}] Error resolving Talk translation: {ex}");
    }
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
  private bool TryGetCurrentResolvedTranslation(
      out string translatedName,
      out string translatedText,
      out string replacementName,
      out string replacementText)
  {
    lock (this.stateGate)
    {
      if (!string.IsNullOrWhiteSpace(this.currentTranslatedText))
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
      out string translatedName,
      out string translatedText)
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
        return true;
      }
    }

    translatedName = string.Empty;
    translatedText = string.Empty;
    return false;
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

      if (!sourceChanged && (this.translationInFlight || hasTranslation))
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
      this.translationInFlight = true;
      this.activeRequestId++;
      requestId = this.activeRequestId;
      return true;
    }
  }
}


