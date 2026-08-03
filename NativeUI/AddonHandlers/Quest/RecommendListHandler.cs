// <copyright file="RecommendListHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Handles the RecommendList quest addon runtime inside the standalone
///     quest-handler model.
/// </summary>
internal sealed class RecommendListHandler : QuestAddonHandlerBase
{
  private const string RecommendListHoverPrefix = "RecommendList-";

  private static readonly TimeSpan RecommendListRetryInterval =
      TimeSpan.FromSeconds(2);

  private readonly Dictionary<nint, RecommendListHoverEntry> recommendListHoverEntries = [];

  private readonly Dictionary<string, RecommendListTextCacheEntry> recommendListTextCache = [];

  private readonly HashSet<nint> recommendListNativeMutationNodeKeys = [];

  private bool hasPendingRecommendListTranslations;

  private bool needsRecommendListApplicationRefresh = true;

  private JournalTranslationDisplayMode? lastAppliedDisplayMode;

  private DateTime nextRecommendListRetryUtc = DateTime.MinValue;

  /// <summary>
  ///     Initializes a new instance of the <see cref="RecommendListHandler" /> class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public RecommendListHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PostReceiveEvent, this.OnRecommendListEvent);
    this.RegisterHandler(AddonEvent.PreRefresh, this.OnRecommendListEvent);
    this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.OnRecommendListEvent);
    this.RegisterHandler(
        AddonEvent.PreDraw,
        this.OnRecommendListHoverRefreshEvent);
    this.RegisterHandler(
        AddonEvent.PostRequestedUpdate,
        this.OnRecommendListEventAsync);
    this.RegisterHandler(AddonEvent.PreHide, this.OnRecommendListCleanupEvent);
    this.RegisterHandler(
        AddonEvent.PreFinalize,
        this.OnRecommendListCleanupEvent);
  }

  /// <summary>
  ///     Gets whether the RecommendList family should use hover tooltips.
  /// </summary>
  private bool RecommendListUsesHoverTooltips =>
      QuestAddonModeHelpers.UsesHoverTooltips(
          this.Config.RecommendListTranslationDisplayMode,
          this.Config.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the RecommendList family should write translated text
  ///     into the native addon.
  /// </summary>
  private bool RecommendListWritesNativeTranslation =>
      QuestAddonModeHelpers.WritesNativeTranslation(
          this.Config.RecommendListTranslationDisplayMode,
          this.Config.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the RecommendList family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool RecommendListHoverShowsOriginal =>
      QuestAddonModeHelpers.ShowsOriginalTooltips(
          this.Config.RecommendListTranslationDisplayMode,
          this.Config.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether translated RecommendList text should be normalized before
  ///     being written into the native UI.
  /// </summary>
  private bool RecommendListShouldRemoveDiacritics =>
      QuestAddonModeHelpers.ShouldRemoveDiacritics(
          this.Config.RecommendListTranslationDisplayMode,
          this.Config.RemoveDiacriticsWhenUsingReplacementQuest,
          this.Config.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether RecommendList may render a hover tooltip for a payload
  ///     whose translated content is ready.
  /// </summary>
  /// <param name="translatedPayloadReady">
  ///     Whether the translated payload required by the current mode is ready.
  /// </param>
  /// <returns><c>true</c> when the hover tooltip may be rendered.</returns>
  private bool CanRenderRecommendListHoverTooltip(
      bool translatedPayloadReady) =>
      QuestAddonModeHelpers.CanRenderHoverTooltip(
          this.Config.RecommendListTranslationDisplayMode,
          translatedPayloadReady,
          this.Config.OverlayOnlyLanguage);

  /// <summary>
  ///     Determines whether the translated RecommendList title is ready for
  ///     native application or tooltip rendering.
  /// </summary>
  /// <param name="translatedQuestName">The translated quest title.</param>
  /// <returns><c>true</c> when the translated title exists.</returns>
  internal static bool IsTranslatedPayloadReady(string? translatedQuestName)
  {
    return !string.IsNullOrWhiteSpace(translatedQuestName);
  }

  /// <summary>
  ///     Updates the visible quest names from the shared translation cache.
  /// </summary>
  /// <param name="sourceLanguage">The operation-captured source identity.</param>
  private unsafe void UpdateRecommendList(
      SourceClientLanguage sourceLanguage)
  {
    this.ProcessRecommendList(sourceLanguage, queueMissingTranslations: false);
  }

  /// <summary>
  ///     Performs the immediate RecommendList translation pass.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnRecommendListEvent(AddonEvent type, AddonArgs args)
  {
#if DEBUG
    // PluginRuntimeLog.Debug(
    //     $"UiRecommendListHandler AddonEvent: {type} {args.AddonName}");
#endif

    if (this.DisableTranslationAccordingToState())
    {
      return;
    }

    if (!this.Config.TranslateRecommendList)
    {
      return;
    }

    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      return;
    }

    this.TranslateRecommendListHandler(sourceLanguage);
  }

  /// <summary>
  ///     Performs the delayed RecommendList translation pass.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private void OnRecommendListEventAsync(AddonEvent type, AddonArgs args)
  {
#if DEBUG
    // PluginRuntimeLog.Debug(
    //     $"UiRecommendListHandlerAsync AddonEvent: {type} {args.AddonName}");
#endif

    if (!this.Config.TranslateRecommendList ||
        this.DisableTranslationAccordingToState())
    {
      return;
    }

    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      return;
    }

    // delay added to be sure the nodes are loaded when the player changes zones
    Task.Delay(200).ContinueWith(
        t => this.TranslateRecommendListHandler(sourceLanguage));
  }

  /// <summary>
  ///     Refreshes RecommendList application and hover targets after delayed
  ///     translations settle.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private void OnRecommendListHoverRefreshEvent(AddonEvent type, AddonArgs args)
  {
    if (!this.Config.TranslateRecommendList)
    {
      this.RemoveHoverTooltipsByPrefix(RecommendListHoverPrefix);
      this.lastAppliedDisplayMode = null;
      this.needsRecommendListApplicationRefresh = true;
      return;
    }

    if (this.DisableTranslationAccordingToState())
    {
      this.RemoveHoverTooltipsByPrefix(RecommendListHoverPrefix);
      this.lastAppliedDisplayMode = null;
      this.needsRecommendListApplicationRefresh = true;
      return;
    }

    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      return;
    }

    var shouldRefresh =
        this.needsRecommendListApplicationRefresh ||
        this.lastAppliedDisplayMode !=
        this.Config.RecommendListTranslationDisplayMode ||
        (this.hasPendingRecommendListTranslations &&
         DateTime.UtcNow >= this.nextRecommendListRetryUtc);
    if (shouldRefresh)
    {
      this.TranslateRecommendListHandler(sourceLanguage);
      return;
    }

    if (this.RecommendListUsesHoverTooltips)
    {
      this.RefreshRecommendListHoverTooltips(sourceLanguage);
    }
    else
    {
      this.RemoveHoverTooltipsByPrefix(RecommendListHoverPrefix);
    }
  }

  /// <summary>
  ///     Clears RecommendList hover registrations when the addon closes.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private void OnRecommendListCleanupEvent(AddonEvent type, AddonArgs args)
  {
    if (string.Equals(args.AddonName, "RecommendList", StringComparison.Ordinal))
    {
      this.recommendListHoverEntries.Clear();
      this.recommendListNativeMutationNodeKeys.Clear();
      this.RemoveHoverTooltipsByPrefix(RecommendListHoverPrefix);
      this.hasPendingRecommendListTranslations = false;
      this.needsRecommendListApplicationRefresh = true;
      this.lastAppliedDisplayMode = null;
      this.nextRecommendListRetryUtc = DateTime.MinValue;
    }
  }

  /// <summary>
  ///     Re-registers visible RecommendList hover targets using only cached or
  ///     already-persisted translations.
  /// </summary>
  /// <param name="sourceLanguage">The operation-captured source identity.</param>
  private unsafe void RefreshRecommendListHoverTooltips(
      SourceClientLanguage sourceLanguage)
  {
    this.ProcessRecommendList(sourceLanguage, queueMissingTranslations: false);
  }

  /// <summary>
  ///     Runs the two-pass RecommendList translation flow.
  /// </summary>
  /// <param name="sourceLanguage">The operation-captured source identity.</param>
  private unsafe void TranslateRecommendListHandler(
      SourceClientLanguage sourceLanguage)
  {
    this.ProcessRecommendList(sourceLanguage, queueMissingTranslations: true);
    this.UpdateRecommendList(sourceLanguage);
  }

  /// <summary>
  ///     Processes the visible RecommendList rows by resolving cached or
  ///     persisted translations, applying the selected display mode, and
  ///     optionally queueing missing background translations.
  /// </summary>
  /// <param name="sourceLanguage">The operation-captured source identity.</param>
  /// <param name="queueMissingTranslations">
  ///     Whether missing rows should be sent to the shared translation broker.
  /// </param>
  private unsafe void ProcessRecommendList(
      SourceClientLanguage sourceLanguage,
      bool queueMissingTranslations)
  {
    var atkStage = AtkStage.Instance();
    var recommendList =
        atkStage->RaptureAtkUnitManager->GetAddonByName("RecommendList");
    if (recommendList == null || !recommendList->IsVisible)
    {
      return;
    }

    try
    {
      var questListNode = recommendList->GetNodeById(5);
      if (questListNode == null || !questListNode->IsVisible())
      {
        return;
      }

      var hasPendingTranslations = false;
      HashSet<nint> visibleQuestNameNodeKeys = [];
      var questListComponent =
          questListNode->GetAsAtkComponentNode()->Component;
      for (var i = 0;
           i < questListComponent->UldManager.NodeListCount;
           i++)
      {
        if (!questListComponent->UldManager.NodeList[i]->IsVisible())
        {
          continue;
        }

        if (questListComponent->UldManager.NodeList[i]->Type ==
            NodeType.Collision ||
            questListComponent->UldManager.NodeList[i]->Type ==
            NodeType.Res)
        {
          continue;
        }

        var questItemNode =
            questListComponent->UldManager.NodeList[i]->
                GetAsAtkComponentNode();
        var questNameNode =
            questItemNode->Component->UldManager.SearchNodeById(6);
        if (questNameNode == null || !questNameNode->IsVisible() ||
            questNameNode->Type != NodeType.Text)
        {
          continue;
        }

        var questName = questNameNode->GetAsAtkTextNode();
        if (questName->NodeText.IsEmpty)
        {
          continue;
        }

        var questNameText = MemoryHelper.ReadSeStringAsString(
            out _,
            (nint)questName->NodeText.StringPtr.Value);
        var questNameNodeKey = (nint)questNameNode;
        visibleQuestNameNodeKeys.Add(questNameNodeKey);
        var originalQuestNameText = this.ResolveOriginalRecommendListText(
            questNameNodeKey,
            questNameText);

        if (this.TryResolveRecommendListTranslation(
                sourceLanguage,
                originalQuestNameText,
                questNameText,
                out var translatedQuestName))
        {
          var displayText =
              this.GetRecommendListTranslatedDisplayText(translatedQuestName);
          if (this.RecommendListWritesNativeTranslation)
          {
            // Because we are translating names, SetString is safer than SetText.
            questName->NodeText.SetString(displayText);
            this.recommendListNativeMutationNodeKeys.Add(questNameNodeKey);
          }
          else if (this.recommendListNativeMutationNodeKeys.Remove(
                       questNameNodeKey))
          {
            questName->NodeText.SetString(originalQuestNameText);
          }

          this.RememberRecommendListHoverEntry(
              questNameNodeKey,
              originalQuestNameText,
              displayText);
          this.RememberRecommendListCachedText(
              originalQuestNameText,
              displayText);
          if (this.RecommendListUsesHoverTooltips)
          {
            this.RegisterTranslatedHoverTooltip(
                $"RecommendList-{questNameNodeKey:X}",
                questName,
                originalQuestNameText,
                displayText,
                translatedPayloadReady:
                    this.CanRenderRecommendListHoverTooltip(true),
                swapEnabled: this.RecommendListHoverShowsOriginal,
                forceEnabled: true,
                denseHitbox: true);
          }

          continue;
        }

        hasPendingTranslations = true;
        if (!this.RecommendListWritesNativeTranslation &&
            this.recommendListNativeMutationNodeKeys.Remove(
                questNameNodeKey))
        {
          questName->NodeText.SetString(originalQuestNameText);
        }

        if (this.RecommendListUsesHoverTooltips)
        {
          this.RegisterTranslatedHoverTooltip(
              $"RecommendList-{questNameNodeKey:X}",
              questName,
              originalQuestNameText,
              questNameText,
              translatedPayloadReady:
                  this.CanRenderRecommendListHoverTooltip(false),
              swapEnabled: this.RecommendListHoverShowsOriginal,
              forceEnabled: true,
              denseHitbox: true);
        }

        if (queueMissingTranslations)
        {
          this.QueueRecommendListTranslation(
              sourceLanguage,
              originalQuestNameText);
        }
#if DEBUG
        // PluginRuntimeLog.Debug(
        //     $"Name translated queued: {originalQuestNameText}");
#endif
      }

      this.TrimRecommendListRuntimeState(visibleQuestNameNodeKeys);
      this.hasPendingRecommendListTranslations = hasPendingTranslations;
      this.nextRecommendListRetryUtc = hasPendingTranslations
          ? DateTime.UtcNow + RecommendListRetryInterval
          : DateTime.MinValue;
      this.lastAppliedDisplayMode =
          this.Config.RecommendListTranslationDisplayMode;
      this.needsRecommendListApplicationRefresh = false;
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Error($"Error in UIRecommendListHandler: {e}");
    }
  }

  /// <summary>
  ///     Resolves a translated RecommendList title from local state, the DB, or
  ///     the shared translation queue.
  /// </summary>
  /// <param name="sourceLanguage">The operation-captured source identity.</param>
  /// <param name="originalQuestNameText">The original quest title.</param>
  /// <param name="visibleQuestNameText">The current visible node text.</param>
  /// <param name="translatedQuestName">The resolved translated title.</param>
  /// <returns><c>true</c> when a translated title exists.</returns>
  private bool TryResolveRecommendListTranslation(
      SourceClientLanguage sourceLanguage,
      string originalQuestNameText,
      string visibleQuestNameText,
      out string translatedQuestName)
  {
    translatedQuestName = string.Empty;
    if (this.TryGetRecommendListCachedText(
            originalQuestNameText,
            out var translatedQuestSnapshot) &&
        IsTranslatedPayloadReady(translatedQuestSnapshot.TranslatedText))
    {
      translatedQuestName = translatedQuestSnapshot.TranslatedText;
      return true;
    }

    var questPlate = this.CreateQuestPlate(
        sourceLanguage,
        originalQuestNameText,
        string.Empty);
    var foundQuestPlate = this.FindQuestPlateByName(questPlate);
    if (foundQuestPlate == null &&
        !string.Equals(
            originalQuestNameText,
            visibleQuestNameText,
            StringComparison.Ordinal))
    {
      questPlate = this.CreateQuestPlate(
          sourceLanguage,
          visibleQuestNameText,
          string.Empty);
      foundQuestPlate = this.FindQuestPlateByName(questPlate);
    }

    if (foundQuestPlate != null &&
        IsTranslatedPayloadReady(foundQuestPlate.TranslatedQuestName))
    {
      translatedQuestName = foundQuestPlate.TranslatedQuestName ?? string.Empty;
      return true;
    }

    if (this.TryGetQueuedTranslation(
            BuildRecommendListCacheKey(originalQuestNameText),
            out var cachedTranslatedName) &&
        IsTranslatedPayloadReady(cachedTranslatedName))
    {
      translatedQuestName = cachedTranslatedName;
      return true;
    }

    return false;
  }

  /// <summary>
  ///     Enqueues one RecommendList title through the shared quest broker.
  /// </summary>
  /// <param name="sourceLanguage">The operation-captured source identity.</param>
  /// <param name="questNameText">The original quest title.</param>
  private void QueueRecommendListTranslation(
      SourceClientLanguage sourceLanguage,
      string questNameText)
  {
    if (string.IsNullOrWhiteSpace(questNameText))
    {
      return;
    }

    this.QueueTranslation(
        BuildRecommendListCacheKey(questNameText),
        () => this.Translate(questNameText, sourceLanguage),
        translatedNameText =>
        {
          if (!IsTranslatedPayloadReady(translatedNameText))
          {
            return;
          }

          var translatedQuestPlate = this.CreateTranslatedQuestPlate(
              sourceLanguage,
              questNameText,
              string.Empty,
              translatedNameText,
              string.Empty,
              string.Empty);

          var result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
          // PluginRuntimeLog.Debug(
          //     $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
        });
  }

  /// <summary>
  ///     Builds the shared broker cache key for one RecommendList title.
  /// </summary>
  /// <param name="questNameText">The original quest title.</param>
  /// <returns>The stable cache key.</returns>
  private static string BuildRecommendListCacheKey(string questNameText)
  {
    return $"RecommendList|{questNameText}";
  }

  /// <summary>
  ///     Resolves the original title for a visible RecommendList node, even if
  ///     the node currently contains text written by a previous native mode.
  /// </summary>
  /// <param name="questNameNodeKey">The stable node pointer key.</param>
  /// <param name="visibleText">The text currently visible in the native node.</param>
  /// <returns>The original quest title backing this node.</returns>
  private string ResolveOriginalRecommendListText(
      nint questNameNodeKey,
      string visibleText)
  {
    if (this.TryGetRecommendListHoverEntry(
            questNameNodeKey,
            out var previousEntry) &&
        !string.IsNullOrWhiteSpace(previousEntry.OriginalText))
    {
      return QuestAddonOriginalTextHelper.ResolveOriginalVisibleText(
          visibleText,
          previousEntry.OriginalText,
          previousEntry.TranslatedText);
    }

    return visibleText;
  }

  /// <summary>
  ///     Normalizes translated RecommendList text before native application.
  /// </summary>
  /// <param name="translatedText">The translated text.</param>
  /// <returns>The translated text as it should be displayed.</returns>
  private string GetRecommendListTranslatedDisplayText(string translatedText)
  {
    if (!this.RecommendListShouldRemoveDiacritics)
    {
      return translatedText;
    }

    return this.NormalizeQuestText(translatedText ?? string.Empty);
  }

  /// <summary>
  ///     Removes stale node-scoped runtime state for rows that are no longer
  ///     visible in the current RecommendList payload.
  /// </summary>
  /// <param name="visibleQuestNameNodeKeys">The currently visible node keys.</param>
  private void TrimRecommendListRuntimeState(
      IReadOnlySet<nint> visibleQuestNameNodeKeys)
  {
    foreach (var questNameNodeKey in this.recommendListHoverEntries.Keys.ToList())
    {
      if (visibleQuestNameNodeKeys.Contains(questNameNodeKey))
      {
        continue;
      }

      this.recommendListHoverEntries.Remove(questNameNodeKey);
      this.recommendListNativeMutationNodeKeys.Remove(questNameNodeKey);
    }
  }

  /// <summary>
  ///     Attempts to read the handler-local RecommendList translated-text
  ///     cache.
  /// </summary>
  /// <param name="questNameText">The original visible quest name.</param>
  /// <param name="cachedText">The locally cached text pair.</param>
  /// <returns>True when a local cached text pair exists.</returns>
  private bool TryGetRecommendListCachedText(
      string questNameText,
      out RecommendListTextCacheEntry cachedText)
  {
    if (this.recommendListTextCache.TryGetValue(
            questNameText,
            out var foundCachedText))
    {
      cachedText = foundCachedText;
      return true;
    }

    cachedText = null!;
    return false;
  }

  /// <summary>
  ///     Remembers the latest translated RecommendList text pair in the
  ///     handler-local runtime cache.
  /// </summary>
  /// <param name="originalText">The original visible quest name.</param>
  /// <param name="translatedText">The translated quest name.</param>
  private void RememberRecommendListCachedText(
      string originalText,
      string translatedText)
  {
    this.recommendListTextCache[originalText ?? string.Empty] =
        new RecommendListTextCacheEntry(
            originalText ?? string.Empty,
            translatedText ?? string.Empty);
  }

  /// <summary>
  ///     Attempts to read the handler-local hover payload for one visible
  ///     RecommendList node.
  /// </summary>
  /// <param name="questNameNodeKey">The stable node pointer key.</param>
  /// <param name="hoverEntry">The cached hover entry.</param>
  /// <returns>True when a cached hover entry exists.</returns>
  private bool TryGetRecommendListHoverEntry(
      nint questNameNodeKey,
      out RecommendListHoverEntry hoverEntry)
  {
    if (this.recommendListHoverEntries.TryGetValue(
            questNameNodeKey,
            out var foundHoverEntry))
    {
      hoverEntry = foundHoverEntry;
      return true;
    }

    hoverEntry = null!;
    return false;
  }

  /// <summary>
  ///     Remembers the latest hover payload for one visible RecommendList
  ///     node.
  /// </summary>
  /// <param name="questNameNodeKey">The stable node pointer key.</param>
  /// <param name="originalText">The original visible quest name.</param>
  /// <param name="translatedText">The translated quest name.</param>
  private void RememberRecommendListHoverEntry(
      nint questNameNodeKey,
      string originalText,
      string translatedText)
  {
    this.recommendListHoverEntries[questNameNodeKey] =
        new RecommendListHoverEntry(
            originalText ?? string.Empty,
            translatedText ?? string.Empty);
  }

  /// <summary>
  ///     Captures the handler-local RecommendList text-cache payload.
  /// </summary>
  /// <param name="OriginalText">The original visible quest name.</param>
  /// <param name="TranslatedText">The translated quest name.</param>
  private sealed record RecommendListTextCacheEntry(
      string OriginalText,
      string TranslatedText);

  /// <summary>
  ///     Captures the handler-local RecommendList hover payload.
  /// </summary>
  /// <param name="OriginalText">The original visible quest name.</param>
  /// <param name="TranslatedText">The translated quest name.</param>
  private sealed record RecommendListHoverEntry(
      string OriginalText,
      string TranslatedText);
}


