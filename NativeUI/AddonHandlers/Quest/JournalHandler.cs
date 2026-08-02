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
  ///     Represents one accepted Journal quest entry projected from live quest
  ///     state and the persisted canonical quest row.
  /// </summary>
  /// <param name="AcceptedQuestId">The accepted live quest identifier.</param>
  /// <param name="OriginalQuestName">The source quest title.</param>
  /// <param name="RenderedTranslatedQuestName">
  ///     The translated quest title exactly as Journal would render it in the
  ///     current display mode.
  /// </param>
  /// <param name="FoundQuestPlate">The persisted canonical quest row.</param>
  internal readonly record struct AcceptedJournalQuestEntry(
      uint AcceptedQuestId,
      string OriginalQuestName,
      string RenderedTranslatedQuestName,
      QuestPlate? FoundQuestPlate);

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
          this.Config.JournalTranslationDisplayMode,
          this.Config.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether Journal should write translated text into the native
  ///     addon.
  /// </summary>
  private bool JournalWritesNativeTranslation =>
      QuestAddonModeHelpers.WritesNativeTranslation(
          this.Config.JournalTranslationDisplayMode,
          this.Config.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether Journal hover tooltips should show the original text.
  /// </summary>
  private bool JournalHoverShowsOriginal =>
      QuestAddonModeHelpers.ShowsOriginalTooltips(
          this.Config.JournalTranslationDisplayMode,
          this.Config.OverlayOnlyLanguage);

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
          translatedPayloadReady,
          this.Config.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether translated Journal text should be normalized before being
  ///     written into the native UI.
  /// </summary>
  private bool JournalShouldRemoveDiacritics =>
      QuestAddonModeHelpers.ShouldRemoveDiacritics(
          this.Config.JournalTranslationDisplayMode,
          this.Config.RemoveDiacriticsWhenUsingReplacementQuest,
          this.Config.OverlayOnlyLanguage);

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

      var hasAcceptedJournalQuestEntries =
          this.TryCollectAcceptedJournalQuestEntries(
              operationSourceLanguage,
              out var acceptedJournalQuestEntries);
      HashSet<nint> visibleJournalQuestNodeKeys = [];

      for (var i = 0; i < questListNode->UldManager.NodeListCount; i++)
      {
        var rowNode = questListNode->UldManager.NodeList[i];
        if (!TryGetJournalQuestTitleNode(
                rowNode,
                out var questName))
        {
          continue;
        }

        var liveQuestNameText = MemoryHelper.ReadSeStringAsString(
            out _,
            (nint)questName->NodeText.StringPtr.Value);
        var questNameNodeKey = (nint)questName;
        visibleJournalQuestNodeKeys.Add(questNameNodeKey);
        var originalQuestName = liveQuestNameText;
        var foundQuestPlate = default(QuestPlate?);
        var questId = 0U;

        if (hasAcceptedJournalQuestEntries)
        {
          if (!TryResolveAcceptedJournalQuestEntry(
                  acceptedJournalQuestEntries,
                  liveQuestNameText,
                  out var acceptedJournalQuestEntry))
          {
            this.ForgetJournalListNodeState(questNameNodeKey);
            continue;
          }

          originalQuestName = acceptedJournalQuestEntry.OriginalQuestName;
          foundQuestPlate = acceptedJournalQuestEntry.FoundQuestPlate;
          questId = acceptedJournalQuestEntry.AcceptedQuestId;
        }
        else if (!this.TryResolveJournalQuestPlate(
                     operationSourceLanguage,
                     liveQuestNameText,
                     out foundQuestPlate,
                     out questId))
        {
          if (this.TryGetJournalListOriginalTextForLiveText(
                  questNameNodeKey,
                  liveQuestNameText,
                  out var cachedOriginalQuestName) &&
              this.TryResolveJournalQuestPlate(
                  operationSourceLanguage,
                  cachedOriginalQuestName,
                  out foundQuestPlate,
                  out questId))
          {
            originalQuestName = cachedOriginalQuestName;
          }
          else
          {
            this.ForgetJournalListNodeState(questNameNodeKey);
            continue;
          }
        }

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

        if (!this.JournalUsesHoverTooltips)
        {
          continue;
        }

        if (TryGetJournalHoverBounds(
                rowNode,
                questName,
                out var topLeft,
                out var bottomRight))
        {
          this.RegisterTranslatedHoverTooltip(
              $"JournalList-{questNameNodeKey:X}",
              topLeft,
              bottomRight,
              originalQuestName,
              translatedQuestName,
              translatedPayloadReady: this.CanRenderJournalHoverTooltip(
                  translatedQuestNameReady),
              swapEnabled: this.JournalHoverShowsOriginal,
              forceEnabled: true);
        }
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

    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      return;
    }

    this.TranslateJournalQuests(sourceLanguage);
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
      if (!TryGetJournalQuestTitleNode(
              rowNode,
              out var questName))
      {
        continue;
      }

      var liveQuestNameText = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)questName->NodeText.StringPtr.Value);
      var questNameNodeKey = (nint)questName;
      var signatureQuestName =
          this.TryGetJournalListOriginalTextForLiveText(
              questNameNodeKey,
              liveQuestNameText,
              out var cachedOriginalQuestName)
              ? cachedOriginalQuestName
              : liveQuestNameText;
      hash.Add(questNameNodeKey);
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
    if (MatchesJournalListOriginalSnapshot(
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
  internal static bool MatchesJournalListOriginalSnapshot(
      string? liveText,
      string? originalText,
      string? translatedText)
  {
    return string.Equals(liveText, originalText, StringComparison.Ordinal);
  }

  /// <summary>
  ///     Gets whether one live Journal list text still belongs to a native
  ///     mutation owned by this handler so the original source title can be
  ///     restored safely.
  /// </summary>
  /// <param name="liveText">The text currently rendered by the game.</param>
  /// <param name="originalText">The cached source title.</param>
  /// <param name="translatedText">The cached translated title.</param>
  /// <returns>
  ///     True when the live text still matches the source title or the
  ///     translated title previously written by this handler.
  /// </returns>
  internal static bool MatchesJournalListOwnedMutationSnapshot(
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
        LangDict[LanguageInt].Code,
        this.Config.ChosenTransEngine,
        DateTime.Now);
    foundQuestPlate = this.FindQuestPlate(questPlate);
    return true;
  }

  /// <summary>
  ///     Collects the currently accepted Journal quest entries from live quest
  ///     state and the persisted canonical quest table.
  /// </summary>
  /// <param name="sourceLanguage">The captured source client language.</param>
  /// <param name="acceptedJournalQuestEntries">
  ///     The accepted Journal quest entries for the current frame.
  /// </param>
  /// <returns>True when at least one accepted Journal entry was collected.</returns>
  private bool TryCollectAcceptedJournalQuestEntries(
      SourceClientLanguage sourceLanguage,
      out List<AcceptedJournalQuestEntry> acceptedJournalQuestEntries)
  {
    acceptedJournalQuestEntries = [];
    if (!QuestProgressResolver.TryCollectAcceptedQuestIds(
            out var acceptedQuestIds))
    {
      return false;
    }

    foreach (var acceptedQuestId in acceptedQuestIds)
    {
      if (!QuestProgressResolver.TryResolveQuestProgress(
              acceptedQuestId.ToString(CultureInfo.InvariantCulture),
              out var questProgressSnapshot))
      {
        continue;
      }

      var questCanonicalData = QuestCanonicalData.Create(
          questProgressSnapshot,
          GetGameVersion());
      var questPlate = questCanonicalData.ToQuestPlate(
          sourceLanguage.PersistenceCode,
          LangDict[LanguageInt].Code,
          this.Config.ChosenTransEngine,
          DateTime.Now);
      var foundQuestPlate = this.FindQuestPlate(questPlate);
      var renderedTranslatedQuestName = string.IsNullOrWhiteSpace(
              foundQuestPlate?.TranslatedQuestName)
          ? string.Empty
          : foundQuestPlate.TranslatedQuestName;
      if (this.JournalShouldRemoveDiacritics &&
          !string.IsNullOrWhiteSpace(renderedTranslatedQuestName))
      {
        renderedTranslatedQuestName = this.NormalizeQuestText(
            renderedTranslatedQuestName);
      }

      acceptedJournalQuestEntries.Add(
          new AcceptedJournalQuestEntry(
              acceptedQuestId,
              questProgressSnapshot.QuestName,
              renderedTranslatedQuestName,
              foundQuestPlate));
    }

    return acceptedJournalQuestEntries.Count > 0;
  }

  /// <summary>
  ///     Tries to resolve one visible Journal row against the accepted quest
  ///     snapshot for the current frame.
  /// </summary>
  /// <param name="acceptedJournalQuestEntries">
  ///     The accepted Journal quest entries for the current frame.
  /// </param>
  /// <param name="visibleQuestName">The currently visible Journal title.</param>
  /// <param name="acceptedJournalQuestEntry">The uniquely resolved entry.</param>
  /// <returns>
  ///     True when the visible Journal title maps uniquely to one accepted
  ///     quest by source title or rendered translated title.
  /// </returns>
  internal static bool TryResolveAcceptedJournalQuestEntry(
      IReadOnlyCollection<AcceptedJournalQuestEntry> acceptedJournalQuestEntries,
      string visibleQuestName,
      out AcceptedJournalQuestEntry acceptedJournalQuestEntry)
  {
    acceptedJournalQuestEntry = default;
    return TryFindUniqueAcceptedJournalQuestEntry(
               acceptedJournalQuestEntries,
               visibleQuestName,
               static acceptedEntry => acceptedEntry.OriginalQuestName,
               out acceptedJournalQuestEntry) ||
           TryFindUniqueAcceptedJournalQuestEntry(
               acceptedJournalQuestEntries,
               visibleQuestName,
               static acceptedEntry =>
                   acceptedEntry.RenderedTranslatedQuestName,
               out acceptedJournalQuestEntry);
  }

  /// <summary>
  ///     Tries to find exactly one accepted Journal quest entry whose projected
  ///     title matches the visible Journal title.
  /// </summary>
  /// <param name="acceptedJournalQuestEntries">
  ///     The accepted Journal quest entries for the current frame.
  /// </param>
  /// <param name="visibleQuestName">The currently visible Journal title.</param>
  /// <param name="titleSelector">Selects the title to compare for one entry.</param>
  /// <param name="acceptedJournalQuestEntry">The uniquely resolved entry.</param>
  /// <returns>True when exactly one accepted entry matched the visible title.</returns>
  private static bool TryFindUniqueAcceptedJournalQuestEntry(
      IReadOnlyCollection<AcceptedJournalQuestEntry> acceptedJournalQuestEntries,
      string visibleQuestName,
      Func<AcceptedJournalQuestEntry, string> titleSelector,
      out AcceptedJournalQuestEntry acceptedJournalQuestEntry)
  {
    acceptedJournalQuestEntry = default;
    var normalizedVisibleQuestName =
        NormalizeJournalQuestTitleForMatch(visibleQuestName);
    if (normalizedVisibleQuestName.Length == 0)
    {
      return false;
    }

    var matchedEntryCount = 0;
    foreach (var candidateEntry in acceptedJournalQuestEntries)
    {
      var candidateTitle = titleSelector(candidateEntry);
      if (!string.Equals(
              normalizedVisibleQuestName,
              NormalizeJournalQuestTitleForMatch(candidateTitle),
              StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      acceptedJournalQuestEntry = candidateEntry;
      matchedEntryCount++;
      if (matchedEntryCount > 1)
      {
        acceptedJournalQuestEntry = default;
        return false;
      }
    }

    return matchedEntryCount == 1;
  }

  /// <summary>
  ///     Normalizes one Journal title for accepted-quest snapshot matching.
  /// </summary>
  /// <param name="questTitle">The raw visible or projected Journal title.</param>
  /// <returns>The normalized Journal title.</returns>
  private static string NormalizeJournalQuestTitleForMatch(string? questTitle)
  {
    return QuestLuminaResolver.NormalizeQuestNameForLookup(questTitle);
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
  ///     Tries to resolve the direct quest-title text node from one Journal
  ///     quest row, rejecting category headers and nested icon subnodes that
  ///     reuse the same recursive node identifiers.
  /// </summary>
  /// <param name="rowNode">The visible Journal row candidate.</param>
  /// <param name="questName">The direct quest-title text node.</param>
  /// <returns>True when the row matches the quest-row shape.</returns>
  private static unsafe bool TryGetJournalQuestTitleNode(
      AtkResNode* rowNode,
      out AtkTextNode* questName)
  {
    questName = null;
    if (rowNode == null ||
        !rowNode->IsVisible() ||
        rowNode->NodeId == 5 ||
        rowNode->Type == NodeType.Collision ||
        rowNode->Type == NodeType.Res)
    {
      return false;
    }

    var questItemNode = rowNode->GetAsAtkComponentNode();
    if (questItemNode == null || questItemNode->Component == null)
    {
      return false;
    }

    return TryGetJournalQuestTitleNode(
        questItemNode,
        out questName);
  }

  /// <summary>
  ///     Tries to resolve the direct quest-title text node from one Journal
  ///     quest row component, ignoring recursive matches under the icon
  ///     subcomponent.
  /// </summary>
  /// <param name="questItemNode">The quest-row component node.</param>
  /// <param name="questName">The direct quest-title text node.</param>
  /// <returns>True when the component matches the quest-row shape.</returns>
  private static unsafe bool TryGetJournalQuestTitleNode(
      AtkComponentNode* questItemNode,
      out AtkTextNode* questName)
  {
    questName = null;
    if (questItemNode == null || questItemNode->Component == null)
    {
      return false;
    }

    AtkTextNode* levelNode = null;
    for (var i = 0; i < questItemNode->Component->UldManager.NodeListCount; i++)
    {
      var childNode = questItemNode->Component->UldManager.NodeList[i];
      if (childNode == null ||
          !childNode->IsVisible() ||
          childNode->Type != NodeType.Text)
      {
        continue;
      }

      if (childNode->NodeId == 3)
      {
        questName = childNode->GetAsAtkTextNode();
        continue;
      }

      if (childNode->NodeId == 4)
      {
        levelNode = childNode->GetAsAtkTextNode();
      }
    }

    return questName != null &&
           levelNode != null &&
           !questName->NodeText.IsEmpty;
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
  ///     Tries to resolve one stable hover rectangle for a visible Journal row
  ///     using the row geometry instead of only the text-node glyph bounds.
  /// </summary>
  /// <param name="rowNode">The visible Journal row node.</param>
  /// <param name="questName">The visible Journal title node.</param>
  /// <param name="topLeft">The resolved top-left screen coordinate.</param>
  /// <param name="bottomRight">The resolved bottom-right screen coordinate.</param>
  /// <returns>True when stable hover bounds were resolved.</returns>
  private static unsafe bool TryGetJournalHoverBounds(
      AtkResNode* rowNode,
      AtkTextNode* questName,
      out Vector2 topLeft,
      out Vector2 bottomRight)
  {
    topLeft = default;
    bottomRight = default;

    if (rowNode == null ||
        questName == null ||
        !rowNode->IsVisible() ||
        !questName->AtkResNode.IsVisible())
    {
      return false;
    }

    (topLeft, bottomRight) = ResolveJournalHoverBounds(
        rowNode->ScreenX,
        rowNode->ScreenY,
        rowNode->Width,
        rowNode->Height,
        questName->ScreenX,
        questName->ScreenY,
        questName->GetWidth(),
        questName->GetHeight());
    return bottomRight.X > topLeft.X &&
           bottomRight.Y > topLeft.Y;
  }

  /// <summary>
  ///     Resolves the Journal hover rectangle from the live row and title
  ///     bounds, keeping the hitbox aligned to the row without vertically
  ///     overlapping adjacent recycled entries.
  /// </summary>
  /// <param name="rowLeft">The Journal row left screen coordinate.</param>
  /// <param name="rowTop">The Journal row top screen coordinate.</param>
  /// <param name="rowWidth">The Journal row width.</param>
  /// <param name="rowHeight">The Journal row height.</param>
  /// <param name="textLeft">The title-node left screen coordinate.</param>
  /// <param name="textTop">The title-node top screen coordinate.</param>
  /// <param name="textWidth">The title-node width.</param>
  /// <param name="textHeight">The title-node height.</param>
  /// <returns>The resolved Journal hover rectangle.</returns>
  internal static (Vector2 TopLeft, Vector2 BottomRight)
      ResolveJournalHoverBounds(
          float rowLeft,
          float rowTop,
          float rowWidth,
          float rowHeight,
          float textLeft,
          float textTop,
          float textWidth,
          float textHeight)
  {
    var resolvedRowWidth = Math.Max(1f, rowWidth);
    var resolvedRowHeight = Math.Max(1f, rowHeight);
    var resolvedTextWidth = Math.Max(1f, textWidth);
    var resolvedTextHeight = Math.Max(1f, textHeight);

    var left = Math.Max(0f, Math.Min(rowLeft, textLeft) - 8f);
    var top = Math.Max(0f, Math.Min(rowTop, textTop) + 2f);
    var right = Math.Max(
        left + 1f,
        Math.Max(
            rowLeft + resolvedRowWidth,
            textLeft + resolvedTextWidth) + 8f);
    var bottom = Math.Max(
        top + 1f,
        Math.Max(
            rowTop + resolvedRowHeight,
            textTop + resolvedTextHeight) - 2f);
    return (new Vector2(left, top), new Vector2(right, bottom));
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
      if (!TryGetJournalQuestTitleNode(
              rowNode,
              out var questName))
      {
        continue;
      }

      var questNameNodeKey = (nint)questName;
      if (!this.journalListNativeMutationNodeKeys.Remove(questNameNodeKey) ||
          !this.TryGetJournalListOriginalText(
              questNameNodeKey,
              out var originalQuestName))
      {
        continue;
      }

      var liveQuestNameText = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)questName->NodeText.StringPtr.Value);
      var translatedQuestName = this.TryGetJournalListHover(
              questNameNodeKey,
              out var snapshot)
          ? snapshot.TranslatedText
          : string.Empty;
      if (MatchesJournalListOwnedMutationSnapshot(
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
