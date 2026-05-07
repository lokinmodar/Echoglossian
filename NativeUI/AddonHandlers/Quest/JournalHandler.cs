// <copyright file="JournalHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Handles the Journal quest-list runtime inside the standalone
///     quest-handler model.
/// </summary>
internal sealed class JournalHandler : QuestAddonHandlerBase
{
  private const string JournalAddonName = "Journal";

  private const string JournalListHoverPrefix = "JournalList-";

  private readonly Dictionary<string, string> journalListTextCache =
      new(StringComparer.Ordinal);

  private readonly Dictionary<nint, QuestHoverTranslationSnapshot> journalListHoverCache =
      [];

  private readonly Dictionary<nint, string> journalListOriginalTextCache =
      [];
  private readonly HashSet<nint> journalListNativeMutationNodeKeys =
      [];

  /// <summary>
  ///     Initializes a new instance of the <see cref="JournalHandler" /> class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public JournalHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PreUpdate, this.OnJournalQuestEvent);
    this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.OnJournalQuestEvent);
    this.RegisterHandler(AddonEvent.PreHide, this.OnJournalCleanupEvent);
    this.RegisterHandler(AddonEvent.PreFinalize, this.OnJournalCleanupEvent);
  }

  /// <summary>
  ///     Gets whether Journal should use hover tooltips.
  /// </summary>
  private bool JournalUsesHoverTooltips =>
      QuestAddonModeHelpers.UsesHoverTooltips(
          this.Config.JournalTranslationDisplayMode);

  /// <summary>
  ///     Gets whether Journal should write translated text into the native
  ///     addon.
  /// </summary>
  private bool JournalWritesNativeTranslation =>
      QuestAddonModeHelpers.WritesNativeTranslation(
          this.Config.JournalTranslationDisplayMode);

  /// <summary>
  ///     Gets whether Journal hover tooltips should show the original text.
  /// </summary>
  private bool JournalHoverShowsOriginal =>
      QuestAddonModeHelpers.ShowsOriginalTooltips(
          this.Config.JournalTranslationDisplayMode);

  /// <summary>
  ///     Gets whether Journal may render a hover tooltip for a payload whose
  ///     translated content is ready.
  /// </summary>
  /// <param name="translatedPayloadReady">
  ///     Whether the translated payload required by the current mode is ready.
  /// </param>
  /// <returns><c>true</c> when the hover tooltip may be rendered.</returns>
  private bool CanRenderJournalHoverTooltip(bool translatedPayloadReady) =>
      QuestAddonModeHelpers.CanRenderHoverTooltip(
          this.Config.JournalTranslationDisplayMode,
          translatedPayloadReady);

  /// <summary>
  ///     Gets whether translated Journal text should be normalized before being
  ///     written into the native UI.
  /// </summary>
  private bool JournalShouldRemoveDiacritics =>
      QuestAddonModeHelpers.ShouldRemoveDiacritics(
          this.Config.JournalTranslationDisplayMode,
          this.Config.RemoveDiacriticsWhenUsingReplacementQuest);

  /// <summary>
  ///     Translates the active Journal quest list addon.
  /// </summary>
  private unsafe void TranslateJournalQuests()
  {
    if (!this.Config.TranslateJournal)
    {
      return;
    }

    var atkStage = AtkStage.Instance();
    var journal =
        atkStage->RaptureAtkUnitManager->GetAddonByName("Journal");
    if (journal == null || !journal->IsVisible)
    {
      return;
    }

    if (!this.JournalUsesHoverTooltips)
    {
      this.RemoveHoverTooltipsByPrefix(JournalListHoverPrefix);
    }

    try
    {
      var questListRoot = journal->GetNodeById(25);
      if (questListRoot == null || !questListRoot->IsVisible())
      {
        return;
      }

      var questListNode = questListRoot->GetAsAtkComponentNode()->Component;
      if (questListNode == null)
      {
        return;
      }

      HashSet<string> visibleJournalQuestNames = new(StringComparer.Ordinal);
      HashSet<nint> visibleJournalQuestNodeKeys = [];

      for (var i = 0; i < questListNode->UldManager.NodeListCount; i++)
      {
        if (!questListNode->UldManager.NodeList[i]->IsVisible() ||
            questListNode->UldManager.NodeList[i]->NodeId == 5)
        {
          continue;
        }

        if (questListNode->UldManager.NodeList[i]->Type ==
            NodeType.Collision ||
            questListNode->UldManager.NodeList[i]->Type == NodeType.Res)
        {
          continue;
        }

        var questItemNode =
            questListNode->UldManager.NodeList[i]->GetAsAtkComponentNode();
        var questNameNode =
            questItemNode->Component->UldManager.SearchNodeById(3);
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

        var liveQuestNameText = MemoryHelper.ReadSeStringAsString(
            out _,
            (nint)questName->NodeText.StringPtr.Value);
        var questNameNodeKey = (nint)questNameNode;
        visibleJournalQuestNodeKeys.Add(questNameNodeKey);
        var originalQuestName = this.TryGetJournalListOriginalText(
                questNameNodeKey,
                out var cachedOriginalQuestName)
            ? cachedOriginalQuestName
            : liveQuestNameText;
        visibleJournalQuestNames.Add(originalQuestName);

        if (this.TryGetJournalListCachedText(
                originalQuestName,
                out var cachedTranslatedQuestName))
        {
          if (this.JournalWritesNativeTranslation)
          {
            questName->SetText(cachedTranslatedQuestName);
            this.journalListNativeMutationNodeKeys.Add(questNameNodeKey);
          }
          else if (this.journalListNativeMutationNodeKeys.Remove(questNameNodeKey))
          {
            questName->SetText(originalQuestName);
          }

          this.RememberJournalListHover(
              questNameNodeKey,
              originalQuestName,
              cachedTranslatedQuestName);
          if (this.JournalUsesHoverTooltips &&
              this.TryGetJournalListHover(
                  questNameNodeKey,
                  out var cachedHoverTranslation))
          {
            this.RegisterTranslatedHoverTooltip(
                $"JournalList-{questNameNodeKey:X}",
                questName,
                cachedHoverTranslation.OriginalText,
                cachedHoverTranslation.TranslatedText,
                translatedPayloadReady: this.CanRenderJournalHoverTooltip(true),
                swapEnabled: this.JournalHoverShowsOriginal,
                forceEnabled: true,
                denseHitbox: true);
          }

          continue;
        }

        var questPlate = this.CreateQuestPlate(
            originalQuestName,
            string.Empty,
            string.Empty);
        var foundQuestPlate = this.FindQuestPlateByName(questPlate);
        if (foundQuestPlate == null &&
            !string.Equals(
                originalQuestName,
                liveQuestNameText,
                StringComparison.Ordinal))
        {
          questPlate = this.CreateQuestPlate(
              liveQuestNameText,
              string.Empty,
              string.Empty);
          foundQuestPlate = this.FindQuestPlateByName(questPlate);
        }

        if (foundQuestPlate != null)
        {
          originalQuestName = string.IsNullOrWhiteSpace(foundQuestPlate.QuestName)
              ? originalQuestName
              : foundQuestPlate.QuestName;
          visibleJournalQuestNames.Add(originalQuestName);
          this.RememberJournalListOriginalText(
              questNameNodeKey,
              originalQuestName);
          var translatedQuestNameReady = !string.IsNullOrWhiteSpace(
              foundQuestPlate.TranslatedQuestName);
          var translatedQuestName = string.IsNullOrWhiteSpace(
                  foundQuestPlate.TranslatedQuestName)
              ? originalQuestName
              : foundQuestPlate.TranslatedQuestName;
          if (this.JournalShouldRemoveDiacritics)
          {
            translatedQuestName = this.NormalizeQuestText(
                translatedQuestName ?? string.Empty);
          }

          if (this.JournalWritesNativeTranslation)
          {
            questName->SetText(translatedQuestName);
            this.journalListNativeMutationNodeKeys.Add(questNameNodeKey);
          }
          else if (this.journalListNativeMutationNodeKeys.Remove(questNameNodeKey))
          {
            questName->SetText(originalQuestName);
          }

          this.RememberJournalListHover(
              questNameNodeKey,
              originalQuestName,
              translatedQuestName);

          this.RememberJournalListCachedText(
              originalQuestName,
              translatedQuestName);

          if (this.JournalUsesHoverTooltips)
          {
            this.RegisterTranslatedHoverTooltip(
                $"JournalList-{questNameNodeKey:X}",
                questName,
                originalQuestName,
                translatedQuestName,
                translatedPayloadReady: this.CanRenderJournalHoverTooltip(
                    translatedQuestNameReady),
                swapEnabled: this.JournalHoverShowsOriginal,
                forceEnabled: true,
                denseHitbox: true);
          }

          continue;
        }

        this.RememberJournalListOriginalText(
            questNameNodeKey,
            originalQuestName);
        if (!this.JournalWritesNativeTranslation &&
            this.journalListNativeMutationNodeKeys.Remove(questNameNodeKey) &&
            !string.Equals(
                liveQuestNameText,
                originalQuestName,
                StringComparison.Ordinal))
        {
          questName->SetText(originalQuestName);
        }

        if (this.JournalUsesHoverTooltips)
        {
          this.RegisterTranslatedHoverTooltip(
              $"JournalList-{questNameNodeKey:X}",
              questName,
              originalQuestName,
              liveQuestNameText,
              translatedPayloadReady: this.CanRenderJournalHoverTooltip(false),
              swapEnabled: this.JournalHoverShowsOriginal,
              forceEnabled: true,
              denseHitbox: true);
        }
      }

      this.TrimJournalListRuntimeState(
          visibleJournalQuestNames,
          visibleJournalQuestNodeKeys);
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Error($"Error in JournalHandler: {e}");
    }
  }

  /// <summary>
  ///     Handles Journal quest-list refresh events.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnJournalQuestEvent(AddonEvent type, AddonArgs args)
  {
    if (!string.Equals(args.AddonName, JournalAddonName, StringComparison.Ordinal))
    {
      return;
    }

    this.TranslateJournalQuests();
  }

  /// <summary>
  ///     Clears Journal quest-list hover registrations when the addon closes.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private void OnJournalCleanupEvent(AddonEvent type, AddonArgs args)
  {
    if (!string.Equals(args.AddonName, JournalAddonName, StringComparison.Ordinal))
    {
      return;
    }

    this.journalListTextCache.Clear();
    this.journalListHoverCache.Clear();
    this.journalListOriginalTextCache.Clear();
    this.journalListNativeMutationNodeKeys.Clear();
    this.RemoveHoverTooltipsByPrefix(JournalListHoverPrefix);
  }

  /// <summary>
  ///     Attempts to get translated text for a visible Journal quest-list
  ///     entry from the current list runtime cache.
  /// </summary>
  /// <param name="originalText">The original quest name.</param>
  /// <param name="translatedText">The cached translated quest name.</param>
  /// <returns>True when the current visible Journal list already cached the quest.</returns>
  private bool TryGetJournalListCachedText(
      string originalText,
      out string translatedText)
  {
    translatedText = string.Empty;
    return !string.IsNullOrWhiteSpace(originalText) &&
           this.journalListTextCache.TryGetValue(
               originalText,
               out translatedText);
  }

  /// <summary>
  ///     Remembers a translated quest name inside the current Journal visible
  ///     list runtime cache.
  /// </summary>
  /// <param name="originalText">The original quest name.</param>
  /// <param name="translatedText">The translated quest name.</param>
  private void RememberJournalListCachedText(
      string originalText,
      string translatedText)
  {
    if (string.IsNullOrWhiteSpace(originalText) ||
        string.IsNullOrWhiteSpace(translatedText) ||
        string.Equals(originalText, translatedText, StringComparison.Ordinal))
    {
      return;
    }

    this.journalListTextCache[originalText] = translatedText;
  }

  /// <summary>
  ///     Attempts to get a local hover snapshot for a visible Journal quest
  ///     list node.
  /// </summary>
  /// <param name="nodeKey">The live quest-name node key.</param>
  /// <param name="snapshot">The cached local hover snapshot.</param>
  /// <returns>True when a local hover snapshot exists for that node.</returns>
  private bool TryGetJournalListHover(
      nint nodeKey,
      out QuestHoverTranslationSnapshot snapshot)
  {
    return this.journalListHoverCache.TryGetValue(
        nodeKey,
        out snapshot!);
  }

  /// <summary>
  ///     Remembers the hover translation pair for a visible Journal quest list
  ///     node.
  /// </summary>
  /// <param name="nodeKey">The live quest-name node key.</param>
  /// <param name="originalText">The original visible quest name.</param>
  /// <param name="translatedText">The translated visible quest name.</param>
  private void RememberJournalListHover(
      nint nodeKey,
      string originalText,
      string translatedText)
  {
    if (nodeKey == nint.Zero ||
        string.IsNullOrWhiteSpace(originalText) ||
        string.IsNullOrWhiteSpace(translatedText))
    {
      return;
    }

    this.journalListHoverCache[nodeKey] =
        new QuestHoverTranslationSnapshot(
            originalText,
            translatedText);
  }

  /// <summary>
  ///     Attempts to get the original quest title currently associated with a
  ///     visible Journal list node.
  /// </summary>
  /// <param name="nodeKey">The live quest-name node key.</param>
  /// <param name="originalText">The cached original quest title.</param>
  /// <returns>True when an original quest title is cached for that node.</returns>
  private bool TryGetJournalListOriginalText(
      nint nodeKey,
      out string originalText)
  {
    return this.journalListOriginalTextCache.TryGetValue(
        nodeKey,
        out originalText!);
  }

  /// <summary>
  ///     Remembers the original quest title associated with a visible Journal
  ///     list node so mode switches can restore native UI state correctly.
  /// </summary>
  /// <param name="nodeKey">The live quest-name node key.</param>
  /// <param name="originalText">The original quest title.</param>
  private void RememberJournalListOriginalText(
      nint nodeKey,
      string originalText)
  {
    if (nodeKey == nint.Zero || string.IsNullOrWhiteSpace(originalText))
    {
      return;
    }

    this.journalListOriginalTextCache[nodeKey] = originalText;
  }

  /// <summary>
  ///     Trims Journal quest-list runtime caches so they only keep the quest
  ///     names and node anchors visible in the current list snapshot.
  /// </summary>
  /// <param name="visibleQuestNames">The currently visible quest names.</param>
  /// <param name="visibleQuestNodeKeys">The currently visible quest node keys.</param>
  private void TrimJournalListRuntimeState(
      HashSet<string> visibleQuestNames,
      HashSet<nint> visibleQuestNodeKeys)
  {
    List<string> hiddenQuestNames = [];
    foreach (var cachedQuestName in this.journalListTextCache.Keys)
    {
      if (!visibleQuestNames.Contains(cachedQuestName))
      {
        hiddenQuestNames.Add(cachedQuestName);
      }
    }

    foreach (var hiddenQuestName in hiddenQuestNames)
    {
      this.journalListTextCache.Remove(hiddenQuestName);
    }

    List<nint> hiddenQuestNodeKeys = [];
    foreach (var cachedQuestNodeKey in this.journalListHoverCache.Keys)
    {
      if (!visibleQuestNodeKeys.Contains(cachedQuestNodeKey))
      {
        hiddenQuestNodeKeys.Add(cachedQuestNodeKey);
      }
    }

    foreach (var hiddenQuestNodeKey in hiddenQuestNodeKeys)
    {
      this.journalListHoverCache.Remove(hiddenQuestNodeKey);
      this.journalListOriginalTextCache.Remove(hiddenQuestNodeKey);
    }
  }
}
