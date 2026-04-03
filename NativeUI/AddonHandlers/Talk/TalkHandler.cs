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
  private string currentTranslatedName = string.Empty;
  private string currentTranslatedText = string.Empty;
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
    this.RegisterHandler(AddonEvent.PreDraw, this.OnPreDraw);
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

    if (string.IsNullOrWhiteSpace(originalText))
    {
      return;
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
  ///     Applies translated Talk text to the visible addon when replacement mode is
  ///     enabled and an async translation becomes available after refresh.
  /// </summary>
  /// <param name="type">The lifecycle event that triggered the handler.</param>
  /// <param name="args">The addon arguments associated with the draw.</param>
  private unsafe void OnPreDraw(AddonEvent type, AddonArgs args)
  {
    if (args.AddonName != TalkAddonName || !this.ShouldApplyNativeTalkText())
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

    var nameNode = talkAddon->GetTextNodeById(2);
    var textNode = talkAddon->GetTextNodeById(3);
    var parentNode = talkAddon->GetNodeById(10);

    if (textNode == null || textNode->NodeText.IsEmpty || parentNode == null)
    {
      return;
    }

    var originalName = nameNode != null && !nameNode->NodeText.IsEmpty
        ? MemoryHelper.ReadSeStringAsString(
            out _,
            (nint)nameNode->NodeText.StringPtr.Value)
        : string.Empty;
    var originalText = MemoryHelper.ReadSeStringAsString(
        out _,
        (nint)textNode->NodeText.StringPtr.Value);

    if (!this.TryGetCachedTranslation(
            originalName,
            originalText,
            out var translatedName,
            out var translatedText))
    {
      return;
    }

    if (this.config.TranslateNpcNames &&
        nameNode != null &&
        !string.IsNullOrWhiteSpace(translatedName))
    {
      nameNode->SetText(this.NormalizeForReplacement(translatedName));
    }

    textNode->TextFlags = TextFlags.WordWrap
                          | TextFlags.MultiLine
                          | TextFlags.AutoAdjustNodeSize;
    textNode->FontSize = (byte)(translatedText.Length >= 350
        ? 11
        : translatedText.Length >= 256 ? 12 : 14);
    textNode->SetWidth(parentNode->GetWidth());
    textNode->SetText(this.NormalizeForReplacement(translatedText));
    textNode->ResizeNodeForCurrentText();
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

    if (this.config.TranslateNpcNames &&
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
    var overlayName = this.config.SwapTextsUsingImGui
        ? originalName
        : this.config.TranslateNpcNames ? translatedName : string.Empty;
    var overlayText = this.config.SwapTextsUsingImGui
        ? originalText
        : translatedText;

    this.updateOverlay(
        overlayName,
        overlayText,
        this.config.TranslateNpcNames ? originalName : string.Empty);
  }

  /// <summary>
  ///     Reads a string value from a Talk ATK value.
  /// </summary>
  /// <param name="atkValue">The ATK value to inspect.</param>
  /// <returns>The extracted text, or an empty string when unavailable.</returns>
  private unsafe string ReadTalkAtkString(AtkValue atkValue)
  {
    return atkValue.String != null
        ? MemoryHelper.ReadSeStringAsString(out _, (nint)atkValue.String.Value)
        : string.Empty;
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
        translatedName = this.config.TranslateNpcNames
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

        translatedName = this.config.TranslateNpcNames && !originalName.IsNullOrEmpty()
            ? await this.translationService.TranslateAsync(
                originalName,
                ClientStateInterface.ClientLanguage.Humanize(),
                LangDict[LanguageInt].Code)
            : string.Empty;

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

      lock (this.stateGate)
      {
        if (requestId != this.activeRequestId)
        {
          return;
        }

        this.translationInFlight = false;
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

      PluginLog.Error($"[{TalkAddonName}] Error resolving Talk translation: {ex}");
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
    return !this.config.UseImGuiForTalk || this.config.SwapTextsUsingImGui;
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
      this.currentTranslatedName = string.Empty;
      this.currentTranslatedText = string.Empty;
      this.translationInFlight = true;
      this.activeRequestId++;
      requestId = this.activeRequestId;
      return true;
    }
  }
}
