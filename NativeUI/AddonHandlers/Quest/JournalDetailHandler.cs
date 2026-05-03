// <copyright file="JournalDetailHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Handles the JournalDetail quest addon runtime inside the standalone
///     quest-handler model.
/// </summary>
internal sealed class JournalDetailHandler : QuestAddonHandlerBase
{
  private const string JournalAddonName = "Journal";

  private const string JournalDetailAddonName = "JournalDetail";

  private const string JournalDetailHoverPrefix = "JournalDetail-";

  private readonly Dictionary<string, string> journalDetailTextCache =
      new(StringComparer.Ordinal);

  private readonly Dictionary<string, JournalDetailOriginalSnapshot>
      journalDetailOriginalCache =
          new(StringComparer.Ordinal);

  private string currentJournalDetailScopeKey = string.Empty;

  /// <summary>
  ///     Initializes a new instance of the <see cref="JournalDetailHandler" />
  ///     class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public JournalDetailHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PreUpdate, this.OnJournalDetailEvent);
    this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.OnJournalDetailEvent);
    this.RegisterHandler(AddonEvent.PostRequestedUpdate, this.OnJournalDetailEvent);
    this.RegisterHandler(AddonEvent.PreHide, this.OnJournalDetailCleanupEvent);
    this.RegisterHandler(AddonEvent.PreFinalize, this.OnJournalDetailCleanupEvent);
  }

  /// <summary>
  ///     Gets the active plugin configuration through the legacy JournalDetail
  ///     member name used by the ported code.
  /// </summary>
  private Config configuration => this.Config;

  /// <summary>
  ///     Gets whether JournalDetail should use hover tooltips.
  /// </summary>
  private bool JournalDetailUsesHoverTooltips =>
      QuestAddonModeHelpers.UsesHoverTooltips(
          this.Config.JournalDetailTranslationDisplayMode);

  /// <summary>
  ///     Gets whether JournalDetail should write translated text into the
  ///     native addon.
  /// </summary>
  private bool JournalDetailWritesNativeTranslation =>
      QuestAddonModeHelpers.WritesNativeTranslation(
          this.Config.JournalDetailTranslationDisplayMode);

  /// <summary>
  ///     Gets whether JournalDetail hover tooltips should show the original
  ///     text.
  /// </summary>
  private bool JournalDetailHoverShowsOriginal =>
      QuestAddonModeHelpers.ShowsOriginalTooltips(
          this.Config.JournalDetailTranslationDisplayMode);

  /// <summary>
  ///     Gets whether JournalDetail may render a hover tooltip for a payload
  ///     whose translated content is ready.
  /// </summary>
  /// <param name="translatedPayloadReady">
  ///     Whether the translated payload required by the current mode is ready.
  /// </param>
  /// <returns><c>true</c> when the hover tooltip may be rendered.</returns>
  private bool CanRenderJournalDetailHoverTooltip(bool translatedPayloadReady) =>
      QuestAddonModeHelpers.CanRenderHoverTooltip(
          this.Config.JournalDetailTranslationDisplayMode,
          translatedPayloadReady);

  /// <summary>
  ///     Gets whether translated JournalDetail text should be normalized before
  ///     being written into the native UI.
  /// </summary>
  private bool JournalDetailShouldRemoveDiacritics =>
      QuestAddonModeHelpers.ShouldRemoveDiacritics(
          this.Config.JournalDetailTranslationDisplayMode,
          this.Config.RemoveDiacriticsWhenUsingReplacementQuest);

  /// <summary>
  ///     Gets the quest sequence row text for the current quest phase so the
  ///     JournalDetail hover body can stay anchored to one sheet row instead of
  ///     aggregating multiple quest steps.
  /// </summary>
  /// <param name="questProgressSnapshot">The Lumina-backed quest progress snapshot.</param>
  /// <returns>The current quest sequence row text, or an empty string when unavailable.</returns>
  private static string GetCurrentQuestSequenceText(
      QuestProgressSnapshot questProgressSnapshot)
  {
    return QuestCanonicalData.ResolveCurrentSequenceText(questProgressSnapshot);
  }

  /// <summary>
  ///     Gets the translated quest sequence text for the current quest phase.
  ///     The result is cached back into the quest plate so the same sequence
  ///     row does not keep re-reading while the addon repaints.
  /// </summary>
  /// <param name="foundQuestPlate">The quest plate currently resolved from the DB.</param>
  /// <param name="questProgressSnapshot">The Lumina-backed quest progress snapshot.</param>
  /// <param name="journalDetailScopeKey">The current detail runtime scope key.</param>
  /// <param name="translatedCurrentQuestSequenceTextReady">
  ///     Whether the current SEQ row translation is ready.
  /// </param>
  /// <returns>The translated quest sequence row text, or the source text if translation is not ready yet.</returns>
  private string TranslateCurrentQuestSequenceText(
      QuestPlate? foundQuestPlate,
      QuestProgressSnapshot questProgressSnapshot,
      string journalDetailScopeKey,
      out bool translatedCurrentQuestSequenceTextReady)
  {
    translatedCurrentQuestSequenceTextReady = false;
    var questCanonicalData = QuestCanonicalData.Create(
        questProgressSnapshot,
        GetGameVersion());
    if (!questCanonicalData.TryGetCurrentSequenceEntry(
            out var currentSequenceEntry) ||
        string.IsNullOrWhiteSpace(currentSequenceEntry.Text))
    {
      return string.Empty;
    }

    var currentQuestSequenceText = currentSequenceEntry.Text;

    if (this.TryGetJournalDetailCachedText(
            journalDetailScopeKey,
            currentQuestSequenceText,
            out var cachedQuestSequenceText))
    {
      translatedCurrentQuestSequenceTextReady = true;
      return cachedQuestSequenceText;
    }

    if (foundQuestPlate != null &&
        foundQuestPlate.TryGetTranslatedSummaryText(
            currentSequenceEntry.KeyText,
            currentQuestSequenceText,
            out var storedQuestSequenceText))
    {
      translatedCurrentQuestSequenceTextReady = true;
      this.RememberJournalDetailCachedText(
          journalDetailScopeKey,
          currentQuestSequenceText,
          storedQuestSequenceText);
      return storedQuestSequenceText;
    }

    return currentQuestSequenceText;
  }

  /// <summary>
  ///     Builds the current JournalDetail cache scope key so each quest detail
  ///     view can keep its own local runtime state.
  /// </summary>
  /// <param name="questProgressSnapshot">The resolved quest progress snapshot, if any.</param>
  /// <param name="questName">The current quest name.</param>
  /// <param name="questMessage">The current quest message.</param>
  /// <returns>A stable scope key for the current quest detail body.</returns>
  private static string BuildJournalDetailScopeKey(
      QuestProgressSnapshot? questProgressSnapshot,
      string questName,
      string questMessage)
  {
    return questProgressSnapshot?.CacheKey ??
           $"{questName}|{questMessage}";
  }

  /// <summary>
  ///     Ensures the JournalDetail runtime cache is scoped to the currently
  ///     visible quest detail body.
  /// </summary>
  /// <param name="scopeKey">The quest-detail scope key.</param>
  private void EnsureJournalDetailScope(string scopeKey)
  {
    if (string.Equals(
            this.currentJournalDetailScopeKey,
            scopeKey,
            StringComparison.Ordinal))
    {
      return;
    }

    this.currentJournalDetailScopeKey = scopeKey;
    this.journalDetailTextCache.Clear();
    this.RemoveHoverTooltipsByPrefix(JournalDetailHoverPrefix);
  }

  /// <summary>
  ///     Attempts to get the original JournalDetail text snapshot for the
  ///     current quest scope.
  /// </summary>
  /// <param name="scopeKey">The current quest-detail scope key.</param>
  /// <param name="snapshot">The cached original snapshot.</param>
  /// <returns>True when the scope already has an original snapshot.</returns>
  private bool TryGetJournalDetailOriginalSnapshot(
      string scopeKey,
      out JournalDetailOriginalSnapshot snapshot)
  {
    if (string.IsNullOrWhiteSpace(scopeKey))
    {
      snapshot = null!;
      return false;
    }

    return this.journalDetailOriginalCache.TryGetValue(
        scopeKey,
        out snapshot!);
  }

  /// <summary>
  ///     Remembers the original JournalDetail texts for the current quest
  ///     scope so mode switches can restore native UI state correctly.
  /// </summary>
  /// <param name="scopeKey">The current quest-detail scope key.</param>
  /// <param name="questName">The original quest name.</param>
  /// <param name="questMessage">The original quest description.</param>
  /// <param name="objectiveText">The original visible objective text.</param>
  /// <param name="summaryText">The original visible summary text.</param>
  /// <param name="summaryNode">The live primary summary text node, if any.</param>
  /// <param name="summaryContainerNode">
  ///     The live summary container node, if any.
  /// </param>
  /// <param name="additionalSummaryNodeAddresses">
  ///     The supplemental summary text nodes discovered for this scope.
  /// </param>
  /// <param name="additionalSummaryTexts">
  ///     The original visible supplemental summary texts.
  /// </param>
  private unsafe void RememberJournalDetailOriginalSnapshot(
      string scopeKey,
      string questName,
      string questMessage,
      string objectiveText,
      string summaryText,
      AtkTextNode* summaryNode,
      AtkResNode* summaryContainerNode,
      IReadOnlyList<nint> additionalSummaryNodeAddresses,
      IReadOnlyList<string> additionalSummaryTexts)
  {
    if (string.IsNullOrWhiteSpace(scopeKey))
    {
      return;
    }

    this.journalDetailOriginalCache[scopeKey] =
        new JournalDetailOriginalSnapshot(
            questName,
            questMessage,
            objectiveText,
            summaryText,
            additionalSummaryNodeAddresses,
            additionalSummaryTexts,
            summaryNode != null ? summaryNode->GetWidth() : (ushort)0,
            summaryContainerNode != null ? summaryContainerNode->GetHeight() : (ushort)0,
            summaryNode != null ? summaryNode->TextFlags : default,
            summaryNode != null ? summaryNode->FontSize : (byte)0);
  }

  /// <summary>
  ///     Attempts to get translated JournalDetail text from the local
  ///     quest-scoped runtime cache.
  /// </summary>
  /// <param name="scopeKey">The current quest-detail scope key.</param>
  /// <param name="originalText">The source text visible for that scope.</param>
  /// <param name="translatedText">The cached translated text.</param>
  /// <returns>True when the scoped runtime cache already has the text.</returns>
  private bool TryGetJournalDetailCachedText(
      string scopeKey,
      string originalText,
      out string translatedText)
  {
    translatedText = string.Empty;
    if (string.IsNullOrWhiteSpace(scopeKey) ||
        string.IsNullOrWhiteSpace(originalText))
    {
      return false;
    }

    return this.journalDetailTextCache.TryGetValue(
        $"{scopeKey}|{originalText}",
        out translatedText);
  }

  /// <summary>
  ///     Remembers translated JournalDetail text inside the local quest-scoped
  ///     runtime cache.
  /// </summary>
  /// <param name="scopeKey">The current quest-detail scope key.</param>
  /// <param name="originalText">The source text visible for that scope.</param>
  /// <param name="translatedText">The translated text resolved for that scope.</param>
  private void RememberJournalDetailCachedText(
      string scopeKey,
      string originalText,
      string translatedText)
  {
    if (string.IsNullOrWhiteSpace(scopeKey) ||
        string.IsNullOrWhiteSpace(originalText) ||
        string.IsNullOrWhiteSpace(translatedText) ||
        string.Equals(originalText, translatedText, StringComparison.Ordinal))
    {
      return;
    }

    this.journalDetailTextCache[$"{scopeKey}|{originalText}"] =
        translatedText;
  }

  /// <summary>
  ///     Applies canonical quest metadata resolved from the current quest
  ///     progress snapshot so persisted JournalDetail rows stay aligned with
  ///     the sheet-first quest model.
  /// </summary>
  /// <param name="questPlate">The quest plate being materialized.</param>
  /// <param name="questProgressSnapshot">The resolved quest progress snapshot, if any.</param>
  private void ApplyQuestProgressMetadata(
      QuestPlate questPlate,
      QuestProgressSnapshot? questProgressSnapshot)
  {
    if (questPlate == null)
    {
      return;
    }

    questPlate.GameVersion ??= GetGameVersion();
    if (!questProgressSnapshot.HasValue)
    {
      return;
    }

    var questCanonicalData = QuestCanonicalData.Create(
        questProgressSnapshot.Value,
        questPlate.GameVersion ?? GetGameVersion());
    questPlate.ApplyCanonicalPayload(questCanonicalData);
  }

  /// <summary>
  ///     Persists canonical quest metadata into an existing JournalDetail row
  ///     when the row was created before the sheet-first fields were populated.
  /// </summary>
  /// <param name="questPlate">The quest plate currently loaded from the DB.</param>
  /// <param name="questProgressSnapshot">The resolved quest progress snapshot, if any.</param>
  private void EnsureQuestPlateMetadataPersisted(
      QuestPlate? questPlate,
      QuestProgressSnapshot? questProgressSnapshot)
  {
    if (questPlate == null || !questProgressSnapshot.HasValue)
    {
      return;
    }

    var expectedQuestId = questProgressSnapshot.Value.QuestId.ToString();
    var expectedSheetName = questProgressSnapshot.Value.QuestSheetName;
    var expectedContentHash = questProgressSnapshot.Value.ContentHash;
    if (string.Equals(
            questPlate.QuestId,
            expectedQuestId,
            StringComparison.Ordinal) &&
        string.Equals(
            questPlate.QuestTextSheetName,
            expectedSheetName,
            StringComparison.Ordinal) &&
        string.Equals(
            questPlate.SourceContentHash,
            expectedContentHash,
            StringComparison.Ordinal))
    {
      return;
    }

    var questPlateToUpdate = questPlate.Clone();
    this.ApplyQuestProgressMetadata(
        questPlateToUpdate,
        questProgressSnapshot);
    questPlateToUpdate.UpdatedDate = DateTime.Now;
    this.UpdateQuestPlate(questPlateToUpdate);

    this.ApplyQuestProgressMetadata(
        questPlate,
        questProgressSnapshot);
  }

  /// <summary>
  ///     Builds a single multi-paragraph tooltip body from the quest plate text
  ///     sections that are currently visible.
  /// </summary>
  /// <param name="sections">Quest plate text sections to join.</param>
  /// <returns>A multi-paragraph tooltip body.</returns>
  private static string BuildQuestPlateHoverBody(params string?[] sections)
  {
    List<string> lines = [];
    HashSet<string> seenSections = new(StringComparer.Ordinal);
    foreach (var section in sections)
    {
      if (string.IsNullOrWhiteSpace(section))
      {
        continue;
      }

      var normalizedSection = section.Trim();
      if (!seenSections.Add(normalizedSection))
      {
        continue;
      }

      lines.Add(normalizedSection);
    }

    return string.Join(Environment.NewLine + Environment.NewLine, lines);
  }

  /// <summary>
  ///     Builds the visible JournalDetail summary block from the supplied
  ///     ordered summary sections.
  /// </summary>
  /// <param name="sections">The summary sections to join.</param>
  /// <returns>A deduplicated summary block for the JournalDetail body.</returns>
  private static string BuildQuestPlateSummarySection(
      IEnumerable<string?> sections)
  {
    List<string> summarySections = [];
    HashSet<string> seenSections = new(StringComparer.Ordinal);

    void AddSection(string? text)
    {
      if (string.IsNullOrWhiteSpace(text))
      {
        return;
      }

      var normalizedSection = text.Trim();
      if (!seenSections.Add(normalizedSection))
      {
        return;
      }

      summarySections.Add(normalizedSection);
    }

    foreach (var section in sections)
    {
      AddSection(section);
    }

    return string.Join(Environment.NewLine + Environment.NewLine, summarySections);
  }

  /// <summary>
  ///     Collects the visible JournalDetail supplemental summary nodes in their
  ///     current display order.
  /// </summary>
  /// <param name="journalBox">The live JournalDetail box component.</param>
  /// <param name="descriptionNode">The live description text node.</param>
  /// <param name="objectiveNode">The live objective text node.</param>
  /// <param name="summaryNode">The live primary summary text node, if any.</param>
  /// <returns>The visible supplemental summary text nodes.</returns>
  private unsafe List<nint> CollectVisibleAdditionalSummaryNodes(
      AtkComponentBase* journalBox,
      AtkTextNode* descriptionNode,
      AtkTextNode* objectiveNode,
      AtkTextNode* summaryNode)
  {
    List<nint> summaryNodes = [];
    var descriptionNodeAddress = (nint)descriptionNode;
    var objectiveNodeAddress = (nint)objectiveNode;
    var summaryNodeAddress = (nint)summaryNode;
    var summaryAnchorX = summaryNode != null ? summaryNode->ScreenX : 0f;
    var summaryAnchorY = summaryNode != null ? summaryNode->ScreenY : 0f;
    var summaryAnchorWidth = summaryNode != null
        ? Math.Max(1f, summaryNode->GetWidth())
        : 0f;

    for (var i = 0; i < journalBox->UldManager.NodeListCount; i++)
    {
      var node = journalBox->UldManager.NodeList[i];
      if (node == null ||
          !node->IsVisible())
      {
        continue;
      }

      var summaryItemNode = node->GetAsAtkComponentNode();
      if (summaryItemNode == null || summaryItemNode->Component == null)
      {
        continue;
      }

      var summaryTextResNode =
          summaryItemNode->Component->UldManager.SearchNodeById(2);
      if (summaryTextResNode == null ||
          summaryTextResNode->Type != NodeType.Text ||
          !summaryTextResNode->IsVisible())
      {
        continue;
      }

      var summaryTextNode = summaryTextResNode->GetAsAtkTextNode();
      if (summaryTextNode == null)
      {
        continue;
      }

      var summaryTextNodeAddress = (nint)summaryTextNode;
      if (summaryTextNodeAddress == descriptionNodeAddress ||
          summaryTextNodeAddress == objectiveNodeAddress ||
          summaryTextNodeAddress == summaryNodeAddress)
      {
        continue;
      }

      var matchesLegacySummaryRange =
          node->NodeId >= 480700 && node->NodeId <= 481200;
      var matchesSummaryLayout = summaryNode != null &&
                                 summaryTextNode->ScreenY >= summaryAnchorY - 4f &&
                                 summaryTextNode->ScreenX >= summaryAnchorX - 24f &&
                                 summaryTextNode->ScreenX <= summaryAnchorX + 64f &&
                                 Math.Abs(
                                     Math.Max(1f, summaryTextNode->GetWidth()) -
                                     summaryAnchorWidth) <=
                                 Math.Max(96f, summaryAnchorWidth * 0.75f);
      if (!matchesLegacySummaryRange && !matchesSummaryLayout)
      {
        continue;
      }

      summaryNodes.Add((nint)summaryTextNode);
    }

    summaryNodes.Sort(
        (left, right) =>
        {
          var leftNode = (AtkTextNode*)left;
          var rightNode = (AtkTextNode*)right;
          var verticalComparison =
              leftNode->ScreenY.CompareTo(rightNode->ScreenY);
          return verticalComparison != 0
              ? verticalComparison
              : leftNode->ScreenX.CompareTo(rightNode->ScreenX);
        });

    return summaryNodes;
  }

  /// <summary>
  ///     Reads the current visible text of the supplied text nodes in order.
  /// </summary>
  /// <param name="nodes">The visible text nodes.</param>
  /// <returns>The current visible text for each node.</returns>
  private static unsafe List<string> CaptureVisibleTextNodeTexts(
      IEnumerable<nint> nodes)
  {
    List<string> visibleTexts = [];
    foreach (var nodeAddress in nodes)
    {
      var node = (AtkTextNode*)nodeAddress;
      if (node == null || node->NodeText.IsEmpty)
      {
        visibleTexts.Add(string.Empty);
        continue;
      }

      visibleTexts.Add(
          MemoryHelper.ReadSeStringAsString(
              out _,
              (nint)node->NodeText.StringPtr.Value));
    }

    return visibleTexts;
  }

  /// <summary>
  ///     Resolves a translated summary row from the JournalDetail cache or the
  ///     persisted quest plate.
  /// </summary>
  /// <param name="foundQuestPlate">The persisted quest plate, if any.</param>
  /// <param name="journalDetailScopeKey">The current JournalDetail scope key.</param>
  /// <param name="rowKey">The canonical summary row key.</param>
  /// <param name="sourceText">The canonical summary row text.</param>
  /// <param name="translatedText">The translated summary row text.</param>
  /// <returns>True when the translated summary row is ready.</returns>
  private bool TryResolveTranslatedSummaryText(
      QuestPlate? foundQuestPlate,
      string journalDetailScopeKey,
      string rowKey,
      string sourceText,
      out string translatedText)
  {
    translatedText = sourceText;
    if (string.IsNullOrWhiteSpace(sourceText))
    {
      return true;
    }

    if (this.TryGetJournalDetailCachedText(
            journalDetailScopeKey,
            sourceText,
            out translatedText))
    {
      return true;
    }

    if (foundQuestPlate != null &&
        foundQuestPlate.TryGetTranslatedSummaryText(
            rowKey,
            sourceText,
            out translatedText))
    {
      this.RememberJournalDetailCachedText(
          journalDetailScopeKey,
          sourceText,
          translatedText);
      return true;
    }

    translatedText = sourceText;
    return false;
  }

  /// <summary>
  ///     Builds the canonical JournalDetail summary rows for the current quest
  ///     phase using the quest sequence resolved from QuestManager.
  /// </summary>
  /// <param name="foundQuestPlate">The persisted quest plate, if any.</param>
  /// <param name="questCanonicalData">The canonical quest payload, if any.</param>
  /// <param name="journalDetailScopeKey">The current JournalDetail scope key.</param>
  /// <returns>The ordered summary rows that should populate the detail panel.</returns>
  private unsafe List<SummaryQuest> BuildCanonicalSummaryRows(
      QuestPlate? foundQuestPlate,
      QuestCanonicalData? questCanonicalData,
      string journalDetailScopeKey)
  {
    List<SummaryQuest> summaries = [];
    if (questCanonicalData == null)
    {
      return summaries;
    }

    foreach (var summaryEntry in questCanonicalData.GetSummaryEntriesBeforeCurrentSequence())
    {
      var translatedTextReady = this.TryResolveTranslatedSummaryText(
          foundQuestPlate,
          journalDetailScopeKey,
          summaryEntry.KeyText,
          summaryEntry.Text,
          out var translatedText);
      summaries.Add(
          new SummaryQuest(
              summaryEntry.Text,
              translatedText,
              null,
              translatedTextReady));
    }

    return summaries;
  }

  /// <summary>
  ///     Expands an existing quest-body hover rectangle to include a visible
  ///     text node and some practical padding around it.
  /// </summary>
  /// <param name="topLeft">The current top-left coordinate.</param>
  /// <param name="bottomRight">The current bottom-right coordinate.</param>
  /// <param name="textNode">The text node to include.</param>
  private static unsafe void ExpandQuestPlateHoverBoundsForTextNode(
      ref Vector2 topLeft,
      ref Vector2 bottomRight,
      AtkTextNode* textNode)
  {
    if (textNode == null || !textNode->IsVisible())
    {
      return;
    }

    topLeft = new Vector2(
        Math.Max(0f, Math.Min(topLeft.X, textNode->ScreenX - 20f)),
        Math.Max(0f, Math.Min(topLeft.Y, textNode->ScreenY - 12f)));
    bottomRight = new Vector2(
        Math.Max(
            bottomRight.X,
            textNode->ScreenX + Math.Max(1f, textNode->GetWidth()) + 20f),
        Math.Max(
            bottomRight.Y,
            textNode->ScreenY + Math.Max(1f, textNode->GetHeight()) + 16f));
  }

  /// <summary>
  ///     Gets the bounds of the JournalCanvasComponentNode used as the quest
  ///     plate hover trigger.
  /// </summary>
  /// <param name="questCanvasNode">The journal detail component.</param>
  /// <param name="topLeft">The top-left screen coordinate of the node.</param>
  /// <param name="bottomRight">The bottom-right screen coordinate of the node.</param>
  /// <returns>True when the node is visible and the bounds are usable.</returns>
  private unsafe bool TryGetQuestPlateHoverBounds(
      AtkResNode* questCanvasNode,
      out Vector2 topLeft,
      out Vector2 bottomRight)
  {
    topLeft = default;
    bottomRight = default;

    if (questCanvasNode == null || !questCanvasNode->IsVisible())
    {
      return false;
    }

    const float hoverPaddingX = 48f;
    const float hoverPaddingY = 28f;

    topLeft = new Vector2(
        Math.Max(0f, questCanvasNode->ScreenX - hoverPaddingX),
        Math.Max(0f, questCanvasNode->ScreenY - hoverPaddingY));
    bottomRight = new Vector2(
        questCanvasNode->ScreenX +
            Math.Max(1f, questCanvasNode->Width) +
            hoverPaddingX,
        questCanvasNode->ScreenY +
            Math.Max(1f, questCanvasNode->Height) +
            hoverPaddingY);

    return true;
  }

  /// <summary>
  ///     Applies translations to the active Journal detail box.
  /// </summary>
  /// <param name="journalBox">The journal detail component.</param>
  /// <param name="foundQuestPlate">The quest plate currently resolved from the DB.</param>
  /// <param name="questProgressSnapshot">The Lumina-backed quest progress snapshot.</param>
  /// <param name="questName">The quest name.</param>
  /// <param name="questMessage">The quest message.</param>
  /// <param name="objectiveText">The objective text.</param>
  /// <param name="summaryText">The summary text.</param>
  /// <param name="questNameNode">The quest-name text node.</param>
  /// <param name="descriptionNode">The description text node.</param>
  /// <param name="objectiveNode">The objective text node.</param>
  /// <param name="summaryContainerNode">The summary container node, if any.</param>
  /// <param name="summaryNode">The optional summary text node.</param>
  private unsafe void TranslateQuestOnJournalBox(
      AtkComponentBase* journalBox,
      QuestPlate? foundQuestPlate,
      QuestProgressSnapshot? questProgressSnapshot,
      string questName,
      string questMessage,
      string objectiveText,
      string summaryText,
      AtkTextNode* questNameNode,
      AtkTextNode* descriptionNode,
      AtkTextNode* objectiveNode,
      AtkResNode* summaryContainerNode,
      AtkTextNode* summaryNode)
  {
    string translatedQuestName = questName;
    string translatedQuestObjective = objectiveText;
    var translatedQuestNameReady = false;
    var translatedQuestObjectiveReady = string.IsNullOrWhiteSpace(objectiveText);
    var journalDetailScopeKey = BuildJournalDetailScopeKey(
        questProgressSnapshot,
        questName,
        questMessage);
    var questCanonicalData = questProgressSnapshot.HasValue
        ? QuestCanonicalData.Create(
            questProgressSnapshot.Value,
            GetGameVersion())
        : null;
    var objectiveRowKeys = questCanonicalData == null
        ? []
        : questCanonicalData.EnumerateObjectiveRowKeysByText(objectiveText).ToArray();
    this.EnsureJournalDetailScope(journalDetailScopeKey);

    var visibleAdditionalSummaryNodes =
        this.CollectVisibleAdditionalSummaryNodes(
            journalBox,
            descriptionNode,
            objectiveNode,
            summaryNode);

    if (!this.TryGetJournalDetailOriginalSnapshot(
            journalDetailScopeKey,
            out var originalSnapshot))
    {
      var capturedAdditionalSummaryTexts =
          CaptureVisibleTextNodeTexts(visibleAdditionalSummaryNodes);
      originalSnapshot = new JournalDetailOriginalSnapshot(
          questName,
          questMessage,
          objectiveText,
          summaryText,
          visibleAdditionalSummaryNodes,
          capturedAdditionalSummaryTexts,
          summaryNode != null ? summaryNode->GetWidth() : (ushort)0,
          summaryContainerNode != null ? summaryContainerNode->GetHeight() : (ushort)0,
          summaryNode != null ? summaryNode->TextFlags : default,
          summaryNode != null ? summaryNode->FontSize : (byte)0);
      this.RememberJournalDetailOriginalSnapshot(
          journalDetailScopeKey,
          questName,
          questMessage,
          objectiveText,
          summaryText,
          summaryNode,
          summaryContainerNode,
          visibleAdditionalSummaryNodes,
          capturedAdditionalSummaryTexts);
    }

    var originalQuestName = originalSnapshot.QuestName;
    var originalQuestMessage = originalSnapshot.QuestMessage;
    var originalObjectiveText = originalSnapshot.ObjectiveText;
    var originalSummaryText = originalSnapshot.SummaryText;
    var additionalSummaryNodeAddresses =
        originalSnapshot.AdditionalSummaryNodeAddresses.Count != 0
            ? originalSnapshot.AdditionalSummaryNodeAddresses
            : visibleAdditionalSummaryNodes;
    var originalAdditionalSummaryTexts =
        originalSnapshot.AdditionalSummaryTexts;

    var currentQuestSequenceText = questProgressSnapshot.HasValue
        ? GetCurrentQuestSequenceText(questProgressSnapshot.Value)
        : string.Empty;
    var originalQuestDescription = !string.IsNullOrWhiteSpace(currentQuestSequenceText)
        ? currentQuestSequenceText
        : originalQuestMessage;
    var translatedQuestDescription = originalQuestDescription;
    var translatedQuestDescriptionReady =
        string.IsNullOrWhiteSpace(originalQuestDescription);

    var canonicalSummaryRows = this.BuildCanonicalSummaryRows(
        foundQuestPlate,
        questCanonicalData,
        journalDetailScopeKey);
    var primaryCanonicalSummary = canonicalSummaryRows.FirstOrDefault();
    var additionalCanonicalSummaryRows = canonicalSummaryRows
        .Skip(1)
        .ToArray();

    var originalPrimarySummaryText = primaryCanonicalSummary?.OriginalText ??
                                     originalSummaryText;
    var translatedPrimarySummaryText = primaryCanonicalSummary?.TranslatedText ??
                                       originalPrimarySummaryText;
    var translatedPrimarySummaryReady = primaryCanonicalSummary == null
        ? string.IsNullOrWhiteSpace(originalPrimarySummaryText)
        : primaryCanonicalSummary.IsTranslated;

    if (foundQuestPlate != null)
    {
      if (!this.TryGetJournalDetailCachedText(
              journalDetailScopeKey,
              originalQuestName,
              out translatedQuestName))
      {
        translatedQuestName = string.IsNullOrWhiteSpace(
                foundQuestPlate.TranslatedQuestName)
            ? originalQuestName
            : foundQuestPlate.TranslatedQuestName;
        translatedQuestNameReady = !string.IsNullOrWhiteSpace(
            foundQuestPlate.TranslatedQuestName);
      }
      else
      {
        translatedQuestNameReady = true;
      }

      if (!string.IsNullOrWhiteSpace(currentQuestSequenceText) &&
          questProgressSnapshot.HasValue)
      {
        translatedQuestDescription =
            this.TranslateCurrentQuestSequenceText(
                foundQuestPlate,
                questProgressSnapshot.Value,
                journalDetailScopeKey,
                out translatedQuestDescriptionReady);
      }
      else if (this.TryGetJournalDetailCachedText(
                   journalDetailScopeKey,
                   originalQuestMessage,
                   out translatedQuestDescription))
      {
        translatedQuestDescriptionReady = true;
      }
      else if (!string.IsNullOrWhiteSpace(foundQuestPlate.TranslatedQuestMessage))
      {
        translatedQuestDescription = foundQuestPlate.TranslatedQuestMessage;
        translatedQuestDescriptionReady = true;
      }

      if (this.TryGetJournalDetailCachedText(
              journalDetailScopeKey,
              originalObjectiveText,
              out translatedQuestObjective))
      {
        translatedQuestObjectiveReady = true;
      }
      else if (foundQuestPlate.TryGetTranslatedObjectiveText(
                   objectiveRowKeys.FirstOrDefault(),
                   originalObjectiveText,
                   out var storedObjectiveText))
      {
        translatedQuestObjective = storedObjectiveText;
        translatedQuestObjectiveReady = true;
      }
      else
      {
        translatedQuestObjective = objectiveText;
      }

      if (primaryCanonicalSummary == null &&
          originalSummaryText != string.Empty)
      {
        if (this.TryGetJournalDetailCachedText(
                journalDetailScopeKey,
                originalSummaryText,
                out translatedPrimarySummaryText))
        {
          translatedPrimarySummaryReady = true;
        }
        else if (foundQuestPlate.TryGetTranslatedSummaryText(
                     null,
                     originalSummaryText,
                     out var storedSummaryText))
        {
          translatedPrimarySummaryText = storedSummaryText;
          translatedPrimarySummaryReady = true;
        }
        else
        {
          translatedPrimarySummaryText = originalSummaryText;
        }
      }
    }
    else
    {
      translatedQuestName = originalQuestName;
      translatedQuestDescription = originalQuestDescription;
      translatedQuestObjective = originalObjectiveText;
      translatedPrimarySummaryText = originalPrimarySummaryText;
    }

    var originalSummarySections = canonicalSummaryRows.Count != 0
        ? canonicalSummaryRows
            .Select(summary => summary.OriginalText)
            .ToArray()
        : originalAdditionalSummaryTexts
            .Prepend(originalPrimarySummaryText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
    var translatedSummarySections = canonicalSummaryRows.Count != 0
        ? canonicalSummaryRows
            .Select(summary => summary.TranslatedText)
            .ToArray()
        : originalAdditionalSummaryTexts
            .Prepend(translatedPrimarySummaryText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
    var originalSummaryDisplayText = BuildQuestPlateSummarySection(
        originalSummarySections);
    var translatedSummaryDisplayText = BuildQuestPlateSummarySection(
        translatedSummarySections);
    var translatedQuestSummaryReady =
        translatedPrimarySummaryReady &&
        additionalCanonicalSummaryRows.All(summary => summary.IsTranslated);

    if (this.JournalDetailShouldRemoveDiacritics)
    {
      translatedQuestName = this.NormalizeQuestText(
          translatedQuestName ?? string.Empty);
      translatedQuestDescription = this.NormalizeQuestText(
          translatedQuestDescription ?? string.Empty);
      translatedQuestObjective = this.NormalizeQuestText(
          translatedQuestObjective ?? string.Empty);
      translatedPrimarySummaryText = this.NormalizeQuestText(
          translatedPrimarySummaryText ?? string.Empty);

      for (var i = 0; i < translatedSummarySections.Length; i++)
      {
        translatedSummarySections[i] = this.NormalizeQuestText(
            translatedSummarySections[i] ?? string.Empty);
      }
    }

    if (this.JournalDetailWritesNativeTranslation)
    {
      questNameNode->SetText(translatedQuestName);
      descriptionNode->SetText(translatedQuestDescription);
      objectiveNode->SetText(translatedQuestObjective);
      if (summaryNode != null)
      {
        if (originalSnapshot.SummaryNodeWidth != 0)
        {
          summaryNode->SetWidth(originalSnapshot.SummaryNodeWidth);
        }

        if (originalSnapshot.SummaryNodeFontSize != 0)
        {
          summaryNode->FontSize = originalSnapshot.SummaryNodeFontSize;
        }

        summaryNode->TextFlags =
            originalSnapshot.SummaryNodeTextFlags |
            TextFlags.WordWrap |
            TextFlags.MultiLine |
            TextFlags.AutoAdjustNodeSize;
        summaryNode->SetText(translatedSummaryDisplayText ?? string.Empty);
        summaryNode->ResizeNodeForCurrentText();

        if (summaryContainerNode != null && originalSnapshot.SummaryContainerHeight != 0)
        {
          var desiredSummaryContainerHeight = (ushort)Math.Max(
              originalSnapshot.SummaryContainerHeight,
              summaryNode->GetHeight() + 12f);
          summaryContainerNode->SetHeight(desiredSummaryContainerHeight);
        }

        foreach (var additionalSummaryNodeAddress in additionalSummaryNodeAddresses)
        {
          var additionalSummaryNode =
              (AtkTextNode*)additionalSummaryNodeAddress;
          additionalSummaryNode->SetText(string.Empty);
          additionalSummaryNode->ResizeNodeForCurrentText();
        }
      }
      else
      {
        for (var i = 0; i < additionalSummaryNodeAddresses.Count; i++)
        {
          var additionalSummaryNode =
              (AtkTextNode*)additionalSummaryNodeAddresses[i];
          if (i < additionalCanonicalSummaryRows.Length)
          {
            additionalSummaryNode->SetText(
                additionalCanonicalSummaryRows[i].TranslatedText ?? string.Empty);
          }
          else
          {
            additionalSummaryNode->SetText(string.Empty);
          }
        }
      }
    }
    else
    {
      questNameNode->SetText(originalQuestName);
      descriptionNode->SetText(originalQuestMessage);
      objectiveNode->SetText(originalObjectiveText);
      if (summaryNode != null)
      {
        if (originalSnapshot.SummaryNodeWidth != 0)
        {
          summaryNode->SetWidth(originalSnapshot.SummaryNodeWidth);
        }

        if (originalSnapshot.SummaryNodeFontSize != 0)
        {
          summaryNode->FontSize = originalSnapshot.SummaryNodeFontSize;
        }

        summaryNode->TextFlags = originalSnapshot.SummaryNodeTextFlags;
        summaryNode->SetText(originalSummaryText ?? string.Empty);
        summaryNode->ResizeNodeForCurrentText();

        if (summaryContainerNode != null && originalSnapshot.SummaryContainerHeight != 0)
        {
          summaryContainerNode->SetHeight(originalSnapshot.SummaryContainerHeight);
        }
      }

      for (var i = 0; i < additionalSummaryNodeAddresses.Count; i++)
      {
        var additionalSummaryNode =
            (AtkTextNode*)additionalSummaryNodeAddresses[i];
        var originalAdditionalSummaryText = i < originalAdditionalSummaryTexts.Count
            ? originalAdditionalSummaryTexts[i]
            : string.Empty;
        additionalSummaryNode->SetText(
            originalAdditionalSummaryText ?? string.Empty);
        additionalSummaryNode->ResizeNodeForCurrentText();
      }
    }

    this.RememberJournalDetailCachedText(
        journalDetailScopeKey,
        originalQuestName,
        translatedQuestName);
    if (!string.IsNullOrWhiteSpace(originalQuestDescription) &&
        !string.IsNullOrWhiteSpace(translatedQuestDescription))
    {
      this.RememberJournalDetailCachedText(
          journalDetailScopeKey,
          originalQuestDescription,
          translatedQuestDescription);
    }

    this.RememberJournalDetailCachedText(
        journalDetailScopeKey,
        originalObjectiveText,
        translatedQuestObjective);
    if (primaryCanonicalSummary == null &&
        originalSummaryText != string.Empty)
    {
      this.RememberJournalDetailCachedText(
          journalDetailScopeKey,
          originalSummaryText,
          translatedPrimarySummaryText);
    }

    if (this.JournalDetailUsesHoverTooltips)
    {
      this.RegisterTranslatedHoverTooltip(
          $"JournalDetail-QuestName-{(nint)questNameNode:X}",
          questNameNode,
          originalQuestName,
          translatedQuestName,
          translatedPayloadReady: this.CanRenderJournalDetailHoverTooltip(
              translatedQuestNameReady),
          swapEnabled: this.JournalDetailHoverShowsOriginal,
          forceEnabled: true,
          denseHitbox: true);
      var translatedQuestBodyReady =
          translatedQuestDescriptionReady &&
          translatedQuestObjectiveReady &&
          translatedQuestSummaryReady;
      var originalQuestSummaryBody = originalSummaryDisplayText;
      var translatedQuestSummaryBody = translatedSummaryDisplayText;
      var originalQuestBody = BuildQuestPlateHoverBody(
          originalQuestDescription,
          originalObjectiveText,
          originalQuestSummaryBody);
      var translatedQuestBody = BuildQuestPlateHoverBody(
          translatedQuestDescription,
          translatedQuestObjective,
          translatedQuestSummaryBody);

      if (!string.IsNullOrWhiteSpace(originalQuestBody) ||
          !string.IsNullOrWhiteSpace(translatedQuestBody))
      {
        var questCanvasNode = journalBox->UldManager.SearchNodeById(14);
        var questBodyHoverKey = questCanvasNode != null
            ? $"JournalDetail-QuestBody-{(nint)questCanvasNode:X}"
            : $"JournalDetail-QuestBody-{(nint)descriptionNode:X}";
        if (questCanvasNode != null &&
            this.TryGetQuestPlateHoverBounds(
                questCanvasNode,
                out var bodyTopLeft,
                out var bodyBottomRight))
        {
          ExpandQuestPlateHoverBoundsForTextNode(
              ref bodyTopLeft,
              ref bodyBottomRight,
              descriptionNode);
          ExpandQuestPlateHoverBoundsForTextNode(
              ref bodyTopLeft,
              ref bodyBottomRight,
              objectiveNode);
          ExpandQuestPlateHoverBoundsForTextNode(
              ref bodyTopLeft,
              ref bodyBottomRight,
              summaryNode);
          foreach (var additionalSummaryNode in visibleAdditionalSummaryNodes)
          {
            ExpandQuestPlateHoverBoundsForTextNode(
                ref bodyTopLeft,
                ref bodyBottomRight,
                (AtkTextNode*)additionalSummaryNode);
          }

          this.RegisterTranslatedHoverTooltip(
              questBodyHoverKey,
              bodyTopLeft,
              bodyBottomRight,
              originalQuestBody,
              translatedQuestBody,
              translatedPayloadReady: this.CanRenderJournalDetailHoverTooltip(
                  translatedQuestBodyReady),
              swapEnabled: this.JournalDetailHoverShowsOriginal,
              forceEnabled: true);
        }
        else
        {
          var bodyLeft = descriptionNode->ScreenX;
          var bodyTop = descriptionNode->ScreenY;
          var bodyRight =
              bodyLeft + Math.Max(1f, descriptionNode->GetWidth());
          var bodyBottom =
              bodyTop + Math.Max(1f, descriptionNode->GetHeight());

          void ExpandBodyBounds(AtkTextNode* node)
          {
            if (node == null || !node->IsVisible())
            {
              return;
            }

            bodyLeft = Math.Min(bodyLeft, node->ScreenX);
            bodyTop = Math.Min(bodyTop, node->ScreenY);
            bodyRight = Math.Max(
                bodyRight,
                node->ScreenX + Math.Max(1f, node->GetWidth()));
            bodyBottom = Math.Max(
                bodyBottom,
                node->ScreenY + Math.Max(1f, node->GetHeight()));
          }

          ExpandBodyBounds(objectiveNode);
          if (summaryNode != null)
          {
            ExpandBodyBounds(summaryNode);
          }

          foreach (var additionalSummaryNode in visibleAdditionalSummaryNodes)
          {
            ExpandBodyBounds((AtkTextNode*)additionalSummaryNode);
          }

          bodyLeft -= 28f;
          bodyTop -= 18f;
          bodyRight += 28f;
          bodyBottom += 22f;

          this.RegisterTranslatedHoverTooltip(
              questBodyHoverKey,
              new Vector2(bodyLeft, bodyTop),
              new Vector2(bodyRight, bodyBottom),
              originalQuestBody,
              translatedQuestBody,
              translatedPayloadReady: this.CanRenderJournalDetailHoverTooltip(
                  translatedQuestBodyReady),
              swapEnabled: this.JournalDetailHoverShowsOriginal,
              forceEnabled: true);
        }
      }
    }
  }

  /// <summary>
  ///     Translates the active JournalDetail addon.
  /// </summary>
  private unsafe void TranslateJournalDetail()
  {
    if (!this.configuration.TranslateJournalDetail)
    {
      return;
    }

    var atkStage = AtkStage.Instance();
    var journalDetail =
        atkStage->RaptureAtkUnitManager->GetAddonByName(JournalDetailAddonName);
    if (journalDetail == null || !journalDetail->IsVisible)
    {
      return;
    }

    if (!this.JournalDetailUsesHoverTooltips)
    {
      this.RemoveHoverTooltipsByPrefix(JournalDetailHoverPrefix);
    }

    if (!this.TranslateJournalBox(journalDetail))
    {
      this.TranslateCompletedQuest(journalDetail);
    }
  }

  /// <summary>
  ///     Translates a completed JournalDetail quest view.
  /// </summary>
  /// <param name="journalDetail">The journal detail addon.</param>
  private unsafe void TranslateCompletedQuest(AtkUnitBase* journalDetail)
  {
    try
    {
      var questNameNode = journalDetail->GetTextNodeById(38);
      if (questNameNode == null || questNameNode->NodeText.IsEmpty)
      {
        return;
      }

      if (!journalDetail->GetNodeById(46)->IsVisible())
      {
        return;
      }

      var description = journalDetail->GetNodeById(46);
      if (description == null || description->Type != NodeType.Text)
      {
        return;
      }

      var questName = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)questNameNode->NodeText.StringPtr.Value);
      var descriptionNode = description->GetAsAtkTextNode();
      var questMessage = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)descriptionNode->NodeText.StringPtr.Value);
      var questPlate = this.CreateQuestPlate(questName, questMessage, string.Empty);
      if (QuestProgressResolver.TryResolveQuestProgress(
              questPlate,
              out var resolvedCompletedSnapshot))
      {
        questPlate.SourceContentHash = resolvedCompletedSnapshot.ContentHash;
      }

      var foundQuestPlate = this.FindQuestPlate(questPlate);
      if (foundQuestPlate != null &&
          !string.Equals(
              foundQuestPlate.GameVersion,
              GetGameVersion(),
              StringComparison.Ordinal))
      {
        this.UpdateQuestPlateGameVersion(
            foundQuestPlate.Id,
            GetGameVersion());
      }

      string translatedQuestName = questName;
      string translatedQuestMessage = questMessage;
      var translatedQuestNameReady = false;
      var translatedQuestMessageReady = false;
      var journalDetailScopeKey = BuildJournalDetailScopeKey(
          resolvedCompletedSnapshot,
          questName,
          questMessage);
      this.EnsureJournalDetailScope(journalDetailScopeKey);
      if (!this.TryGetJournalDetailOriginalSnapshot(
              journalDetailScopeKey,
              out var originalSnapshot))
      {
        originalSnapshot = new JournalDetailOriginalSnapshot(
            questName,
            questMessage,
            string.Empty,
            string.Empty,
            Array.Empty<nint>(),
            Array.Empty<string>(),
            0,
            0,
            default,
            0);
        this.RememberJournalDetailOriginalSnapshot(
            journalDetailScopeKey,
            questName,
            questMessage,
            string.Empty,
            string.Empty,
            null,
            null,
            Array.Empty<nint>(),
            Array.Empty<string>());
      }

      var originalQuestName = originalSnapshot.QuestName;
      var originalQuestMessage = originalSnapshot.QuestMessage;

      if (this.TryGetJournalDetailCachedText(
              journalDetailScopeKey,
              originalQuestName,
              out translatedQuestName) &&
          this.TryGetJournalDetailCachedText(
              journalDetailScopeKey,
              originalQuestMessage,
              out translatedQuestMessage))
      {
        translatedQuestNameReady = true;
        translatedQuestMessageReady = true;
      }
      else if (foundQuestPlate != null)
      {
        translatedQuestName = string.IsNullOrWhiteSpace(
                foundQuestPlate.TranslatedQuestName)
            ? originalQuestName
            : foundQuestPlate.TranslatedQuestName;
        translatedQuestMessage = string.IsNullOrWhiteSpace(
                foundQuestPlate.TranslatedQuestMessage)
            ? originalQuestMessage
            : foundQuestPlate.TranslatedQuestMessage;
        translatedQuestNameReady = !string.IsNullOrWhiteSpace(
            foundQuestPlate.TranslatedQuestName);
        translatedQuestMessageReady = !string.IsNullOrWhiteSpace(
            foundQuestPlate.TranslatedQuestMessage);
      }

      if (this.JournalDetailShouldRemoveDiacritics)
      {
        translatedQuestName = this.NormalizeQuestText(
            translatedQuestName ?? string.Empty);
        translatedQuestMessage = this.NormalizeQuestText(
            translatedQuestMessage ?? string.Empty);
      }

      if (this.JournalDetailWritesNativeTranslation)
      {
        questNameNode->SetText(translatedQuestName);
        descriptionNode->SetText(translatedQuestMessage);
      }
      else
      {
        questNameNode->SetText(originalQuestName);
        descriptionNode->SetText(originalQuestMessage);
      }

      this.RememberJournalDetailCachedText(
          journalDetailScopeKey,
          originalQuestName,
          translatedQuestName);
      this.RememberJournalDetailCachedText(
          journalDetailScopeKey,
          originalQuestMessage,
          translatedQuestMessage);

      if (this.JournalDetailUsesHoverTooltips)
      {
        this.RegisterTranslatedHoverTooltip(
            $"JournalDetail-CompletedQuestName-{(nint)questNameNode:X}",
            questNameNode,
            originalQuestName,
            translatedQuestName,
            translatedPayloadReady: this.CanRenderJournalDetailHoverTooltip(
                translatedQuestNameReady),
            swapEnabled: this.JournalDetailHoverShowsOriginal,
            forceEnabled: true,
            denseHitbox: true);
        this.RegisterTranslatedHoverTooltip(
            $"JournalDetail-CompletedQuestMessage-{(nint)descriptionNode:X}",
            descriptionNode,
            originalQuestMessage,
            translatedQuestMessage,
            translatedPayloadReady: this.CanRenderJournalDetailHoverTooltip(
                translatedQuestMessageReady),
            swapEnabled: this.JournalDetailHoverShowsOriginal,
            forceEnabled: true,
            denseHitbox: true);

        var completedQuestBodyHoverKey =
            $"JournalDetail-CompletedQuestBody-{(nint)descriptionNode:X}";
        var questCanvasNode = journalDetail->GetNodeById(14);
        if (this.TryGetQuestPlateHoverBounds(
                questCanvasNode,
                out var bodyTopLeft,
                out var bodyBottomRight))
        {
          this.RegisterTranslatedHoverTooltip(
              completedQuestBodyHoverKey,
              bodyTopLeft,
              bodyBottomRight,
              originalQuestMessage,
              translatedQuestMessage,
              translatedPayloadReady: this.CanRenderJournalDetailHoverTooltip(
                  translatedQuestMessageReady),
              swapEnabled: this.JournalDetailHoverShowsOriginal,
              forceEnabled: true);
        }
        else
        {
          var bodyLeft = descriptionNode->ScreenX;
          var bodyTop = descriptionNode->ScreenY;
          var bodyRight =
              bodyLeft + Math.Max(1f, descriptionNode->GetWidth());
          var bodyBottom =
              bodyTop + Math.Max(1f, descriptionNode->GetHeight());
          bodyLeft -= 28f;
          bodyTop -= 18f;
          bodyRight += 28f;
          bodyBottom += 22f;

          this.RegisterTranslatedHoverTooltip(
              completedQuestBodyHoverKey,
              new Vector2(bodyLeft, bodyTop),
              new Vector2(bodyRight, bodyBottom),
              originalQuestMessage,
              translatedQuestMessage,
              translatedPayloadReady: this.CanRenderJournalDetailHoverTooltip(
                  translatedQuestMessageReady),
              swapEnabled: this.JournalDetailHoverShowsOriginal,
              forceEnabled: true);
        }
      }
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Error($"Error in UiJournalDetailHandler: {e}");
    }
  }

  /// <summary>
  ///     Translates the active JournalDetail detail view.
  /// </summary>
  /// <param name="journalDetail">The live JournalDetail addon.</param>
  /// <returns><c>true</c> when the active detail pane is the current-quest view.</returns>
  private unsafe bool TranslateJournalBox(AtkUnitBase* journalDetail)
  {
    try
    {
      var questNameNode = journalDetail->GetTextNodeById(38);
      if (questNameNode == null || questNameNode->NodeText.IsEmpty)
      {
        return false;
      }

      if (!journalDetail->GetNodeById(43)->IsVisible())
      {
        return false;
      }

      var journalBox = journalDetail->GetNodeById(43)->GetComponent();
      var description = journalBox->UldManager.SearchNodeById(8);
      if (description == null || description->Type != NodeType.Text)
      {
        return false;
      }

      var objectiveResNode =
          journalBox->UldManager.SearchNodeById(12)->GetComponent()->
              UldManager.SearchNodeById(3);
      if (objectiveResNode == null ||
          objectiveResNode->Type != NodeType.Text)
      {
        return true;
      }

      var summaryText = string.Empty;
      AtkTextNode* summaryNode = null;
      var summaryBox = journalBox->UldManager.SearchNodeById(52);
      if (summaryBox != null && summaryBox->IsVisible())
      {
        var summaryResNode =
            summaryBox->GetComponent()->UldManager.SearchNodeById(2);
        if (summaryResNode != null &&
            summaryResNode->Type == NodeType.Text)
        {
          summaryNode = summaryResNode->GetAsAtkTextNode();
          summaryText = MemoryHelper.ReadSeStringAsString(
              out _,
              (nint)summaryNode->NodeText.StringPtr.Value);
        }
      }

      var liveQuestName = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)questNameNode->NodeText.StringPtr.Value);
      var descriptionNode = description->GetAsAtkTextNode();
      var liveQuestMessage = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)descriptionNode->NodeText.StringPtr.Value);
      var objectiveNode = objectiveResNode->GetAsAtkTextNode();
      var liveObjectiveText = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)objectiveNode->NodeText.StringPtr.Value);
      var questName = liveQuestName;
      var questMessage = liveQuestMessage;
      var objectiveText = liveObjectiveText;
      var questPlate = this.CreateQuestPlate(questName, questMessage, string.Empty);

      QuestProgressSnapshot? questProgressSnapshot = null;
      if (QuestProgressResolver.TryResolveQuestProgress(
              questPlate,
              out var resolvedQuestProgressSnapshot))
      {
        questProgressSnapshot = resolvedQuestProgressSnapshot;
        questPlate.SourceContentHash = resolvedQuestProgressSnapshot.ContentHash;
      }
      else if (this.TryGetJournalDetailOriginalSnapshot(
                   this.currentJournalDetailScopeKey,
                   out var originalSnapshot))
      {
        questName = originalSnapshot.QuestName;
        questMessage = originalSnapshot.QuestMessage;
        objectiveText = originalSnapshot.ObjectiveText;
        summaryText = originalSnapshot.SummaryText;
        questPlate = this.CreateQuestPlate(questName, questMessage, string.Empty);
        if (QuestProgressResolver.TryResolveQuestProgress(
                questPlate,
                out resolvedQuestProgressSnapshot))
        {
          questProgressSnapshot = resolvedQuestProgressSnapshot;
          questPlate.SourceContentHash = resolvedQuestProgressSnapshot.ContentHash;
        }
      }

      var foundQuestPlate = this.FindQuestPlate(questPlate);
      if (foundQuestPlate != null &&
          !string.Equals(
              foundQuestPlate.GameVersion,
              GetGameVersion(),
              StringComparison.Ordinal))
      {
        this.UpdateQuestPlateGameVersion(
            foundQuestPlate.Id,
            GetGameVersion());
      }

      this.EnsureQuestPlateMetadataPersisted(
          foundQuestPlate,
          questProgressSnapshot);

      this.TranslateQuestOnJournalBox(
          journalBox,
          foundQuestPlate,
          questProgressSnapshot,
          questName,
          questMessage,
          objectiveText,
          summaryText,
          questNameNode,
          descriptionNode,
          objectiveNode,
          summaryBox,
          summaryNode);
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Error($"Error in UIJournalDetailHandler: {e}");
    }

    return true;
  }

  /// <summary>
  ///     Handles JournalDetail refresh events.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnJournalDetailEvent(AddonEvent type, AddonArgs args)
  {
    var isDirectJournalDetailEvent = string.Equals(
        args.AddonName,
        JournalDetailAddonName,
        StringComparison.Ordinal);
    var isJournalDrivenSelectionRefresh =
        type == AddonEvent.PostRequestedUpdate &&
        string.Equals(
            args.AddonName,
            JournalAddonName,
            StringComparison.Ordinal);
    if (!isDirectJournalDetailEvent && !isJournalDrivenSelectionRefresh)
    {
      return;
    }

    this.TranslateJournalDetail();
  }

  /// <summary>
  ///     Clears JournalDetail hover registrations and runtime cache when the
  ///     detail addon closes.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private void OnJournalDetailCleanupEvent(AddonEvent type, AddonArgs args)
  {
    if (!string.Equals(
            args.AddonName,
            JournalDetailAddonName,
            StringComparison.Ordinal))
    {
      return;
    }

    this.journalDetailTextCache.Clear();
    this.journalDetailOriginalCache.Clear();
    this.currentJournalDetailScopeKey = string.Empty;
    this.RemoveHoverTooltipsByPrefix(JournalDetailHoverPrefix);
  }

  /// <summary>
  ///     Stores the original JournalDetail texts for a single visible quest
  ///     scope so mode changes can restore native UI state and drive swap
  ///     tooltips from stable source text.
  /// </summary>
  /// <param name="QuestName">The original quest title.</param>
  /// <param name="QuestMessage">The original quest description.</param>
  /// <param name="ObjectiveText">The original visible objective text.</param>
  /// <param name="SummaryText">The original visible summary text.</param>
  /// <param name="AdditionalSummaryNodeAddresses">
  ///     The supplemental summary text nodes captured for this scope.
  /// </param>
  /// <param name="AdditionalSummaryTexts">
  ///     The original visible supplemental summary texts.
  /// </param>
  /// <param name="SummaryNodeWidth">The original primary summary node width.</param>
  /// <param name="SummaryContainerHeight">
  ///     The original summary container height.
  /// </param>
  /// <param name="SummaryNodeTextFlags">
  ///     The original primary summary node text flags.
  /// </param>
  /// <param name="SummaryNodeFontSize">
  ///     The original primary summary node font size.
  /// </param>
  private sealed record JournalDetailOriginalSnapshot(
      string QuestName,
      string QuestMessage,
      string ObjectiveText,
      string SummaryText,
      IReadOnlyList<nint> AdditionalSummaryNodeAddresses,
      IReadOnlyList<string> AdditionalSummaryTexts,
      ushort SummaryNodeWidth,
      ushort SummaryContainerHeight,
      TextFlags SummaryNodeTextFlags,
      byte SummaryNodeFontSize);
}
