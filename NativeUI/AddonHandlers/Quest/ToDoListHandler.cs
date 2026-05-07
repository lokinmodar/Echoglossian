// <copyright file="ToDoListHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Handles the ToDoList quest addon runtime using only QuestManager,
///     canonical quest data, and persisted quest plates.
/// </summary>
internal sealed class ToDoListHandler : QuestAddonHandlerBase
{
  private const string EmptyObjective = "???";

  private const string ToDoListAddonName = "_ToDoList";

  private const string ToDoListHoverPrefix = "ToDoList-";

  private static readonly TimeSpan ToDoListRetryInterval =
      TimeSpan.FromSeconds(2);

  private readonly Dictionary<string, ToDoRuntimeEntry> toDoRuntimeEntries = [];
  private readonly HashSet<string> toDoNativeMutationKeys = [];

  private readonly QuestWaitingNotificationGate toDoListWaitingNotificationGate
      = new();

  private bool currentToDoListDataReady;

  private JournalTranslationDisplayMode? lastAppliedDisplayMode;

  private DateTime nextToDoListRetryUtc = DateTime.MinValue;

  /// <summary>
  ///     Initializes a new instance of the <see cref="ToDoListHandler" /> class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public ToDoListHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PostRequestedUpdate, this.OnToDoListEvent);
    this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.OnToDoListEvent);
    this.RegisterHandler(AddonEvent.PreDraw, this.OnToDoListPreDrawEvent);
    this.RegisterHandler(AddonEvent.PreHide, this.OnToDoListCleanupEvent);
    this.RegisterHandler(AddonEvent.PreFinalize, this.OnToDoListCleanupEvent);
  }

  /// <summary>
  ///     Gets whether the ToDoList family should use hover tooltips.
  /// </summary>
  private bool ToDoListUsesHoverTooltips =>
      QuestAddonModeHelpers.UsesHoverTooltips(
          this.Config.ToDoListTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the ToDoList family should write translated text into the
  ///     native addon.
  /// </summary>
  private bool ToDoListWritesNativeTranslation =>
      QuestAddonModeHelpers.WritesNativeTranslation(
          this.Config.ToDoListTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the ToDoList family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool ToDoListHoverShowsOriginal =>
      QuestAddonModeHelpers.ShowsOriginalTooltips(
          this.Config.ToDoListTranslationDisplayMode);

  /// <summary>
  ///     Gets whether translated ToDoList text should be normalized before
  ///     being written into the native UI.
  /// </summary>
  private bool ToDoListShouldRemoveDiacritics =>
      QuestAddonModeHelpers.ShouldRemoveDiacritics(
          this.Config.ToDoListTranslationDisplayMode,
          this.Config.RemoveDiacriticsWhenUsingReplacementQuest);

  /// <summary>
  ///     Refreshes the ToDoList runtime from canonical quest data and the DB.
  /// </summary>
  private unsafe void RefreshToDoList()
  {
    if (!TryGetVisibleToDoList(out var todoList))
    {
      return;
    }

    if (!this.Config.TranslateToDoList ||
        this.DisableTranslationAccordingToState())
    {
      this.RestoreToDoListOriginals(todoList);
      this.RemoveHoverTooltipsByPrefix(ToDoListHoverPrefix);
      this.currentToDoListDataReady = false;
      this.lastAppliedDisplayMode = null;
      return;
    }

    var visibleQuests = this.CollectVisibleToDoQuests(todoList);
    var runtimeEntries = new Dictionary<string, ToDoRuntimeEntry>(
        StringComparer.Ordinal);
    HashSet<string> blockingQuestLabels = new(StringComparer.Ordinal);

    foreach (var visibleQuest in visibleQuests)
    {
      if (!this.TryResolveVisibleQuestEntries(
              visibleQuest,
              out var resolvedEntries,
              out var blockingQuestLabel))
      {
        blockingQuestLabels.Add(blockingQuestLabel);
      }

      foreach (var resolvedEntry in resolvedEntries)
      {
        runtimeEntries[resolvedEntry.Key] = resolvedEntry;
      }
    }

    this.toDoRuntimeEntries.Clear();
    foreach (var (entryKey, entryValue) in runtimeEntries)
    {
      this.toDoRuntimeEntries[entryKey] = entryValue;
    }

    if (blockingQuestLabels.Count != 0)
    {
      this.currentToDoListDataReady = false;
      this.nextToDoListRetryUtc = DateTime.UtcNow + ToDoListRetryInterval;
      this.RestoreToDoListOriginals(todoList);
      this.RemoveHoverTooltipsByPrefix(ToDoListHoverPrefix);
      this.lastAppliedDisplayMode = null;
      this.NotifyToDoListWaitingForQuestData(blockingQuestLabels);
      return;
    }

    this.currentToDoListDataReady = true;
    this.nextToDoListRetryUtc = DateTime.MinValue;
    this.ClearToDoListWaitingState();
    this.ApplyToDoListPresentation(todoList);
  }

  /// <summary>
  ///     Tries to resolve the live ToDoList addon when it is visible.
  /// </summary>
  /// <param name="todoList">The live addon pointer.</param>
  /// <returns>True when the visible addon was resolved.</returns>
  private static unsafe bool TryGetVisibleToDoList(out AtkUnitBase* todoList)
  {
    todoList = AtkStage.Instance()->RaptureAtkUnitManager->GetAddonByName(
        ToDoListAddonName);
    return todoList != null && todoList->IsVisible;
  }

  /// <summary>
  ///     Collects the currently visible quest rows and objective rows from the
  ///     ToDoList addon.
  /// </summary>
  /// <param name="todoList">The live ToDoList addon.</param>
  /// <returns>The visible quest rows grouped with their objective rows.</returns>
  private unsafe List<ToDoVisibleQuest> CollectVisibleToDoQuests(
      AtkUnitBase* todoList)
  {
    List<ToDoItem> questNameRows = [];
    List<ToDoItem> objectiveRows = [];
    List<ToDoItem> levelQuestObjectiveRows = [];

    for (var i = 0; i < todoList->UldManager.NodeListCount; i++)
    {
      var outerNode = todoList->UldManager.NodeList[i];
      if (outerNode == null || !outerNode->IsVisible())
      {
        continue;
      }

      if (outerNode->Type == NodeType.Collision ||
          outerNode->Type == NodeType.Res)
      {
        continue;
      }

      var nodeId = outerNode->NodeId;
      if (nodeId == 8 || nodeId == 9)
      {
        continue;
      }

      var componentNode = outerNode->GetAsAtkComponentNode();
      if (componentNode == null || componentNode->Component == null)
      {
        continue;
      }

      for (var j = 0; j < componentNode->Component->UldManager.NodeListCount; j++)
      {
        var childNode = componentNode->Component->UldManager.NodeList[j];
        if (childNode == null ||
            !childNode->IsVisible() ||
            childNode->Type != NodeType.Text)
        {
          continue;
        }

        var childNodeId = childNode->NodeId;
        var originalStep = childNode->GetAsAtkTextNode()->NodeText;
        if (originalStep.IsEmpty)
        {
          continue;
        }

        var originalStepText = MemoryHelper.ReadSeStringAsString(
            out _,
            (nint)originalStep.StringPtr.Value);
        if (string.IsNullOrWhiteSpace(originalStepText))
        {
          continue;
        }

        if (IsValidTimeFormat(originalStepText))
        {
          continue;
        }

        if (nodeId == 4 && childNodeId == 8)
        {
          continue;
        }

        var todoItem = new ToDoItem(
            originalStepText,
            i,
            j,
            nodeId);

        if (nodeId > 60000 ||
            (nodeId == 4 && childNodeId == 3) ||
            (nodeId == 6 && childNodeId == 2))
        {
          questNameRows.Add(todoItem);
        }
        else if (nodeId == 4 || nodeId == 5)
        {
          levelQuestObjectiveRows.Add(todoItem);
        }
        else
        {
          objectiveRows.Add(todoItem);
        }
      }
    }

    objectiveRows.Reverse();

    List<ToDoVisibleQuest> visibleQuests = [];
    var objectiveIndex = 0;
    foreach (var questNameRow in questNameRows)
    {
      List<ToDoItem> questObjectives = [];
      if (objectiveIndex < objectiveRows.Count)
      {
        var currentObjective = objectiveRows[objectiveIndex];
        questObjectives.Add(currentObjective);
        questObjectives = this.GetQuestObjectives(
            currentObjective.NodeId,
            objectiveIndex,
            objectiveRows,
            questObjectives);
      }

      objectiveIndex += questObjectives.Count;
      if (questNameRow.NodeId == 4)
      {
        questObjectives.AddRange(levelQuestObjectiveRows);
      }

      visibleQuests.Add(
          new ToDoVisibleQuest(
              questNameRow,
              questObjectives));
    }

    return visibleQuests;
  }

  /// <summary>
  ///     Gets the contiguous objective block that belongs to a quest row.
  /// </summary>
  /// <param name="currentObjectiveNode">The current node identifier.</param>
  /// <param name="objectiveIndex">The current objective index.</param>
  /// <param name="objectiveRows">The full objective list.</param>
  /// <param name="questObjectives">The objectives collected for this quest.</param>
  /// <returns>The collected quest objectives.</returns>
  private List<ToDoItem> GetQuestObjectives(
      uint currentObjectiveNode,
      int objectiveIndex,
      List<ToDoItem> objectiveRows,
      List<ToDoItem> questObjectives)
  {
    var currentIndex = objectiveIndex + 1;
    if (currentIndex >= objectiveRows.Count)
    {
      return questObjectives;
    }

    var objective = objectiveRows[currentIndex];
    if (Math.Abs((long)currentObjectiveNode - objective.NodeId) > 1)
    {
      return questObjectives;
    }

    questObjectives.Add(objective);
    return this.GetQuestObjectives(
        objective.NodeId,
        currentIndex,
        objectiveRows,
        questObjectives);
  }

  /// <summary>
  ///     Resolves the visible quest row and its objective rows entirely from
  ///     canonical quest data and persisted quest plates.
  /// </summary>
  /// <param name="visibleQuest">The visible quest row and grouped objectives.</param>
  /// <param name="runtimeEntries">The resolved runtime entries.</param>
  /// <param name="blockingQuestLabel">
  ///     The quest label to use if this visible quest blocks activation.
  /// </param>
  /// <returns>
  ///     <c>true</c> when every required translated payload is already stored
  ///     in the DB.
  /// </returns>
  private bool TryResolveVisibleQuestEntries(
      ToDoVisibleQuest visibleQuest,
      out List<ToDoRuntimeEntry> runtimeEntries,
      out string blockingQuestLabel)
  {
    runtimeEntries = [];

    var originalQuestText = this.ResolveOriginalToDoText(visibleQuest.QuestRow);
    blockingQuestLabel = originalQuestText;

    if (!QuestTodoProgressResolver.TryResolveQuestTodoProgress(
            originalQuestText,
            out var todoProgressSnapshot))
    {
      runtimeEntries.Add(
          this.CreateQuestRuntimeEntry(
              visibleQuest.QuestRow,
              progressKey: string.Empty,
              originalQuestText,
              originalQuestText));

      foreach (var objectiveRow in visibleQuest.Objectives)
      {
        var originalObjectiveText = this.ResolveOriginalToDoText(objectiveRow);
        runtimeEntries.Add(
            this.CreateObjectiveRuntimeEntry(
                objectiveRow,
                progressKey: string.Empty,
                originalObjectiveText,
                originalObjectiveText));
      }

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
        string.IsNullOrWhiteSpace(foundQuestPlate.TranslatedQuestName))
    {
      runtimeEntries.Add(
          this.CreateQuestRuntimeEntry(
              visibleQuest.QuestRow,
              todoProgressSnapshot.CacheKey,
              originalQuestText,
              originalQuestText));

      foreach (var objectiveRow in visibleQuest.Objectives)
      {
        var originalObjectiveText = this.ResolveOriginalToDoText(objectiveRow);
        runtimeEntries.Add(
            this.CreateObjectiveRuntimeEntry(
                objectiveRow,
                todoProgressSnapshot.CacheKey,
                originalObjectiveText,
                originalObjectiveText));
      }

      return false;
    }

    runtimeEntries.Add(
        this.CreateQuestRuntimeEntry(
            visibleQuest.QuestRow,
            todoProgressSnapshot.CacheKey,
            originalQuestText,
            foundQuestPlate.TranslatedQuestName));

    var canonicalObjectiveRows = todoProgressSnapshot.QuestProgress.QuestSteps
        .Where(step => !string.IsNullOrWhiteSpace(step.Text))
        .ToArray();
    var trackedObjectiveRows = visibleQuest.Objectives
        .Where(item => this.ShouldTrackObjectiveRow(this.ResolveOriginalToDoText(item)))
        .ToArray();
    if (trackedObjectiveRows.Length > canonicalObjectiveRows.Length)
    {
      foreach (var objectiveRow in visibleQuest.Objectives)
      {
        var originalObjectiveText = this.ResolveOriginalToDoText(objectiveRow);
        runtimeEntries.Add(
            this.CreateObjectiveRuntimeEntry(
                objectiveRow,
                todoProgressSnapshot.CacheKey,
                originalObjectiveText,
                originalObjectiveText));
      }

      return false;
    }

    var trackedObjectiveIndex = 0;
    foreach (var objectiveRow in visibleQuest.Objectives)
    {
      var originalObjectiveText = this.ResolveOriginalToDoText(objectiveRow);
      if (!this.ShouldTrackObjectiveRow(originalObjectiveText))
      {
        runtimeEntries.Add(
            this.CreateObjectiveRuntimeEntry(
                objectiveRow,
                todoProgressSnapshot.CacheKey,
                originalObjectiveText,
                originalObjectiveText));
        continue;
      }

      var canonicalObjectiveRow = canonicalObjectiveRows[trackedObjectiveIndex++];
      if (!foundQuestPlate.TryGetTranslatedObjectiveText(
              canonicalObjectiveRow.KeyText,
              canonicalObjectiveRow.Text,
              out var translatedObjectiveText) ||
          string.IsNullOrWhiteSpace(translatedObjectiveText))
      {
        runtimeEntries.Add(
            this.CreateObjectiveRuntimeEntry(
                objectiveRow,
                todoProgressSnapshot.CacheKey,
                originalObjectiveText,
                originalObjectiveText));
        return false;
      }

      runtimeEntries.Add(
          this.CreateObjectiveRuntimeEntry(
              objectiveRow,
              todoProgressSnapshot.CacheKey,
              originalObjectiveText,
              translatedObjectiveText));
    }

    return true;
  }

  /// <summary>
  ///     Creates the runtime payload for one visible quest-name row.
  /// </summary>
  /// <param name="questRow">The visible quest row.</param>
  /// <param name="progressKey">The stable quest progress key.</param>
  /// <param name="originalText">The resolved original quest text.</param>
  /// <param name="translatedText">The translated quest text.</param>
  /// <returns>The runtime entry.</returns>
  private ToDoRuntimeEntry CreateQuestRuntimeEntry(
      ToDoItem questRow,
      string progressKey,
      string originalText,
      string translatedText)
  {
    return new ToDoRuntimeEntry(
        this.BuildToDoRuntimeEntryKey(
            progressKey,
            questRow.IndexI,
            questRow.IndexJ,
            questRow.NodeId),
        progressKey,
        questRow.IndexI,
        questRow.IndexJ,
        questRow.NodeId,
        originalText,
        translatedText);
  }

  /// <summary>
  ///     Creates the runtime payload for one visible objective row.
  /// </summary>
  /// <param name="objectiveRow">The visible objective row.</param>
  /// <param name="progressKey">The stable quest progress key.</param>
  /// <param name="originalText">The resolved original objective text.</param>
  /// <param name="translatedText">The translated objective text.</param>
  /// <returns>The runtime entry.</returns>
  private ToDoRuntimeEntry CreateObjectiveRuntimeEntry(
      ToDoItem objectiveRow,
      string progressKey,
      string originalText,
      string translatedText)
  {
    return new ToDoRuntimeEntry(
        this.BuildToDoRuntimeEntryKey(
            progressKey,
            objectiveRow.IndexI,
            objectiveRow.IndexJ,
            objectiveRow.NodeId),
        progressKey,
        objectiveRow.IndexI,
        objectiveRow.IndexJ,
        objectiveRow.NodeId,
        originalText,
        translatedText);
  }

  /// <summary>
  ///     Builds the stable runtime key for a ToDoList row.
  /// </summary>
  /// <param name="progressKey">The quest progress key.</param>
  /// <param name="indexI">The outer row index.</param>
  /// <param name="indexJ">The inner row index.</param>
  /// <param name="nodeId">The backing node id.</param>
  /// <returns>The stable runtime key.</returns>
  private string BuildToDoRuntimeEntryKey(
      string progressKey,
      int indexI,
      int indexJ,
      uint nodeId)
  {
    var effectiveProgressKey = string.IsNullOrWhiteSpace(progressKey)
        ? "pending"
        : progressKey;
    return $"{ToDoListHoverPrefix}{effectiveProgressKey}-{indexI}-{indexJ}-{nodeId}";
  }

  /// <summary>
  ///     Resolves the original visible text for a ToDoList row, even if the
  ///     addon currently shows translated text from a previously applied mode.
  /// </summary>
  /// <param name="todoItem">The visible ToDoList row.</param>
  /// <returns>The original source text for that row.</returns>
  private string ResolveOriginalToDoText(ToDoItem todoItem)
  {
    var previousEntry = this.toDoRuntimeEntries.Values.FirstOrDefault(entry =>
        entry.IndexI == todoItem.IndexI &&
        entry.IndexJ == todoItem.IndexJ &&
        entry.NodeId == todoItem.NodeId);
    if (previousEntry != null &&
        !string.IsNullOrWhiteSpace(previousEntry.OriginalText))
    {
      return QuestAddonOriginalTextHelper.ResolveOriginalVisibleText(
          todoItem.Text,
          previousEntry.OriginalText,
          this.GetToDoTranslatedDisplayText(previousEntry.TranslatedText));
    }

    return todoItem.Text;
  }

  /// <summary>
  ///     Gets whether a visible objective row should be backed by canonical
  ///     TODO data.
  /// </summary>
  /// <param name="objectiveText">The visible objective text.</param>
  /// <returns><c>true</c> when the row should map to canonical TODO data.</returns>
  private bool ShouldTrackObjectiveRow(string objectiveText)
  {
    return !string.IsNullOrWhiteSpace(objectiveText) &&
           !string.Equals(objectiveText, EmptyObjective, StringComparison.Ordinal) &&
           !IsValidTimeFormat(objectiveText);
  }

  /// <summary>
  ///     Applies the current ToDoList presentation mode using only the local
  ///     runtime entries resolved from the DB.
  /// </summary>
  /// <param name="todoList">The live ToDoList addon.</param>
  private unsafe void ApplyToDoListPresentation(AtkUnitBase* todoList)
  {
    if (!this.currentToDoListDataReady)
    {
      this.RestoreToDoListOriginals(todoList);
      this.RemoveHoverTooltipsByPrefix(ToDoListHoverPrefix);
      this.lastAppliedDisplayMode = null;
      return;
    }

    foreach (var runtimeEntry in this.toDoRuntimeEntries.Values)
    {
      if (!TryGetLiveToDoTextNode(
              todoList,
              runtimeEntry.IndexI,
              runtimeEntry.IndexJ,
              out var textNode))
      {
        continue;
      }

      if (this.ToDoListWritesNativeTranslation)
      {
        var displayText = this.GetToDoTranslatedDisplayText(runtimeEntry.TranslatedText);
        textNode->SetText(displayText ?? string.Empty);
        this.toDoNativeMutationKeys.Add(runtimeEntry.Key);
      }
    }

    if (!this.ToDoListWritesNativeTranslation)
    {
      this.RestoreToDoListOriginals(todoList);
    }

    if (this.ToDoListUsesHoverTooltips)
    {
      this.RefreshToDoListHoverTargets(todoList);
    }
    else
    {
      this.RemoveHoverTooltipsByPrefix(ToDoListHoverPrefix);
    }

    this.lastAppliedDisplayMode = this.Config.ToDoListTranslationDisplayMode;
  }

  /// <summary>
  ///     Restores the original ToDoList text for all rows currently tracked in
  ///     the local runtime state.
  /// </summary>
  /// <param name="todoList">The live ToDoList addon.</param>
  private unsafe void RestoreToDoListOriginals(AtkUnitBase* todoList)
  {
    foreach (var runtimeEntry in this.toDoRuntimeEntries.Values)
    {
      if (!this.toDoNativeMutationKeys.Remove(runtimeEntry.Key))
      {
        continue;
      }

      if (!TryGetLiveToDoTextNode(
              todoList,
              runtimeEntry.IndexI,
              runtimeEntry.IndexJ,
              out var textNode))
      {
        continue;
      }

      textNode->SetText(runtimeEntry.OriginalText ?? string.Empty);
    }

    this.RemoveHoverTooltipsByPrefix(ToDoListHoverPrefix);
  }

  /// <summary>
  ///     Normalizes translated ToDoList text before it is written into the
  ///     native UI.
  /// </summary>
  /// <param name="translatedText">The translated text.</param>
  /// <returns>The translated text as it should be displayed natively.</returns>
  private string GetToDoTranslatedDisplayText(string translatedText)
  {
    if (!this.ToDoListShouldRemoveDiacritics)
    {
      return translatedText;
    }

    return this.NormalizeQuestText(translatedText ?? string.Empty);
  }

  /// <summary>
  ///     Refreshes hover targets for the currently visible ToDoList rows.
  /// </summary>
  /// <param name="todoList">The live ToDoList addon.</param>
  private unsafe void RefreshToDoListHoverTargets(AtkUnitBase* todoList)
  {
    if (!this.currentToDoListDataReady || !this.ToDoListUsesHoverTooltips)
    {
      this.RemoveHoverTooltipsByPrefix(ToDoListHoverPrefix);
      return;
    }

    foreach (var runtimeEntry in this.toDoRuntimeEntries.Values)
    {
      this.RegisterToDoTooltip(todoList, runtimeEntry);
    }
  }

  /// <summary>
  ///     Registers the hover tooltip for one resolved ToDoList runtime row.
  /// </summary>
  /// <param name="todoList">The live ToDoList addon.</param>
  /// <param name="runtimeEntry">The runtime entry to register.</param>
  private unsafe void RegisterToDoTooltip(
      AtkUnitBase* todoList,
      ToDoRuntimeEntry runtimeEntry)
  {
    if (!this.ToDoListUsesHoverTooltips)
    {
      return;
    }

    var translatedPayloadReady =
        !string.IsNullOrWhiteSpace(runtimeEntry.TranslatedText);
    if (!translatedPayloadReady)
    {
      this.RemoveHoverTooltipsByPrefix(runtimeEntry.Key);
      return;
    }

    if (this.TryGetToDoHoverBounds(
            todoList,
            runtimeEntry.IndexI,
            runtimeEntry.IndexJ,
            out var topLeft,
            out var bottomRight))
    {
      this.RegisterTranslatedHoverTooltip(
          runtimeEntry.Key,
          topLeft,
          bottomRight,
          runtimeEntry.OriginalText,
          runtimeEntry.TranslatedText,
          translatedPayloadReady,
          this.ToDoListHoverShowsOriginal,
          forceEnabled: true);
      return;
    }

    if (!TryGetLiveToDoTextNode(
            todoList,
            runtimeEntry.IndexI,
            runtimeEntry.IndexJ,
            out var textNode))
    {
      return;
    }

    this.RegisterTranslatedHoverTooltip(
        runtimeEntry.Key,
        textNode,
        runtimeEntry.OriginalText,
        runtimeEntry.TranslatedText,
        translatedPayloadReady,
        this.ToDoListHoverShowsOriginal,
        forceEnabled: true,
        denseHitbox: true);
  }

  /// <summary>
  ///     Tries to resolve the live ToDoList text node for one tracked row.
  /// </summary>
  /// <param name="todoList">The live ToDoList addon.</param>
  /// <param name="indexI">The outer row index.</param>
  /// <param name="indexJ">The inner text-node index.</param>
  /// <param name="textNode">The resolved text node.</param>
  /// <returns><c>true</c> when the live text node was resolved.</returns>
  private static unsafe bool TryGetLiveToDoTextNode(
      AtkUnitBase* todoList,
      int indexI,
      int indexJ,
      out AtkTextNode* textNode)
  {
    textNode = null;

    if (todoList == null ||
        indexI < 0 ||
        indexI >= todoList->UldManager.NodeListCount)
    {
      return false;
    }

    var rowNode = todoList->UldManager.NodeList[indexI];
    if (rowNode == null || !rowNode->IsVisible())
    {
      return false;
    }

    var rowComponentNode = rowNode->GetAsAtkComponentNode();
    if (rowComponentNode == null ||
        rowComponentNode->Component == null ||
        indexJ < 0 ||
        indexJ >= rowComponentNode->Component->UldManager.NodeListCount)
    {
      return false;
    }

    var childNode = rowComponentNode->Component->UldManager.NodeList[indexJ];
    if (childNode == null ||
        !childNode->IsVisible() ||
        childNode->Type != NodeType.Text)
    {
      return false;
    }

    textNode = childNode->GetAsAtkTextNode();
    return textNode != null;
  }

  /// <summary>
  ///     Tries to resolve a practical hover rectangle for a ToDoList row by
  ///     combining the full row node bounds with the inner text node bounds.
  /// </summary>
  /// <param name="todoList">The live ToDoList addon.</param>
  /// <param name="indexI">The outer row index.</param>
  /// <param name="indexJ">The inner text-node index.</param>
  /// <param name="topLeft">The resolved top-left screen coordinate.</param>
  /// <param name="bottomRight">The resolved bottom-right screen coordinate.</param>
  /// <returns>True when usable hover bounds were resolved.</returns>
  private unsafe bool TryGetToDoHoverBounds(
      AtkUnitBase* todoList,
      int indexI,
      int indexJ,
      out Vector2 topLeft,
      out Vector2 bottomRight)
  {
    topLeft = default;
    bottomRight = default;

    if (!TryGetLiveToDoTextNode(
            todoList,
            indexI,
            indexJ,
            out var textNode))
    {
      return false;
    }

    var rowNode = todoList->UldManager.NodeList[indexI];
    var left = Math.Min(rowNode->ScreenX, textNode->ScreenX);
    var top = Math.Min(rowNode->ScreenY, textNode->ScreenY);
    var right = Math.Max(
        rowNode->ScreenX + Math.Max(1f, rowNode->Width),
        textNode->ScreenX + Math.Max(1f, textNode->GetWidth()));
    var bottom = Math.Max(
        rowNode->ScreenY + Math.Max(1f, rowNode->Height),
        textNode->ScreenY + Math.Max(1f, textNode->GetHeight()));

    topLeft = new Vector2(
        Math.Max(0f, left - 16f),
        Math.Max(0f, top - 10f));
    bottomRight = new Vector2(
        right + 16f,
        bottom + 12f);
    return true;
  }

  /// <summary>
  ///     Emits a notification while the ToDoList is waiting for all visible
  ///     quest data to exist in the DB.
  /// </summary>
  /// <param name="blockingQuestLabels">The visible quest labels still waiting.</param>
  private void NotifyToDoListWaitingForQuestData(
      IReadOnlyCollection<string> blockingQuestLabels)
  {
    if (!this.Config.ShowQuestProgressNotifications)
    {
      return;
    }

    if (!this.toDoListWaitingNotificationGate.TryBeginWaiting(
            blockingQuestLabels.Count))
    {
      return;
    }

    NotificationManager.AddNotification(new Notification
    {
      Title = Resources.Name,
      Content = string.Format(
          CultureInfo.CurrentCulture,
          Resources.ToDoListAwaitingQuestDataNotification,
          blockingQuestLabels.Count),
      Icon = FontAwesomeIcon.Book.ToNotificationIcon(),
      Type = NotificationType.Info,
    });
  }

  /// <summary>
  ///     Clears the debounced ToDoList waiting-notification state.
  /// </summary>
  private void ClearToDoListWaitingState()
  {
    this.toDoListWaitingNotificationGate.Clear();
  }

  /// <summary>
  ///     Handles ToDoList update events.
  /// </summary>
  /// <param name="type">The lifecycle event.</param>
  /// <param name="args">The lifecycle arguments.</param>
  private unsafe void OnToDoListEvent(AddonEvent type, AddonArgs args)
  {
    this.RefreshToDoList();
  }

  /// <summary>
  ///     Handles ToDoList draw events so the addon can retry activation after
  ///     the background prefetch finishes and react immediately to mode
  ///     switches.
  /// </summary>
  /// <param name="type">The lifecycle event.</param>
  /// <param name="args">The lifecycle arguments.</param>
  private unsafe void OnToDoListPreDrawEvent(AddonEvent type, AddonArgs args)
  {
    if (!TryGetVisibleToDoList(out var todoList))
    {
      return;
    }

    if (!this.Config.TranslateToDoList ||
        this.DisableTranslationAccordingToState())
    {
      this.RestoreToDoListOriginals(todoList);
      this.RemoveHoverTooltipsByPrefix(ToDoListHoverPrefix);
      this.currentToDoListDataReady = false;
      this.lastAppliedDisplayMode = null;
      return;
    }

    if (!this.currentToDoListDataReady)
    {
      if (DateTime.UtcNow >= this.nextToDoListRetryUtc)
      {
        this.RefreshToDoList();
      }

      return;
    }

    if (this.lastAppliedDisplayMode != this.Config.ToDoListTranslationDisplayMode)
    {
      this.ApplyToDoListPresentation(todoList);
      return;
    }

    if (this.ToDoListUsesHoverTooltips)
    {
      this.RefreshToDoListHoverTargets(todoList);
    }
  }

  /// <summary>
  ///     Clears the ToDoList runtime state when the addon closes.
  /// </summary>
  /// <param name="type">The lifecycle event.</param>
  /// <param name="args">The lifecycle arguments.</param>
  private void OnToDoListCleanupEvent(AddonEvent type, AddonArgs args)
  {
    this.toDoRuntimeEntries.Clear();
    this.toDoNativeMutationKeys.Clear();
    this.RemoveHoverTooltipsByPrefix(ToDoListHoverPrefix);
    this.currentToDoListDataReady = false;
    this.lastAppliedDisplayMode = null;
    this.nextToDoListRetryUtc = DateTime.MinValue;
    this.ClearToDoListWaitingState();
  }

  /// <summary>
  ///     Represents one visible ToDoList quest row and the objective rows
  ///     grouped under it.
  /// </summary>
  /// <param name="QuestRow">The visible quest row.</param>
  /// <param name="Objectives">The grouped objective rows.</param>
  private sealed record ToDoVisibleQuest(
      ToDoItem QuestRow,
      IReadOnlyList<ToDoItem> Objectives);

  /// <summary>
  ///     Represents one resolved ToDoList runtime row.
  /// </summary>
  /// <param name="Key">The stable runtime key.</param>
  /// <param name="ProgressKey">The stable quest progress key.</param>
  /// <param name="IndexI">The outer row index.</param>
  /// <param name="IndexJ">The inner row index.</param>
  /// <param name="NodeId">The backing node id.</param>
  /// <param name="OriginalText">The original source text.</param>
  /// <param name="TranslatedText">The translated text from the DB.</param>
  private sealed record ToDoRuntimeEntry(
      string Key,
      string ProgressKey,
      int IndexI,
      int IndexJ,
      uint NodeId,
      string OriginalText,
      string TranslatedText);
}
