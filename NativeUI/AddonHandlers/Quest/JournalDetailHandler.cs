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

  private const uint JournalDetailSummaryLabelTextId = 476;

  private const uint JournalDetailDescriptionLabelTextId = 543;

  private static readonly uint[] JournalDetailObjectiveLabelTextIds =
  [
    462,
    3160,
  ];

  private readonly Dictionary<string, string> journalDetailTextCache =
      new(StringComparer.Ordinal);

  private readonly Dictionary<string, JournalDetailOriginalSnapshot>
      journalDetailOriginalCache =
          new(StringComparer.Ordinal);
  private readonly HashSet<string> journalDetailNativeMutationScopes =
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
  private bool CanRenderJournalDetailHoverTooltip(bool translatedPayloadReady)
  {
    if (!this.JournalDetailUsesHoverTooltips)
    {
      return false;
    }

    if (this.JournalDetailHoverShowsOriginal)
    {
      return true;
    }

    return translatedPayloadReady;
  }

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
    this.journalDetailNativeMutationScopes.Clear();
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
  /// <param name="descriptionNode">The live description text node.</param>
  /// <param name="objectiveNodeAddresses">
  ///     The live visible objective text nodes in order.
  /// </param>
  /// <param name="objectiveTexts">
  ///     The original visible objective texts in order.
  /// </param>
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
      AtkComponentBase* journalBox,
      string scopeKey,
      string questName,
      string questMessage,
      string objectiveText,
      string summaryText,
      AtkTextNode* descriptionNode,
      IReadOnlyList<nint> objectiveNodeAddresses,
      IReadOnlyList<string> objectiveTexts,
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
        this.CreateJournalDetailOriginalSnapshot(
            journalBox,
            questName,
            questMessage,
            objectiveText,
            summaryText,
            descriptionNode,
            objectiveNodeAddresses,
            objectiveTexts,
            summaryNode,
            summaryContainerNode,
            additionalSummaryNodeAddresses,
            additionalSummaryTexts);
  }

  /// <summary>
  ///     Merges newly discovered supplemental summary nodes into the existing
  ///     JournalDetail original snapshot so later native restores and hover
  ///     bounds stay stable even if the addon exposes more summary nodes on a
  ///     later repaint.
  /// </summary>
  /// <param name="scopeKey">The current quest-detail scope key.</param>
  /// <param name="originalSnapshot">The original snapshot for that scope.</param>
  /// <param name="objectiveNodeAddresses">
  ///     The currently visible objective text nodes.
  /// </param>
  /// <param name="objectiveTexts">
  ///     The currently visible objective texts.
  /// </param>
  /// <param name="visibleAdditionalSummaryNodes">
  ///     The supplemental summary nodes visible on the current repaint.
  /// </param>
  /// <returns>The merged snapshot.</returns>
  private unsafe JournalDetailOriginalSnapshot
      MergeJournalDetailOriginalSnapshotAdditionalNodes(
          AtkComponentBase* journalBox,
          string scopeKey,
          JournalDetailOriginalSnapshot originalSnapshot,
          AtkTextNode* descriptionNode,
          IReadOnlyList<nint> objectiveNodeAddresses,
          IReadOnlyList<string> objectiveTexts,
          AtkTextNode* summaryNode,
          AtkResNode* summaryContainerNode,
          IReadOnlyList<nint> visibleAdditionalSummaryNodes)
  {
    if (string.IsNullOrWhiteSpace(scopeKey))
    {
      return originalSnapshot;
    }

    var existingSummaryNodes = new HashSet<nint>(
        originalSnapshot.AdditionalSummaryNodeAddresses);
    List<nint> newlyDiscoveredSummaryNodes = [];
    foreach (var visibleAdditionalSummaryNode in visibleAdditionalSummaryNodes)
    {
      if (existingSummaryNodes.Add(visibleAdditionalSummaryNode))
      {
        newlyDiscoveredSummaryNodes.Add(visibleAdditionalSummaryNode);
      }
    }

    var mergedSnapshot = originalSnapshot with
    {
      AdditionalSummaryNodeAddresses =
          originalSnapshot.AdditionalSummaryNodeAddresses
              .Concat(newlyDiscoveredSummaryNodes)
              .ToArray(),
      AdditionalSummaryTexts =
          originalSnapshot.AdditionalSummaryTexts
              .Concat(
                  CaptureVisibleTextNodeTexts(
                      newlyDiscoveredSummaryNodes))
              .ToArray(),
      AdditionalSummaryNodeLayouts =
          originalSnapshot.AdditionalSummaryNodeLayouts
              .Concat(
                  CaptureVisibleTextNodeLayouts(
                      newlyDiscoveredSummaryNodes))
              .ToArray(),
    };

    mergedSnapshot = this.CreateJournalDetailOriginalSnapshot(
        journalBox,
        mergedSnapshot.QuestName,
        mergedSnapshot.QuestMessage,
        mergedSnapshot.ObjectiveText,
        mergedSnapshot.SummaryText,
        descriptionNode,
        objectiveNodeAddresses,
        objectiveTexts,
        summaryNode,
        summaryContainerNode,
        mergedSnapshot.AdditionalSummaryNodeAddresses,
        mergedSnapshot.AdditionalSummaryTexts);

    this.journalDetailOriginalCache[scopeKey] = mergedSnapshot;
    return mergedSnapshot;
  }

  /// <summary>
  ///     Creates one JournalDetail original snapshot, including the ordered
  ///     native reflow blocks and container chain used by native translation.
  /// </summary>
  /// <param name="journalBox">The live JournalDetail body component.</param>
  /// <param name="questName">The original quest name.</param>
  /// <param name="questMessage">The original quest message.</param>
  /// <param name="objectiveText">The original visible objective text.</param>
  /// <param name="summaryText">The original visible summary text.</param>
  /// <param name="descriptionNode">The live description text node.</param>
  /// <param name="objectiveNodeAddresses">
  ///     The live visible objective text nodes in order.
  /// </param>
  /// <param name="objectiveTexts">
  ///     The original visible objective texts in order.
  /// </param>
  /// <param name="summaryNode">The live primary summary text node, if any.</param>
  /// <param name="summaryContainerNode">The live summary container node, if any.</param>
  /// <param name="additionalSummaryNodeAddresses">The supplemental summary nodes.</param>
  /// <param name="additionalSummaryTexts">The original supplemental summary texts.</param>
  /// <returns>The captured original snapshot.</returns>
  private unsafe JournalDetailOriginalSnapshot CreateJournalDetailOriginalSnapshot(
      AtkComponentBase* journalBox,
      string questName,
      string questMessage,
      string objectiveText,
      string summaryText,
      AtkTextNode* descriptionNode,
      IReadOnlyList<nint> objectiveNodeAddresses,
      IReadOnlyList<string> objectiveTexts,
      AtkTextNode* summaryNode,
      AtkResNode* summaryContainerNode,
      IReadOnlyList<nint> additionalSummaryNodeAddresses,
      IReadOnlyList<string> additionalSummaryTexts)
  {
    var objectiveNode =
        objectiveNodeAddresses.Count != 0
            ? (AtkTextNode*)objectiveNodeAddresses[0]
            : null;
    var flowRoot = journalBox != null
        ? journalBox->UldManager.RootNode
        : null;
    var nativeFlowTextNodes =
        this.CollectVisibleJournalDetailFlowTextNodes(
            journalBox,
            descriptionNode,
            objectiveNodeAddresses,
            summaryNode,
            additionalSummaryNodeAddresses);
    var nativeFlowBlocks = flowRoot != null
        ? NativeTextFlowReflowHelper.CaptureOrderedFlowBlocks(
            flowRoot,
            nativeFlowTextNodes)
        : [];
    var nativeFlowContainers =
        CaptureJournalDetailNativeFlowContainers(
            flowRoot,
            nativeFlowBlocks);

    return new JournalDetailOriginalSnapshot(
        questName,
        questMessage,
        objectiveText,
        summaryText,
        objectiveNodeAddresses,
        objectiveTexts,
        additionalSummaryNodeAddresses,
        additionalSummaryTexts,
        descriptionNode != null ? descriptionNode->GetWidth() : (ushort)0,
        descriptionNode != null ? descriptionNode->TextFlags : default,
        descriptionNode != null ? descriptionNode->FontSize : (byte)0,
        objectiveNode != null ? objectiveNode->GetWidth() : (ushort)0,
        objectiveNode != null ? objectiveNode->TextFlags : default,
        objectiveNode != null ? objectiveNode->FontSize : (byte)0,
        summaryNode != null ? summaryNode->GetWidth() : (ushort)0,
        summaryContainerNode != null ? summaryContainerNode->GetHeight() : (ushort)0,
        summaryNode != null ? summaryNode->TextFlags : default,
        summaryNode != null ? summaryNode->FontSize : (byte)0,
        CaptureTextNodeLayout(summaryNode),
        CaptureVisibleTextNodeLayouts(additionalSummaryNodeAddresses),
        nativeFlowBlocks,
        nativeFlowContainers);
  }

  /// <summary>
  ///     Applies the native multiline presentation used by JournalDetail body
  ///     nodes when translated text is written into the game UI.
  /// </summary>
  /// <param name="textNode">The target text node.</param>
  /// <param name="originalWidth">The original node width.</param>
  /// <param name="originalTextFlags">The original text flags.</param>
  /// <param name="originalFontSize">The original font size.</param>
  /// <param name="text">The translated text to render.</param>
  private unsafe void ApplyJournalDetailNativeTextNodePresentation(
      AtkTextNode* textNode,
      ushort originalWidth,
      TextFlags originalTextFlags,
      byte originalFontSize,
      string? text)
  {
    if (textNode == null)
    {
      return;
    }

    if (originalWidth != 0)
    {
      textNode->SetWidth(originalWidth);
    }

    if (originalFontSize != 0)
    {
      textNode->FontSize = originalFontSize;
    }

    textNode->TextFlags =
        originalTextFlags |
        TextFlags.WordWrap |
        TextFlags.MultiLine |
        TextFlags.AutoAdjustNodeSize;
    textNode->SetText(text ?? string.Empty);
    textNode->ResizeNodeForCurrentText();
  }

  /// <summary>
  ///     Restores the original JournalDetail presentation for a body text
  ///     node after leaving native translation mode.
  /// </summary>
  /// <param name="textNode">The target text node.</param>
  /// <param name="originalWidth">The original node width.</param>
  /// <param name="originalTextFlags">The original text flags.</param>
  /// <param name="originalFontSize">The original font size.</param>
  /// <param name="text">The original text to restore.</param>
  private unsafe void RestoreJournalDetailTextNodePresentation(
      AtkTextNode* textNode,
      ushort originalWidth,
      TextFlags originalTextFlags,
      byte originalFontSize,
      string? text)
  {
    if (textNode == null)
    {
      return;
    }

    if (originalWidth != 0)
    {
      textNode->SetWidth(originalWidth);
    }

    if (originalFontSize != 0)
    {
      textNode->FontSize = originalFontSize;
    }

    textNode->TextFlags = originalTextFlags;
    textNode->SetText(text ?? string.Empty);
    textNode->ResizeNodeForCurrentText();
  }

  /// <summary>
  ///     Captures the current layout metadata of a visible JournalDetail text
  ///     node so native mode can restore both presentation and position.
  /// </summary>
  /// <param name="textNode">The text node to snapshot.</param>
  /// <returns>The captured layout, or <c>null</c> when the node is unavailable.</returns>
  private static unsafe JournalDetailTextNodeLayout? CaptureTextNodeLayout(
      AtkTextNode* textNode)
  {
    if (textNode == null)
    {
      return null;
    }

    return new JournalDetailTextNodeLayout(
        (nint)textNode,
        (short)Math.Clamp(
            Math.Round(textNode->X),
            short.MinValue,
            short.MaxValue),
        (short)Math.Clamp(
            Math.Round(textNode->Y),
            short.MinValue,
            short.MaxValue),
        textNode->GetWidth(),
        textNode->GetHeight(),
        textNode->TextFlags,
        textNode->FontSize);
  }

  /// <summary>
  ///     Captures the current layout metadata of the supplied text-node
  ///     addresses in their current display order.
  /// </summary>
  /// <param name="nodes">The visible text-node addresses.</param>
  /// <returns>The captured node layouts.</returns>
  private static unsafe List<JournalDetailTextNodeLayout>
      CaptureVisibleTextNodeLayouts(IEnumerable<nint> nodes)
  {
    List<JournalDetailTextNodeLayout> layouts = [];
    foreach (var nodeAddress in nodes)
    {
      var nodeLayout = CaptureTextNodeLayout((AtkTextNode*)nodeAddress);
      if (nodeLayout != null)
      {
        layouts.Add(nodeLayout);
      }
    }

    return layouts;
  }

  /// <summary>
  ///     Applies the translated JournalDetail summary block to the primary
  ///     summary node while clearing the supplemental summary nodes that would
  ///     otherwise leak original text underneath the native translation.
  /// </summary>
  /// <param name="originalSnapshot">The original quest-detail snapshot.</param>
  /// <param name="summaryContainerNode">The live summary container node, if any.</param>
  /// <param name="translatedSections">The translated summary sections in order.</param>
  private unsafe void ApplyJournalDetailNativeSummaryFlow(
      JournalDetailOriginalSnapshot originalSnapshot,
      AtkResNode* summaryContainerNode,
      IReadOnlyList<string> translatedSections)
  {
    var translatedSummaryDisplayText = BuildQuestPlateSummarySection(
        translatedSections);
    if (originalSnapshot.SummaryNodeLayout != null)
    {
      var summaryTextNode =
          (AtkTextNode*)originalSnapshot.SummaryNodeLayout.NodeAddress;
      if (summaryTextNode != null)
      {
        summaryTextNode->SetPositionShort(
            originalSnapshot.SummaryNodeLayout.X,
            originalSnapshot.SummaryNodeLayout.Y);
        this.ApplyJournalDetailNativeTextNodePresentation(
            summaryTextNode,
            originalSnapshot.SummaryNodeLayout.Width,
            originalSnapshot.SummaryNodeLayout.TextFlags,
            originalSnapshot.SummaryNodeLayout.FontSize,
            translatedSummaryDisplayText);

        if (summaryContainerNode != null &&
            originalSnapshot.SummaryContainerHeight != 0)
        {
          var desiredSummaryContainerHeight = (ushort)Math.Clamp(
              Math.Ceiling(
                  Math.Max(
                      originalSnapshot.SummaryContainerHeight,
                      summaryTextNode->GetHeight() + 12f)),
              ushort.MinValue,
              ushort.MaxValue);
          summaryContainerNode->SetHeight(desiredSummaryContainerHeight);
        }
      }
    }

    foreach (var layout in originalSnapshot.AdditionalSummaryNodeLayouts)
    {
      var summaryTextNode = (AtkTextNode*)layout.NodeAddress;
      if (summaryTextNode == null)
      {
        continue;
      }

      summaryTextNode->SetPositionShort(layout.X, layout.Y);
      this.RestoreJournalDetailTextNodePresentation(
          summaryTextNode,
          layout.Width,
          layout.TextFlags,
          layout.FontSize,
          string.Empty);
    }
  }

  /// <summary>
  ///     Restores the original JournalDetail summary-node positions,
  ///     presentation, and texts after leaving native translation mode.
  /// </summary>
  /// <param name="originalSnapshot">The original quest-detail snapshot.</param>
  /// <param name="summaryContainerNode">The live summary container node, if any.</param>
  private unsafe void RestoreJournalDetailOriginalSummaryFlow(
      JournalDetailOriginalSnapshot originalSnapshot,
      AtkResNode* summaryContainerNode)
  {
    if (originalSnapshot.SummaryNodeLayout != null)
    {
      var summaryTextNode =
          (AtkTextNode*)originalSnapshot.SummaryNodeLayout.NodeAddress;
      if (summaryTextNode != null)
      {
        summaryTextNode->SetPositionShort(
            originalSnapshot.SummaryNodeLayout.X,
            originalSnapshot.SummaryNodeLayout.Y);
        this.RestoreJournalDetailTextNodePresentation(
            summaryTextNode,
            originalSnapshot.SummaryNodeLayout.Width,
            originalSnapshot.SummaryNodeLayout.TextFlags,
            originalSnapshot.SummaryNodeLayout.FontSize,
            originalSnapshot.SummaryText);
      }
    }

    for (var i = 0; i < originalSnapshot.AdditionalSummaryNodeLayouts.Count; i++)
    {
      var layout = originalSnapshot.AdditionalSummaryNodeLayouts[i];
      var summaryTextNode = (AtkTextNode*)layout.NodeAddress;
      if (summaryTextNode == null)
      {
        continue;
      }

      var originalAdditionalSummaryText =
          i < originalSnapshot.AdditionalSummaryTexts.Count
              ? originalSnapshot.AdditionalSummaryTexts[i]
              : string.Empty;
      summaryTextNode->SetPositionShort(layout.X, layout.Y);
      this.RestoreJournalDetailTextNodePresentation(
          summaryTextNode,
          layout.Width,
          layout.TextFlags,
          layout.FontSize,
          originalAdditionalSummaryText);
    }

    if (summaryContainerNode != null &&
        originalSnapshot.SummaryContainerHeight != 0)
    {
      summaryContainerNode->SetHeight(originalSnapshot.SummaryContainerHeight);
    }
  }

  /// <summary>
  ///     Applies the translated JournalDetail native body flow using the
  ///     shared block-reflow helper when a flow snapshot is available.
  /// </summary>
  /// <param name="originalSnapshot">The original quest-detail snapshot.</param>
  /// <param name="descriptionNode">The live description text node.</param>
  /// <param name="translatedDescriptionText">The translated description text.</param>
  /// <param name="objectiveNodeAddresses">
  ///     The live visible objective text nodes in order.
  /// </param>
  /// <param name="translatedObjectiveTexts">
  ///     The translated objective texts in visible order.
  /// </param>
  /// <param name="summaryNode">The live primary summary text node.</param>
  /// <param name="translatedSummarySections">The translated summary sections.</param>
  /// <returns><c>true</c> when the shared reflow helper handled the body flow.</returns>
  private unsafe bool TryApplyJournalDetailNativeBodyFlow(
      JournalDetailOriginalSnapshot originalSnapshot,
      AtkTextNode* descriptionNode,
      string translatedDescriptionText,
      IReadOnlyList<nint> objectiveNodeAddresses,
      IReadOnlyList<string> translatedObjectiveTexts,
      AtkTextNode* summaryNode,
      IReadOnlyList<string> translatedSummarySections)
  {
    if (originalSnapshot.NativeFlowBlocks.Count == 0)
    {
      return false;
    }

    var translatedFlowTexts = BuildJournalDetailNativeFlowTextMap(
        descriptionNode,
        translatedDescriptionText,
        objectiveNodeAddresses,
        translatedObjectiveTexts,
        summaryNode,
        originalSnapshot.AdditionalSummaryNodeAddresses,
        translatedSummarySections);
    NativeTextFlowReflowHelper.ApplyVerticalTextFlow(
        originalSnapshot.NativeFlowBlocks,
        translatedFlowTexts,
        this.ApplyJournalDetailNativeTextNodePresentation,
        originalSnapshot.NativeFlowContainerSnapshots);
    return true;
  }

  /// <summary>
  ///     Restores the original JournalDetail body flow using the shared
  ///     reflow helper when a flow snapshot is available.
  /// </summary>
  /// <param name="originalSnapshot">The original quest-detail snapshot.</param>
  /// <param name="descriptionNode">The live description text node.</param>
  /// <param name="objectiveNodeAddresses">
  ///     The live visible objective text nodes in order.
  /// </param>
  /// <param name="summaryNode">The live primary summary text node.</param>
  /// <returns><c>true</c> when the shared reflow helper handled the restore.</returns>
  private unsafe bool TryRestoreJournalDetailOriginalBodyFlow(
      JournalDetailOriginalSnapshot originalSnapshot,
      AtkTextNode* descriptionNode,
      IReadOnlyList<nint> objectiveNodeAddresses,
      AtkTextNode* summaryNode)
  {
    if (originalSnapshot.NativeFlowBlocks.Count == 0)
    {
      return false;
    }

    var originalFlowTexts = BuildJournalDetailNativeFlowTextMap(
        descriptionNode,
        originalSnapshot.QuestMessage,
        objectiveNodeAddresses,
        originalSnapshot.ObjectiveTexts,
        summaryNode,
        originalSnapshot.AdditionalSummaryNodeAddresses,
        originalSnapshot.AdditionalSummaryTexts
            .Prepend(originalSnapshot.SummaryText)
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .ToArray());
    NativeTextFlowReflowHelper.RestoreVerticalTextFlow(
        originalSnapshot.NativeFlowBlocks,
        originalFlowTexts,
        this.RestoreJournalDetailTextNodePresentation,
        originalSnapshot.NativeFlowContainerSnapshots);
    return true;
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
  ///     Normalizes JournalDetail summary text for stable node-content
  ///     matching across live addon refreshes and native writes.
  /// </summary>
  /// <param name="text">The summary text to normalize.</param>
  /// <returns>The normalized summary text.</returns>
  private static string NormalizeJournalDetailSummaryCandidateText(
      string? text)
  {
    if (string.IsNullOrWhiteSpace(text))
    {
      return string.Empty;
    }

    return string.Join(
        " ",
        text.Split(
            ["\r", "\n"],
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries));
  }

  /// <summary>
  ///     Builds the set of summary texts that should be considered part of the
  ///     active JournalDetail summary block on the current repaint.
  /// </summary>
  /// <param name="summarySections">The known summary sections.</param>
  /// <returns>The normalized summary text candidates.</returns>
  private static HashSet<string> BuildJournalDetailSummaryTextCandidates(
      IEnumerable<string?> summarySections)
  {
    HashSet<string> summaryTextCandidates = new(StringComparer.Ordinal);
    foreach (var summarySection in summarySections)
    {
      var normalizedSummarySection =
          NormalizeJournalDetailSummaryCandidateText(summarySection);
      if (!string.IsNullOrWhiteSpace(normalizedSummarySection))
      {
        summaryTextCandidates.Add(normalizedSummarySection);
      }
    }

    return summaryTextCandidates;
  }

  /// <summary>
  ///     Gets whether a visible JournalDetail text node looks like one of the
  ///     known summary paragraphs for the active quest.
  /// </summary>
  /// <param name="visibleText">The current visible text of the node.</param>
  /// <param name="summaryTextCandidates">The known summary texts.</param>
  /// <returns><c>true</c> when the node text matches the summary set.</returns>
  private static bool MatchesJournalDetailSummaryCandidate(
      string? visibleText,
      IReadOnlyCollection<string> summaryTextCandidates)
  {
    if (summaryTextCandidates.Count == 0)
    {
      return false;
    }

    var normalizedVisibleText =
        NormalizeJournalDetailSummaryCandidateText(visibleText);
    if (string.IsNullOrWhiteSpace(normalizedVisibleText))
    {
      return false;
    }

    if (summaryTextCandidates.Contains(normalizedVisibleText))
    {
      return true;
    }

    foreach (var summaryTextCandidate in summaryTextCandidates)
    {
      if (summaryTextCandidate.Length < 24 &&
          normalizedVisibleText.Length < 24)
      {
        continue;
      }

      if (summaryTextCandidate.Contains(
              normalizedVisibleText,
              StringComparison.Ordinal) ||
          normalizedVisibleText.Contains(
              summaryTextCandidate,
              StringComparison.Ordinal))
      {
        return true;
      }
    }

    return false;
  }

  /// <summary>
  ///     Collects the visible JournalDetail supplemental summary nodes in their
  ///     current display order.
  /// </summary>
  /// <param name="journalBox">The live JournalDetail body component.</param>
  /// <param name="descriptionNode">The live description text node.</param>
  /// <param name="objectiveNode">The live objective text node.</param>
  /// <param name="summaryNode">The live primary summary text node, if any.</param>
  /// <param name="summaryTextCandidates">
  ///     The normalized summary texts expected for the active quest.
  /// </param>
  /// <returns>The visible supplemental summary text nodes.</returns>
  private unsafe List<nint> CollectVisibleAdditionalSummaryNodes(
      AtkComponentBase* journalBox,
      AtkTextNode* descriptionNode,
      AtkTextNode* objectiveNode,
      AtkTextNode* summaryNode,
      IReadOnlyCollection<string> summaryTextCandidates)
  {
    List<nint> summaryNodes = [];
    HashSet<nint> seenSummaryNodes = [];
    var descriptionNodeAddress = (nint)descriptionNode;
    var objectiveNodeAddress = (nint)objectiveNode;
    var summaryNodeAddress = (nint)summaryNode;
    var summaryAnchorX = summaryNode != null ? summaryNode->ScreenX : 0f;
    var summaryAnchorY = summaryNode != null ? summaryNode->ScreenY : 0f;
    var summaryAnchorWidth = summaryNode != null
        ? Math.Max(1f, summaryNode->GetWidth())
        : 0f;

    void ConsiderSummaryTextNode(AtkTextNode* summaryTextNode)
    {
      if (summaryTextNode == null || !summaryTextNode->IsVisible())
      {
        return;
      }

      var summaryTextNodeAddress = (nint)summaryTextNode;
      if (summaryTextNodeAddress == descriptionNodeAddress ||
          summaryTextNodeAddress == objectiveNodeAddress ||
          summaryTextNodeAddress == summaryNodeAddress ||
          !seenSummaryNodes.Add(summaryTextNodeAddress))
      {
        return;
      }

      var matchesLegacySummaryRange =
          summaryTextNode->NodeId >= 480700 && summaryTextNode->NodeId <= 481200;
      var summaryTextValue = summaryTextNode->NodeText.IsEmpty
          ? string.Empty
          : MemoryHelper.ReadSeStringAsString(
              out _,
              (nint)summaryTextNode->NodeText.StringPtr.Value);
      var matchesSummaryText =
          MatchesJournalDetailSummaryCandidate(
              summaryTextValue,
              summaryTextCandidates);
      var matchesSummaryLayout = summaryNode != null &&
                                 summaryTextNode->ScreenY >= summaryAnchorY - 6f &&
                                 summaryTextNode->ScreenX >= summaryAnchorX - 72f &&
                                 summaryTextNode->ScreenX <=
                                 summaryAnchorX + Math.Max(120f, summaryAnchorWidth + 96f) &&
                                 Math.Max(1f, summaryTextNode->GetWidth()) >=
                                 Math.Max(48f, summaryAnchorWidth * 0.35f);
      var shouldIncludeSummaryNode =
          matchesLegacySummaryRange ||
          matchesSummaryText ||
          (summaryTextCandidates.Count == 0 && matchesSummaryLayout);
      if (!shouldIncludeSummaryNode)
      {
        return;
      }

      summaryNodes.Add(summaryTextNodeAddress);
    }

    void CollectVisibleSummaryTextNodesFromComponent(AtkComponentBase* component)
    {
      if (component == null)
      {
        return;
      }

      for (var childIndex = 0; childIndex < component->UldManager.NodeListCount; childIndex++)
      {
        var childNode = component->UldManager.NodeList[childIndex];
        if (childNode == null || !childNode->IsVisible())
        {
          continue;
        }

        if (childNode->Type == NodeType.Text)
        {
          ConsiderSummaryTextNode(childNode->GetAsAtkTextNode());
          continue;
        }

        var componentNode = childNode->GetAsAtkComponentNode();
        if (componentNode != null && componentNode->Component != null)
        {
          CollectVisibleSummaryTextNodesFromComponent(componentNode->Component);
        }
      }
    }

    if (journalBox != null)
    {
      CollectVisibleSummaryTextNodesFromComponent(journalBox);
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
  ///     Builds the visible JournalDetail objective block text for hover and
  ///     native-payload readiness checks.
  /// </summary>
  /// <param name="objectiveSections">The visible objective sections in order.</param>
  /// <returns>The combined objective text.</returns>
  private static string BuildJournalDetailObjectiveDisplayText(
      IEnumerable<string> objectiveSections)
  {
    return string.Join(
        Environment.NewLine,
        objectiveSections.Where(
            static section => !string.IsNullOrWhiteSpace(section)));
  }

  /// <summary>
  ///     Resolves the currently visible JournalDetail body sections from the
  ///     active body flow instead of relying on ambiguous repeated node ids.
  /// </summary>
  /// <param name="journalBox">The live JournalDetail body component.</param>
  /// <param name="descriptionNode">The resolved description text node.</param>
  /// <param name="objectiveNodeAddresses">
  ///     The resolved visible objective text nodes in order.
  /// </param>
  /// <param name="summaryContainerNode">
  ///     The resolved summary section container node.
  /// </param>
  /// <param name="summaryNode">The resolved primary summary text node.</param>
  /// <param name="additionalSummaryNodeAddresses">
  ///     The resolved supplemental summary text nodes in order.
  /// </param>
  /// <returns><c>true</c> when the visible body sections were resolved.</returns>
  private static unsafe bool TryResolveJournalDetailActiveBodyNodes(
      AtkComponentBase* journalBox,
      out AtkTextNode* descriptionNode,
      out List<nint> objectiveNodeAddresses,
      out AtkResNode* summaryContainerNode,
      out AtkTextNode* summaryNode,
      out List<nint> additionalSummaryNodeAddresses)
  {
    descriptionNode = null;
    objectiveNodeAddresses = [];
    summaryContainerNode = null;
    summaryNode = null;
    additionalSummaryNodeAddresses = [];
    if (journalBox == null)
    {
      return false;
    }

    var flowRoot = journalBox->UldManager.RootNode;
    var viewportNode = flowRoot;
    if (flowRoot != null && flowRoot->ParentNode != null)
    {
      viewportNode = flowRoot->ParentNode;
    }
    var viewportTop = viewportNode != null
        ? viewportNode->ScreenY - 4f
        : float.MinValue;
    var viewportBottom = viewportNode != null
        ? viewportNode->ScreenY + Math.Max(1f, viewportNode->GetHeight()) + 4f
        : float.MaxValue;

    if (!TryResolveVisibleJournalDetailSection(
            journalBox,
            [JournalDetailDescriptionLabelTextId],
            viewportTop,
            viewportBottom,
            out _,
            out var descriptionLabelNode,
            out var descriptionBodyNodes) ||
        descriptionBodyNodes.Count == 0)
    {
      return false;
    }

    descriptionNode = (AtkTextNode*)descriptionBodyNodes[0];

    if (!TryResolveVisibleJournalDetailSection(
            journalBox,
            JournalDetailObjectiveLabelTextIds,
            viewportTop,
            viewportBottom,
            out _,
            out _,
            out objectiveNodeAddresses) ||
        objectiveNodeAddresses.Count == 0)
    {
      return false;
    }

    if (!TryResolveVisibleJournalDetailSection(
            journalBox,
            [JournalDetailSummaryLabelTextId],
            viewportTop,
            viewportBottom,
            out summaryContainerNode,
            out _,
            out var summaryBodyNodes) ||
        summaryBodyNodes.Count == 0)
    {
      return false;
    }

    summaryNode = (AtkTextNode*)summaryBodyNodes[0];
    additionalSummaryNodeAddresses = summaryBodyNodes.Skip(1).ToList();
    return descriptionLabelNode != null;
  }

  /// <summary>
  ///     Resolves one visible JournalDetail section by its label text id and
  ///     returns the visible body text nodes that belong to that section.
  /// </summary>
  /// <param name="journalBox">The live JournalDetail body component.</param>
  /// <param name="labelTextIds">The section label text ids.</param>
  /// <param name="viewportTop">The visible viewport top.</param>
  /// <param name="viewportBottom">The visible viewport bottom.</param>
  /// <param name="sectionContainerNode">The resolved section container.</param>
  /// <param name="labelNode">The resolved visible label node.</param>
  /// <param name="bodyNodeAddresses">The resolved visible body text nodes.</param>
  /// <returns><c>true</c> when the section was resolved.</returns>
  private static unsafe bool TryResolveVisibleJournalDetailSection(
      AtkComponentBase* journalBox,
      IReadOnlyCollection<uint> labelTextIds,
      float viewportTop,
      float viewportBottom,
      out AtkResNode* sectionContainerNode,
      out AtkTextNode* labelNode,
      out List<nint> bodyNodeAddresses)
  {
    sectionContainerNode = null;
    labelNode = null;
    bodyNodeAddresses = [];
    if (journalBox == null || labelTextIds.Count == 0)
    {
      return false;
    }

    var flowRoot = journalBox->UldManager.RootNode;
    if (flowRoot == null)
    {
      return false;
    }

    var candidateLabelNodes = CollectVisibleJournalDetailLabelTextNodes(
        flowRoot,
        labelTextIds,
        viewportTop,
        viewportBottom);
    if (candidateLabelNodes.Count == 0)
    {
      return false;
    }

    foreach (var candidateLabelNodeAddress in candidateLabelNodes)
    {
      var candidateLabelNode = (AtkTextNode*)candidateLabelNodeAddress;
      var candidateSectionContainerNode =
          ResolveJournalDetailSectionContainerNode(
              flowRoot,
              candidateLabelNode);
      if (candidateSectionContainerNode == null)
      {
        continue;
      }

      var candidateBodyNodes = CollectVisibleJournalDetailSectionBodyTextNodes(
          candidateSectionContainerNode,
          candidateLabelNode,
          viewportTop,
          viewportBottom);
      if (candidateBodyNodes.Count == 0)
      {
        continue;
      }

      sectionContainerNode = candidateSectionContainerNode;
      labelNode = candidateLabelNode;
      bodyNodeAddresses = candidateBodyNodes;
      return true;
    }

    return false;
  }

  /// <summary>
  ///     Collects the visible JournalDetail section label text nodes inside
  ///     the active body flow and sorts them by on-screen order.
  /// </summary>
  /// <param name="rootNode">The candidate section subtree root.</param>
  /// <param name="labelTextIds">The accepted label text ids.</param>
  /// <param name="viewportTop">The visible viewport top.</param>
  /// <param name="viewportBottom">The visible viewport bottom.</param>
  /// <returns>The resolved visible label text nodes in screen order.</returns>
  private static unsafe List<nint>
      CollectVisibleJournalDetailLabelTextNodes(
      AtkResNode* rootNode,
      IReadOnlyCollection<uint> labelTextIds,
      float viewportTop,
      float viewportBottom)
  {
    List<nint> resolvedLabelNodes = [];
    HashSet<nint> visitedNodes = [];

    void Traverse(AtkResNode* node)
    {
      if (node == null || !visitedNodes.Add((nint)node))
      {
        return;
      }

      if (node->Type == NodeType.Text)
      {
        var textNode = node->GetAsAtkTextNode();
        if (textNode != null &&
            textNode->IsVisible() &&
            labelTextIds.Contains(textNode->TextId) &&
            IsJournalDetailNodeWithinViewport(
                node,
                viewportTop,
                viewportBottom))
        {
          if (!resolvedLabelNodes.Contains((nint)textNode))
          {
            resolvedLabelNodes.Add((nint)textNode);
          }
        }
      }

      var componentNode = node->GetAsAtkComponentNode();
      if (componentNode != null && componentNode->Component != null)
      {
        for (var childIndex = 0;
             childIndex < componentNode->Component->UldManager.NodeListCount;
             childIndex++)
        {
          Traverse(componentNode->Component->UldManager.NodeList[childIndex]);
        }
      }

      Traverse(node->ChildNode);
      Traverse(node->NextSiblingNode);
    }

    Traverse(rootNode);
    resolvedLabelNodes.Sort(
        static (left, right) =>
        {
          var leftNode = (AtkTextNode*)left;
          var rightNode = (AtkTextNode*)right;
          var verticalComparison = leftNode->ScreenY.CompareTo(rightNode->ScreenY);
          return verticalComparison != 0
              ? verticalComparison
              : leftNode->ScreenX.CompareTo(rightNode->ScreenX);
        });
    return resolvedLabelNodes;
  }

  /// <summary>
  ///     Resolves the narrowest useful JournalDetail section container for one
  ///     visible section label inside the live body flow.
  /// </summary>
  /// <param name="flowRoot">The live JournalDetail body flow root.</param>
  /// <param name="labelNode">The resolved visible section label node.</param>
  /// <returns>The section container that owns the label and body flow.</returns>
  private static unsafe AtkResNode* ResolveJournalDetailSectionContainerNode(
      AtkResNode* flowRoot,
      AtkTextNode* labelNode)
  {
    if (flowRoot == null || labelNode == null)
    {
      return null;
    }

    var currentNode = (AtkResNode*)labelNode;
    while (currentNode != null &&
           currentNode->ParentNode != null &&
           currentNode->ParentNode != flowRoot)
    {
      currentNode = currentNode->ParentNode;
    }

    return currentNode;
  }

  /// <summary>
  ///     Collects the visible body text nodes that belong to one resolved
  ///     JournalDetail section.
  /// </summary>
  /// <param name="sectionContainerNode">The section container subtree root.</param>
  /// <param name="labelNode">The resolved label node for that section.</param>
  /// <param name="viewportTop">The visible viewport top.</param>
  /// <param name="viewportBottom">The visible viewport bottom.</param>
  /// <returns>The visible body text nodes in display order.</returns>
  private static unsafe List<nint> CollectVisibleJournalDetailSectionBodyTextNodes(
      AtkResNode* sectionContainerNode,
      AtkTextNode* labelNode,
      float viewportTop,
      float viewportBottom)
  {
    List<nint> bodyNodeAddresses = [];
    HashSet<nint> seenBodyNodes = [];
    if (sectionContainerNode == null || labelNode == null)
    {
      return bodyNodeAddresses;
    }

    HashSet<nint> visitedNodes = [];

    void Traverse(AtkResNode* node)
    {
      if (node == null || !visitedNodes.Add((nint)node))
      {
        return;
      }

      if (node->Type == NodeType.Text)
      {
        var textNode = node->GetAsAtkTextNode();
        if (textNode != null &&
            textNode != labelNode &&
            textNode->IsVisible() &&
            textNode->TextId == 0 &&
            textNode->ScreenY >= labelNode->ScreenY - 6f &&
            IsJournalDetailNodeWithinViewport(
                node,
                viewportTop,
                viewportBottom))
        {
          var bodyText = textNode->NodeText.IsEmpty
              ? string.Empty
              : MemoryHelper.ReadSeStringAsString(
                  out _,
                  (nint)textNode->NodeText.StringPtr.Value);
          if (!string.IsNullOrWhiteSpace(bodyText) &&
              seenBodyNodes.Add((nint)textNode))
          {
            bodyNodeAddresses.Add((nint)textNode);
          }
        }
      }

      var componentNode = node->GetAsAtkComponentNode();
      if (componentNode != null && componentNode->Component != null)
      {
        for (var childIndex = 0;
             childIndex < componentNode->Component->UldManager.NodeListCount;
             childIndex++)
        {
          Traverse(componentNode->Component->UldManager.NodeList[childIndex]);
        }
      }

      for (var childNode = node->ChildNode;
           childNode != null;
           childNode = childNode->NextSiblingNode)
      {
        Traverse(childNode);
      }
    }

    Traverse(sectionContainerNode);
    bodyNodeAddresses.Sort(
        static (left, right) =>
        {
          var leftNode = (AtkTextNode*)left;
          var rightNode = (AtkTextNode*)right;
          var verticalComparison = leftNode->ScreenY.CompareTo(rightNode->ScreenY);
          return verticalComparison != 0
              ? verticalComparison
              : leftNode->ScreenX.CompareTo(rightNode->ScreenX);
        });
    return bodyNodeAddresses;
  }

  /// <summary>
  ///     Gets whether one node intersects the visible JournalDetail viewport.
  /// </summary>
  /// <param name="node">The node to evaluate.</param>
  /// <param name="viewportTop">The visible viewport top.</param>
  /// <param name="viewportBottom">The visible viewport bottom.</param>
  /// <returns><c>true</c> when the node intersects the viewport.</returns>
  private static unsafe bool IsJournalDetailNodeWithinViewport(
      AtkResNode* node,
      float viewportTop,
      float viewportBottom)
  {
    if (node == null)
    {
      return false;
    }

    var nodeTop = node->ScreenY;
    var nodeBottom = node->ScreenY + Math.Max(1f, node->GetHeight());
    return nodeBottom >= viewportTop && nodeTop <= viewportBottom;
  }

  /// <summary>
  ///     Collects the visible JournalDetail body-flow text nodes that should
  ///     participate in native vertical reflow.
  /// </summary>
  /// <param name="journalBox">The live JournalDetail body component.</param>
  /// <param name="descriptionNode">The live description text node.</param>
  /// <param name="objectiveNodeAddresses">
  ///     The live visible objective text nodes in order.
  /// </param>
  /// <param name="summaryNode">The live primary summary text node, if any.</param>
  /// <param name="additionalSummaryNodeAddresses">The supplemental summary nodes.</param>
  /// <returns>The ordered text-node addresses that belong to the body flow.</returns>
  private unsafe List<nint> CollectVisibleJournalDetailFlowTextNodes(
      AtkComponentBase* journalBox,
      AtkTextNode* descriptionNode,
      IReadOnlyList<nint> objectiveNodeAddresses,
      AtkTextNode* summaryNode,
      IReadOnlyList<nint> additionalSummaryNodeAddresses)
  {
    List<nint> flowNodes = [];
    HashSet<nint> seenFlowNodes = [];
    Dictionary<nint, nint> wrapperRepresentativeNodes = [];
    var flowRoot = journalBox != null
        ? journalBox->UldManager.RootNode
        : null;
    var targetNodeAddresses = new HashSet<nint>(
        additionalSummaryNodeAddresses);
    foreach (var objectiveNodeAddress in objectiveNodeAddresses)
    {
      targetNodeAddresses.Add(objectiveNodeAddress);
    }

    if (descriptionNode != null)
    {
      targetNodeAddresses.Add((nint)descriptionNode);
    }

    if (summaryNode != null)
    {
      targetNodeAddresses.Add((nint)summaryNode);
    }

    List<nint> flowAnchorNodes = [];
    if (descriptionNode != null && descriptionNode->IsVisible())
    {
      flowAnchorNodes.Add((nint)descriptionNode);
    }

    foreach (var objectiveNodeAddress in objectiveNodeAddresses)
    {
      var objectiveNode = (AtkTextNode*)objectiveNodeAddress;
      if (objectiveNode != null && objectiveNode->IsVisible())
      {
        flowAnchorNodes.Add(objectiveNodeAddress);
      }
    }

    if (summaryNode != null && summaryNode->IsVisible())
    {
      flowAnchorNodes.Add((nint)summaryNode);
    }

    foreach (var additionalSummaryNodeAddress in additionalSummaryNodeAddresses)
    {
      var additionalSummaryNode = (AtkTextNode*)additionalSummaryNodeAddress;
      if (additionalSummaryNode != null && additionalSummaryNode->IsVisible())
      {
        flowAnchorNodes.Add(additionalSummaryNodeAddress);
      }
    }

    if (flowAnchorNodes.Count == 0)
    {
      return flowNodes;
    }

    var flowStartY = flowAnchorNodes.Min(
        static nodeAddress => ((AtkTextNode*)nodeAddress)->ScreenY) - 8f;

    void ConsiderFlowTextNode(AtkTextNode* textNode)
    {
      if (textNode == null || !textNode->IsVisible())
      {
        return;
      }

      var textNodeAddress = (nint)textNode;
      if (!seenFlowNodes.Add(textNodeAddress) ||
          textNode->ScreenY < flowStartY)
      {
        return;
      }

      var visibleText = textNode->NodeText.IsEmpty
          ? string.Empty
          : MemoryHelper.ReadSeStringAsString(
              out _,
              (nint)textNode->NodeText.StringPtr.Value);
      var isTargetNode =
          targetNodeAddresses.Contains(textNodeAddress);
      if (!isTargetNode &&
          string.IsNullOrWhiteSpace(visibleText))
      {
        return;
      }

      var wrapperNodeAddress = flowRoot == null
          ? textNodeAddress
          : NativeTextFlowReflowHelper.ResolveFlowWrapperNodeAddress(
              flowRoot,
              textNode);
      if (wrapperRepresentativeNodes.TryGetValue(
              wrapperNodeAddress,
              out var existingRepresentativeNodeAddress))
      {
        var existingRepresentativeIsTarget =
            targetNodeAddresses.Contains(existingRepresentativeNodeAddress);
        if (existingRepresentativeIsTarget || !isTargetNode)
        {
          return;
        }

        var existingRepresentativeIndex = flowNodes.IndexOf(
            existingRepresentativeNodeAddress);
        if (existingRepresentativeIndex >= 0)
        {
          flowNodes[existingRepresentativeIndex] = textNodeAddress;
        }

        wrapperRepresentativeNodes[wrapperNodeAddress] = textNodeAddress;
        return;
      }

      wrapperRepresentativeNodes[wrapperNodeAddress] = textNodeAddress;
      flowNodes.Add(textNodeAddress);
    }

    void CollectFlowTextNodesFromComponent(AtkComponentBase* component)
    {
      if (component == null)
      {
        return;
      }

      for (var childIndex = 0; childIndex < component->UldManager.NodeListCount; childIndex++)
      {
        var childNode = component->UldManager.NodeList[childIndex];
        if (childNode == null || !childNode->IsVisible())
        {
          continue;
        }

        if (childNode->Type == NodeType.Text)
        {
          ConsiderFlowTextNode(childNode->GetAsAtkTextNode());
          continue;
        }

        var componentNode = childNode->GetAsAtkComponentNode();
        if (componentNode != null && componentNode->Component != null)
        {
          CollectFlowTextNodesFromComponent(componentNode->Component);
        }
      }
    }

    if (journalBox != null)
    {
      CollectFlowTextNodesFromComponent(journalBox);
    }

    flowNodes.Sort(
        static (left, right) =>
        {
          var leftNode = (AtkTextNode*)left;
          var rightNode = (AtkTextNode*)right;
          var verticalComparison =
              leftNode->ScreenY.CompareTo(rightNode->ScreenY);
          return verticalComparison != 0
              ? verticalComparison
              : leftNode->ScreenX.CompareTo(rightNode->ScreenX);
        });

    return flowNodes;
  }

  /// <summary>
  ///     Captures the JournalDetail container state that should grow together
  ///     with the native body flow.
  /// </summary>
  /// <param name="flowRoot">The JournalDetail body-flow root node.</param>
  /// <param name="flowBlocks">The captured ordered body-flow blocks.</param>
  /// <returns>The captured container snapshots.</returns>
  private static unsafe List<NativeTextFlowContainerSnapshot>
      CaptureJournalDetailNativeFlowContainers(
          AtkResNode* flowRoot,
          IReadOnlyList<NativeTextFlowBlockSnapshot> flowBlocks)
  {
    List<NativeTextFlowContainerSnapshot> containerSnapshots = [];
    if (flowRoot == null || flowBlocks.Count == 0)
    {
      return containerSnapshots;
    }

    // JournalDetail keeps the outer viewport fixed and only grows the
    // internal scroll content root when the body text becomes more verbose.
    var flowContainerSnapshot =
        NativeTextFlowReflowHelper.CaptureContainerSnapshot(
            flowRoot,
            0,
            flowBlocks);
    if (flowContainerSnapshot != null)
    {
      containerSnapshots.Add(flowContainerSnapshot);
    }

    return containerSnapshots;
  }

  /// <summary>
  ///     Builds the translated or original text payloads that the JournalDetail
  ///     native body-flow reflow should apply.
  /// </summary>
  /// <param name="descriptionNode">The description text node.</param>
  /// <param name="descriptionText">The description text to render.</param>
  /// <param name="objectiveNodeAddresses">
  ///     The visible objective text nodes in order.
  /// </param>
  /// <param name="objectiveTexts">The objective texts to render in order.</param>
  /// <param name="summaryNode">The primary summary text node.</param>
  /// <param name="additionalSummaryNodeAddresses">The supplemental summary nodes.</param>
  /// <param name="summarySections">The summary sections in display order.</param>
  /// <returns>The node-address keyed text payloads.</returns>
  private static unsafe Dictionary<nint, string> BuildJournalDetailNativeFlowTextMap(
      AtkTextNode* descriptionNode,
      string descriptionText,
      IReadOnlyList<nint> objectiveNodeAddresses,
      IReadOnlyList<string> objectiveTexts,
      AtkTextNode* summaryNode,
      IReadOnlyList<nint> additionalSummaryNodeAddresses,
      IReadOnlyList<string> summarySections)
  {
    Dictionary<nint, string> flowTexts = new();

    if (descriptionNode != null)
    {
      flowTexts[(nint)descriptionNode] = descriptionText ?? string.Empty;
    }

    var objectiveNodeTexts = BuildJournalDetailObjectiveNodeTextAssignments(
        objectiveNodeAddresses,
        objectiveTexts);
    foreach (var objectiveNodeText in objectiveNodeTexts)
    {
      flowTexts[objectiveNodeText.Key] = objectiveNodeText.Value;
    }

    var summaryNodeTexts = BuildJournalDetailSummaryNodeTextAssignments(
        summaryNode,
        additionalSummaryNodeAddresses,
        summarySections);
    foreach (var summaryNodeText in summaryNodeTexts)
    {
      flowTexts[summaryNodeText.Key] = summaryNodeText.Value;
    }

    return flowTexts;
  }

  /// <summary>
  ///     Distributes JournalDetail objective sections across the currently
  ///     visible native objective nodes.
  /// </summary>
  /// <param name="objectiveNodeAddresses">
  ///     The visible objective text nodes in order.
  /// </param>
  /// <param name="objectiveSections">The objective sections in display order.</param>
  /// <returns>The objective text payload keyed by node address.</returns>
  private static Dictionary<nint, string> BuildJournalDetailObjectiveNodeTextAssignments(
      IReadOnlyList<nint> objectiveNodeAddresses,
      IReadOnlyList<string> objectiveSections)
  {
    Dictionary<nint, string> assignedObjectiveTexts = new();
    if (objectiveNodeAddresses.Count == 0)
    {
      return assignedObjectiveTexts;
    }

    if (objectiveSections.Count == 0)
    {
      foreach (var objectiveNodeAddress in objectiveNodeAddresses)
      {
        assignedObjectiveTexts[objectiveNodeAddress] = string.Empty;
      }

      return assignedObjectiveTexts;
    }

    for (var nodeIndex = 0; nodeIndex < objectiveNodeAddresses.Count; nodeIndex++)
    {
      if (nodeIndex < objectiveSections.Count - 1 &&
          nodeIndex < objectiveNodeAddresses.Count - 1)
      {
        assignedObjectiveTexts[objectiveNodeAddresses[nodeIndex]] =
            objectiveSections[nodeIndex] ?? string.Empty;
        continue;
      }

      if (nodeIndex >= objectiveSections.Count)
      {
        assignedObjectiveTexts[objectiveNodeAddresses[nodeIndex]] = string.Empty;
        continue;
      }

      assignedObjectiveTexts[objectiveNodeAddresses[nodeIndex]] =
          string.Join(
              Environment.NewLine,
              objectiveSections.Skip(nodeIndex).Select(
                  static section => section ?? string.Empty));
      break;
    }

    return assignedObjectiveTexts;
  }

  /// <summary>
  ///     Distributes JournalDetail summary sections across the currently
  ///     visible native summary nodes.
  /// </summary>
  /// <param name="summaryNode">The primary summary text node.</param>
  /// <param name="additionalSummaryNodeAddresses">The supplemental summary nodes.</param>
  /// <param name="summarySections">The summary sections in display order.</param>
  /// <returns>The summary text payload keyed by node address.</returns>
  private static unsafe Dictionary<nint, string> BuildJournalDetailSummaryNodeTextAssignments(
      AtkTextNode* summaryNode,
      IReadOnlyList<nint> additionalSummaryNodeAddresses,
      IReadOnlyList<string> summarySections)
  {
    Dictionary<nint, string> assignedSummaryTexts = new();
    List<nint> summaryTextNodes = [];
    if (summaryNode != null)
    {
      summaryTextNodes.Add((nint)summaryNode);
    }

    foreach (var additionalSummaryNodeAddress in additionalSummaryNodeAddresses)
    {
      if (additionalSummaryNodeAddress != 0 &&
          !summaryTextNodes.Contains(additionalSummaryNodeAddress))
      {
        summaryTextNodes.Add(additionalSummaryNodeAddress);
      }
    }

    if (summaryTextNodes.Count == 0)
    {
      return assignedSummaryTexts;
    }

    if (summarySections.Count == 0)
    {
      foreach (var summaryTextNode in summaryTextNodes)
      {
        assignedSummaryTexts[summaryTextNode] = string.Empty;
      }

      return assignedSummaryTexts;
    }

    for (var nodeIndex = 0; nodeIndex < summaryTextNodes.Count; nodeIndex++)
    {
      if (nodeIndex < summarySections.Count - 1 &&
          nodeIndex < summaryTextNodes.Count - 1)
      {
        assignedSummaryTexts[summaryTextNodes[nodeIndex]] =
            summarySections[nodeIndex] ?? string.Empty;
        continue;
      }

      if (nodeIndex >= summarySections.Count)
      {
        assignedSummaryTexts[summaryTextNodes[nodeIndex]] = string.Empty;
        continue;
      }

      assignedSummaryTexts[summaryTextNodes[nodeIndex]] =
          string.Join(
              Environment.NewLine + Environment.NewLine,
              summarySections.Skip(nodeIndex).Select(
                  static section => section ?? string.Empty));
      break;
    }

    return assignedSummaryTexts;
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
  ///     Resolves one translated objective row from the JournalDetail cache or
  ///     the persisted quest plate.
  /// </summary>
  /// <param name="foundQuestPlate">The persisted quest plate, if any.</param>
  /// <param name="questCanonicalData">The canonical quest payload, if any.</param>
  /// <param name="journalDetailScopeKey">The current JournalDetail scope key.</param>
  /// <param name="sourceText">The visible source objective text.</param>
  /// <param name="translatedText">The translated objective text.</param>
  /// <returns>True when the translated objective row is ready.</returns>
  private bool TryResolveTranslatedObjectiveText(
      QuestPlate? foundQuestPlate,
      QuestCanonicalData? questCanonicalData,
      string journalDetailScopeKey,
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

    if (foundQuestPlate == null)
    {
      translatedText = sourceText;
      return false;
    }

    var rowKeys = questCanonicalData?.EnumerateObjectiveRowKeysByText(sourceText)
                      .Where(static rowKey => !string.IsNullOrWhiteSpace(rowKey))
                      .Distinct(StringComparer.Ordinal)
                      .ToArray() ??
                  [];
    foreach (var rowKey in rowKeys)
    {
      if (!foundQuestPlate.TryGetTranslatedObjectiveText(
              rowKey,
              sourceText,
              out translatedText))
      {
        continue;
      }

      this.RememberJournalDetailCachedText(
          journalDetailScopeKey,
          sourceText,
          translatedText);
      return true;
    }

    if (foundQuestPlate.TryGetTranslatedObjectiveText(
            null,
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
  /// <param name="objectiveNodeAddresses">
  ///     The visible objective text nodes in order.
  /// </param>
  /// <param name="objectiveTexts">
  ///     The visible objective texts in order.
  /// </param>
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
      IReadOnlyList<nint> objectiveNodeAddresses,
      IReadOnlyList<string> objectiveTexts,
      AtkResNode* summaryContainerNode,
      AtkTextNode* summaryNode)
  {
    string translatedQuestName = questName;
    string translatedQuestObjective = BuildJournalDetailObjectiveDisplayText(
        objectiveTexts);
    var translatedQuestNameReady = false;
    var translatedQuestObjectiveReady = objectiveTexts.Count == 0 ||
                                        objectiveTexts.All(
                                            static text => string.IsNullOrWhiteSpace(text));
    var journalDetailScopeKey = BuildJournalDetailScopeKey(
        questProgressSnapshot,
        questName,
        questMessage);
    var questCanonicalData = questProgressSnapshot.HasValue
        ? QuestCanonicalData.Create(
            questProgressSnapshot.Value,
            GetGameVersion())
        : null;
    this.EnsureJournalDetailScope(journalDetailScopeKey);
    var canonicalSummaryRows = this.BuildCanonicalSummaryRows(
        foundQuestPlate,
        questCanonicalData,
        journalDetailScopeKey);
    var summaryTextCandidates = BuildJournalDetailSummaryTextCandidates(
        canonicalSummaryRows
            .SelectMany(
                summary => new[]
                {
                  summary.OriginalText,
                  summary.TranslatedText,
                })
            .Append(summaryText));

    var hasOriginalSnapshot = this.TryGetJournalDetailOriginalSnapshot(
        journalDetailScopeKey,
        out var originalSnapshot);
    if (hasOriginalSnapshot)
    {
      foreach (var originalAdditionalSummaryText
               in originalSnapshot.AdditionalSummaryTexts)
      {
        var normalizedAdditionalSummaryText =
            NormalizeJournalDetailSummaryCandidateText(
                originalAdditionalSummaryText);
        if (!string.IsNullOrWhiteSpace(normalizedAdditionalSummaryText))
        {
          summaryTextCandidates.Add(normalizedAdditionalSummaryText);
        }
      }

      var normalizedOriginalSummaryText =
          NormalizeJournalDetailSummaryCandidateText(
              originalSnapshot.SummaryText);
      if (!string.IsNullOrWhiteSpace(normalizedOriginalSummaryText))
      {
        summaryTextCandidates.Add(normalizedOriginalSummaryText);
      }
    }

    var primaryObjectiveNode =
        objectiveNodeAddresses.Count != 0
            ? (AtkTextNode*)objectiveNodeAddresses[0]
            : null;
    var visibleAdditionalSummaryNodes =
        this.CollectVisibleAdditionalSummaryNodes(
            journalBox,
            descriptionNode,
            primaryObjectiveNode,
            summaryNode,
            summaryTextCandidates);

    if (!hasOriginalSnapshot)
    {
      var capturedAdditionalSummaryTexts =
          CaptureVisibleTextNodeTexts(visibleAdditionalSummaryNodes);
      originalSnapshot = this.CreateJournalDetailOriginalSnapshot(
          journalBox,
          questName,
          questMessage,
          objectiveText,
          summaryText,
          descriptionNode,
          objectiveNodeAddresses,
          objectiveTexts,
          summaryNode,
          summaryContainerNode,
          visibleAdditionalSummaryNodes,
          capturedAdditionalSummaryTexts);
      this.RememberJournalDetailOriginalSnapshot(
          journalBox,
          journalDetailScopeKey,
          questName,
          questMessage,
          objectiveText,
          summaryText,
          descriptionNode,
          objectiveNodeAddresses,
          objectiveTexts,
          summaryNode,
          summaryContainerNode,
          visibleAdditionalSummaryNodes,
          capturedAdditionalSummaryTexts);
    }
    else
    {
      originalSnapshot =
          this.MergeJournalDetailOriginalSnapshotAdditionalNodes(
              journalBox,
              journalDetailScopeKey,
              originalSnapshot,
              descriptionNode,
              objectiveNodeAddresses,
              objectiveTexts,
              summaryNode,
              summaryContainerNode,
              visibleAdditionalSummaryNodes);
    }

    var originalQuestName = originalSnapshot.QuestName;
    var originalQuestMessage = originalSnapshot.QuestMessage;
    var originalObjectiveText = originalSnapshot.ObjectiveText;
    var originalObjectiveTexts = originalSnapshot.ObjectiveTexts;
    var originalObjectiveDisplayText = BuildJournalDetailObjectiveDisplayText(
        originalObjectiveTexts);
    var originalSummaryText = originalSnapshot.SummaryText;
    var additionalSummaryNodeAddresses =
        originalSnapshot.AdditionalSummaryNodeAddresses.Count != 0
            ? originalSnapshot.AdditionalSummaryNodeAddresses
            : visibleAdditionalSummaryNodes;
    var originalAdditionalSummaryTexts =
        originalSnapshot.AdditionalSummaryTexts;
    var translatedObjectiveSections = originalObjectiveTexts.ToArray();
    translatedQuestObjective = BuildJournalDetailObjectiveDisplayText(
        translatedObjectiveSections);
    translatedQuestObjectiveReady = translatedObjectiveSections.Length == 0 ||
                                    translatedObjectiveSections.All(
                                        static text => string.IsNullOrWhiteSpace(text));

    var currentQuestSequenceText = questProgressSnapshot.HasValue
        ? GetCurrentQuestSequenceText(questProgressSnapshot.Value)
        : string.Empty;
    var originalQuestDescription = !string.IsNullOrWhiteSpace(currentQuestSequenceText)
        ? currentQuestSequenceText
        : originalQuestMessage;
    var translatedQuestDescription = originalQuestDescription;
    var translatedQuestDescriptionReady =
        string.IsNullOrWhiteSpace(originalQuestDescription);

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

      if (originalObjectiveTexts.Count != 0)
      {
        List<string> resolvedObjectiveSections = [];
        translatedQuestObjectiveReady = true;
        foreach (var originalObjectiveSectionText in originalObjectiveTexts)
        {
          if (this.TryResolveTranslatedObjectiveText(
                  foundQuestPlate,
                  questCanonicalData,
                  journalDetailScopeKey,
                  originalObjectiveSectionText,
                  out var translatedObjectiveSectionText))
          {
            resolvedObjectiveSections.Add(translatedObjectiveSectionText);
            continue;
          }

          translatedQuestObjectiveReady = false;
          resolvedObjectiveSections.Add(originalObjectiveSectionText);
        }

        translatedObjectiveSections = resolvedObjectiveSections.ToArray();
        translatedQuestObjective = BuildJournalDetailObjectiveDisplayText(
            translatedObjectiveSections);
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
      translatedObjectiveSections = originalObjectiveTexts.ToArray();
      translatedQuestObjective = BuildJournalDetailObjectiveDisplayText(
          translatedObjectiveSections);
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
      translatedPrimarySummaryText = this.NormalizeQuestText(
          translatedPrimarySummaryText ?? string.Empty);

      for (var i = 0; i < translatedObjectiveSections.Length; i++)
      {
        translatedObjectiveSections[i] = this.NormalizeQuestText(
            translatedObjectiveSections[i] ?? string.Empty);
      }

      translatedQuestObjective = BuildJournalDetailObjectiveDisplayText(
          translatedObjectiveSections);

      for (var i = 0; i < translatedSummarySections.Length; i++)
      {
        translatedSummarySections[i] = this.NormalizeQuestText(
            translatedSummarySections[i] ?? string.Empty);
      }
    }

    var translatedQuestBodyReady =
        translatedQuestDescriptionReady &&
        translatedQuestObjectiveReady &&
        translatedQuestSummaryReady;
    var translatedQuestNativeReady =
        translatedQuestNameReady &&
        translatedQuestBodyReady;

    if (this.JournalDetailWritesNativeTranslation &&
        translatedQuestNativeReady)
    {
      questNameNode->SetText(translatedQuestName ?? string.Empty);
      this.journalDetailNativeMutationScopes.Add(journalDetailScopeKey);
      if (!this.TryApplyJournalDetailNativeBodyFlow(
              originalSnapshot,
              descriptionNode,
              translatedQuestDescription,
              objectiveNodeAddresses,
              translatedObjectiveSections,
              summaryNode,
              translatedSummarySections))
      {
        this.ApplyJournalDetailNativeTextNodePresentation(
            descriptionNode,
            originalSnapshot.DescriptionNodeWidth,
            originalSnapshot.DescriptionNodeTextFlags,
            originalSnapshot.DescriptionNodeFontSize,
            translatedQuestDescription);
        if (objectiveNodeAddresses.Count != 0)
        {
          var fallbackObjectiveNode = (AtkTextNode*)objectiveNodeAddresses[0];
          this.ApplyJournalDetailNativeTextNodePresentation(
              fallbackObjectiveNode,
              originalSnapshot.ObjectiveNodeWidth,
              originalSnapshot.ObjectiveNodeTextFlags,
              originalSnapshot.ObjectiveNodeFontSize,
              translatedQuestObjective);
        }

        this.ApplyJournalDetailNativeSummaryFlow(
            originalSnapshot,
            summaryContainerNode,
            translatedSummarySections);
      }
    }
    else if (this.journalDetailNativeMutationScopes.Remove(journalDetailScopeKey))
    {
      questNameNode->SetText(originalQuestName ?? string.Empty);
      if (!this.TryRestoreJournalDetailOriginalBodyFlow(
              originalSnapshot,
              descriptionNode,
              objectiveNodeAddresses,
              summaryNode))
      {
        this.RestoreJournalDetailTextNodePresentation(
            descriptionNode,
            originalSnapshot.DescriptionNodeWidth,
            originalSnapshot.DescriptionNodeTextFlags,
            originalSnapshot.DescriptionNodeFontSize,
            originalQuestMessage);
        if (objectiveNodeAddresses.Count != 0)
        {
          var fallbackObjectiveNode = (AtkTextNode*)objectiveNodeAddresses[0];
          this.RestoreJournalDetailTextNodePresentation(
              fallbackObjectiveNode,
              originalSnapshot.ObjectiveNodeWidth,
              originalSnapshot.ObjectiveNodeTextFlags,
              originalSnapshot.ObjectiveNodeFontSize,
              originalObjectiveText);
        }

        this.RestoreJournalDetailOriginalSummaryFlow(
            originalSnapshot,
            summaryContainerNode);
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

    for (var objectiveIndex = 0;
         objectiveIndex < originalObjectiveTexts.Count &&
         objectiveIndex < translatedObjectiveSections.Length;
         objectiveIndex++)
    {
      if (string.IsNullOrWhiteSpace(originalObjectiveTexts[objectiveIndex]))
      {
        continue;
      }

      this.RememberJournalDetailCachedText(
          journalDetailScopeKey,
          originalObjectiveTexts[objectiveIndex],
          translatedObjectiveSections[objectiveIndex]);
    }
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
      var originalQuestSummaryBody = originalSummaryDisplayText;
      var translatedQuestSummaryBody = translatedSummaryDisplayText;
      var originalQuestBody = BuildQuestPlateHoverBody(
          originalQuestDescription,
          originalObjectiveDisplayText,
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
              primaryObjectiveNode);
          ExpandQuestPlateHoverBoundsForTextNode(
              ref bodyTopLeft,
              ref bodyBottomRight,
              summaryNode);
          foreach (var additionalSummaryNode in additionalSummaryNodeAddresses)
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

          foreach (var objectiveNodeAddress in objectiveNodeAddresses)
          {
            ExpandBodyBounds((AtkTextNode*)objectiveNodeAddress);
          }
          if (summaryNode != null)
          {
            ExpandBodyBounds(summaryNode);
          }

          foreach (var additionalSummaryNode in additionalSummaryNodeAddresses)
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
            Array.Empty<nint>(),
            Array.Empty<string>(),
            descriptionNode != null ? descriptionNode->GetWidth() : (ushort)0,
            descriptionNode != null ? descriptionNode->TextFlags : default,
            descriptionNode != null ? descriptionNode->FontSize : (byte)0,
            0,
            default,
            0,
            0,
            0,
            default,
            0,
            null,
            Array.Empty<JournalDetailTextNodeLayout>(),
            Array.Empty<NativeTextFlowBlockSnapshot>(),
            Array.Empty<NativeTextFlowContainerSnapshot>());
        this.RememberJournalDetailOriginalSnapshot(
            null,
            journalDetailScopeKey,
            questName,
            questMessage,
            string.Empty,
            string.Empty,
            descriptionNode,
            Array.Empty<nint>(),
            Array.Empty<string>(),
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

      var translatedCompletedQuestReady =
          translatedQuestNameReady &&
          translatedQuestMessageReady;

      if (this.JournalDetailWritesNativeTranslation &&
          translatedCompletedQuestReady)
      {
        questNameNode->SetText(translatedQuestName ?? string.Empty);
        this.ApplyJournalDetailNativeTextNodePresentation(
            descriptionNode,
            originalSnapshot.DescriptionNodeWidth,
            originalSnapshot.DescriptionNodeTextFlags,
            originalSnapshot.DescriptionNodeFontSize,
            translatedQuestMessage);
        this.journalDetailNativeMutationScopes.Add(journalDetailScopeKey);
      }
      else if (this.journalDetailNativeMutationScopes.Remove(journalDetailScopeKey))
      {
        questNameNode->SetText(originalQuestName ?? string.Empty);
        this.RestoreJournalDetailTextNodePresentation(
            descriptionNode,
            originalSnapshot.DescriptionNodeWidth,
            originalSnapshot.DescriptionNodeTextFlags,
            originalSnapshot.DescriptionNodeFontSize,
            originalQuestMessage);
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
      if (!TryResolveJournalDetailActiveBodyNodes(
              journalBox,
              out var descriptionNode,
              out var objectiveNodeAddresses,
              out var summaryContainerNode,
              out var summaryNode,
              out _))
      {
        this.RemoveHoverTooltipsByPrefix(JournalDetailHoverPrefix);
        return false;
      }

      var objectiveTexts = CaptureVisibleTextNodeTexts(objectiveNodeAddresses);
      if (objectiveTexts.Count == 0)
      {
        this.RemoveHoverTooltipsByPrefix(JournalDetailHoverPrefix);
        return true;
      }

      var summaryText = summaryNode != null && !summaryNode->NodeText.IsEmpty
          ? MemoryHelper.ReadSeStringAsString(
              out _,
              (nint)summaryNode->NodeText.StringPtr.Value)
          : string.Empty;

      var liveQuestName = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)questNameNode->NodeText.StringPtr.Value);
      var liveQuestMessage = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)descriptionNode->NodeText.StringPtr.Value);
      var liveObjectiveText = objectiveTexts[0];
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
          objectiveNodeAddresses,
          objectiveTexts,
          summaryContainerNode,
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
    this.journalDetailNativeMutationScopes.Clear();
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
  /// <param name="DescriptionNodeWidth">The original description-node width.</param>
  /// <param name="DescriptionNodeTextFlags">
  ///     The original description-node text flags.
  /// </param>
  /// <param name="DescriptionNodeFontSize">
  ///     The original description-node font size.
  /// </param>
  /// <param name="ObjectiveNodeWidth">The original objective-node width.</param>
  /// <param name="ObjectiveNodeTextFlags">
  ///     The original objective-node text flags.
  /// </param>
  /// <param name="ObjectiveNodeFontSize">
  ///     The original objective-node font size.
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
  /// <param name="SummaryNodeLayout">
  ///     The original primary summary node layout.
  /// </param>
  /// <param name="AdditionalSummaryNodeLayouts">
  ///     The original supplemental summary node layouts.
  /// </param>
  /// <param name="NativeFlowBlocks">
  ///     The ordered native body-flow block snapshots captured for reflow.
  /// </param>
  /// <param name="NativeFlowContainerSnapshots">
  ///     The container chain that should grow and restore with the native body
  ///     flow.
  /// </param>
  private sealed record JournalDetailOriginalSnapshot(
      string QuestName,
      string QuestMessage,
      string ObjectiveText,
      string SummaryText,
      IReadOnlyList<nint> ObjectiveNodeAddresses,
      IReadOnlyList<string> ObjectiveTexts,
      IReadOnlyList<nint> AdditionalSummaryNodeAddresses,
      IReadOnlyList<string> AdditionalSummaryTexts,
      ushort DescriptionNodeWidth,
      TextFlags DescriptionNodeTextFlags,
      byte DescriptionNodeFontSize,
      ushort ObjectiveNodeWidth,
      TextFlags ObjectiveNodeTextFlags,
      byte ObjectiveNodeFontSize,
      ushort SummaryNodeWidth,
      ushort SummaryContainerHeight,
      TextFlags SummaryNodeTextFlags,
      byte SummaryNodeFontSize,
      JournalDetailTextNodeLayout? SummaryNodeLayout,
      IReadOnlyList<JournalDetailTextNodeLayout> AdditionalSummaryNodeLayouts,
      IReadOnlyList<NativeTextFlowBlockSnapshot> NativeFlowBlocks,
      IReadOnlyList<NativeTextFlowContainerSnapshot> NativeFlowContainerSnapshots);

  /// <summary>
  ///     Captures the original layout state for a JournalDetail body text
  ///     node.
  /// </summary>
  /// <param name="NodeAddress">The live text-node address.</param>
  /// <param name="X">The original local X position.</param>
  /// <param name="Y">The original local Y position.</param>
  /// <param name="Width">The original node width.</param>
  /// <param name="Height">The original node height.</param>
  /// <param name="TextFlags">The original text flags.</param>
  /// <param name="FontSize">The original font size.</param>
  private sealed record JournalDetailTextNodeLayout(
      nint NodeAddress,
      short X,
      short Y,
      ushort Width,
      ushort Height,
      TextFlags TextFlags,
      byte FontSize);
}
