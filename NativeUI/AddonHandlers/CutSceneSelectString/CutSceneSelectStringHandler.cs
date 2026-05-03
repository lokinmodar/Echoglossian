// <copyright file="CutSceneSelectStringHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.CutSceneSelectString;

/// <summary>
///     Synchronizes the overlay bounds for the visible CutSceneSelectString addon.
/// </summary>
/// <param name="addon">The visible addon instance.</param>
public unsafe delegate void SyncCutSceneSelectStringOverlayBoundsDelegate(
    AtkUnitBase* addon);

/// <summary>
///     Handles the CutSceneSelectString addon runtime inside the new addon-handler model.
///     The first readable text node becomes the question/title and the remaining
///     text nodes become the selectable options.
/// </summary>
public sealed class CutSceneSelectStringHandler : IAddonTranslationHandler
{
  private const string AddonName = "CutSceneSelectString";

  private readonly Action clearOverlay;
  private readonly Config config;
  private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>> eventHandlers = new();
  private readonly Func<SelectString, SelectString?> findCutSceneSelectStringMessage;
  private readonly Func<SelectString, Task<string>> insertCutSceneSelectStringMessageAsync;
  private readonly Func<string, string> normalizeReplacementText;
  private readonly object stateGate = new();
  private readonly Action<string, string, string> updateOverlay;
  private readonly TranslationService translationService;
  private readonly SyncCutSceneSelectStringOverlayBoundsDelegate syncOverlayBounds;

  private DialogState state = new();

  /// <summary>
  ///     In-memory state for the currently visible CutSceneSelectString dialog.
  /// </summary>
  private sealed class DialogState
  {
    public int ActiveRequestId;

    public string CurrentOriginalQuestion { get; set; } = string.Empty;

    public List<string> CurrentOriginalOptions { get; set; } = [];

    public string CurrentReplacementQuestion { get; set; } = string.Empty;

    public List<string> CurrentReplacementOptions { get; set; } = [];

    public string CurrentTranslatedQuestion { get; set; } = string.Empty;

    public List<string> CurrentTranslatedOptions { get; set; } = [];

    public string LastFailedSourceKey { get; set; } = string.Empty;

    public bool TranslationInFlight { get; set; }
  }

