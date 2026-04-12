// <copyright file="RecommendListHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Handles the RecommendList quest addon runtime inside the standalone
///     quest-handler model.
/// </summary>
internal sealed class RecommendListHandler : QuestAddonHandlerBase
{
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
        AddonEvent.PostRequestedUpdate,
        this.OnRecommendListEventAsync);
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
          this.RegisterTranslatedHoverTooltip(
              $"RecommendList-{questNameNodeKey:X}",
              questName,
              questNameText,
              questNameText,
              swapEnabled: this.RecommendListHoverShowsOriginal,
              forceEnabled: true,
              denseHitbox: true);
        }

        if (QuestUiTranslationCache.TryGetAppliedSnapshot(
                questNameText,
                out var translatedQuestSnapshot))
        {
          if (this.RecommendListUsesHoverTooltips &&
              QuestHoverTranslationCache.TryGet(
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
                translatedQuestSnapshot.AppliedText,
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
          // PluginLog.Debug(
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

          QuestHoverTranslationCache.Remember(
              questNameNodeKey,
              questNameText,
              translatedQuestName);
          QuestUiTranslationCache.Remember(
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

          QuestHoverTranslationCache.Remember(
              questNameNodeKey,
              questNameText,
              translatedNameText);
          QuestUiTranslationCache.Remember(
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
              // PluginLog.Debug(
              //     $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
            });
#if DEBUG
        // PluginLog.Debug(
        //     $"Name translated queued: {questNameText}");
#endif
      }

      // Then we replace the text in the nodes
      this.UpdateRecommendList();
    }
    catch (Exception e)
    {
      PluginLog.Error($"Error in UIRecommendListHandler: {e}");
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
    // PluginLog.Debug(
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
    // PluginLog.Debug(
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
          this.RegisterTranslatedHoverTooltip(
              $"RecommendList-{questNameNodeKey:X}",
              questName,
              questNameText,
              questNameText,
              swapEnabled: this.RecommendListHoverShowsOriginal,
              forceEnabled: true,
              denseHitbox: true);
        }

        if (QuestUiTranslationCache.TryGetAppliedSnapshot(
                questNameText,
                out var translatedQuestSnapshot))
        {
          if (this.RecommendListUsesHoverTooltips &&
              QuestHoverTranslationCache.TryGet(
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
                translatedQuestSnapshot.AppliedText,
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
          // PluginLog.Debug(
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
          QuestHoverTranslationCache.Remember(
              questNameNodeKey,
              questNameText,
              translatedQuestName);
          QuestUiTranslationCache.Remember(
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
          QuestHoverTranslationCache.Remember(
              questNameNodeKey,
              questNameText,
              translatedNameText);
          QuestUiTranslationCache.Remember(
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
              // PluginLog.Debug(
              //     $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
            });
#if DEBUG
        // PluginLog.Debug(
        //     $"Name translated queued: {questNameText}");
#endif
      }

      // Then we replace the text in the nodes
      this.UpdateRecommendList();
    }
    catch (Exception e)
    {
      PluginLog.Error($"Error in UIRecommendListHandler: {e}");
    }
  }
}