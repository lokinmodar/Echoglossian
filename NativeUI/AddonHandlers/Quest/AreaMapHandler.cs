// <copyright file="AreaMapHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Handles the AreaMap quest addon runtime inside the standalone quest-
///     handler model.
/// </summary>
internal sealed class AreaMapHandler : QuestAddonHandlerBase
{
  private const string AreaMapAddonName = "AreaMap";

  private const string AreaMapHoverPrefix = "AreaMap-";

  private static readonly Regex AreaMapLevelQuestTextPattern =
      new(
          @"^(?<levelPrefix>Lv\.\s*\d+\s+)(?<questName>\S.*)$",
          RegexOptions.Compiled | RegexOptions.CultureInvariant);

  private static readonly TimeSpan AreaMapRetryInterval =
      TimeSpan.FromSeconds(2);

  private readonly Dictionary<string, AreaMapTextCacheEntry> areaMapTextCache = [];

  private nint areaMapQuestNameNodeKey;

  private string areaMapQuestLevelPrefix = string.Empty;

  private string areaMapOriginalQuestNameText = string.Empty;

  private string areaMapTranslatedQuestNameText = string.Empty;

  private bool hasPendingAreaMapTranslation;

  private bool needsAreaMapApplicationRefresh = true;

  private bool ownsAreaMapNativeMutation;

  private JournalTranslationDisplayMode? lastAppliedDisplayMode;

  private DateTime nextAreaMapRetryUtc = DateTime.MinValue;