  /// <summary>
  ///     Initializes a new instance of the <see cref="CutSceneSelectStringHandler" /> class.
  /// </summary>
  public CutSceneSelectStringHandler(
      Config config,
      TranslationService translationService,
      Func<SelectString, SelectString?> findCutSceneSelectStringMessage,
      Func<SelectString, Task<string>> insertCutSceneSelectStringMessageAsync,
      Action<string, string, string> updateOverlay,
      Action clearOverlay,
      SyncCutSceneSelectStringOverlayBoundsDelegate syncOverlayBounds,
      Func<string, string> normalizeReplacementText)
  {
    this.config = config;
    this.translationService = translationService;
    this.findCutSceneSelectStringMessage = findCutSceneSelectStringMessage;
    this.insertCutSceneSelectStringMessageAsync = insertCutSceneSelectStringMessageAsync;
    this.updateOverlay = updateOverlay;
    this.clearOverlay = clearOverlay;
    this.syncOverlayBounds = syncOverlayBounds;
    this.normalizeReplacementText = normalizeReplacementText;

    this.Trace(
        $"handler initialized translate={this.config.TranslateCutSceneSelectString} " +
        $"overlay={this.ShouldUseOverlay()} swap={this.ShouldSwapTexts()}");

    this.RegisterHandler(AddonEvent.PreSetup, this.OnCaptureDialog);
    this.RegisterHandler(AddonEvent.PreRefresh, this.OnCaptureDialog);
    this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.OnCaptureDialog);
    this.RegisterHandler(AddonEvent.PostUpdate, this.OnUpdateVisibleAddon);
    this.RegisterHandler(AddonEvent.PreDraw, this.OnUpdateVisibleAddon);
    this.RegisterHandler(AddonEvent.PreHide, this.OnResetState);
    this.RegisterHandler(AddonEvent.PreFinalize, this.OnResetState);
  }

  /// <summary>
  ///     Returns the event handlers required to drive the CutSceneSelectString addon flow.
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
  ///     Captures CutSceneSelectString source text early in the lifecycle so a
  ///     translation can be queued before the first draw pass completes.
  /// </summary>
  private unsafe void OnCaptureDialog(AddonEvent type, AddonArgs args)
  {
    if (!this.ShouldHandleCutSceneSelectString(args, out var addon))
    {
      return;
    }

    var textNodes = CutSceneSelectStringNodeResolvers.ResolveReadableTextNodes(addon);
    if (textNodes.Count == 0)
    {
      return;
    }

    if (!this.TryReadDialogSource(
            textNodes,
            out var originalQuestion,
            out var originalOptions))
    {
      this.Trace(
          $"trigger={type} source read failed addon='{args.AddonName}' visibleNodes={textNodes.Count}");
      return;
    }

    this.Trace(
        $"trigger={type} captured source question='{this.Preview(originalQuestion)}' " +
        $"options={originalOptions.Count} overlay={this.ShouldUseOverlay()} " +
        $"swap={this.ShouldSwapTexts()} native={this.ShouldApplyNativeText()}");
    this.syncOverlayBounds(addon);

    var sourceKey = this.BuildSourceKey(originalQuestion, originalOptions);
    if (this.TryGetCachedTranslation(
            originalQuestion,
            originalOptions,
            out var translatedQuestion,
            out var translatedOptions,
            out var replacementQuestion,
            out var replacementOptions))
    {
      this.Trace(
          $"trigger={type} cache-hit question='{this.Preview(originalQuestion)}' " +
          $"options={originalOptions.Count} translatedOptions={translatedOptions.Count}");
      this.SetResolvedState(
          originalQuestion,
          originalOptions,
          translatedQuestion,
          translatedOptions,
          replacementQuestion,
          replacementOptions);
      this.PublishOverlay();
      return;
    }

    if (this.TryLoadStoredTranslation(
            originalQuestion,
            originalOptions,
            out var storedTranslatedQuestion,
            out var storedTranslatedOptions,
            out var storedReplacementQuestion,
            out var storedReplacementOptions))
    {
      this.Trace(
          $"trigger={type} db-hit question='{this.Preview(originalQuestion)}' " +
          $"options={originalOptions.Count} translatedOptions={storedTranslatedOptions.Count}");
      this.SetResolvedState(
          originalQuestion,
          originalOptions,
          storedTranslatedQuestion,
          storedTranslatedOptions,
          storedReplacementQuestion,
          storedReplacementOptions);
      this.PublishOverlay();
      return;
    }

    if (this.ShouldUseOverlay())
    {
      this.Trace(
          $"trigger={type} waiting overlay question='{this.Preview(originalQuestion)}' " +
          $"options={originalOptions.Count} swap={this.ShouldSwapTexts()}");
      this.updateOverlay(
          string.Empty,
          string.Join('\n', originalOptions),
          originalQuestion);
    }

    if (this.TryQueueTranslation(sourceKey, originalQuestion, originalOptions, out var requestId))
    {
      this.Trace(
          $"trigger={type} queued translation request={requestId} " +
          $"source='{this.Preview(originalQuestion)}' options={originalOptions.Count}");
      Task.Run(() => this.ResolveTranslationAsync(originalQuestion, originalOptions, requestId));
    }
  }

  /// <summary>
  ///     Updates the visible CutSceneSelectString addon when translated text
  ///     is available.
  /// </summary>
  private unsafe void OnUpdateVisibleAddon(AddonEvent type, AddonArgs args)
  {
    if (!this.ShouldHandleCutSceneSelectString(args, out var addon))
    {
      return;
    }

    var textNodes = CutSceneSelectStringNodeResolvers.ResolveReadableTextNodes(addon);
    if (textNodes.Count == 0)
    {
      return;
    }

    if (!this.TryReadDialogSource(
            textNodes,
            out var originalQuestion,
            out var originalOptions))
    {
      return;
    }

    this.syncOverlayBounds(addon);

    if (!this.TryGetCurrentResolvedTranslation(
            originalQuestion,
            originalOptions,
            out var translatedQuestion,
            out var translatedOptions,
            out var replacementQuestion,
            out var replacementOptions))
    {
      return;
    }

    if (this.ShouldUseOverlay())
    {
      this.PublishOverlay();
      if (!this.ShouldSwapTexts())
      {
        return;
      }
    }

    if (!this.ShouldApplyNativeText())
    {
      return;
    }

    this.ApplyNativeTranslation(
        textNodes,
        replacementQuestion,
        replacementOptions);
  }

  /// <summary>
  ///     Clears the in-memory CutSceneSelectString state when the addon hides or is finalized.
  /// </summary>
  private void OnResetState(AddonEvent type, AddonArgs args)
  {
    lock (this.stateGate)
    {
      this.state = new DialogState();
    }

    this.clearOverlay();
  }

  /// <summary>
  ///     Reads the current question/title and option list from the live addon tree.
  /// </summary>
  private unsafe bool TryReadDialogSource(
      IReadOnlyList<nint> textNodes,
      out string originalQuestion,
      out List<string> originalOptions)
  {
    originalQuestion = string.Empty;
    originalOptions = [];

    if (textNodes.Count == 0)
    {
      return false;
    }

    originalQuestion = CutSceneSelectStringNodeResolvers.ReadTextNode((AtkTextNode*)textNodes[0]);
    if (string.IsNullOrWhiteSpace(originalQuestion))
    {
      return false;
    }

    for (var i = 1; i < textNodes.Count; i++)
    {
      var optionText = CutSceneSelectStringNodeResolvers.ReadTextNode((AtkTextNode*)textNodes[i]);
      if (string.IsNullOrWhiteSpace(optionText))
      {
        continue;
      }

      originalOptions.Add(optionText);
    }

    return true;
  }

  /// <summary>
  ///     Resolves a CutSceneSelectString translation without blocking the game UI.
  /// </summary>
  private async Task ResolveTranslationAsync(
      string originalQuestion,
      List<string> originalOptions,
      int requestId)
  {
    string translatedQuestion;
    List<string> translatedOptions;

    try
    {
      this.Trace(
          $"request={requestId} translating question='{this.Preview(originalQuestion)}' " +
          $"options={originalOptions.Count}");
      (translatedQuestion, translatedOptions) =
          await this.TranslateDialogAsync(originalQuestion, originalOptions);
    }
    catch (Exception e)
    {
      this.Trace(
          $"request={requestId} translation failed question='{this.Preview(originalQuestion)}' " +
          $"options={originalOptions.Count} error='{e.GetType().Name}: {e.Message}'");
      lock (this.stateGate)
      {
        if (this.state.ActiveRequestId == requestId)
        {
          this.state.TranslationInFlight = false;
          this.state.LastFailedSourceKey = this.BuildSourceKey(originalQuestion, originalOptions);
        }
      }

      return;
    }

    var replacementQuestion = this.NormalizeForReplacement(translatedQuestion);
    var replacementOptions = translatedOptions
        .Select(this.NormalizeForReplacement)
        .ToList();

    this.Trace(
        $"request={requestId} translation resolved question='{this.Preview(originalQuestion)}' " +
        $"translatedQuestion='{this.Preview(translatedQuestion)}' translatedOptions={translatedOptions.Count}");

    var selectString = new SelectString(
        originalQuestion,
        ClientStateInterface.ClientLanguage.Humanize(),
        JsonConvert.SerializeObject(originalOptions),
        translatedQuestion,
        JsonConvert.SerializeObject(translatedOptions),
        LangDict[LanguageInt].Code,
        this.config.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now);

    try
    {
      await this.insertCutSceneSelectStringMessageAsync(selectString);
      this.Trace(
          $"request={requestId} translation stored question='{this.Preview(originalQuestion)}' " +
          $"options={originalOptions.Count}");
    }
    catch (Exception e)
    {
      this.Trace(
          $"request={requestId} translation store failed question='{this.Preview(originalQuestion)}' " +
          $"error='{e.GetType().Name}: {e.Message}'");
    }

    lock (this.stateGate)
    {
      if (this.state.ActiveRequestId != requestId)
      {
        return;
      }

      this.state.CurrentOriginalQuestion = originalQuestion;
      this.state.CurrentOriginalOptions = [.. originalOptions];
      this.state.CurrentTranslatedQuestion = translatedQuestion;
      this.state.CurrentTranslatedOptions = [.. translatedOptions];
      this.state.CurrentReplacementQuestion = replacementQuestion;
      this.state.CurrentReplacementOptions = [.. replacementOptions];
      this.state.TranslationInFlight = false;
      this.state.LastFailedSourceKey = string.Empty;
    }
  }

  /// <summary>
  ///     Translates the question and all options in a single batched request
  ///     when possible, falling back to per-item translation if the batch
  ///     parsing fails.
  /// </summary>
  private async Task<(string TranslatedQuestion, List<string> TranslatedOptions)>
      TranslateDialogAsync(
          string originalQuestion,
          IReadOnlyList<string> originalOptions)
  {
    var sourceLang = ClientStateInterface.ClientLanguage.Humanize();
    var targetLang = LangDict[LanguageInt].Code;
    var translatedMap = new Dictionary<int, string>();

    var indexedEntries = new List<(int Index, string Text)>(originalOptions.Count + 1)
    {
      (0, originalQuestion),
    };

    for (var i = 0; i < originalOptions.Count; i++)
    {
      indexedEntries.Add((i + 1, originalOptions[i]));
    }

    foreach (var chunk in BuildIndexedTranslationChunks(indexedEntries))
    {
      var translatedChunk = await this.translationService.TranslateAsync(
          chunk,
          sourceLang,
          targetLang);

      if (string.IsNullOrWhiteSpace(translatedChunk))
      {
        continue;
      }

      foreach (var (index, value) in ParseIndexedTranslationPairs(translatedChunk))
      {
        translatedMap[index] = value;
      }
    }

    if (translatedMap.Count == 0 || !translatedMap.ContainsKey(0))
    {
      return await this.TranslateDialogIndividuallyAsync(
          originalQuestion,
          originalOptions);
    }

    var translatedQuestion = translatedMap.TryGetValue(0, out var questionText) &&
                             !string.IsNullOrWhiteSpace(questionText)
        ? questionText
        : originalQuestion;

    var translatedOptions = new List<string>(originalOptions.Count);
    for (var i = 0; i < originalOptions.Count; i++)
    {
      var optionIndex = i + 1;
      if (translatedMap.TryGetValue(optionIndex, out var optionText) &&
          !string.IsNullOrWhiteSpace(optionText))
      {
        translatedOptions.Add(optionText);
        continue;
      }

      translatedOptions.Add(originalOptions[i]);
    }

    return (translatedQuestion, translatedOptions);
  }

  /// <summary>
  ///     Falls back to translating each dialog element individually.
  /// </summary>
  private async Task<(string TranslatedQuestion, List<string> TranslatedOptions)>
      TranslateDialogIndividuallyAsync(
          string originalQuestion,
          IReadOnlyList<string> originalOptions)
  {
    var translatedQuestion = await this.TranslateOrFallbackAsync(originalQuestion);
    var translatedOptions = new List<string>(originalOptions.Count);

    foreach (var option in originalOptions)
    {
      translatedOptions.Add(await this.TranslateOrFallbackAsync(option));
    }

    return (translatedQuestion, translatedOptions);
  }

  /// <summary>
  ///     Publishes the currently resolved text to the overlay in the active
  ///     display mode.
  /// </summary>
  private void PublishOverlay()
  {
    string translatedQuestion;
    string translatedOptions;
    string originalQuestion;
    string originalOptions;

    lock (this.stateGate)
    {
      translatedQuestion = this.state.CurrentTranslatedQuestion;
      translatedOptions = string.Join('\n', this.state.CurrentTranslatedOptions);
      originalQuestion = this.state.CurrentOriginalQuestion;
      originalOptions = string.Join('\n', this.state.CurrentOriginalOptions);
    }

    if (!this.ShouldUseOverlay())
    {
      return;
    }

    if (this.ShouldSwapTexts())
    {
      this.updateOverlay(
          originalQuestion,
          originalOptions,
          translatedQuestion);
      return;
    }

    this.updateOverlay(
        translatedQuestion,
        translatedOptions,
        originalQuestion);
  }

  /// <summary>
  ///     Applies the translated question/options back into the visible addon.
  /// </summary>
  private unsafe void ApplyNativeTranslation(
      IReadOnlyList<nint> textNodes,
      string replacementQuestion,
      IReadOnlyList<string> replacementOptions)
  {
    if (textNodes.Count == 0)
    {
      return;
    }

    var questionNode = (AtkTextNode*)textNodes[0];
    var visibleQuestion = CutSceneSelectStringNodeResolvers.ReadTextNode(questionNode);
    var mutated = false;
    if (!this.TextMatches(visibleQuestion, replacementQuestion))
    {
      questionNode->SetText(replacementQuestion);
      mutated = true;
    }

    var optionCount = Math.Min(textNodes.Count - 1, replacementOptions.Count);
    for (var i = 0; i < optionCount; i++)
    {
      var optionNode = (AtkTextNode*)textNodes[i + 1];
      var replacementOption = replacementOptions[i];
      var visibleOption = CutSceneSelectStringNodeResolvers.ReadTextNode(optionNode);
      if (this.TextMatches(visibleOption, replacementOption))
      {
        continue;
      }

      optionNode->SetText(replacementOption);
      mutated = true;
    }

    if (mutated)
    {
      this.Trace(
          $"native apply question='{this.Preview(replacementQuestion)}' options={replacementOptions.Count}");
    }
  }

  /// <summary>
  ///     Tries to get the current resolved translation for the currently visible source.
  /// </summary>
  private bool TryGetCurrentResolvedTranslation(
      string originalQuestion,
      List<string> originalOptions,
      out string translatedQuestion,
      out List<string> translatedOptions,
      out string replacementQuestion,
      out List<string> replacementOptions)
  {
    lock (this.stateGate)
    {
      if (this.state.CurrentOriginalQuestion != originalQuestion ||
          !this.OptionsMatch(this.state.CurrentOriginalOptions, originalOptions) ||
          string.IsNullOrWhiteSpace(this.state.CurrentTranslatedQuestion))
      {
        translatedQuestion = string.Empty;
        translatedOptions = [];
        replacementQuestion = string.Empty;
        replacementOptions = [];
        return false;
      }

      translatedQuestion = this.state.CurrentTranslatedQuestion;
      translatedOptions = [.. this.state.CurrentTranslatedOptions];
      replacementQuestion = this.state.CurrentReplacementQuestion;
      replacementOptions = [.. this.state.CurrentReplacementOptions];
      return true;
    }
  }

  /// <summary>
  ///     Tries to use a cached translation already held in memory.
  /// </summary>
  private bool TryGetCachedTranslation(
      string originalQuestion,
      List<string> originalOptions,
      out string translatedQuestion,
      out List<string> translatedOptions,
      out string replacementQuestion,
      out List<string> replacementOptions)
  {
    translatedQuestion = string.Empty;
    translatedOptions = [];
    replacementQuestion = string.Empty;
    replacementOptions = [];

    var selectString = this.BuildLookupSelectString(originalQuestion, originalOptions);
    var lookup = this.findCutSceneSelectStringMessage(selectString);
    if (lookup == null ||
        string.IsNullOrWhiteSpace(lookup.TranslatedSelectString))
    {
      return false;
    }

    translatedQuestion = lookup.TranslatedSelectString!;
    translatedOptions = this.DeserializeOptions(lookup.TranslatedOptionsAsText);
    replacementQuestion = this.NormalizeForReplacement(translatedQuestion);
    replacementOptions = translatedOptions
        .Select(this.NormalizeForReplacement)
        .ToList();

    this.Trace(
        $"cache-hit question='{this.Preview(originalQuestion)}' options={originalOptions.Count} " +
        $"translatedOptions={translatedOptions.Count}");

    this.SetResolvedState(
        originalQuestion,
        originalOptions,
        translatedQuestion,
        translatedOptions,
        replacementQuestion,
        replacementOptions);
    return true;
  }

  /// <summary>
  ///     Tries to load a translated CutSceneSelectString row from the database.
  /// </summary>
  private bool TryLoadStoredTranslation(
      string originalQuestion,
      List<string> originalOptions,
      out string translatedQuestion,
      out List<string> translatedOptions,
      out string replacementQuestion,
      out List<string> replacementOptions)
  {
    translatedQuestion = string.Empty;
    translatedOptions = [];
    replacementQuestion = string.Empty;
    replacementOptions = [];

    var selectString = this.BuildLookupSelectString(originalQuestion, originalOptions);
    var lookup = this.findCutSceneSelectStringMessage(selectString);
    if (lookup == null ||
        string.IsNullOrWhiteSpace(lookup.TranslatedSelectString))
    {
      return false;
    }

    translatedQuestion = lookup.TranslatedSelectString!;
    translatedOptions = this.DeserializeOptions(lookup.TranslatedOptionsAsText);
    replacementQuestion = this.NormalizeForReplacement(translatedQuestion);
    replacementOptions = translatedOptions
        .Select(this.NormalizeForReplacement)
        .ToList();

    this.Trace(
        $"db-hit question='{this.Preview(originalQuestion)}' options={originalOptions.Count} " +
        $"translatedOptions={translatedOptions.Count}");

    this.SetResolvedState(
        originalQuestion,
        originalOptions,
        translatedQuestion,
        translatedOptions,
        replacementQuestion,
        replacementOptions);
    return true;
  }

  /// <summary>
  ///     Queues a translation request if the same source is not already in
  ///     flight.
  /// </summary>
  private bool TryQueueTranslation(
      string sourceKey,
      string originalQuestion,
      List<string> originalOptions,
      out int requestId)
  {
    lock (this.stateGate)
    {
      if (this.state.TranslationInFlight ||
          this.state.LastFailedSourceKey == sourceKey)
      {
        this.Trace(
            $"queue skipped source='{this.Preview(originalQuestion)}' inFlight={this.state.TranslationInFlight} " +
            $"lastFailed={this.state.LastFailedSourceKey == sourceKey}");
        requestId = this.state.ActiveRequestId;
        return false;
      }

      this.state.ActiveRequestId++;
      requestId = this.state.ActiveRequestId;
      this.state.CurrentOriginalQuestion = originalQuestion;
      this.state.CurrentOriginalOptions = [.. originalOptions];
      this.state.CurrentTranslatedQuestion = string.Empty;
      this.state.CurrentTranslatedOptions = [];
      this.state.CurrentReplacementQuestion = string.Empty;
      this.state.CurrentReplacementOptions = [];
      this.state.TranslationInFlight = true;
      this.state.LastFailedSourceKey = string.Empty;
      return true;
    }
  }

  /// <summary>
  ///     Builds a stable source key for the current question and option list so
  ///     we can suppress duplicate retries for the same visible dialog.
  /// </summary>
  private string BuildSourceKey(
      string originalQuestion,
      IReadOnlyList<string> originalOptions)
  {
    var keyParts = new List<string>(originalOptions.Count + 1)
    {
      this.NormalizeForComparison(originalQuestion),
    };

    keyParts.AddRange(
        originalOptions.Select(this.NormalizeForComparison));

    return string.Join('\u001F', keyParts);
  }

  /// <summary>
  ///     Updates the in-memory resolved translation state.
  /// </summary>
  private void SetResolvedState(
      string originalQuestion,
      List<string> originalOptions,
      string translatedQuestion,
      List<string> translatedOptions,
      string replacementQuestion,
      List<string> replacementOptions)
  {
    lock (this.stateGate)
    {
      this.state.CurrentOriginalQuestion = originalQuestion;
      this.state.CurrentOriginalOptions = [.. originalOptions];
      this.state.CurrentTranslatedQuestion = translatedQuestion;
      this.state.CurrentTranslatedOptions = [.. translatedOptions];
      this.state.CurrentReplacementQuestion = replacementQuestion;
      this.state.CurrentReplacementOptions = [.. replacementOptions];
      this.state.TranslationInFlight = false;
    }
  }

  /// <summary>
  ///     Builds a database lookup entity for the currently visible question and options.
  /// </summary>
  private SelectString BuildLookupSelectString(
      string originalQuestion,
      List<string> originalOptions)
  {
    return new SelectString(
        originalQuestion,
        ClientStateInterface.ClientLanguage.Humanize(),
        JsonConvert.SerializeObject(originalOptions),
        string.Empty,
        string.Empty,
        LangDict[LanguageInt].Code,
        this.config.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now);
  }

  /// <summary>
  ///     Translates a piece of text or falls back to the original text when the
  ///     translation service does not return content.
  /// </summary>
  private async Task<string> TranslateOrFallbackAsync(string text)
  {
    var translatedText = await this.translationService.TranslateAsync(
        text,
        ClientStateInterface.ClientLanguage.Humanize(),
        LangDict[LanguageInt].Code);

    return string.IsNullOrWhiteSpace(translatedText) ? text : translatedText;
  }

  /// <summary>
  ///     Normalizes translated text before native replacement when the active config requests diacritic stripping.
  /// </summary>
  private string NormalizeForReplacement(string text)
  {
    return this.config.RemoveDiacriticsWhenUsingReplacementTalkBTalk
        ? this.normalizeReplacementText(text)
        : text;
  }

  /// <summary>
  ///     Compares two strings after normalizing them for native replacement.
  /// </summary>
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
  ///     Returns whether the overlay mode is active for CutSceneSelectString.
  /// </summary>
  private bool ShouldUseOverlay()
  {
    return this.config.TranslateCutSceneSelectString &&
           TranslationDisplayModeHelper.UsesOverlayPresentation(
               this.config.CutSceneSelectStringTranslationDisplayMode,
               this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Returns whether the native addon should be updated with translated text.
  /// </summary>
  private bool ShouldApplyNativeText()
  {
    return TranslationDisplayModeHelper.WritesNativeTranslation(
        this.config.CutSceneSelectStringTranslationDisplayMode,
        this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Returns whether overlay and native texts are swapped.
  /// </summary>
  private bool ShouldSwapTexts()
  {
    return TranslationDisplayModeHelper.ShowsOriginalOverlayText(
        this.config.CutSceneSelectStringTranslationDisplayMode,
        this.config.OverlayOnlyLanguage);
  }

  /// <summary>
  ///     Determines whether the callback is for the visible CutSceneSelectString addon.
  /// </summary>
  private unsafe bool ShouldHandleCutSceneSelectString(
      AddonArgs args,
      out AtkUnitBase* addon)
  {
    addon = null;
    if (args.AddonName != AddonName || args.Addon.Address == IntPtr.Zero)
    {
      return false;
    }

    addon = (AtkUnitBase*)args.Addon.Address;
    return addon != null && addon->IsVisible;
  }

  /// <summary>
  ///     Compares two ordered option lists.
  /// </summary>
  private bool OptionsMatch(IReadOnlyList<string> left, IReadOnlyList<string> right)
  {
    if (left.Count != right.Count)
    {
      return false;
    }

    for (var i = 0; i < left.Count; i++)
    {
      if (!this.TextMatches(left[i], right[i]))
      {
        return false;
      }
    }

    return true;
  }

  /// <summary>
  ///     Writes a focused debug message for the CutSceneSelectString flow.
  /// </summary>
  /// <param name="message">Message to write.</param>
  private void Trace(string message)
  {
    _ = message;
  }

  /// <summary>
  ///     Produces a compact preview for log messages.
  /// </summary>
  /// <param name="text">Text to preview.</param>
  /// <returns>A compact log-friendly preview string.</returns>
  private string Preview(string? text)
  {
    if (string.IsNullOrWhiteSpace(text))
    {
      return string.Empty;
    }

    var preview = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
    return preview.Length <= 120 ? preview : preview[..120] + "…";
  }

  /// <summary>
  ///     Converts the stored options JSON into a list.
  /// </summary>
  private List<string> DeserializeOptions(string? optionsJson)
  {
    if (string.IsNullOrWhiteSpace(optionsJson))
    {
      return [];
    }

    try
    {
      return JsonConvert.DeserializeObject<List<string>>(optionsJson) ?? [];
    }
    catch
    {
      return [];
    }
  }
}


