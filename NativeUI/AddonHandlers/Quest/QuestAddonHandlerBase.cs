// <copyright file="QuestAddonHandlerBase.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Shared base implementation for standalone quest addon handlers.
/// </summary>
internal abstract class QuestAddonHandlerBase : IAddonTranslationHandler
{
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
  ///     Creates a canonical quest plate snapshot using the current plugin
  ///     language and engine settings.
  /// </summary>
  /// <param name="questName">The quest name.</param>
  /// <param name="questMessage">The quest message.</param>
  /// <param name="questId">Optional quest id.</param>
  /// <returns>A canonical quest plate snapshot.</returns>
  protected QuestPlate CreateQuestPlate(
      string questName,
      string questMessage,
      string? questId = null)
  {
    return new QuestPlate(
        questName,
        questMessage,
        ClientStateInterface.ClientLanguage.Humanize(),
        string.Empty,
        string.Empty,
        questId,
        LangDict[LanguageInt].Code,
        this.Config.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now,
        GetGameVersion());
  }

  /// <summary>
  ///     Creates a canonical quest plate snapshot with translated fields.
  /// </summary>
  /// <param name="questName">The original quest name.</param>
  /// <param name="questMessage">The original quest message.</param>
  /// <param name="translatedQuestName">The translated quest name.</param>
  /// <param name="translatedQuestMessage">The translated quest message.</param>
  /// <param name="questId">Optional quest id.</param>
  /// <returns>A translated quest plate snapshot.</returns>
  protected QuestPlate CreateTranslatedQuestPlate(
      string questName,
      string questMessage,
      string translatedQuestName,
      string translatedQuestMessage,
      string? questId = null)
  {
    return new QuestPlate(
        questName,
        questMessage,
        ClientStateInterface.ClientLanguage.Humanize(),
        translatedQuestName,
        translatedQuestMessage,
        questId,
        LangDict[LanguageInt].Code,
        this.Config.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now,
        GetGameVersion());
  }

  /// <summary>
  ///     Translates the given text using the shared translation service.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <returns>The translated text.</returns>
  protected string Translate(string text)
  {
    return this.TranslationService.Translate(
        text,
        ClientStateInterface.ClientLanguage.Humanize(),
        LangDict[LanguageInt].Code);
  }

  /// <summary>
  ///     Translates the given text asynchronously using the shared translation
  ///     service.
  /// </summary>
  /// <param name="text">Text to translate.</param>
  /// <returns>The translated text task.</returns>
  protected Task<string> TranslateAsync(string text)
  {
    return this.TranslationService.TranslateAsync(
        text,
        ClientStateInterface.ClientLanguage.Humanize(),
        LangDict[LanguageInt].Code);
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
  /// <param name="onResolved">Optional callback invoked with the translated batch.</param>
  /// <returns>True if the request was queued.</returns>
  protected bool QueueTranslationBatch(
      string key,
      IReadOnlyCollection<string> sourceTexts,
      Action<string[]>? onResolved = null)
  {
    return this.Dependencies.QueueTranslationBatch(key, sourceTexts, onResolved);
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
  ///     Persists a quest plate insert into the database.
  /// </summary>
  /// <param name="questPlate">The quest plate to insert.</param>
  /// <returns>The persistence result.</returns>
  protected string InsertQuestPlate(QuestPlate questPlate)
  {
    return this.Dependencies.InsertQuestPlate(questPlate);
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
}
