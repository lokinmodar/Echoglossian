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

  private readonly Dictionary<nint, RecommendListHoverEntry> recommendListHoverEntries = [];

  private readonly Dictionary<string, RecommendListTextCacheEntry> recommendListTextCache = [];

  /// <summary>
  ///     Initializes a new instance of the <see cref="RecommendListHandler" /> class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public RecommendListHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PostReceiveEvent, this.OnRecommendListEvent);
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
          this.Config.RecommendListTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the RecommendList family should write translated text
  ///     into the native addon.
  /// </summary>
  private bool RecommendListWritesNativeTranslation =>
      QuestAddonModeHelpers.WritesNativeTranslation(
          this.Config.RecommendListTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the RecommendList family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool RecommendListHoverShowsOriginal =>
      QuestAddonModeHelpers.ShowsOriginalTooltips(
          this.Config.RecommendListTranslationDisplayMode);

  /// <summary>
  ///     Gets whether translated RecommendList text should be normalized before
  ///     being written into the native UI.
  /// </summary>
  private bool RecommendListShouldRemoveDiacritics =>
      QuestAddonModeHelpers.ShouldRemoveDiacritics(
          this.Config.RecommendListTranslationDisplayMode,
          this.Config.RemoveDiacriticsWhenUsingReplacementQuest);

  /// <summary>
  ///     Updates the visible quest names from the shared translation cache.
  /// </summary>
  private unsafe void UpdateRecommendList()
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
      // Replace the text in the nodes reading from the DB
      var questListNode = recommendList->GetNodeById(5);
      if (questListNode == null || !questListNode->IsVisible())
      {
        return;
      }

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
            questItemNode->Component->UldManager.SearchNodeById(5);
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
        if (this.RecommendListUsesHoverTooltips)
        {
          this.RememberRecommendListHoverEntry(
              questNameNodeKey,
              questNameText,
              questNameText);
          this.RegisterTranslatedHoverTooltip(
              $"RecommendList-{questNameNodeKey:X}",
              questName,
              questNameText,
              questNameText,
              swapEnabled: this.RecommendListHoverShowsOriginal,
              forceEnabled: true,
              denseHitbox: true);
        }

        if (this.TryGetRecommendListCachedText(
                questNameText,
                out var translatedQuestSnapshot))
        {
          if (this.RecommendListUsesHoverTooltips &&
              this.TryGetRecommendListHoverEntry(
                  questNameNodeKey,
                  out var cachedHoverTranslation))
          {
            this.RegisterTranslatedHoverTooltip(
                $"RecommendList-{questNameNodeKey:X}",
                questName,
                cachedHoverTranslation.OriginalText,
                cachedHoverTranslation.TranslatedText,
                swapEnabled: this.RecommendListHoverShowsOriginal,
                forceEnabled: true,
                denseHitbox: true);
          }
          else if (this.RecommendListUsesHoverTooltips)
          {
            this.RegisterTranslatedHoverTooltip(
                $"RecommendList-{questNameNodeKey:X}",
                questName,
                translatedQuestSnapshot.OriginalText,
                translatedQuestSnapshot.TranslatedText,
                swapEnabled: this.RecommendListHoverShowsOriginal,
                forceEnabled: true,
                denseHitbox: true);
          }

          continue;
        }

        var questPlate = this.CreateQuestPlate(
            questNameText,
            string.Empty);
        var foundQuestPlate = this.FindQuestPlateByName(questPlate);
        if (foundQuestPlate != null)
        {
#if DEBUG
          // PluginRuntimeLog.Debug(
          //     $"Name from database: {questNameText} -> {foundQuestPlate.TranslatedQuestName}");
#endif
          var translatedQuestName = foundQuestPlate.TranslatedQuestName;

          if (this.RecommendListShouldRemoveDiacritics)
          {
            translatedQuestName = this.NormalizeQuestText(
                translatedQuestName ?? string.Empty);
          }

          if (this.RecommendListWritesNativeTranslation)
          {
            // because we are translating names, it's safer to use SetString instead of SetText
            questName->NodeText.SetString(translatedQuestName);
          }

          this.RememberRecommendListHoverEntry(
              questNameNodeKey,
              questNameText,
              translatedQuestName);
          this.RememberRecommendListCachedText(
              questNameText,
              translatedQuestName);
          if (this.RecommendListUsesHoverTooltips)
          {
            this.RegisterTranslatedHoverTooltip(
                $"RecommendList-{questNameNodeKey:X}",
                questName,
                questNameText,
                translatedQuestName,
                swapEnabled: this.RecommendListHoverShowsOriginal,
                forceEnabled: true,
                denseHitbox: true);
          }

          continue;
        }

        var cacheKey = $"RecommendList|{questNameText}";
        if (this.TryGetQueuedTranslation(
                cacheKey,
                out var cachedTranslatedName))
        {
          var translatedNameText = cachedTranslatedName;
          if (this.RecommendListShouldRemoveDiacritics)
          {
            translatedNameText = this.NormalizeQuestText(
                translatedNameText ?? string.Empty);
          }

          if (this.RecommendListWritesNativeTranslation)
          {
            // because we are translating names, it's safer to use SetString instead of SetText
            questName->NodeText.SetString(translatedNameText);
          }

          this.RememberRecommendListHoverEntry(
              questNameNodeKey,
              questNameText,
              translatedNameText);
          this.RememberRecommendListCachedText(
              questNameText,
              translatedNameText);
          if (this.RecommendListUsesHoverTooltips)
          {
            this.RegisterTranslatedHoverTooltip(
                $"RecommendList-{questNameNodeKey:X}",
                questName,
                questNameText,
                translatedNameText,
                swapEnabled: this.RecommendListHoverShowsOriginal,
                forceEnabled: true,
                denseHitbox: true);
          }

          continue;
        }

        this.QueueTranslation(
            cacheKey,
            () => this.Translate(questNameText),
            translatedNameText =>
            {
              var translatedQuestPlate = this.CreateTranslatedQuestPlate(
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
#if DEBUG
        // PluginRuntimeLog.Debug(
        //     $"Name translated queued: {questNameText}");
#endif
      }

      // Then we replace the text in the nodes
      this.UpdateRecommendList();
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Error($"Error in UIRecommendListHandler: {e}");
    }
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

    this.TranslateRecommendListHandler();
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

    if (!this.Config.TranslateRecommendList)
    {
      return;
    }

    // delay added to be sure the nodes are loaded when the player changes zones
    Task.Delay(200).ContinueWith(t => this.TranslateRecommendListHandler());
  }

  /// <summary>
  ///     Refreshes RecommendList hover targets every draw without queueing new
  ///     translations.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private void OnRecommendListHoverRefreshEvent(AddonEvent type, AddonArgs args)
  {
    if (!this.Config.TranslateRecommendList || !this.RecommendListUsesHoverTooltips)
    {
      return;
    }

    this.RefreshRecommendListHoverTooltips();
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
      this.RemoveHoverTooltipsByPrefix(RecommendListHoverPrefix);
    }
  }

  /// <summary>
  ///     Re-registers visible RecommendList hover targets using only cached or
  ///     already-persisted translations.
  /// </summary>
  private unsafe void RefreshRecommendListHoverTooltips()
  {
    var atkStage = AtkStage.Instance();
    var recommendList =
        atkStage->RaptureAtkUnitManager->GetAddonByName("RecommendList");
    if (recommendList == null || !recommendList->IsVisible)
    {
      return;
    }

    var questListNode = recommendList->GetNodeById(5);
    if (questListNode == null || !questListNode->IsVisible())
    {
      return;
    }

    var questListComponent = questListNode->GetAsAtkComponentNode()->Component;
    for (var i = 0; i < questListComponent->UldManager.NodeListCount; i++)
    {
      if (!questListComponent->UldManager.NodeList[i]->IsVisible())
      {
        continue;
      }

      if (questListComponent->UldManager.NodeList[i]->Type == NodeType.Collision ||
          questListComponent->UldManager.NodeList[i]->Type == NodeType.Res)
      {
        continue;
      }

      var questItemNode =
          questListComponent->UldManager.NodeList[i]->GetAsAtkComponentNode();
      var questNameNode = questItemNode->Component->UldManager.SearchNodeById(5);
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
      var originalText = questNameText;
      var translatedText = questNameText;

      if (this.TryGetRecommendListHoverEntry(
              questNameNodeKey,
              out var cachedHoverTranslation))
      {
        originalText = cachedHoverTranslation.OriginalText;
        translatedText = cachedHoverTranslation.TranslatedText;
      }
      else if (this.TryGetRecommendListCachedText(
                   questNameText,
                   out var translatedQuestSnapshot))
      {
        originalText = translatedQuestSnapshot.OriginalText;
        translatedText = translatedQuestSnapshot.TranslatedText;
        this.RememberRecommendListHoverEntry(
            questNameNodeKey,
            originalText,
            translatedText);
      }
      else
      {
        var questPlate = this.CreateQuestPlate(questNameText, string.Empty);
        var foundQuestPlate = this.FindQuestPlateByName(questPlate);
        if (foundQuestPlate != null)
        {
          translatedText = foundQuestPlate.TranslatedQuestName;
          if (this.RecommendListShouldRemoveDiacritics)
          {
            translatedText = this.NormalizeQuestText(
                translatedText ?? string.Empty);
          }

          this.RememberRecommendListHoverEntry(
              questNameNodeKey,
              originalText,
              translatedText);
          this.RememberRecommendListCachedText(
              originalText,
              translatedText);
        }
        else
        {
          var cacheKey = $"RecommendList|{questNameText}";
          if (this.TryGetQueuedTranslation(cacheKey, out var cachedTranslatedName))
          {
            translatedText = cachedTranslatedName;
            if (this.RecommendListShouldRemoveDiacritics)
            {
              translatedText = this.NormalizeQuestText(
                  translatedText ?? string.Empty);
            }

            this.RememberRecommendListHoverEntry(
                questNameNodeKey,
                originalText,
                translatedText);
            this.RememberRecommendListCachedText(
                originalText,
                translatedText);
          }
        }
      }

      this.RegisterTranslatedHoverTooltip(
          $"RecommendList-{questNameNodeKey:X}",
          questName,
          originalText,
          translatedText,
          swapEnabled: this.RecommendListHoverShowsOriginal,
          forceEnabled: true,
          denseHitbox: true);
    }
  }

  /// <summary>
  ///     Runs the two-pass RecommendList translation flow.
  /// </summary>
  private unsafe void TranslateRecommendListHandler()
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
      // First we store the non translated quest names in the DB
      var questListNode = recommendList->GetNodeById(5);
      if (questListNode == null || !questListNode->IsVisible())
      {
        return;
      }

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
            questItemNode->Component->UldManager.SearchNodeById(5);
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
        if (this.RecommendListUsesHoverTooltips)
        {
          this.RememberRecommendListHoverEntry(
              questNameNodeKey,
              questNameText,
              questNameText);
          this.RegisterTranslatedHoverTooltip(
              $"RecommendList-{questNameNodeKey:X}",
              questName,
              questNameText,
              questNameText,
              swapEnabled: this.RecommendListHoverShowsOriginal,
              forceEnabled: true,
              denseHitbox: true);
        }

        if (this.TryGetRecommendListCachedText(
                questNameText,
                out var translatedQuestSnapshot))
        {
          if (this.RecommendListUsesHoverTooltips &&
              this.TryGetRecommendListHoverEntry(
                  questNameNodeKey,
                  out var cachedHoverTranslation))
          {
            this.RegisterTranslatedHoverTooltip(
                $"RecommendList-{questNameNodeKey:X}",
                questName,
                cachedHoverTranslation.OriginalText,
                cachedHoverTranslation.TranslatedText,
                swapEnabled: this.RecommendListHoverShowsOriginal,
                forceEnabled: true,
                denseHitbox: true);
          }
          else if (this.RecommendListUsesHoverTooltips)
          {
            this.RegisterTranslatedHoverTooltip(
                $"RecommendList-{questNameNodeKey:X}",
                questName,
                translatedQuestSnapshot.OriginalText,
                translatedQuestSnapshot.TranslatedText,
                swapEnabled: this.RecommendListHoverShowsOriginal,
                forceEnabled: true,
                denseHitbox: true);
          }

          continue;
        }

        var questPlate = this.CreateQuestPlate(
            questNameText,
            string.Empty);
        var foundQuestPlate = this.FindQuestPlateByName(questPlate);
        if (foundQuestPlate != null)
        {
#if DEBUG
          // PluginRuntimeLog.Debug(
          //     $"Name from database: {questNameText} -> {foundQuestPlate.TranslatedQuestName}");
#endif
          var translatedQuestName = foundQuestPlate.TranslatedQuestName;
          if (this.RecommendListShouldRemoveDiacritics)
          {
            translatedQuestName = this.NormalizeQuestText(
                translatedQuestName ?? string.Empty);
          }

          // because we are translating names, it's safer to use SetString instead of SetText
          if (this.RecommendListWritesNativeTranslation)
          {
            questName->NodeText.SetString(translatedQuestName);
          }
          this.RememberRecommendListHoverEntry(
              questNameNodeKey,
              questNameText,
              translatedQuestName);
          this.RememberRecommendListCachedText(
              questNameText,
              translatedQuestName);
          if (this.RecommendListUsesHoverTooltips)
          {
            this.RegisterTranslatedHoverTooltip(
                $"RecommendList-{questNameNodeKey:X}",
                questName,
                questNameText,
                translatedQuestName,
                swapEnabled: this.RecommendListHoverShowsOriginal,
                forceEnabled: true,
                denseHitbox: true);
          }

          continue;
        }

        var cacheKey = $"RecommendList|{questNameText}";
        if (this.TryGetQueuedTranslation(
                cacheKey,
                out var cachedTranslatedName))
        {
          var translatedNameText = cachedTranslatedName;
          if (this.RecommendListShouldRemoveDiacritics)
          {
            translatedNameText = this.NormalizeQuestText(
                translatedNameText ?? string.Empty);
          }

          if (this.RecommendListWritesNativeTranslation)
          {
            // because we are translating names, it's safer to use SetString instead of SetText
            questName->NodeText.SetString(translatedNameText);
          }
          this.RememberRecommendListHoverEntry(
              questNameNodeKey,
              questNameText,
              translatedNameText);
          this.RememberRecommendListCachedText(
              questNameText,
              translatedNameText);
          if (this.RecommendListUsesHoverTooltips)
          {
            this.RegisterTranslatedHoverTooltip(
                $"RecommendList-{questNameNodeKey:X}",
                questName,
                questNameText,
                translatedNameText,
                swapEnabled: this.RecommendListHoverShowsOriginal,
                forceEnabled: true,
                denseHitbox: true);
          }

          continue;
        }

        this.QueueTranslation(
            cacheKey,
            () => this.Translate(questNameText),
            translatedNameText =>
            {
              var translatedQuestPlate = this.CreateTranslatedQuestPlate(
                  questNameText,
                  string.Empty,
                  translatedNameText,
                  string.Empty,
                  string.Empty);

              var result = this.InsertQuestPlate(
                  translatedQuestPlate);
#if DEBUG
              // PluginRuntimeLog.Debug(
              //     $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
            });
#if DEBUG
        // PluginRuntimeLog.Debug(
        //     $"Name translated queued: {questNameText}");
#endif
      }

      // Then we replace the text in the nodes
      this.UpdateRecommendList();
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Error($"Error in UIRecommendListHandler: {e}");
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
    return this.recommendListTextCache.TryGetValue(
        questNameText,
        out cachedText);
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
    return this.recommendListHoverEntries.TryGetValue(
        questNameNodeKey,
        out hoverEntry);
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


