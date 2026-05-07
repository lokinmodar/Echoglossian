// <copyright file="ScenarioTreeHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using AgentScenarioTree = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentScenarioTree;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Handles the ScenarioTree quest addon runtime using only QuestManager,
///     canonical quest data, and persisted quest plates.
/// </summary>
internal sealed class ScenarioTreeHandler : QuestAddonHandlerBase
{
  private const string ScenarioTreeAddonName = "ScenarioTree";

  private const string ScenarioTreeHoverPrefix = "ScenarioTree-";

  private const int MainScenarioValueIndex = 7;

  private const int JobScenarioValueIndex = 2;

  private static readonly TimeSpan ScenarioTreeRetryInterval =
      TimeSpan.FromSeconds(2);

  private readonly Dictionary<int, ScenarioTreeRuntimeEntry>
      scenarioTreeRuntimeEntries = [];
  private readonly HashSet<int> scenarioTreeNativeMutationIndices = [];

  private readonly QuestWaitingNotificationGate
      scenarioTreeWaitingNotificationGate = new();

  private bool currentScenarioTreeDataReady;

  private JournalTranslationDisplayMode? lastAppliedDisplayMode;

  private DateTime nextScenarioTreeRetryUtc = DateTime.MinValue;

  /// <summary>
  ///     Initializes a new instance of the <see cref="ScenarioTreeHandler" />
  ///     class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public ScenarioTreeHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PreRefresh, this.OnScenarioTreeEvent);
    this.RegisterHandler(
        AddonEvent.PreRequestedUpdate,
        this.OnScenarioTreeEvent);
    this.RegisterHandler(
        AddonEvent.PreDraw,
        this.OnScenarioTreePreDrawEvent);
    this.RegisterHandler(
        AddonEvent.PreHide,
        this.OnScenarioTreeCleanupEvent);
    this.RegisterHandler(
        AddonEvent.PreFinalize,
        this.OnScenarioTreeCleanupEvent);
  }

  /// <summary>
  ///     Gets whether the ScenarioTree family should use hover tooltips.
  /// </summary>
  private bool ScenarioTreeUsesHoverTooltips =>
      QuestAddonModeHelpers.UsesHoverTooltips(
          this.Config.ScenarioTreeTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the ScenarioTree family should write translated text into
  ///     the native addon.
  /// </summary>
  private bool ScenarioTreeWritesNativeTranslation =>
      QuestAddonModeHelpers.WritesNativeTranslation(
          this.Config.ScenarioTreeTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the ScenarioTree family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool ScenarioTreeHoverShowsOriginal =>
      QuestAddonModeHelpers.ShowsOriginalTooltips(
          this.Config.ScenarioTreeTranslationDisplayMode);

  /// <summary>
  ///     Gets whether translated ScenarioTree text should be normalized before
  ///     being written into the native UI.
  /// </summary>
  private bool ScenarioTreeShouldRemoveDiacritics =>
      QuestAddonModeHelpers.ShouldRemoveDiacritics(
          this.Config.ScenarioTreeTranslationDisplayMode,
          this.Config.RemoveDiacriticsWhenUsingReplacementQuest);

  /// <summary>
  ///     Refreshes the ScenarioTree runtime from canonical quest data and the
  ///     DB.
  /// </summary>
  private unsafe void RefreshScenarioTree()
  {
    if (!TryGetVisibleScenarioTree(out var addon, out var atkValues))
    {
      return;
    }

    if (!this.Config.TranslateScenarioTree ||
        this.DisableTranslationAccordingToState())
    {
      this.RestoreScenarioTreeOriginals(atkValues);
      this.RemoveHoverTooltipsByPrefix(ScenarioTreeHoverPrefix);
      this.currentScenarioTreeDataReady = false;
      this.lastAppliedDisplayMode = null;
      return;
    }

    var visibleEntries = this.CollectVisibleScenarioTreeEntries(atkValues);
    if (visibleEntries.Count == 0)
    {
      this.scenarioTreeRuntimeEntries.Clear();
      this.RemoveHoverTooltipsByPrefix(ScenarioTreeHoverPrefix);
      this.currentScenarioTreeDataReady = false;
      this.lastAppliedDisplayMode = null;
      this.nextScenarioTreeRetryUtc = DateTime.MinValue;
      this.ClearScenarioTreeWaitingState();
      return;
    }

    Dictionary<int, ScenarioTreeRuntimeEntry> runtimeEntries = new();
    HashSet<string> blockingQuestLabels = new(StringComparer.Ordinal);

    foreach (var visibleEntry in visibleEntries)
    {
      if (!this.TryResolveVisibleScenarioTreeEntry(
              visibleEntry,
              out var runtimeEntry,
              out var blockingQuestLabel))
      {
        blockingQuestLabels.Add(blockingQuestLabel);
      }

      runtimeEntries[runtimeEntry.ValueIndex] = runtimeEntry;
    }

    this.scenarioTreeRuntimeEntries.Clear();
    foreach (var (valueIndex, runtimeEntry) in runtimeEntries)
    {
      this.scenarioTreeRuntimeEntries[valueIndex] = runtimeEntry;
    }

    if (blockingQuestLabels.Count != 0)
    {
      this.currentScenarioTreeDataReady = false;
      this.nextScenarioTreeRetryUtc = DateTime.UtcNow + ScenarioTreeRetryInterval;
      this.RestoreScenarioTreeOriginals(atkValues);
      this.RemoveHoverTooltipsByPrefix(ScenarioTreeHoverPrefix);
      this.lastAppliedDisplayMode = null;
      this.NotifyScenarioTreeWaitingForQuestData(blockingQuestLabels);
      return;
    }

    this.currentScenarioTreeDataReady = true;
    this.nextScenarioTreeRetryUtc = DateTime.MinValue;
    this.ClearScenarioTreeWaitingState();

    this.ApplyScenarioTreePresentation(addon, atkValues);
  }

  /// <summary>
  ///     Tries to resolve the live ScenarioTree addon when it is visible.
  /// </summary>
  /// <param name="addon">The live addon pointer.</param>
  /// <param name="atkValues">The live ATK value pointer.</param>
  /// <returns>True when the visible addon was resolved.</returns>
  private static unsafe bool TryGetVisibleScenarioTree(
      out AtkUnitBase* addon,
      out AtkValue* atkValues)
  {
    addon = AtkStage.Instance()->RaptureAtkUnitManager->GetAddonByName(
        ScenarioTreeAddonName);
    atkValues = null;

    if (addon == null || !addon->IsVisible || addon->AtkValues == null)
    {
      return false;
    }

    atkValues = addon->AtkValues;
    return true;
  }

  /// <summary>
  ///     Collects the currently visible ScenarioTree quest slots.
  /// </summary>
  /// <param name="atkValues">The live addon payload.</param>
  /// <returns>The visible quest slots.</returns>
  private unsafe List<ScenarioTreeVisibleEntry> CollectVisibleScenarioTreeEntries(
      AtkValue* atkValues)
  {
    List<ScenarioTreeVisibleEntry> visibleEntries = [];

    foreach (var questSlot in CollectScenarioTreeQuestSlots())
    {
      if (!TryGetScenarioTreeText(
              atkValues,
              questSlot.ValueIndex,
              out var visibleText))
      {
        continue;
      }

      var originalText = this.ResolveOriginalScenarioTreeText(
          questSlot.ValueIndex,
          visibleText);
      if (string.IsNullOrWhiteSpace(originalText))
      {
        continue;
      }

      visibleEntries.Add(
          new ScenarioTreeVisibleEntry(
              questSlot.ValueIndex,
              questSlot.QuestRowId,
              originalText));
    }

    return visibleEntries;
  }

  /// <summary>
  ///     Tries to resolve one visible ScenarioTree quest slot entirely from
  ///     canonical quest data and persisted quest plates.
  /// </summary>
  /// <param name="visibleEntry">The visible quest slot.</param>
  /// <param name="runtimeEntry">The resolved runtime entry.</param>
  /// <param name="blockingQuestLabel">
  ///     The quest label to use if this visible quest blocks activation.
  /// </param>
  /// <returns>
  ///     <c>true</c> when the required translated payload is already stored in
  ///     the DB.
  /// </returns>
  private bool TryResolveVisibleScenarioTreeEntry(
      ScenarioTreeVisibleEntry visibleEntry,
      out ScenarioTreeRuntimeEntry runtimeEntry,
      out string blockingQuestLabel)
  {
    blockingQuestLabel = visibleEntry.OriginalText;

    if (!QuestTodoProgressResolver.TryResolveQuestTodoProgress(
            visibleEntry.QuestRowId,
            out var todoProgressSnapshot))
    {
      runtimeEntry = this.CreateScenarioTreeRuntimeEntry(
          visibleEntry.ValueIndex,
          progressKey: string.Empty,
          visibleEntry.OriginalText,
          visibleEntry.OriginalText);
      return false;
    }

    var questCanonicalData = QuestCanonicalData.Create(
        todoProgressSnapshot.QuestProgress,
        GetGameVersion());
    var questPlate = questCanonicalData.ToQuestPlate(
        ClientStateInterface.ClientLanguage.Humanize(),
        LangDict[LanguageInt].Code,
        this.Config.ChosenTransEngine,
        DateTime.Now);
    var foundQuestPlate = this.FindQuestPlate(questPlate);
    if (foundQuestPlate == null ||
        !this.TryResolveScenarioTreeTranslatedText(
            visibleEntry,
            questCanonicalData,
            foundQuestPlate,
            out var translatedText))
    {
      runtimeEntry = this.CreateScenarioTreeRuntimeEntry(
          visibleEntry.ValueIndex,
          todoProgressSnapshot.CacheKey,
          visibleEntry.OriginalText,
          visibleEntry.OriginalText);
      return false;
    }

    runtimeEntry = this.CreateScenarioTreeRuntimeEntry(
        visibleEntry.ValueIndex,
        todoProgressSnapshot.CacheKey,
        visibleEntry.OriginalText,
        translatedText);
    return true;
  }

  /// <summary>
  ///     Collects the currently active ScenarioTree quest slots from the
  ///     native agent state.
  /// </summary>
  /// <returns>The active ScenarioTree quest slots.</returns>
  private static unsafe List<ScenarioTreeQuestSlot> CollectScenarioTreeQuestSlots()
  {
    List<ScenarioTreeQuestSlot> questSlots = [];

    if (!TryGetScenarioTreeAgentData(out var scenarioTreeData))
    {
      return questSlots;
    }

    var mainScenarioQuestRowId = ResolveMainScenarioQuestRowId(scenarioTreeData);
    if (mainScenarioQuestRowId != 0)
    {
      questSlots.Add(
          new ScenarioTreeQuestSlot(
              MainScenarioValueIndex,
              mainScenarioQuestRowId));
    }

    var jobScenarioQuestRowId = ResolveJobScenarioQuestRowId(scenarioTreeData);
    if (jobScenarioQuestRowId != 0)
    {
      questSlots.Add(
          new ScenarioTreeQuestSlot(
              JobScenarioValueIndex,
              jobScenarioQuestRowId));
    }

    return questSlots;
  }

  /// <summary>
  ///     Tries to resolve the native ScenarioTree agent data.
  /// </summary>
  /// <param name="scenarioTreeData">The agent data, if any.</param>
  /// <returns>True when the agent data is available.</returns>
  private static unsafe bool TryGetScenarioTreeAgentData(
      out AgentScenarioTree.AgentScenarioTreeData* scenarioTreeData)
  {
    scenarioTreeData = null;

    var scenarioTreeAgent = AgentScenarioTree.Instance();
    if (scenarioTreeAgent == null || scenarioTreeAgent->Data == null)
    {
      return false;
    }

    scenarioTreeData = scenarioTreeAgent->Data;
    return true;
  }

  /// <summary>
  ///     Resolves the current main-scenario quest row id from the native
  ///     ScenarioTree agent state.
  /// </summary>
  /// <param name="scenarioTreeData">The live agent data.</param>
  /// <returns>The current main-scenario quest row id, or 0.</returns>
  private static unsafe uint ResolveMainScenarioQuestRowId(
      AgentScenarioTree.AgentScenarioTreeData* scenarioTreeData)
  {
    var mainScenarioQuestIds = (ushort*)((byte*)scenarioTreeData + 0x00);
    var selectedQuestIndex = Math.Min((int)scenarioTreeData->MSQPathIndex, 2);
    var selectedQuestId = mainScenarioQuestIds[selectedQuestIndex];
    if (selectedQuestId == 0)
    {
      for (var i = 0; i < 3; i++)
      {
        if (mainScenarioQuestIds[i] != 0)
        {
          selectedQuestId = mainScenarioQuestIds[i];
          break;
        }
      }
    }

    if (selectedQuestId == 0)
    {
      selectedQuestId = mainScenarioQuestIds[3];
    }

    return selectedQuestId == 0
        ? 0
        : selectedQuestId | 0x10000U;
  }

  /// <summary>
  ///     Resolves the current job-scenario quest row id from the native
  ///     ScenarioTree agent state.
  /// </summary>
  /// <param name="scenarioTreeData">The live agent data.</param>
  /// <returns>The current job-scenario quest row id, or 0.</returns>
  private static unsafe uint ResolveJobScenarioQuestRowId(
      AgentScenarioTree.AgentScenarioTreeData* scenarioTreeData)
  {
    var jobQuestIds = (ushort*)((byte*)scenarioTreeData + 0x08);
    var selectedQuestIndex = Math.Min((int)scenarioTreeData->JobQuestIndex, 1);
    var selectedQuestId = jobQuestIds[selectedQuestIndex];
    if (selectedQuestId == 0)
    {
      for (var i = 0; i < 2; i++)
      {
        if (jobQuestIds[i] != 0)
        {
          selectedQuestId = jobQuestIds[i];
          break;
        }
      }
    }

    return selectedQuestId == 0
        ? 0
        : selectedQuestId | 0x10000U;
  }

  /// <summary>
  ///     Tries to resolve the translated ScenarioTree text for one visible slot
  ///     from the canonical quest payload already persisted in the DB.
  /// </summary>
  /// <param name="visibleEntry">The visible ScenarioTree slot.</param>
  /// <param name="questCanonicalData">The canonical quest payload.</param>
  /// <param name="questPlate">The translated quest plate.</param>
  /// <param name="translatedText">The resolved translated text.</param>
  /// <returns>True when the translated text was found in the DB.</returns>
  private bool TryResolveScenarioTreeTranslatedText(
      ScenarioTreeVisibleEntry visibleEntry,
      QuestCanonicalData questCanonicalData,
      QuestPlate questPlate,
      out string translatedText)
  {
    translatedText = string.Empty;

    if (string.Equals(
            visibleEntry.OriginalText,
            questCanonicalData.QuestProgressSnapshot.QuestName,
            StringComparison.Ordinal))
    {
      translatedText = questPlate.TranslatedQuestName ?? string.Empty;
      return !string.IsNullOrWhiteSpace(translatedText);
    }

    if (string.Equals(
            visibleEntry.OriginalText,
            questCanonicalData.CurrentSequenceText,
            StringComparison.Ordinal))
    {
      translatedText = questPlate.TranslatedQuestMessage ?? string.Empty;
      return !string.IsNullOrWhiteSpace(translatedText);
    }

    foreach (var objectiveRow in questCanonicalData.QuestProgressSnapshot.QuestSteps)
    {
      if (!string.Equals(
              objectiveRow.Text,
              visibleEntry.OriginalText,
              StringComparison.Ordinal))
      {
        continue;
      }

      if (questPlate.TryGetTranslatedObjectiveText(
              objectiveRow.KeyText,
              objectiveRow.Text,
              out translatedText) &&
          !string.IsNullOrWhiteSpace(translatedText))
      {
        return true;
      }
    }

    foreach (var summaryRow in questCanonicalData.QuestProgressSnapshot.QuestSeqTexts)
    {
      if (!string.Equals(
              summaryRow.Text,
              visibleEntry.OriginalText,
              StringComparison.Ordinal))
      {
        continue;
      }

      if (questPlate.TryGetTranslatedSummaryText(
              summaryRow.KeyText,
              summaryRow.Text,
              out translatedText) &&
          !string.IsNullOrWhiteSpace(translatedText))
      {
        return true;
      }
    }

    foreach (var systemRow in questCanonicalData.QuestProgressSnapshot.QuestSystemTexts)
    {
      if (!string.Equals(
              systemRow.Text,
              visibleEntry.OriginalText,
              StringComparison.Ordinal))
      {
        continue;
      }

      if (questPlate.TryGetTranslatedSystemText(
              systemRow.KeyText,
              systemRow.Text,
              out translatedText) &&
          !string.IsNullOrWhiteSpace(translatedText))
      {
        return true;
      }
    }

    return false;
  }

  /// <summary>
  ///     Creates the runtime payload for one visible ScenarioTree slot.
  /// </summary>
  /// <param name="valueIndex">The addon value index.</param>
  /// <param name="progressKey">The stable quest progress key.</param>
  /// <param name="originalText">The original source text.</param>
  /// <param name="translatedText">The translated text.</param>
  /// <returns>The runtime entry.</returns>
  private ScenarioTreeRuntimeEntry CreateScenarioTreeRuntimeEntry(
      int valueIndex,
      string progressKey,
      string originalText,
      string translatedText)
  {
    return new ScenarioTreeRuntimeEntry(
        this.BuildScenarioTreeRuntimeEntryKey(progressKey, valueIndex),
        progressKey,
        valueIndex,
        originalText,
        translatedText);
  }

  /// <summary>
  ///     Builds the stable runtime key for one ScenarioTree slot.
  /// </summary>
  /// <param name="progressKey">The quest progress key.</param>
  /// <param name="valueIndex">The addon value index.</param>
  /// <returns>The stable runtime key.</returns>
  private string BuildScenarioTreeRuntimeEntryKey(
      string progressKey,
      int valueIndex)
  {
    var effectiveProgressKey = string.IsNullOrWhiteSpace(progressKey)
        ? "pending"
        : progressKey;
    return $"{ScenarioTreeHoverPrefix}{effectiveProgressKey}-{valueIndex}";
  }

  /// <summary>
  ///     Resolves the original visible text for a ScenarioTree slot, even if
  ///     the addon currently shows translated text from a previously applied
  ///     mode.
  /// </summary>
  /// <param name="valueIndex">The addon value index.</param>
  /// <param name="visibleText">The current visible text.</param>
  /// <returns>The original source text for that slot.</returns>
  private string ResolveOriginalScenarioTreeText(
      int valueIndex,
      string visibleText)
  {
    if (this.scenarioTreeRuntimeEntries.TryGetValue(valueIndex, out var previousEntry) &&
        !string.IsNullOrWhiteSpace(previousEntry.OriginalText))
    {
      return QuestAddonOriginalTextHelper.ResolveOriginalVisibleText(
          visibleText,
          previousEntry.OriginalText,
          this.GetScenarioTreeTranslatedDisplayText(previousEntry.TranslatedText));
    }

    return visibleText;
  }

  /// <summary>
  ///     Applies the current ScenarioTree presentation mode using only the
  ///     local runtime entries resolved from the DB.
  /// </summary>
  /// <param name="addon">The live ScenarioTree addon.</param>
  /// <param name="atkValues">The live addon payload.</param>
  private unsafe void ApplyScenarioTreePresentation(
      AtkUnitBase* addon,
      AtkValue* atkValues)
  {
    if (!this.currentScenarioTreeDataReady)
    {
      this.RestoreScenarioTreeOriginals(atkValues);
      this.RemoveHoverTooltipsByPrefix(ScenarioTreeHoverPrefix);
      this.lastAppliedDisplayMode = null;
      return;
    }

    foreach (var runtimeEntry in this.scenarioTreeRuntimeEntries.Values)
    {
      if (this.ScenarioTreeWritesNativeTranslation)
      {
        var displayText = this.GetScenarioTreeTranslatedDisplayText(runtimeEntry.TranslatedText);
        atkValues[runtimeEntry.ValueIndex].SetManagedString(displayText ?? string.Empty);
        this.scenarioTreeNativeMutationIndices.Add(runtimeEntry.ValueIndex);
      }
    }

    if (!this.ScenarioTreeWritesNativeTranslation)
    {
      this.RestoreScenarioTreeOriginals(atkValues);
    }

    if (this.ScenarioTreeUsesHoverTooltips)
    {
      this.RefreshScenarioTreeHoverTargets(addon);
    }
    else
    {
      this.RemoveHoverTooltipsByPrefix(ScenarioTreeHoverPrefix);
    }

    this.lastAppliedDisplayMode =
        this.Config.ScenarioTreeTranslationDisplayMode;
  }

  /// <summary>
  ///     Restores the original ScenarioTree text for all slots currently
  ///     tracked in the local runtime state.
  /// </summary>
  /// <param name="atkValues">The live addon payload.</param>
  private unsafe void RestoreScenarioTreeOriginals(AtkValue* atkValues)
  {
    foreach (var runtimeEntry in this.scenarioTreeRuntimeEntries.Values)
    {
      if (!this.scenarioTreeNativeMutationIndices.Remove(runtimeEntry.ValueIndex))
      {
        continue;
      }

      atkValues[runtimeEntry.ValueIndex].SetManagedString(
          runtimeEntry.OriginalText ?? string.Empty);
    }

    this.RemoveHoverTooltipsByPrefix(ScenarioTreeHoverPrefix);
  }

  /// <summary>
  ///     Normalizes translated ScenarioTree text before it is written into the
  ///     native UI.
  /// </summary>
  /// <param name="translatedText">The translated text.</param>
  /// <returns>The translated text as it should be displayed natively.</returns>
  private string GetScenarioTreeTranslatedDisplayText(string translatedText)
  {
    if (!this.ScenarioTreeShouldRemoveDiacritics)
    {
      return translatedText;
    }

    return this.NormalizeQuestText(translatedText ?? string.Empty);
  }

  /// <summary>
  ///     Refreshes hover targets for the currently visible ScenarioTree slots.
  /// </summary>
  /// <param name="addon">The live ScenarioTree addon.</param>
  private unsafe void RefreshScenarioTreeHoverTargets(AtkUnitBase* addon)
  {
    if (!this.currentScenarioTreeDataReady || !this.ScenarioTreeUsesHoverTooltips)
    {
      this.RemoveHoverTooltipsByPrefix(ScenarioTreeHoverPrefix);
      return;
    }

    var orderedEntries = this.scenarioTreeRuntimeEntries.Values
        .OrderBy(entry => entry.ValueIndex)
        .ToList();
    var originalText = string.Join(
        $"{Environment.NewLine}{Environment.NewLine}",
        orderedEntries
            .Select(entry => entry.OriginalText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal));
    var translatedText = string.Join(
        $"{Environment.NewLine}{Environment.NewLine}",
        orderedEntries
            .Select(entry => entry.TranslatedText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal));
    if (string.IsNullOrWhiteSpace(originalText) ||
        string.IsNullOrWhiteSpace(translatedText))
    {
      this.RemoveHoverTooltipsByPrefix(ScenarioTreeHoverPrefix);
      return;
    }

    this.RegisterTranslatedHoverTooltip(
        $"{ScenarioTreeHoverPrefix}{(nint)addon:X}",
        addon,
        originalText,
        translatedText,
        translatedPayloadReady: true,
        swapEnabled: this.ScenarioTreeHoverShowsOriginal,
        forceEnabled: true,
        denseHitbox: true);
  }

  /// <summary>
  ///     Emits a notification while the ScenarioTree is waiting for all visible
  ///     quest data to exist in the DB.
  /// </summary>
  /// <param name="blockingQuestLabels">The visible quest labels still waiting.</param>
  private void NotifyScenarioTreeWaitingForQuestData(
      IReadOnlyCollection<string> blockingQuestLabels)
  {
    if (!this.Config.ShowQuestProgressNotifications)
    {
      return;
    }

    if (!this.scenarioTreeWaitingNotificationGate.TryBeginWaiting(
            blockingQuestLabels.Count))
    {
      return;
    }

    NotificationManager.AddNotification(new Notification
    {
      Title = Resources.Name,
      Content = string.Format(
          CultureInfo.CurrentCulture,
          Resources.ScenarioTreeAwaitingQuestDataNotification,
          blockingQuestLabels.Count),
      Icon = FontAwesomeIcon.Book.ToNotificationIcon(),
      Type = NotificationType.Info,
    });
  }

  /// <summary>
  ///     Clears the debounced ScenarioTree waiting-notification state.
  /// </summary>
  private void ClearScenarioTreeWaitingState()
  {
    this.scenarioTreeWaitingNotificationGate.Clear();
  }

  /// <summary>
  ///     Handles ScenarioTree refresh and requested-update events.
  /// </summary>
  /// <param name="type">The lifecycle event.</param>
  /// <param name="args">The lifecycle arguments.</param>
  private unsafe void OnScenarioTreeEvent(AddonEvent type, AddonArgs args)
  {
    this.RefreshScenarioTree();
  }

  /// <summary>
  ///     Handles ScenarioTree draw events so the addon can retry activation
  ///     after the background prefetch finishes and react immediately to mode
  ///     switches.
  /// </summary>
  /// <param name="type">The lifecycle event.</param>
  /// <param name="args">The lifecycle arguments.</param>
  private unsafe void OnScenarioTreePreDrawEvent(
      AddonEvent type,
      AddonArgs args)
  {
    if (!TryGetVisibleScenarioTree(out var addon, out var atkValues))
    {
      return;
    }

    if (!this.Config.TranslateScenarioTree ||
        this.DisableTranslationAccordingToState())
    {
      this.RestoreScenarioTreeOriginals(atkValues);
      this.RemoveHoverTooltipsByPrefix(ScenarioTreeHoverPrefix);
      this.currentScenarioTreeDataReady = false;
      this.lastAppliedDisplayMode = null;
      return;
    }

    if (!this.currentScenarioTreeDataReady)
    {
      if (DateTime.UtcNow >= this.nextScenarioTreeRetryUtc)
      {
        this.RefreshScenarioTree();
      }

      return;
    }

    if (this.lastAppliedDisplayMode !=
        this.Config.ScenarioTreeTranslationDisplayMode)
    {
      this.ApplyScenarioTreePresentation(addon, atkValues);
      return;
    }

    if (this.ScenarioTreeUsesHoverTooltips)
    {
      this.RefreshScenarioTreeHoverTargets(addon);
    }
  }

  /// <summary>
  ///     Clears the ScenarioTree runtime state when the addon closes.
  /// </summary>
  /// <param name="type">The lifecycle event.</param>
  /// <param name="args">The lifecycle arguments.</param>
  private void OnScenarioTreeCleanupEvent(AddonEvent type, AddonArgs args)
  {
    if (!string.Equals(
            args.AddonName,
            ScenarioTreeAddonName,
            StringComparison.Ordinal))
    {
      return;
    }

    this.scenarioTreeRuntimeEntries.Clear();
    this.scenarioTreeNativeMutationIndices.Clear();
    this.RemoveHoverTooltipsByPrefix(ScenarioTreeHoverPrefix);
    this.currentScenarioTreeDataReady = false;
    this.lastAppliedDisplayMode = null;
    this.nextScenarioTreeRetryUtc = DateTime.MinValue;
    this.ClearScenarioTreeWaitingState();
  }

  /// <summary>
  ///     Tries to read one visible ScenarioTree string slot.
  /// </summary>
  /// <param name="atkValues">The live addon payload.</param>
  /// <param name="valueIndex">The value index to read.</param>
  /// <param name="visibleText">The visible string, if any.</param>
  /// <returns>True when a usable visible string was found.</returns>
  private static unsafe bool TryGetScenarioTreeText(
      AtkValue* atkValues,
      int valueIndex,
      out string visibleText)
  {
    visibleText = string.Empty;

    if (atkValues[valueIndex].Type is not
        (ValueType.String or
         ValueType.String8 or
         ValueType.ManagedString) ||
        !atkValues[valueIndex].String.HasValue)
    {
      return false;
    }

    visibleText = MemoryHelper.ReadSeStringAsString(
        out _,
        (nint)atkValues[valueIndex].String.Value);
    return !string.IsNullOrWhiteSpace(visibleText);
  }

  /// <summary>
  ///     Represents one visible ScenarioTree quest slot.
  /// </summary>
  /// <param name="ValueIndex">The addon value index.</param>
  /// <param name="QuestRowId">The resolved quest row id from AgentScenarioTree.</param>
  /// <param name="OriginalText">The original visible text.</param>
  private sealed record ScenarioTreeVisibleEntry(
      int ValueIndex,
      uint QuestRowId,
      string OriginalText);

  /// <summary>
  ///     Represents one native ScenarioTree quest slot resolved from the agent.
  /// </summary>
  /// <param name="ValueIndex">The addon value index that renders the slot.</param>
  /// <param name="QuestRowId">The resolved quest row id for that slot.</param>
  private sealed record ScenarioTreeQuestSlot(
      int ValueIndex,
      uint QuestRowId);

  /// <summary>
  ///     Represents one resolved ScenarioTree runtime slot.
  /// </summary>
  /// <param name="Key">The stable runtime key.</param>
  /// <param name="ProgressKey">The stable quest progress key.</param>
  /// <param name="ValueIndex">The addon value index.</param>
  /// <param name="OriginalText">The original source text.</param>
  /// <param name="TranslatedText">The translated text from the DB.</param>
  private sealed record ScenarioTreeRuntimeEntry(
      string Key,
      string ProgressKey,
      int ValueIndex,
      string OriginalText,
      string TranslatedText);
}
