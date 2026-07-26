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

  private static readonly TimeSpan JournalRetryInterval =
      TimeSpan.FromSeconds(2);

  private readonly Dictionary<nint, QuestHoverTranslationSnapshot> journalListHoverCache =
      [];

  private readonly Dictionary<nint, string> journalListOriginalTextCache =
      [];
  private readonly HashSet<nint> journalListNativeMutationNodeKeys =
      [];

  private bool hasPendingJournalTranslations;

  private int? lastVisibleJournalSignature;

  private JournalTranslationDisplayMode? lastAppliedDisplayMode;

  private DateTime nextJournalRetryUtc = DateTime.MinValue;

  /// <summary>
  ///     Initializes a new instance of the <see cref="JournalHandler" /> class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public JournalHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PreUpdate, this.OnJournalQuestEvent);
    this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.OnJournalQuestEvent);
    this.RegisterHandler(AddonEvent.PreDraw, this.OnJournalPreDrawEvent);
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
  /// <param name="sourceLanguage">
  /// The operation-captured source identity, or no value for disabled cleanup.
  /// </param>
  private unsafe void TranslateJournalQuests(
      SourceClientLanguage? sourceLanguage)
  {
    if (!TryGetVisibleJournal(out var journal))
    {
      return;
    }

    if (!this.Config.TranslateJournal ||
        this.DisableTranslationAccordingToState())
    {
      this.RestoreJournalOriginals(journal);
      this.ClearJournalRuntimeState();
      return;
    }

    if (!sourceLanguage.HasValue)
    {
      return;
    }

    var operationSourceLanguage = sourceLanguage.Value;

    this.RemoveHoverTooltipsByPrefix(JournalListHoverPrefix);

    var hasPendingTranslations = false;
    try
    {
      if (!TryGetJournalQuestListNode(journal, out var questListNode))
      {
        return;
      }

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
        var originalQuestName = this.TryGetJournalListOriginalTextForLiveText(
                questNameNodeKey,
                liveQuestNameText,
                out var cachedOriginalQuestName)
            ? cachedOriginalQuestName
            : liveQuestNameText;

        if (this.TryResolveJournalQuestPlate(
                operationSourceLanguage,
                originalQuestName,
                out var foundQuestPlate,
                out var questId))
        {
          this.RememberJournalListOriginalText(
              questNameNodeKey,
              originalQuestName);
          var translatedQuestNameReady = !string.IsNullOrWhiteSpace(
              foundQuestPlate?.TranslatedQuestName);
          hasPendingTranslations |= !translatedQuestNameReady;
          if (!translatedQuestNameReady)
          {
            this.RequestAcceptedQuestPrefetch(questId);
          }

          var translatedQuestName = string.IsNullOrWhiteSpace(
                  foundQuestPlate?.TranslatedQuestName)
              ? originalQuestName
              : foundQuestPlate.TranslatedQuestName;
          if (this.JournalShouldRemoveDiacritics)
          {
            translatedQuestName = this.NormalizeQuestText(
                translatedQuestName ?? string.Empty);
          }

          if (this.JournalWritesNativeTranslation && translatedQuestNameReady)
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

        this.ForgetJournalListNodeState(questNameNodeKey);
      }

      this.TrimJournalListRuntimeState(visibleJournalQuestNodeKeys);
      this.hasPendingJournalTranslations = hasPendingTranslations;
      this.nextJournalRetryUtc = hasPendingTranslations
          ? DateTime.UtcNow + JournalRetryInterval
          : DateTime.MinValue;
      this.lastAppliedDisplayMode = this.Config.JournalTranslationDisplayMode;
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

    if (!this.Config.TranslateJournal ||
        this.DisableTranslationAccordingToState())
    {
      this.TranslateJournalQuests(null);
      return;
    }

    if (!TryGetVisibleJournal(out var journal))
    {
      return;
    }

    if (this.TryComputeVisibleJournalSignature(
            journal,
            out var visibleJournalSignature) &&
        this.lastVisibleJournalSignature.HasValue &&
        visibleJournalSignature == this.lastVisibleJournalSignature.Value)
    {
      return;
    }

    this.lastVisibleJournalSignature = visibleJournalSignature;
    this.RestoreJournalOriginals(journal);
    this.hasPendingJournalTranslations = true;
    this.nextJournalRetryUtc = DateTime.MinValue;
    this.lastAppliedDisplayMode = null;
  }

  /// <summary>
  ///     Computes one stable signature for the currently visible Journal quest
  ///     rows so refresh work only runs when the visible list changes.
  /// </summary>
  /// <param name="journal">The visible Journal addon.</param>
  /// <param name="visibleJournalSignature">
  ///     The stable signature for the currently visible quest rows.
  /// </param>
  /// <returns>
  ///     True when the visible Journal quest-list component was available.
  /// </returns>
  private unsafe bool TryComputeVisibleJournalSignature(
      AtkUnitBase* journal,
      out int visibleJournalSignature)
  {
    visibleJournalSignature = 0;
    if (!TryGetJournalQuestListNode(journal, out var questListNode))
    {
      return false;
    }

    HashCode hash = default;
    for (var i = 0; i < questListNode->UldManager.NodeListCount; i++)
    {
      var rowNode = questListNode->UldManager.NodeList[i];
      if (rowNode == null ||
          !rowNode->IsVisible() ||
          rowNode->NodeId == 5 ||
          rowNode->Type == NodeType.Collision ||
          rowNode->Type == NodeType.Res)
      {
        continue;
      }

      var questItemNode = rowNode->GetAsAtkComponentNode();
      if (questItemNode == null || questItemNode->Component == null)
      {
        continue;
      }

      var questNameNode = questItemNode->Component->UldManager.SearchNodeById(3);
      if (questNameNode == null ||
          !questNameNode->IsVisible() ||
          questNameNode->Type != NodeType.Text)
      {
        continue;
      }

      var questName = questNameNode->GetAsAtkTextNode();
      if (questName == null || questName->NodeText.IsEmpty)
      {
        continue;
      }

      var liveQuestNameText = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)questName->NodeText.StringPtr.Value);
      var questNameNodeKey = (nint)questNameNode;
      var signatureQuestName =
          this.TryGetJournalListOriginalTextForLiveText(
              questNameNodeKey,
              liveQuestNameText,
              out var cachedOriginalQuestName)
              ? cachedOriginalQuestName
              : liveQuestNameText;
      hash.Add(i);
      hash.Add(signatureQuestName, StringComparer.Ordinal);
    }

    visibleJournalSignature = hash.ToHashCode();
    return true;
  }

  /// <summary>
  ///     Retries Journal quest-list application after delayed DB persistence
  ///     and reacts to display-mode changes while the addon remains open.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnJournalPreDrawEvent(AddonEvent type, AddonArgs args)
  {
    if (!string.Equals(args.AddonName, JournalAddonName, StringComparison.Ordinal))
    {
      return;
    }

    if (!TryGetVisibleJournal(out var journal))
    {
      return;
    }

    if (!this.Config.TranslateJournal ||
        this.DisableTranslationAccordingToState())
    {
      this.RestoreJournalOriginals(journal);
      this.ClearJournalRuntimeState();
      return;
    }

    var shouldRefresh =
        this.lastAppliedDisplayMode != this.Config.JournalTranslationDisplayMode ||
        (this.hasPendingJournalTranslations &&
         DateTime.UtcNow >= this.nextJournalRetryUtc);
    if (!shouldRefresh)
    {
      return;
    }

    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      return;
    }

    this.TranslateJournalQuests(sourceLanguage);
  }

  /// <summary>
  ///     Clears Journal quest-list hover registrations when the addon closes.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnJournalCleanupEvent(AddonEvent type, AddonArgs args)
  {
    if (!string.Equals(args.AddonName, JournalAddonName, StringComparison.Ordinal))
    {
      return;
    }

    if (TryGetVisibleJournal(out var journal))
    {
      this.RestoreJournalOriginals(journal);
    }

    this.ClearJournalRuntimeState();
  }

  /// <inheritdoc />
  public override unsafe void OnPluginUnload()
  {
    if (TryGetVisibleJournal(out var journal))
    {
      this.RestoreJournalOriginals(journal);
    }

    this.ClearJournalRuntimeState();
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
  ///     Tries to get the original quest title associated with a live Journal
  ///     list node, discarding stale state when the game's recycled node now
  ///     displays another quest title.
  /// </summary>
  /// <param name="nodeKey">The live quest-name node key.</param>
  /// <param name="liveText">The text currently rendered by the game.</param>
  /// <param name="originalText">The validated original quest title.</param>
  /// <returns>True when the cached snapshot still belongs to this live node.</returns>
  private bool TryGetJournalListOriginalTextForLiveText(
      nint nodeKey,
      string liveText,
      out string originalText)
  {
    if (!this.TryGetJournalListOriginalText(nodeKey, out originalText))
    {
      return false;
    }

    var translatedText = this.TryGetJournalListHover(
            nodeKey,
            out var snapshot)
        ? snapshot.TranslatedText
        : string.Empty;
    if (MatchesJournalListNodeSnapshot(
            liveText,
            originalText,
            translatedText))
    {
      return true;
    }

    this.ForgetJournalListNodeState(nodeKey);
    originalText = string.Empty;
    return false;
  }

  /// <summary>
  ///     Gets whether one live Journal list text still belongs to a cached
  ///     source-and-translation snapshot for the same node.
  /// </summary>
  /// <param name="liveText">The text currently rendered by the game.</param>
  /// <param name="originalText">The cached source title.</param>
  /// <param name="translatedText">The cached translated title.</param>
  /// <returns>True when the live text belongs to the cached snapshot.</returns>
  internal static bool MatchesJournalListNodeSnapshot(
      string? liveText,
      string? originalText,
      string? translatedText)
  {
    return string.Equals(liveText, originalText, StringComparison.Ordinal) ||
           (!string.IsNullOrWhiteSpace(translatedText) &&
            string.Equals(liveText, translatedText, StringComparison.Ordinal));
  }

  /// <summary>
  ///     Removes state owned by one Journal list node after the game recycles
  ///     that node for another visible quest row.
  /// </summary>
  /// <param name="nodeKey">The recycled quest-name node key.</param>
  private void ForgetJournalListNodeState(nint nodeKey)
  {
    this.journalListHoverCache.Remove(nodeKey);
    this.journalListOriginalTextCache.Remove(nodeKey);
    this.journalListNativeMutationNodeKeys.Remove(nodeKey);
  }

  /// <summary>
  ///     Resolves one visible Journal title through the same canonical
  ///     quest-and-progress path used by JournalDetail and ToDoList.
  /// </summary>
  /// <param name="sourceLanguage">The captured source client language.</param>
  /// <param name="originalQuestName">The visible original quest title.</param>
  /// <param name="foundQuestPlate">
  ///     The persisted canonical quest row, if any.
  /// </param>
  /// <param name="questId">
  ///     The accepted live quest identifier that should feed the shared
  ///     accepted-quest prefetch runtime.
  /// </param>
  /// <returns>
  ///     True when the visible title resolves to one of the player's accepted
  ///     quests, even if canonical progress data is still pending.
  /// </returns>
  private bool TryResolveJournalQuestPlate(
      SourceClientLanguage sourceLanguage,
      string originalQuestName,
      out QuestPlate? foundQuestPlate,
      out uint questId)
  {
    foundQuestPlate = null;
    questId = 0;

    if (!QuestLuminaResolver.TryResolveQuestId(
            originalQuestName,
            out var questIdText) ||
        !QuestProgressResolver.TryResolveAcceptedQuestId(
            questIdText,
            out questId))
    {
      return false;
    }

    if (!QuestProgressResolver.TryResolveQuestProgress(
            questId.ToString(CultureInfo.InvariantCulture),
            out var questProgressSnapshot))
    {
      return true;
    }

    var questCanonicalData = QuestCanonicalData.Create(
        questProgressSnapshot,
        GetGameVersion());
    var questPlate = questCanonicalData.ToQuestPlate(
        sourceLanguage.PersistenceCode,
        RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(this.Config.Lang),
        this.Config.ChosenTransEngine,
        DateTime.Now);
    foundQuestPlate = this.FindQuestPlate(questPlate);
    return true;
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
  ///     Tries to resolve the visible Journal addon.
  /// </summary>
  /// <param name="journal">The visible Journal addon.</param>
  /// <returns>True when the Journal addon is visible.</returns>
  private static unsafe bool TryGetVisibleJournal(out AtkUnitBase* journal)
  {
    journal = AtkStage.Instance()->RaptureAtkUnitManager->GetAddonByName(
        JournalAddonName);
    return journal != null && journal->IsVisible;
  }

  /// <summary>
  ///     Tries to resolve the visible Journal quest-list component.
  /// </summary>
  /// <param name="journal">The live Journal addon.</param>
  /// <param name="questListNode">The visible quest-list component.</param>
  /// <returns>True when the visible quest-list component was resolved.</returns>
  private static unsafe bool TryGetJournalQuestListNode(
      AtkUnitBase* journal,
      out AtkComponentBase* questListNode)
  {
    questListNode = null;
    if (journal == null || !journal->IsVisible)
    {
      return false;
    }

    var questListRoot = journal->GetNodeById(25);
    if (questListRoot == null || !questListRoot->IsVisible())
    {
      return false;
    }

    var questListComponentNode = questListRoot->GetAsAtkComponentNode();
    if (questListComponentNode == null || questListComponentNode->Component == null)
    {
      return false;
    }

    questListNode = questListComponentNode->Component;
    return true;
  }

  /// <summary>
  ///     Restores original Journal quest-list text for any row that is
  ///     currently mutated by this handler.
  /// </summary>
  /// <param name="journal">The live Journal addon.</param>
  private unsafe void RestoreJournalOriginals(AtkUnitBase* journal)
  {
    if (!TryGetJournalQuestListNode(journal, out var questListNode))
    {
      this.RemoveHoverTooltipsByPrefix(JournalListHoverPrefix);
      return;
    }

    for (var i = 0; i < questListNode->UldManager.NodeListCount; i++)
    {
      var rowNode = questListNode->UldManager.NodeList[i];
      if (rowNode == null ||
          !rowNode->IsVisible() ||
          rowNode->NodeId == 5 ||
          rowNode->Type == NodeType.Collision ||
          rowNode->Type == NodeType.Res)
      {
        continue;
      }

      var questItemNode = rowNode->GetAsAtkComponentNode();
      if (questItemNode == null || questItemNode->Component == null)
      {
        continue;
      }

      var questNameNode = questItemNode->Component->UldManager.SearchNodeById(3);
      if (questNameNode == null ||
          !questNameNode->IsVisible() ||
          questNameNode->Type != NodeType.Text)
      {
        continue;
      }

      var questNameNodeKey = (nint)questNameNode;
      if (!this.journalListNativeMutationNodeKeys.Remove(questNameNodeKey) ||
          !this.TryGetJournalListOriginalText(
              questNameNodeKey,
              out var originalQuestName))
      {
        continue;
      }

      var questName = questNameNode->GetAsAtkTextNode();
      var liveQuestNameText = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)questName->NodeText.StringPtr.Value);
      var translatedQuestName = this.TryGetJournalListHover(
              questNameNodeKey,
              out var snapshot)
          ? snapshot.TranslatedText
          : string.Empty;
      if (MatchesJournalListNodeSnapshot(
              liveQuestNameText,
              originalQuestName,
              translatedQuestName))
      {
        questName->SetText(originalQuestName);
      }
      else
      {
        this.ForgetJournalListNodeState(questNameNodeKey);
      }
    }

    this.RemoveHoverTooltipsByPrefix(JournalListHoverPrefix);
  }

  /// <summary>
  ///     Clears Journal quest-list runtime state after the visible UI has been
  ///     restored.
  /// </summary>
  private void ClearJournalRuntimeState()
  {
    this.journalListHoverCache.Clear();
    this.journalListOriginalTextCache.Clear();
    this.journalListNativeMutationNodeKeys.Clear();
    this.hasPendingJournalTranslations = false;
    this.lastVisibleJournalSignature = null;
    this.lastAppliedDisplayMode = null;
    this.nextJournalRetryUtc = DateTime.MinValue;
    this.RemoveHoverTooltipsByPrefix(JournalListHoverPrefix);
  }

  /// <summary>
  ///     Trims Journal quest-list runtime caches so they only keep the node
  ///     anchors visible in the current list snapshot.
  /// </summary>
  /// <param name="visibleQuestNodeKeys">The currently visible quest node keys.</param>
  private void TrimJournalListRuntimeState(
      HashSet<nint> visibleQuestNodeKeys)
  {
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
      this.ForgetJournalListNodeState(hiddenQuestNodeKey);
    }
  }
}
