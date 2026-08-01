// <copyright file="QuestAddonHandlerBase.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Runtime.CompilerServices;

using Echoglossian.NativeUI.Helpers;

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Shared base implementation for standalone quest addon handlers.
/// </summary>
internal abstract class QuestAddonHandlerBase
    : IAddonTranslationHandler, IPluginUnloadAwareAddonHandler
{
  private const int PopupSectionBodySearchMaxSiblingCount = 6;

  private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>>
      eventHandlers = new();

  /// <summary>
  ///     Initializes a new instance of the <see cref="QuestAddonHandlerBase" /> class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  protected QuestAddonHandlerBase(QuestAddonHandlerDependencies dependencies)
  {
    this.Dependencies = dependencies ??
                        throw new ArgumentNullException(nameof(dependencies));
  }

  /// <summary>
  ///     Gets the shared dependencies used by the quest handlers.
  /// </summary>
  protected QuestAddonHandlerDependencies Dependencies { get; }

  /// <summary>Gets the active plugin configuration.</summary>
  protected Config Config => this.Dependencies.Config;

  /// <summary>Gets the shared translation service.</summary>
  protected TranslationService TranslationService =>
      this.Dependencies.TranslationService;

  /// <summary>
  ///     Returns the event handlers required to drive the quest addon flow.
  /// </summary>
  /// <returns>A dictionary mapping addon events to combined delegates.</returns>
  public Dictionary<AddonEvent, IAddonLifecycle.AddonEventDelegate>
      GetEventHandlers()
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
  ///     Clears any visible native quest UI state when the handler is being
  ///     rebuilt or the plugin is unloading.
  /// </summary>
  public virtual void OnPluginUnload()
  {
  }

  /// <summary>
  ///     Registers a local delegate for the specified addon event.
  /// </summary>
  /// <param name="evt">The lifecycle event to handle.</param>
  /// <param name="handler">The delegate invoked for that event.</param>
  protected void RegisterHandler(
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
  ///     Creates a canonical quest plate snapshot using the captured source
  ///     language and current engine settings.
  /// </summary>
  /// <param name="sourceLanguage">The operation-captured source identity.</param>
  /// <param name="questName">The quest name.</param>
  /// <param name="questMessage">The quest message.</param>
  /// <param name="questId">Optional quest id.</param>
  /// <returns>A canonical quest plate snapshot.</returns>
  protected QuestPlate CreateQuestPlate(
      SourceClientLanguage sourceLanguage,
      string questName,
      string questMessage,
      string? questId = null)
  {
    return new QuestPlate(
        questName,
        questMessage,
        sourceLanguage.PersistenceCode,
        string.Empty,
        string.Empty,
        questId,
        RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(this.Config.Lang),
        this.Config.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now,
        GetGameVersion());
  }

  /// <summary>
  ///     Creates a canonical quest plate snapshot with translated fields.
  /// </summary>
  /// <param name="sourceLanguage">The operation-captured source identity.</param>
  /// <param name="questName">The original quest name.</param>
  /// <param name="questMessage">The original quest message.</param>
  /// <param name="translatedQuestName">The translated quest name.</param>
  /// <param name="translatedQuestMessage">The translated quest message.</param>
  /// <param name="questId">Optional quest id.</param>
  /// <returns>A translated quest plate snapshot.</returns>
  protected QuestPlate CreateTranslatedQuestPlate(
      SourceClientLanguage sourceLanguage,
      string questName,
      string questMessage,
      string translatedQuestName,
      string translatedQuestMessage,
      string? questId = null)
  {
    return new QuestPlate(
        questName,
        questMessage,
        sourceLanguage.PersistenceCode,
        translatedQuestName,
        translatedQuestMessage,
        questId,
        RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(this.Config.Lang),
        this.Config.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now,
        GetGameVersion());
  }

  /// <summary>
  ///     Creates a dedicated quest-popup snapshot using the captured source
  ///     language and current engine settings.
  /// </summary>
  /// <param name="surfaceName">The popup surface name.</param>
  /// <param name="sourceLanguage">The operation-captured source identity.</param>
  /// <param name="originalTitle">The original popup title.</param>
  /// <param name="originalBody">The original popup body.</param>
  /// <param name="translatedTitle">The translated popup title.</param>
  /// <param name="translatedBody">The translated popup body.</param>
  /// <param name="questId">The optional canonical quest id.</param>
  /// <returns>A dedicated quest-popup row.</returns>
  protected QuestPopupText CreateQuestPopupText(
      string surfaceName,
      SourceClientLanguage sourceLanguage,
      string originalTitle,
      string originalBody,
      string translatedTitle = "",
      string translatedBody = "",
      string? questId = null)
  {
    return new QuestPopupText(
        surfaceName,
        questId,
        originalTitle,
        originalBody,
        sourceLanguage.PersistenceCode,
        translatedTitle,
        translatedBody,
        RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(this.Config.Lang),
        this.Config.ChosenTransEngine,
        GetGameVersion(),
        sourceContentHash: null,
        DateTime.Now,
        DateTime.Now);
  }

  /// <summary>
  ///     Translates the given text using the shared translation service.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">The operation-captured source identity.</param>
  /// <returns>The translated text.</returns>
  protected string Translate(
      string text,
      SourceClientLanguage sourceLanguage)
  {
    return this.TranslationService.Translate(
        text,
        sourceLanguage,
        LangDict[LanguageInt].Code,
        originContext: $"{this.GetType().Name}/Text");
  }

  /// <summary>
  ///     Translates the given text asynchronously using the shared translation
  ///     service.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <param name="sourceLanguage">The operation-captured source identity.</param>
  /// <returns>The translated text task.</returns>
  protected Task<string> TranslateAsync(
      string text,
      SourceClientLanguage sourceLanguage)
  {
    return this.TranslationService.TranslateAsync(
        text,
        sourceLanguage,
        LangDict[LanguageInt].Code,
        originContext: $"{this.GetType().Name}/Text");
  }

  /// <summary>
  ///     Attempts to read a queued translation from the shared broker cache.
  /// </summary>
  /// <param name="key">Stable translation key.</param>
  /// <param name="translatedText">The cached translated text, if any.</param>
  /// <returns>True when a cached translation exists.</returns>
  protected bool TryGetQueuedTranslation(
      string key,
      out string translatedText)
  {
    return this.Dependencies.TryGetQueuedTranslation(key, out translatedText);
  }

  /// <summary>
  ///     Enqueues a translation request on the shared broker without blocking
  ///     the addon lifecycle callback.
  /// </summary>
  /// <param name="key">Stable translation key.</param>
  /// <param name="resolver">Function that returns the translated text.</param>
  /// <param name="onResolved">Optional callback invoked after the text is cached.</param>
  /// <returns>True if the request was queued, false if one is already in flight.</returns>
  protected bool QueueTranslation(
      string key,
      Func<string> resolver,
      Action<string>? onResolved = null)
  {
    return this.Dependencies.QueueTranslation(key, resolver, onResolved);
  }

  /// <summary>
  ///     Queues a batch translation request on the shared broker.
  /// </summary>
  /// <param name="key">Stable translation key.</param>
  /// <param name="sourceTexts">The source texts to translate.</param>
  /// <param name="sourceLanguage">The captured source client language.</param>
  /// <param name="onResolved">Optional callback invoked with the translated batch.</param>
  /// <returns>True if the request was queued.</returns>
  protected bool QueueTranslationBatch(
      string key,
      IReadOnlyCollection<string> sourceTexts,
      SourceClientLanguage sourceLanguage,
      Action<string[]>? onResolved = null)
  {
    return this.Dependencies.QueueTranslationBatch(
        key,
        sourceTexts,
        sourceLanguage,
        onResolved);
  }

  /// <summary>
  ///     Normalizes one visible quest text for concise runtime diagnostics.
  /// </summary>
  /// <param name="text">The source text to normalize.</param>
  /// <param name="maxLength">The maximum diagnostic length.</param>
  /// <returns>The normalized and truncated diagnostic text.</returns>
  protected string SummarizeDiagnosticText(
      string? text,
      int maxLength = 96)
  {
    if (string.IsNullOrWhiteSpace(text))
    {
      return string.Empty;
    }

    var normalizedText = text.ReplaceLineEndings(" ").Trim();
    if (normalizedText.Length <= maxLength)
    {
      return normalizedText;
    }

    return normalizedText[..Math.Max(0, maxLength - 3)] + "...";
  }

  /// <summary>
  ///     Requests that the shared accepted-quest prefetch runtime resolve and
  ///     persist any missing canonical translations for the specified quest.
  /// </summary>
  /// <param name="questId">The accepted quest identifier to prefetch.</param>
  /// <param name="callerMemberName">
  ///     The calling quest-handler member used to tag diagnostic output.
  /// </param>
  protected void RequestAcceptedQuestPrefetch(
      uint questId,
      [CallerMemberName] string? callerMemberName = null)
  {
    var source =
        $"{this.GetType().Name}.{callerMemberName ?? nameof(this.RequestAcceptedQuestPrefetch)}";
    this.Dependencies.RequestAcceptedQuestPrefetch(questId, source);
  }

  /// <summary>
  ///     Removes quest hover tooltips whose keys share the specified prefix.
  /// </summary>
  /// <param name="prefix">The tooltip key prefix to remove.</param>
  protected void RemoveHoverTooltipsByPrefix(string prefix)
  {
    this.Dependencies.RemoveHoverTooltipByPrefix(prefix);
  }

  /// <summary>
  ///     Serializes a pair of translated strings for broker caching.
  /// </summary>
  /// <param name="first">The first translated string.</param>
  /// <param name="second">The second translated string.</param>
  /// <returns>A JSON payload representing both strings.</returns>
  protected static string SerializeTranslationPair(string first, string second)
  {
    return JsonConvert.SerializeObject(new[] { first, second });
  }

  /// <summary>
  ///     Tries to deserialize a cached translation pair payload.
  /// </summary>
  /// <param name="payload">The cached payload.</param>
  /// <param name="first">The first translated string.</param>
  /// <param name="second">The second translated string.</param>
  /// <returns>True when the payload contains two strings.</returns>
  protected static bool TryDeserializeTranslationPair(
      string payload,
      out string first,
      out string second)
  {
    first = string.Empty;
    second = string.Empty;

    try
    {
      var items = JsonConvert.DeserializeObject<string[]>(payload);
      if (items == null || items.Length < 2)
      {
        return false;
      }

      first = items[0] ?? string.Empty;
      second = items[1] ?? string.Empty;
      return true;
    }
    catch
    {
      return false;
    }
  }

  /// <summary>
  ///     Finds a visible readable text node whose current native text matches
  ///     either the original or translated payload.
  /// </summary>
  /// <param name="addon">The live addon instance to inspect.</param>
  /// <param name="originalText">The original source text.</param>
  /// <param name="translatedText">The translated text.</param>
  /// <param name="textNode">The matched text node, if any.</param>
  /// <returns><c>true</c> when a matching text node was found.</returns>
  protected unsafe bool TryFindReadableTextNodeByText(
      AtkUnitBase* addon,
      string originalText,
      string translatedText,
      out AtkTextNode* textNode)
  {
    textNode = null;
    if (addon == null)
    {
      return false;
    }

    foreach (var nodeAddress in AddonTextNodeResolvers.ResolveReadableTextNodes(addon))
    {
      var candidate = (AtkTextNode*)nodeAddress;
      if (candidate == null || !candidate->IsVisible())
      {
        continue;
      }

      var visibleText = ReadReadableTextNode(candidate);
      if (TextNodePayloadMatches(visibleText, originalText) ||
          TextNodePayloadMatches(visibleText, translatedText))
      {
        textNode = candidate;
        return true;
      }
    }

    return false;
  }

  /// <summary>
  ///     Resolves the first visible popup body text node that belongs to the
  ///     section immediately preceding one heading text node with the supplied
  ///     sheet-text identifier.
  /// </summary>
  /// <param name="addon">The live addon instance to inspect.</param>
  /// <param name="headingTextId">
  ///     The sheet-text identifier carried by the visible section heading.
  /// </param>
  /// <param name="textNode">The resolved visible body node, if any.</param>
  /// <returns><c>true</c> when the popup body node was found.</returns>
  protected static unsafe bool TryFindPopupSectionBodyTextNodeByHeadingTextId(
      AtkUnitBase* addon,
      uint headingTextId,
      out AtkTextNode* textNode)
  {
    textNode = null;
    if (addon == null ||
        !TryFindVisibleTextNodeByTextId(
            addon,
            headingTextId,
            out var headingNode))
    {
      return false;
    }

    var inspectedSiblingCount = 0;
    for (var candidate = ((AtkResNode*)headingNode)->PrevSiblingNode;
         candidate != null &&
         inspectedSiblingCount < PopupSectionBodySearchMaxSiblingCount;
         candidate = candidate->PrevSiblingNode, inspectedSiblingCount++)
    {
      if (TryFindPopupSectionBodyTextNodeFromCandidate(
              candidate,
              headingNode,
              out textNode))
      {
        return true;
      }
    }

    return false;
  }

  /// <summary>
  ///     Reads the best plain-text representation available from a native text
  ///     node without mutating the node.
  /// </summary>
  /// <param name="textNode">The text node to read.</param>
  /// <returns>The readable text, or an empty string.</returns>
  protected static unsafe string ReadReadableTextNode(AtkTextNode* textNode)
  {
    return ReadableSeStringPayloadHelper.ReadReadableTextNode(textNode);
  }

  /// <summary>
  ///     Finds one visible readable text node whose sheet-text identifier
  ///     matches the supplied value.
  /// </summary>
  /// <param name="addon">The live addon instance to inspect.</param>
  /// <param name="textId">The sheet-text identifier to resolve.</param>
  /// <param name="textNode">The matched visible text node, if any.</param>
  /// <returns><c>true</c> when a matching heading node was found.</returns>
  private static unsafe bool TryFindVisibleTextNodeByTextId(
      AtkUnitBase* addon,
      uint textId,
      out AtkTextNode* textNode)
  {
    textNode = null;
    if (addon == null)
    {
      return false;
    }

    foreach (var nodeAddress in AddonTextNodeResolvers.ResolveReadableTextNodes(addon))
    {
      var candidate = (AtkTextNode*)nodeAddress;
      if (candidate == null ||
          !candidate->IsVisible() ||
          candidate->TextId != textId)
      {
        continue;
      }

      textNode = candidate;
      return true;
    }

    return false;
  }

  /// <summary>
  ///     Tries to resolve the first visible text-node descendant from one
  ///     structural popup section candidate that precedes the target heading.
  /// </summary>
  /// <param name="candidate">The structural section candidate.</param>
  /// <param name="headingNode">The heading node that owns the section.</param>
  /// <param name="textNode">The resolved body text node, if any.</param>
  /// <returns><c>true</c> when the candidate exposes one visible body node.</returns>
  private static unsafe bool TryFindPopupSectionBodyTextNodeFromCandidate(
      AtkResNode* candidate,
      AtkTextNode* headingNode,
      out AtkTextNode* textNode)
  {
    textNode = null;
    if (candidate == null || !candidate->IsVisible())
    {
      return false;
    }

    var excludedNode = (AtkResNode*)headingNode;
    if (candidate->Type == NodeType.Text && candidate != excludedNode)
    {
      textNode = candidate->GetAsAtkTextNode();
      return textNode != null && textNode->IsVisible();
    }

    HashSet<nint> visitedNodes = [];
    if ((ushort)candidate->Type >= 1000)
    {
      var componentNode = (AtkComponentNode*)candidate;
      if (componentNode->Component != null &&
          TryFindFirstVisibleTextNodeInSubtree(
              componentNode->Component->UldManager.RootNode,
              headingNode,
              visitedNodes,
              out textNode))
      {
        return true;
      }
    }

    return TryFindFirstVisibleTextNodeInSubtree(
        candidate->ChildNode,
        headingNode,
        visitedNodes,
        out textNode);
  }

  /// <summary>
  ///     Walks one visible node subtree in draw order and returns the first
  ///     visible text node that is not the excluded heading.
  /// </summary>
  /// <param name="node">The subtree root to inspect.</param>
  /// <param name="excludedTextNode">The heading node that must be skipped.</param>
  /// <param name="visitedNodes">The visited native-node addresses.</param>
  /// <param name="textNode">The resolved visible text node, if any.</param>
  /// <returns><c>true</c> when a visible text node was found.</returns>
  private static unsafe bool TryFindFirstVisibleTextNodeInSubtree(
      AtkResNode* node,
      AtkTextNode* excludedTextNode,
      HashSet<nint> visitedNodes,
      out AtkTextNode* textNode)
  {
    textNode = null;
    for (var current = node; current != null; current = current->NextSiblingNode)
    {
      if (!visitedNodes.Add((nint)current) || !current->IsVisible())
      {
        continue;
      }

      if (current->Type == NodeType.Text)
      {
        var candidateTextNode = current->GetAsAtkTextNode();
        if (candidateTextNode != null && candidateTextNode != excludedTextNode)
        {
          textNode = candidateTextNode;
          return true;
        }
      }

      if ((ushort)current->Type >= 1000)
      {
        var componentNode = (AtkComponentNode*)current;
        if (componentNode->Component != null &&
            TryFindFirstVisibleTextNodeInSubtree(
                componentNode->Component->UldManager.RootNode,
                excludedTextNode,
                visitedNodes,
                out textNode))
        {
          return true;
        }
      }

      if (TryFindFirstVisibleTextNodeInSubtree(
              current->ChildNode,
              excludedTextNode,
              visitedNodes,
              out textNode))
      {
        return true;
      }
    }

    return false;
  }

  /// <summary>
  ///     Chooses the safest readable representation for one native text node.
  /// </summary>
  /// <param name="currentText">
  ///     The direct current text returned by the live <see cref="Utf8String" />
  ///     wrapper.
  /// </param>
  /// <param name="originalText">
  ///     The readable text extracted from the node's structured original
  ///     payload.
  /// </param>
  /// <param name="legacyText">
  ///     The legacy SeString buffer read fallback.
  /// </param>
  /// <returns>The preferred readable text for matching and hover work.</returns>
  private static string ResolveReadableTextNodeText(
      string currentText,
      string originalText,
      string legacyText)
  {
    return ReadableSeStringPayloadHelper.ResolveReadableTextNodeText(
        currentText,
        originalText,
        legacyText);
  }

  /// <summary>
  ///     Projects translated readable text onto a captured SeString payload so
  ///     native mutation can preserve original formatting macros when the raw
  ///     payload still matches the expected readable source text.
  /// </summary>
  /// <param name="originalPayload">The captured original SeString bytes.</param>
  /// <param name="originalText">The expected readable original text.</param>
  /// <param name="translatedText">The translated readable text.</param>
  /// <returns>
  ///     The projected payload bytes when payload reuse succeeds; otherwise,
  ///     <see langword="null" />.
  /// </returns>
  protected static byte[]? ProjectReadablePayloadBytes(
      byte[]? originalPayload,
      string originalText,
      string translatedText)
  {
    return ReadableSeStringPayloadHelper.ProjectReadablePayloadBytes(
        originalPayload,
        originalText,
        translatedText);
  }

  /// <summary>
  ///     Compares visible native text with one expected payload while allowing
  ///     line wrapping and SeString whitespace differences.
  /// </summary>
  /// <param name="visibleText">The text read from the native node.</param>
  /// <param name="expectedText">The expected original or translated payload.</param>
  /// <returns><c>true</c> when the texts describe the same payload.</returns>
  private static bool TextNodePayloadMatches(
      string visibleText,
      string expectedText)
  {
    return ReadableSeStringPayloadHelper.PayloadMatches(
        visibleText,
        expectedText);
  }

  /// <summary>
  ///     Normalizes readable native text for popup text-node matching.
  /// </summary>
  /// <param name="text">The text to normalize.</param>
  /// <returns>The normalized text.</returns>
  protected static string NormalizeReadableText(string text)
  {
    return ReadableSeStringPayloadHelper.NormalizeReadableText(text);
  }

  /// <summary>
  ///     Normalizes quest text before writing it to the native UI.
  /// </summary>
  /// <param name="text">The text to normalize.</param>
  /// <returns>The normalized quest text.</returns>
  protected string NormalizeQuestText(string text)
  {
    return this.Dependencies.NormalizeText(text);
  }

  /// <summary>
  ///     Checks whether translation should be disabled for the current game
  ///     state.
  /// </summary>
  /// <returns>True when quest translation should be suppressed.</returns>
  protected bool DisableTranslationAccordingToState()
  {
    return this.Dependencies.DisableTranslationAccordingToState();
  }

  /// <summary>
  ///     Resolves a quest plate using the full quest lookup.
  /// </summary>
  /// <param name="questPlate">The quest plate to look up.</param>
  /// <returns>The matching quest plate, if one exists.</returns>
  protected QuestPlate? FindQuestPlate(QuestPlate questPlate)
  {
    return this.Dependencies.FindQuestPlate(questPlate);
  }

  /// <summary>
  ///     Resolves a quest plate using the quest-name-only lookup.
  /// </summary>
  /// <param name="questPlate">The quest plate to look up.</param>
  /// <returns>The matching quest plate, if one exists.</returns>
  protected QuestPlate? FindQuestPlateByName(QuestPlate questPlate)
  {
    return this.Dependencies.FindQuestPlateByName(questPlate);
  }

  /// <summary>
  ///     Resolves dedicated popup text using the popup lookup delegate.
  /// </summary>
  /// <param name="questPopupText">The popup row to look up.</param>
  /// <returns>The matching popup row, if one exists.</returns>
  protected QuestPopupText? FindQuestPopupText(QuestPopupText questPopupText)
  {
    return this.Dependencies.FindQuestPopupText(questPopupText);
  }

  /// <summary>
  ///     Persists a quest plate insert into the database.
  /// </summary>
  /// <param name="questPlate">The quest plate to insert.</param>
  /// <returns>The persistence result.</returns>
  protected string InsertQuestPlate(QuestPlate questPlate)
  {
    return this.Dependencies.InsertQuestPlate(questPlate);
  }

  /// <summary>
  ///     Persists a quest-popup insert into the database asynchronously.
  /// </summary>
  /// <param name="questPopupText">The popup row to insert.</param>
  /// <returns>The persistence result.</returns>
  protected Task<string> InsertQuestPopupTextAsync(QuestPopupText questPopupText)
  {
    return this.Dependencies.InsertQuestPopupTextAsync(questPopupText);
  }

  /// <summary>
  ///     Persists a quest plate update into the database.
  /// </summary>
  /// <param name="questPlate">The quest plate to update.</param>
  /// <returns>The persistence result.</returns>
  protected string UpdateQuestPlate(QuestPlate questPlate)
  {
    return this.Dependencies.UpdateQuestPlate(questPlate);
  }

  /// <summary>
  ///     Updates the stored game version for a quest plate.
  /// </summary>
  /// <param name="id">The quest plate id.</param>
  /// <param name="newGameVersion">The game version to store.</param>
  protected void UpdateQuestPlateGameVersion(int id, string? newGameVersion)
  {
    this.Dependencies.UpdateQuestPlateGameVersion(id, newGameVersion);
  }

  /// <summary>
  ///     Registers a hover tooltip for a text node using its current screen
  ///     bounds.
  /// </summary>
  /// <param name="key">Stable key used to refresh the tooltip target.</param>
  /// <param name="textNode">The text node to anchor the tooltip to.</param>
  /// <param name="originalText">The original visible text.</param>
  /// <param name="translatedText">The translated text.</param>
  /// <param name="translatedPayloadReady">
  /// Whether the tooltip payload required by the current mode is ready.
  /// </param>
  /// <param name="swapEnabled">Optional explicit swap override.</param>
  /// <param name="forceEnabled">Whether to register even if tooltips are disabled.</param>
  /// <param name="denseHitbox">Whether to use the denser node hitbox.</param>
  protected unsafe void RegisterTranslatedHoverTooltip(
      string key,
      AtkTextNode* textNode,
      string originalText,
      string translatedText,
      bool translatedPayloadReady = true,
      bool? swapEnabled = null,
      bool forceEnabled = false,
      bool denseHitbox = false)
  {
    this.Dependencies.RegisterTranslatedHoverTooltipTextNode(
        key,
        textNode,
        originalText,
        translatedText,
        translatedPayloadReady,
        swapEnabled,
        forceEnabled,
        denseHitbox);
  }

  /// <summary>
  ///     Registers a hover tooltip for a generic node using its current screen
  ///     bounds.
  /// </summary>
  /// <param name="key">Stable key used to refresh the tooltip target.</param>
  /// <param name="node">The node to anchor the tooltip to.</param>
  /// <param name="originalText">The original visible text.</param>
  /// <param name="translatedText">The translated text.</param>
  /// <param name="translatedPayloadReady">
  /// Whether the tooltip payload required by the current mode is ready.
  /// </param>
  /// <param name="swapEnabled">Optional explicit swap override.</param>
  /// <param name="forceEnabled">Whether to register even if tooltips are disabled.</param>
  /// <param name="denseHitbox">Whether to use the denser node hitbox.</param>
  protected unsafe void RegisterTranslatedHoverTooltip(
      string key,
      AtkResNode* node,
      string originalText,
      string translatedText,
      bool translatedPayloadReady = true,
      bool? swapEnabled = null,
      bool forceEnabled = false,
      bool denseHitbox = false)
  {
    this.Dependencies.RegisterTranslatedHoverTooltipResNode(
        key,
        node,
        originalText,
        translatedText,
        translatedPayloadReady,
        swapEnabled,
        forceEnabled,
        denseHitbox);
  }

  /// <summary>
  ///     Registers a hover tooltip for a whole addon window using its root
  ///     node.
  /// </summary>
  /// <param name="key">Stable key used to refresh the tooltip target.</param>
  /// <param name="addon">The live addon window to anchor the tooltip to.</param>
  /// <param name="originalText">The original visible text.</param>
  /// <param name="translatedText">The translated text.</param>
  /// <param name="translatedPayloadReady">
  /// Whether the tooltip payload required by the current mode is ready.
  /// </param>
  /// <param name="swapEnabled">Optional explicit swap override.</param>
  /// <param name="forceEnabled">Whether to register even if tooltips are disabled.</param>
  /// <param name="denseHitbox">Whether to use the denser addon hitbox.</param>
  protected unsafe void RegisterTranslatedHoverTooltip(
      string key,
      AtkUnitBase* addon,
      string originalText,
      string translatedText,
      bool translatedPayloadReady = true,
      bool? swapEnabled = null,
      bool forceEnabled = false,
      bool denseHitbox = false)
  {
    this.Dependencies.RegisterTranslatedHoverTooltipAddon(
        key,
        addon,
        originalText,
        translatedText,
        translatedPayloadReady,
        swapEnabled,
        forceEnabled,
        denseHitbox);
  }

  /// <summary>
  ///     Registers a hover tooltip using explicit screen bounds.
  /// </summary>
  /// <param name="key">Stable key used to refresh the tooltip target.</param>
  /// <param name="topLeft">Top-left screen coordinate.</param>
  /// <param name="bottomRight">Bottom-right screen coordinate.</param>
  /// <param name="originalText">The original visible text.</param>
  /// <param name="translatedText">The translated text.</param>
  /// <param name="translatedPayloadReady">
  /// Whether the tooltip payload required by the current mode is ready.
  /// </param>
  /// <param name="swapEnabled">Optional explicit swap override.</param>
  /// <param name="forceEnabled">Whether to register even if tooltips are disabled.</param>
  protected void RegisterTranslatedHoverTooltip(
      string key,
      Vector2 topLeft,
      Vector2 bottomRight,
      string originalText,
      string translatedText,
      bool translatedPayloadReady = true,
      bool? swapEnabled = null,
      bool forceEnabled = false)
  {
    this.Dependencies.RegisterTranslatedHoverTooltipBounds(
        key,
        topLeft,
        bottomRight,
        originalText,
        translatedText,
        translatedPayloadReady,
        swapEnabled,
        forceEnabled);
  }

  /// <summary>
  ///     Registers a hover tooltip using explicit screen bounds while
  ///     preserving rich original capture from a live text node.
  /// </summary>
  /// <param name="key">Stable key used to refresh the tooltip target.</param>
  /// <param name="topLeft">Top-left screen coordinate.</param>
  /// <param name="bottomRight">Bottom-right screen coordinate.</param>
  /// <param name="textNode">The live text node used for rich original capture.</param>
  /// <param name="originalText">The original visible text.</param>
  /// <param name="translatedText">The translated text.</param>
  /// <param name="translatedPayloadReady">
  /// Whether the tooltip payload required by the current mode is ready.
  /// </param>
  /// <param name="swapEnabled">Optional explicit swap override.</param>
  /// <param name="forceEnabled">Whether to register even if tooltips are disabled.</param>
  protected unsafe void RegisterTranslatedHoverTooltip(
      string key,
      Vector2 topLeft,
      Vector2 bottomRight,
      AtkTextNode* textNode,
      string originalText,
      string translatedText,
      bool translatedPayloadReady = true,
      bool? swapEnabled = null,
      bool forceEnabled = false)
  {
    this.Dependencies.RegisterTranslatedHoverTooltipTextNodeBounds(
        key,
        topLeft,
        bottomRight,
        textNode,
        originalText,
        translatedText,
        translatedPayloadReady,
        swapEnabled,
        forceEnabled);
  }
}