  /// <summary>
  ///     Initializes a new instance of the <see cref="AreaMapHandler" /> class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public AreaMapHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PreRefresh, this.OnAreaMapEvent);
    this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.OnAreaMapEvent);
    this.RegisterHandler(AddonEvent.PreDraw, this.OnAreaMapPreDrawEvent);
    this.RegisterHandler(AddonEvent.PreHide, this.OnAreaMapCleanupEvent);
    this.RegisterHandler(AddonEvent.PreFinalize, this.OnAreaMapCleanupEvent);
  }

  /// <summary>
  ///     Gets whether the AreaMap family should use hover tooltips.
  /// </summary>
  private bool AreaMapUsesHoverTooltips =>
      QuestAddonModeHelpers.UsesHoverTooltips(
          this.Config.AreaMapTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the AreaMap family should write translated text into the
  ///     native addon.
  /// </summary>
  private bool AreaMapWritesNativeTranslation =>
      QuestAddonModeHelpers.WritesNativeTranslation(
          this.Config.AreaMapTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the AreaMap family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool AreaMapHoverShowsOriginal =>
      QuestAddonModeHelpers.ShowsOriginalTooltips(
          this.Config.AreaMapTranslationDisplayMode);

  /// <summary>
  ///     Gets whether translated AreaMap text should be normalized before being
  ///     written to the native UI.
  /// </summary>
  private bool AreaMapShouldRemoveDiacritics =>
      QuestAddonModeHelpers.ShouldRemoveDiacritics(
          this.Config.AreaMapTranslationDisplayMode,
          this.Config.RemoveDiacriticsWhenUsingReplacementQuest);

  /// <summary>
  ///     Gets whether AreaMap may render a hover tooltip for a payload whose
  ///     translated content is ready.
  /// </summary>
  /// <param name="translatedPayloadReady">
  ///     Whether the translated payload required by the current mode is ready.
  /// </param>
  /// <returns><c>true</c> when the hover tooltip may be rendered.</returns>
  private bool CanRenderAreaMapHoverTooltip(bool translatedPayloadReady) =>
      QuestAddonModeHelpers.CanRenderHoverTooltip(
          this.Config.AreaMapTranslationDisplayMode,
          translatedPayloadReady);

  /// <summary>
  ///     Determines whether translated AreaMap quest text is ready for native
  ///     application or tooltip rendering.
  /// </summary>
  /// <param name="translatedQuestText">The translated AreaMap quest text.</param>
  /// <returns><c>true</c> when the translated text exists.</returns>
  internal static bool IsTranslatedPayloadReady(string? translatedQuestText)
  {
    return !string.IsNullOrWhiteSpace(translatedQuestText);
  }

  /// <summary>
  ///     Handles AreaMap refresh and requested-update events.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnAreaMapEvent(AddonEvent type, AddonArgs args)
  {
    this.ProcessAreaMap(args, queueMissingTranslation: true);
  }

  /// <summary>
  ///     Refreshes AreaMap application and hover targets after delayed
  ///     translations settle.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnAreaMapPreDrawEvent(AddonEvent type, AddonArgs args)
  {
    if (!this.TryGetVisibleAreaMap(out var addon))
    {
      return;
    }

    if (!this.Config.TranslateAreaMap ||
        this.DisableTranslationAccordingToState())
    {
      this.RestoreAreaMapOriginal(null);
      this.ClearAreaMapRuntimeState(removeHoverTooltips: true);
      return;
    }

    if (!this.TryResolveAreaMapQuestTextNode(
            addon,
            out var questNameNode,
            out var visibleQuestText))
    {
      this.RemoveHoverTooltipsByPrefix(AreaMapHoverPrefix);
      this.needsAreaMapApplicationRefresh = true;
      return;
    }

    var shouldRefresh =
        this.needsAreaMapApplicationRefresh ||
        this.lastAppliedDisplayMode != this.Config.AreaMapTranslationDisplayMode ||
        this.areaMapQuestNameNodeKey != (nint)questNameNode ||
        !this.AreaMapVisibleTextMatchesCurrent(visibleQuestText.QuestName) ||
        (this.hasPendingAreaMapTranslation &&
         DateTime.UtcNow >= this.nextAreaMapRetryUtc);
    if (shouldRefresh)
    {
      this.TryRefreshAreaMapPendingTranslation();
      this.ProcessAreaMap(addon, queueMissingTranslation: true);
      return;
    }

    this.ApplyAreaMapPresentation(questNameNode);
  }

  /// <summary>
  ///     Processes the visible AreaMap quest row by resolving cached or
  ///     persisted translations, applying the selected display mode, and
  ///     optionally queueing missing background translations.
  /// </summary>
  /// <param name="args">The addon lifecycle arguments, if available.</param>
  /// <param name="queueMissingTranslation">
  ///     Whether missing text should be sent to the shared translation broker.
  /// </param>
  private unsafe void ProcessAreaMap(
      AddonArgs? args,
      bool queueMissingTranslation)
  {
    if (args != null &&
        !string.Equals(args.AddonName, AreaMapAddonName, StringComparison.Ordinal))
    {
      return;
    }

    if (!this.TryGetVisibleAreaMap(out var addon))
    {
      return;
    }

    this.ProcessAreaMap(addon, queueMissingTranslation);
  }

  /// <summary>
  ///     Processes the visible AreaMap quest row from the live addon.
  /// </summary>
  /// <param name="addon">The visible AreaMap addon.</param>
  /// <param name="queueMissingTranslation">
  ///     Whether missing text should be sent to the shared translation broker.
  /// </param>
  private unsafe void ProcessAreaMap(
      AtkUnitBase* addon,
      bool queueMissingTranslation)
  {
    if (!this.Config.TranslateAreaMap ||
        this.DisableTranslationAccordingToState())
    {
      this.RestoreAreaMapOriginal(null);
      this.ClearAreaMapRuntimeState(removeHoverTooltips: true);
      return;
    }

    if (!this.TryResolveAreaMapQuestTextNode(
            addon,
            out var questNameNode,
            out var visibleQuestText))
    {
      this.RemoveHoverTooltipsByPrefix(AreaMapHoverPrefix);
      this.needsAreaMapApplicationRefresh = true;
      return;
    }

    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      return;
    }

    var originalQuestText = this.ResolveOriginalAreaMapText(
        visibleQuestText.QuestName);
    if (this.TryResolveAreaMapTranslation(
            sourceLanguage,
            originalQuestText,
            visibleQuestText.QuestName,
            out var translatedQuestText))
    {
      this.RememberAreaMapRuntimeState(
          (nint)questNameNode,
          visibleQuestText.LevelPrefix,
          originalQuestText,
          translatedQuestText);
      this.RememberAreaMapCachedText(originalQuestText, translatedQuestText);
      this.hasPendingAreaMapTranslation = false;
      this.nextAreaMapRetryUtc = DateTime.MinValue;
      this.ApplyAreaMapPresentation(questNameNode);
      return;
    }

    this.RememberAreaMapRuntimeState(
        (nint)questNameNode,
        visibleQuestText.LevelPrefix,
        originalQuestText,
        string.Empty);
    this.hasPendingAreaMapTranslation = true;
    this.nextAreaMapRetryUtc = DateTime.UtcNow + AreaMapRetryInterval;
    if (queueMissingTranslation)
    {
      this.QueueAreaMapTranslation(sourceLanguage, originalQuestText);
    }

    this.ApplyAreaMapPresentation(questNameNode);
  }

  /// <summary>
  ///     Tries to resolve the visible AreaMap addon.
  /// </summary>
  /// <param name="addon">The visible AreaMap addon.</param>
  /// <returns><c>true</c> when AreaMap is visible.</returns>
  private unsafe bool TryGetVisibleAreaMap(out AtkUnitBase* addon)
  {
    addon = AtkStage.Instance()->RaptureAtkUnitManager->GetAddonByName(
        AreaMapAddonName);
    return addon != null && addon->IsVisible;
  }

  /// <summary>
  ///     Resolves the visible AreaMap quest text nodes observed by addon
  ///     probing.
  /// </summary>
  /// <param name="addon">The visible AreaMap addon.</param>
  /// <returns>The visible quest text node addresses.</returns>
  private static unsafe List<nint> ResolveAreaMapQuestTextNodes(
      AtkUnitBase* addon)
  {
    List<nint> questTextNodes = [];
    foreach (var textNodeAddress in AddonTextNodeResolvers.ResolveReadableTextNodes(addon))
    {
      var textNode = (AtkTextNode*)textNodeAddress;
      if (TryReadAreaMapQuestText(textNode, out _))
      {
        questTextNodes.Add(textNodeAddress);
      }
    }

    return questTextNodes;
  }

  /// <summary>
  ///     Resolves the first visible AreaMap quest text node and parsed text.
  /// </summary>
  /// <param name="addon">The visible AreaMap addon.</param>
  /// <param name="questNameNode">The resolved quest-name text node.</param>
  /// <param name="visibleQuestText">The parsed visible quest text.</param>
  /// <returns><c>true</c> when a visible quest row exists.</returns>
  private unsafe bool TryResolveAreaMapQuestTextNode(
      AtkUnitBase* addon,
      out AtkTextNode* questNameNode,
      out AreaMapVisibleQuestText visibleQuestText)
  {
    questNameNode = null;
    visibleQuestText = default;

    foreach (var textNodeAddress in ResolveAreaMapQuestTextNodes(addon))
    {
      var textNode = (AtkTextNode*)textNodeAddress;
      if (!TryReadAreaMapQuestText(textNode, out visibleQuestText))
      {
        continue;
      }

      questNameNode = textNode;
      return true;
    }

    return false;
  }

  /// <summary>
  ///     Reads and parses an AreaMap quest text node.
  /// </summary>
  /// <param name="textNode">The candidate text node.</param>
  /// <param name="visibleQuestText">The parsed AreaMap quest text.</param>
  /// <returns><c>true</c> when the node contains a quest title.</returns>
  private static unsafe bool TryReadAreaMapQuestText(
      AtkTextNode* textNode,
      out AreaMapVisibleQuestText visibleQuestText)
  {
    visibleQuestText = default;
    var visibleText = ReadAreaMapTextNodeText(textNode);
    if (string.IsNullOrWhiteSpace(visibleText))
    {
      return false;
    }

    var match = AreaMapLevelQuestTextPattern.Match(visibleText.Trim());
    if (!match.Success)
    {
      return false;
    }

    var levelPrefix = match.Groups["levelPrefix"].Value;
    var questName = match.Groups["questName"].Value.Trim();
    if (string.IsNullOrWhiteSpace(questName))
    {
      return false;
    }

    visibleQuestText = new AreaMapVisibleQuestText(levelPrefix, questName);
    return true;
  }

  /// <summary>
  ///     Reads the best plain-text representation from an AreaMap text node.
  /// </summary>
  /// <param name="textNode">The AreaMap text node.</param>
  /// <returns>The readable text, or an empty string.</returns>
  private static unsafe string ReadAreaMapTextNodeText(AtkTextNode* textNode)
  {
    if (textNode == null)
    {
      return string.Empty;
    }

    var currentText = textNode->NodeText.ToString();
    if (!string.IsNullOrWhiteSpace(currentText))
    {
      return currentText;
    }

    try
    {
      var originalText = textNode->OriginalTextPointer
          .AsReadOnlySeStringSpan()
          .ExtractText();
      if (!string.IsNullOrWhiteSpace(originalText))
      {
        return originalText;
      }
    }
    catch
    {
      // Keep falling through to the legacy buffer read below.
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
  ///     Resolves a translated AreaMap quest text from local state, the DB, or
  ///     the shared translation queue.
  /// </summary>
  /// <param name="sourceLanguage">The operation-captured source identity.</param>
  /// <param name="originalQuestText">The original AreaMap quest text.</param>
  /// <param name="visibleQuestText">The current visible AreaMap text.</param>
  /// <param name="translatedQuestText">The resolved translated text.</param>
  /// <returns><c>true</c> when a translated quest text exists.</returns>
  private bool TryResolveAreaMapTranslation(
      SourceClientLanguage sourceLanguage,
      string originalQuestText,
      string visibleQuestText,
      out string translatedQuestText)
  {
    translatedQuestText = string.Empty;
    if (this.TryGetAreaMapCachedText(
            originalQuestText,
            out var cachedAreaMapText) &&
        IsTranslatedPayloadReady(cachedAreaMapText.TranslatedText))
    {
      translatedQuestText = cachedAreaMapText.TranslatedText;
      return true;
    }

    var questPlate = this.CreateQuestPlate(
        sourceLanguage,
        originalQuestText,
        string.Empty);
    var foundQuestPlate = this.FindQuestPlateByName(questPlate);
    if (foundQuestPlate == null &&
        !string.Equals(
            originalQuestText,
            visibleQuestText,
            StringComparison.Ordinal))
    {
      questPlate = this.CreateQuestPlate(
          sourceLanguage,
          visibleQuestText,
          string.Empty);
      foundQuestPlate = this.FindQuestPlateByName(questPlate);
    }

    if (foundQuestPlate != null &&
        IsTranslatedPayloadReady(foundQuestPlate.TranslatedQuestName))
    {
      translatedQuestText = foundQuestPlate.TranslatedQuestName ?? string.Empty;
      return true;
    }

    if (this.TryGetQueuedTranslation(
            BuildAreaMapCacheKey(originalQuestText),
            out var queuedAreaMapTranslation) &&
        IsTranslatedPayloadReady(queuedAreaMapTranslation))
    {
      translatedQuestText = queuedAreaMapTranslation;
      return true;
    }

    return false;
  }

  /// <summary>
  ///     Attempts to promote a pending AreaMap payload from the broker cache.
  /// </summary>
  /// <returns><c>true</c> when translated text became available.</returns>
  private bool TryRefreshAreaMapPendingTranslation()
  {
    if (!this.hasPendingAreaMapTranslation ||
        string.IsNullOrWhiteSpace(this.areaMapOriginalQuestNameText))
    {
      return false;
    }

    if (!this.TryGetQueuedTranslation(
            BuildAreaMapCacheKey(this.areaMapOriginalQuestNameText),
            out var translatedQuestText) ||
        !IsTranslatedPayloadReady(translatedQuestText))
    {
      this.nextAreaMapRetryUtc = DateTime.UtcNow + AreaMapRetryInterval;
      return false;
    }

    this.RememberAreaMapRuntimeState(
        this.areaMapQuestNameNodeKey,
        this.areaMapQuestLevelPrefix,
        this.areaMapOriginalQuestNameText,
        translatedQuestText);
    this.RememberAreaMapCachedText(
        this.areaMapOriginalQuestNameText,
        translatedQuestText);
    this.hasPendingAreaMapTranslation = false;
    this.nextAreaMapRetryUtc = DateTime.MinValue;
    return true;
  }

  /// <summary>
  ///     Enqueues one AreaMap quest text through the shared quest broker.
  /// </summary>
  /// <param name="sourceLanguage">The operation-captured source identity.</param>
  /// <param name="questText">The original AreaMap quest text.</param>
  private void QueueAreaMapTranslation(
      SourceClientLanguage sourceLanguage,
      string questText)
  {
    if (string.IsNullOrWhiteSpace(questText))
    {
      return;
    }

    this.QueueTranslation(
        BuildAreaMapCacheKey(questText),
        () => this.Translate(questText, sourceLanguage),
        translatedQuestText =>
        {
          if (!IsTranslatedPayloadReady(translatedQuestText))
          {
            return;
          }

          var translatedQuestPlate = this.CreateTranslatedQuestPlate(
              sourceLanguage,
              questText,
              string.Empty,
              translatedQuestText,
              string.Empty);

          var result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
          PluginRuntimeLog.Debug(
              $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
        });
  }

  /// <summary>
  ///     Applies the current AreaMap presentation mode to native text and hover
  ///     tooltip state.
  /// </summary>
  /// <param name="questNameNode">The live AreaMap quest text node.</param>
  private unsafe void ApplyAreaMapPresentation(AtkTextNode* questNameNode)
  {
    var translatedPayloadReady = IsTranslatedPayloadReady(
        this.areaMapTranslatedQuestNameText);
    if (this.AreaMapWritesNativeTranslation && translatedPayloadReady)
    {
      questNameNode->NodeText.SetString(
          this.BuildAreaMapTranslatedDisplayText(
              this.areaMapTranslatedQuestNameText));
      this.ownsAreaMapNativeMutation = true;
    }
    else
    {
      this.RestoreAreaMapOriginal(questNameNode);
    }

    if (this.AreaMapUsesHoverTooltips)
    {
      this.RegisterAreaMapHoverTooltip(questNameNode, translatedPayloadReady);
    }
    else
    {
      this.RemoveHoverTooltipsByPrefix(AreaMapHoverPrefix);
    }

    this.lastAppliedDisplayMode = this.Config.AreaMapTranslationDisplayMode;
    this.needsAreaMapApplicationRefresh = false;
  }

  /// <summary>
  ///     Restores the original AreaMap native text when this handler previously
  ///     wrote a native translation.
  /// </summary>
  /// <param name="questNameNode">The live AreaMap quest text node, if known.</param>
  private unsafe void RestoreAreaMapOriginal(AtkTextNode* questNameNode)
  {
    if (!this.ownsAreaMapNativeMutation ||
        string.IsNullOrWhiteSpace(this.areaMapOriginalQuestNameText))
    {
      return;
    }

    var targetNode = questNameNode != null
        ? questNameNode
        : (AtkTextNode*)this.areaMapQuestNameNodeKey;
    if (targetNode == null)
    {
      return;
    }

    targetNode->NodeText.SetString(
        BuildAreaMapDisplayText(
            this.areaMapQuestLevelPrefix,
            this.areaMapOriginalQuestNameText));
    this.ownsAreaMapNativeMutation = false;
  }

  /// <summary>
  ///     Registers or suppresses the AreaMap hover tooltip for the current
  ///     resolved text pair.
  /// </summary>
  /// <param name="questNameNode">The live AreaMap quest text node.</param>
  /// <param name="translatedPayloadReady">
  ///     Whether the translated text required by the tooltip exists.
  /// </param>
  private unsafe void RegisterAreaMapHoverTooltip(
      AtkTextNode* questNameNode,
      bool translatedPayloadReady)
  {
    if (questNameNode == null || !questNameNode->IsVisible())
    {
      return;
    }

    var originalDisplayText = BuildAreaMapDisplayText(
        this.areaMapQuestLevelPrefix,
        this.areaMapOriginalQuestNameText);
    var translatedDisplayText = translatedPayloadReady
        ? this.BuildAreaMapTranslatedDisplayText(
            this.areaMapTranslatedQuestNameText)
        : string.Empty;

    this.RegisterTranslatedHoverTooltip(
        $"{AreaMapHoverPrefix}{(nint)questNameNode:X}",
        questNameNode,
        originalDisplayText,
        translatedDisplayText,
        translatedPayloadReady: this.CanRenderAreaMapHoverTooltip(
            translatedPayloadReady),
        swapEnabled: this.AreaMapHoverShowsOriginal,
        forceEnabled: true,
        denseHitbox: true);
  }

  /// <summary>
  ///     Resolves the original AreaMap text even if the addon currently shows
  ///     a translated value written by a previous native mode.
  /// </summary>
  /// <param name="visibleText">The current visible AreaMap quest title.</param>
  /// <returns>The original source title backing the current AreaMap row.</returns>
  private string ResolveOriginalAreaMapText(string visibleText)
  {
    return QuestAddonOriginalTextHelper.ResolveOriginalVisibleText(
        visibleText,
        this.areaMapOriginalQuestNameText,
        this.GetAreaMapTranslatedDisplayText(
            this.areaMapTranslatedQuestNameText));
  }

  /// <summary>
  ///     Checks whether the visible AreaMap title belongs to the current
  ///     runtime state.
  /// </summary>
  /// <param name="visibleText">The visible AreaMap title without level prefix.</param>
  /// <returns><c>true</c> when the visible title matches current state.</returns>
  private bool AreaMapVisibleTextMatchesCurrent(string visibleText)
  {
    if (string.IsNullOrWhiteSpace(this.areaMapOriginalQuestNameText))
    {
      return false;
    }

    return string.Equals(
               visibleText,
               this.areaMapOriginalQuestNameText,
               StringComparison.Ordinal) ||
           string.Equals(
               visibleText,
               this.GetAreaMapTranslatedDisplayText(
                   this.areaMapTranslatedQuestNameText),
               StringComparison.Ordinal);
  }

  /// <summary>
  ///     Normalizes translated AreaMap text before it is written into the
  ///     native UI.
  /// </summary>
  /// <param name="translatedText">The translated text.</param>
  /// <returns>The translated text as it should be displayed natively.</returns>
  private string GetAreaMapTranslatedDisplayText(string translatedText)
  {
    if (!this.AreaMapShouldRemoveDiacritics)
    {
      return translatedText;
    }

    return this.NormalizeQuestText(translatedText ?? string.Empty);
  }

  /// <summary>
  ///     Builds an AreaMap display string from the level prefix and quest title.
  /// </summary>
  /// <param name="levelPrefix">The visible level prefix.</param>
  /// <param name="questName">The visible quest title.</param>
  /// <returns>The full display string.</returns>
  private static string BuildAreaMapDisplayText(
      string levelPrefix,
      string questName)
  {
    return $"{levelPrefix ?? string.Empty}{questName ?? string.Empty}";
  }

  /// <summary>
  ///     Builds the translated AreaMap display string.
  /// </summary>
  /// <param name="translatedText">The translated quest title.</param>
  /// <returns>The translated display string with the original level prefix.</returns>
  private string BuildAreaMapTranslatedDisplayText(string translatedText)
  {
    return BuildAreaMapDisplayText(
        this.areaMapQuestLevelPrefix,
        this.GetAreaMapTranslatedDisplayText(translatedText));
  }

  /// <summary>
  ///     Remembers the latest AreaMap runtime text pair.
  /// </summary>
  /// <param name="questNameNodeKey">The stable AreaMap text node pointer.</param>
  /// <param name="levelPrefix">The visible level prefix.</param>
  /// <param name="originalText">The current original AreaMap quest title.</param>
  /// <param name="translatedText">The current translated AreaMap quest title.</param>
  private void RememberAreaMapRuntimeState(
      nint questNameNodeKey,
      string levelPrefix,
      string originalText,
      string translatedText)
  {
    this.areaMapQuestNameNodeKey = questNameNodeKey;
    this.areaMapQuestLevelPrefix = levelPrefix ?? string.Empty;
    this.areaMapOriginalQuestNameText = originalText ?? string.Empty;
    this.areaMapTranslatedQuestNameText = translatedText ?? string.Empty;
  }

  /// <summary>
  ///     Clears AreaMap hover registrations when the addon closes.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private void OnAreaMapCleanupEvent(AddonEvent type, AddonArgs args)
  {
    if (string.Equals(args.AddonName, AreaMapAddonName, StringComparison.Ordinal))
    {
      this.ClearAreaMapRuntimeState(removeHoverTooltips: true);
    }
  }

  /// <summary>
  ///     Clears AreaMap runtime state.
  /// </summary>
  /// <param name="removeHoverTooltips">
  ///     Whether registered hover targets should also be removed.
  /// </param>
  private void ClearAreaMapRuntimeState(bool removeHoverTooltips)
  {
    this.areaMapQuestNameNodeKey = nint.Zero;
    this.areaMapQuestLevelPrefix = string.Empty;
    this.areaMapOriginalQuestNameText = string.Empty;
    this.areaMapTranslatedQuestNameText = string.Empty;
    this.hasPendingAreaMapTranslation = false;
    this.needsAreaMapApplicationRefresh = true;
    this.ownsAreaMapNativeMutation = false;
    this.lastAppliedDisplayMode = null;
    this.nextAreaMapRetryUtc = DateTime.MinValue;
    if (removeHoverTooltips)
    {
      this.RemoveHoverTooltipsByPrefix(AreaMapHoverPrefix);
    }
  }

  /// <summary>
  ///     Attempts to read the handler-local AreaMap translated-text cache.
  /// </summary>
  /// <param name="originalText">The original AreaMap quest text.</param>
  /// <param name="cachedText">The cached original/translated pair.</param>
  /// <returns>True when the local cache contains a value.</returns>
  private bool TryGetAreaMapCachedText(
      string originalText,
      out AreaMapTextCacheEntry cachedText)
  {
    if (this.areaMapTextCache.TryGetValue(
            originalText,
            out var foundCachedText))
    {
      cachedText = foundCachedText;
      return true;
    }

    cachedText = null!;
    return false;
  }

  /// <summary>
  ///     Remembers the latest translated AreaMap text pair in the handler-local
  ///     runtime cache.
  /// </summary>
  /// <param name="originalText">The original AreaMap quest text.</param>
  /// <param name="translatedText">The translated AreaMap quest text.</param>
  private void RememberAreaMapCachedText(
      string originalText,
      string translatedText)
  {
    this.areaMapTextCache[originalText ?? string.Empty] =
        new AreaMapTextCacheEntry(
            originalText ?? string.Empty,
            translatedText ?? string.Empty);
  }

  /// <summary>
  ///     Builds the shared broker cache key for one AreaMap quest text.
  /// </summary>
  /// <param name="questText">The original AreaMap quest text.</param>
  /// <returns>The stable cache key.</returns>
  private static string BuildAreaMapCacheKey(string questText)
  {
    return $"AreaMap|{questText}";
  }

  /// <summary>
  ///     Captures one parsed visible AreaMap quest row.
  /// </summary>
  /// <param name="LevelPrefix">The visible level prefix.</param>
  /// <param name="QuestName">The visible quest title without level prefix.</param>
  private readonly record struct AreaMapVisibleQuestText(
      string LevelPrefix,
      string QuestName);

  /// <summary>
  ///     Captures the handler-local AreaMap text-cache payload.
  /// </summary>
  /// <param name="OriginalText">The original AreaMap quest text.</param>
  /// <param name="TranslatedText">The translated AreaMap quest text.</param>
  private sealed record AreaMapTextCacheEntry(
      string OriginalText,
      string TranslatedText);
}
