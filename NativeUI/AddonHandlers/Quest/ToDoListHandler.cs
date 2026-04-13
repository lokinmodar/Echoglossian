// <copyright file="ToDoListHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Handles the ToDoList quest addon runtime inside the standalone
///     quest-handler model.
/// </summary>
internal sealed class ToDoListHandler : QuestAddonHandlerBase
{
  private const string EmptyObjective = "???";

  private const string ToDoListAddonName = "_ToDoList";

  private const string ToDoListHoverPrefix = "ToDoList-";

  private readonly Dictionary<string, ToDoHoverEntry> toDoHoverEntries = [];

  /// <summary>
  ///     Initializes a new instance of the <see cref="ToDoListHandler" /> class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public ToDoListHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PostRequestedUpdate, this.OnToDoListEvent);
    this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.OnToDoListEvent);
    this.RegisterHandler(AddonEvent.PreDraw, this.OnToDoListHoverRefreshEvent);
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
  ///     Registers a hover tooltip for a specific ToDoList text node.
  /// </summary>
  /// <param name="todoList">The live addon window.</param>
  /// <param name="indexI">The outer node index.</param>
  /// <param name="indexJ">The inner node index.</param>
  /// <param name="nodeId">The backing node identifier.</param>
  /// <param name="originalText">The original visible text.</param>
  /// <param name="translatedText">The translated text.</param>
  /// <param name="progressKey">Optional stable quest-progress key.</param>
  private unsafe void RegisterToDoTooltip(
      AtkUnitBase* todoList,
      int indexI,
      int indexJ,
      uint nodeId,
      string originalText,
      string translatedText,
      string? progressKey = null)
  {
    if (!this.ToDoListUsesHoverTooltips)
    {
      return;
    }

    var hoverKey = progressKey == null
        ? $"ToDoList-{indexI}-{indexJ}-{nodeId}"
        : $"ToDoList-{progressKey}-{indexI}-{indexJ}-{nodeId}";

    this.RememberToDoHoverEntry(
        hoverKey,
        indexI,
        indexJ,
        nodeId,
        originalText,
        translatedText);

    if (this.TryGetToDoHoverBounds(
            todoList,
            indexI,
            indexJ,
            out var topLeft,
            out var bottomRight))
    {
      this.RegisterTranslatedHoverTooltip(
          hoverKey,
          topLeft,
          bottomRight,
          originalText,
          translatedText,
          swapEnabled: this.ToDoListHoverShowsOriginal,
          forceEnabled: true);
      return;
    }

    var textNode = todoList->UldManager.NodeList[indexI]
        ->GetAsAtkComponentNode()->Component->UldManager.NodeList[indexJ]
        ->GetAsAtkTextNode();
    this.RegisterTranslatedHoverTooltip(
        hoverKey,
        textNode,
        originalText,
        translatedText,
        swapEnabled: this.ToDoListHoverShowsOriginal,
        forceEnabled: true,
        denseHitbox: true);
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
    if (childNode == null || !childNode->IsVisible() || childNode->Type != NodeType.Text)
    {
      return false;
    }

    var textNode = childNode->GetAsAtkTextNode();
    if (textNode == null)
    {
      return false;
    }

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
  ///     Remembers the latest tooltip payload for one ToDoList row so it can
  ///     be refreshed during draw without recomputing translations.
  /// </summary>
  /// <param name="key">The stable hover key.</param>
  /// <param name="indexI">The outer row index.</param>
  /// <param name="indexJ">The inner text-node index.</param>
  /// <param name="nodeId">The backing node identifier.</param>
  /// <param name="originalText">The current original text.</param>
  /// <param name="translatedText">The current translated text.</param>
  private void RememberToDoHoverEntry(
      string key,
      int indexI,
      int indexJ,
      uint nodeId,
      string originalText,
      string translatedText)
  {
    this.toDoHoverEntries[key] = new ToDoHoverEntry(
        key,
        indexI,
        indexJ,
        nodeId,
        originalText ?? string.Empty,
        translatedText ?? string.Empty);
  }

  /// <summary>
  ///     Scans the live ToDoList addon and queues translations for visible quest
  ///     rows.
  /// </summary>
  private unsafe void TranslateToDoList()
  {
    if (!this.Config.TranslateToDoList)
    {
      return;
    }

    var atkStage = AtkStage.Instance();
    var todoList = atkStage->RaptureAtkUnitManager->GetAddonByName(
        ToDoListAddonName);
    if (todoList == null || !todoList->IsVisible)
    {
      return;
    }

    List<ToDoItem> questNamesToTranslate = [];
    List<ToDoItem> objectivesToTranslate = [];
    List<ToDoItem> levelQuestObjectivesToTranslate = [];
    for (var i = 0; i < todoList->UldManager.NodeListCount; i++)
    {
      if (!todoList->UldManager.NodeList[i]->IsVisible())
      {
        continue;
      }

      if (todoList->UldManager.NodeList[i]->Type == NodeType.Collision ||
          todoList->UldManager.NodeList[i]->Type == NodeType.Res)
      {
        continue;
      }

      var nodeID = todoList->UldManager.NodeList[i]->NodeId;

      if (nodeID == 8 || nodeID == 9)
      {
        continue;
      }

      var component = todoList->UldManager.NodeList[i]->GetAsAtkComponentNode();
      for (var j = 0;
           j < component->Component->UldManager.NodeListCount;
           j++)
      {
        if (!component->Component->UldManager.NodeList[j]->IsVisible())
        {
          continue;
        }

        if (component->Component->UldManager.NodeList[j]->Type != NodeType.Text)
        {
          continue;
        }

        var childrenNodeID = component->Component->UldManager.NodeList[j]->NodeId;
        var originalStep = component->Component->UldManager.NodeList[j]
            ->GetAsAtkTextNode()->NodeText;
        if (originalStep.IsEmpty)
        {
          continue;
        }

        if (IsValidTimeFormat(
                MemoryHelper.ReadSeStringAsString(
                    out _,
                    (nint)originalStep.StringPtr.Value)))
        {
          continue;
        }

        if (nodeID == 4 && childrenNodeID == 8)
        {
          continue;
        }

        var originalStepText = MemoryHelper.ReadSeStringAsString(
            out _,
            (nint)originalStep.StringPtr.Value);

        if (nodeID > 60000 || (nodeID == 4 && childrenNodeID == 3) ||
            (nodeID == 6 && childrenNodeID == 2))
        {
          questNamesToTranslate.Add(
              new ToDoItem(
                  originalStepText,
                  i,
                  j,
                  nodeID));
        }
        else if (nodeID == 4 || nodeID == 5)
        {
          levelQuestObjectivesToTranslate.Add(
              new ToDoItem(
                  originalStepText,
                  i,
                  j,
                  nodeID));
        }
        else
        {
          objectivesToTranslate.Add(
              new ToDoItem(
                  originalStepText,
                  i,
                  j,
                  nodeID));
        }
      }
    }

    if (questNamesToTranslate.Count == 0)
    {
      return;
    }

    objectivesToTranslate.Reverse();
    this.TranslateTodoItems(
        questNamesToTranslate,
        objectivesToTranslate,
        levelQuestObjectivesToTranslate,
        todoList);
  }

  /// <summary>
  ///     Gets the contiguous objective block that belongs to a quest row.
  /// </summary>
  /// <param name="currentObjectiveNode">The current node identifier.</param>
  /// <param name="objectiveIndex">The current objective index.</param>
  /// <param name="objectivesToTranslate">The full objective list.</param>
  /// <param name="questObjectives">The objectives collected for this quest.</param>
  /// <returns>The collected quest objectives.</returns>
  private List<ToDoItem> GetQuestObjectives(
      uint currentObjectiveNode,
      int objectiveIndex,
      List<ToDoItem> objectivesToTranslate,
      List<ToDoItem> questObjectives)
  {
    var currentIndex = objectiveIndex + 1;
    if (currentIndex >= objectivesToTranslate.Count)
    {
      return questObjectives;
    }

    var objective = objectivesToTranslate[currentIndex];

    if (Math.Abs(currentObjectiveNode - objective.NodeId) > 1)
    {
      return questObjectives;
    }

    questObjectives.Add(objective);
    return this.GetQuestObjectives(
        objective.NodeId,
        currentIndex,
        objectivesToTranslate,
        questObjectives);
  }

  /// <summary>
  ///     Applies translations to quest names and objectives for the current
  ///     ToDoList scan.
  /// </summary>
  /// <param name="questNamesToTranslate">The quest name rows.</param>
  /// <param name="objectivesToTranslate">The normal objective rows.</param>
  /// <param name="levelQuestObjectivesToTranslate">The level-quest objective rows.</param>
  /// <param name="todoList">The live addon window.</param>
  private unsafe void TranslateTodoItems(
      List<ToDoItem> questNamesToTranslate,
      List<ToDoItem> objectivesToTranslate,
      List<ToDoItem> levelQuestObjectivesToTranslate,
      AtkUnitBase* todoList)
  {
    try
    {
      var objectiveIndex = 0;
      foreach (var quest in questNamesToTranslate)
      {
        List<ToDoItem> objectives = [];
        if (objectiveIndex < objectivesToTranslate.Count)
        {
          var currentObjective = objectivesToTranslate[objectiveIndex];
          objectives.Add(currentObjective);
          objectives = this.GetQuestObjectives(
              currentObjective.NodeId,
              objectiveIndex,
              objectivesToTranslate,
              objectives);
        }

        objectiveIndex += objectives.Count;
        if (quest.NodeId == 4)
        {
          objectives.AddRange(levelQuestObjectivesToTranslate);
        }

        QuestTodoProgressSnapshot? questTodoProgressSnapshot = null;
        if (QuestTodoProgressResolver.TryResolveQuestTodoProgress(
                quest.Text,
                out var resolvedTodoProgressSnapshot))
        {
          questTodoProgressSnapshot = resolvedTodoProgressSnapshot;
        }

        var questTodoProgressKey = questTodoProgressSnapshot?.CacheKey ?? quest.Text;

        var effectiveQuestName = quest.Text;
        if (questTodoProgressSnapshot == null &&
            QuestUiTranslationCache.TryGetAppliedSnapshot(
                quest.Text,
                out var reverseNameSnapshot) &&
            !string.Equals(
                reverseNameSnapshot.OriginalText,
                quest.Text,
                StringComparison.Ordinal))
        {
          effectiveQuestName = reverseNameSnapshot.OriginalText;
          if (QuestTodoProgressResolver.TryResolveQuestTodoProgress(
                  effectiveQuestName,
                  out var recoveredTodoSnapshot))
          {
            questTodoProgressSnapshot = recoveredTodoSnapshot;
            questTodoProgressKey = recoveredTodoSnapshot.CacheKey;
          }
        }

        var questPlate = this.CreateQuestPlate(
            effectiveQuestName,
            string.Empty);
        var foundQuestPlate = this.FindQuestPlateByName(questPlate);

        if (QuestUiTranslationCache.TryGetAppliedSnapshot(
                questTodoProgressKey,
                out _) &&
            foundQuestPlate == null)
        {
          continue;
        }

        if (foundQuestPlate != null)
        {
          var foundTranslatedQuestName = foundQuestPlate.TranslatedQuestName;
          if (this.ToDoListShouldRemoveDiacritics)
          {
            foundTranslatedQuestName = this.NormalizeQuestText(
                foundTranslatedQuestName ?? string.Empty);
          }

          if (this.ToDoListWritesNativeTranslation)
          {
            todoList->UldManager.NodeList[quest.IndexI]->
                    GetAsAtkComponentNode()->Component->UldManager
                .NodeList[quest.IndexJ]->GetAsAtkTextNode()
                ->SetText(foundTranslatedQuestName);
          }

          this.RegisterToDoTooltip(
              todoList,
              quest.IndexI,
              quest.IndexJ,
              quest.NodeId,
              quest.Text,
              foundTranslatedQuestName,
              questTodoProgressKey);

          var snapshotSteps = questTodoProgressSnapshot?.QuestProgress.QuestSteps;
          var objIdx = 0;
          List<string> translatedStoredObjectives = [];
          var hasPendingFoundObjectives = false;
          foreach (var objective in objectives)
          {
            var stepText = snapshotSteps != null &&
                           objIdx < snapshotSteps.Count
                ? snapshotSteps[objIdx].Text
                : objective.Text;
            var stepKey = snapshotSteps != null &&
                          objIdx < snapshotSteps.Count
                ? snapshotSteps[objIdx].KeyText
                : objective.Text;
            objIdx++;

            if (objective.Text == EmptyObjective)
            {
              translatedStoredObjectives.Add(EmptyObjective);
              continue;
            }

            if (IsValidTimeFormat(objective.Text))
            {
              continue;
            }

            string? storedObjectiveText = null;
            if (foundQuestPlate.TranslatedObjectives.TryGetValue(
                    stepKey,
                    out var byKey))
            {
              storedObjectiveText = byKey;
            }
            else if (foundQuestPlate.Objectives.TryGetValue(
                         stepText,
                         out var byLegacy))
            {
              storedObjectiveText = byLegacy;
            }

            if (storedObjectiveText != null)
            {
              translatedStoredObjectives.Add(storedObjectiveText);

              if (this.ToDoListShouldRemoveDiacritics)
              {
                storedObjectiveText = this.NormalizeQuestText(
                    storedObjectiveText ?? string.Empty);
              }

              if (this.ToDoListWritesNativeTranslation)
              {
                todoList->UldManager.NodeList[objective.IndexI]
                        ->GetAsAtkComponentNode()->Component->UldManager
                    .NodeList[objective.IndexJ]->GetAsAtkTextNode()
                    ->SetText(storedObjectiveText);
              }
              this.RegisterToDoTooltip(
                  todoList,
                  objective.IndexI,
                  objective.IndexJ,
                  objective.NodeId,
                  objective.Text,
                  storedObjectiveText,
                  questTodoProgressKey);
              continue;
            }

            var objectiveCacheKey =
                $"ToDoListObjective|{questTodoProgressKey}|{stepKey}";
            if (!this.TryGetQueuedTranslation(
                    objectiveCacheKey,
                    out var translatedQuestObjective))
            {
              this.QueueTranslation(
                  objectiveCacheKey,
                  () => this.Translate(stepText),
                  translatedObjectiveText =>
                  {
                    var questPlateToUpdate = foundQuestPlate.Clone();
                    questPlateToUpdate.TranslatedObjectives[stepKey] =
                        translatedObjectiveText;
                    questPlateToUpdate.Objectives[stepText] = translatedObjectiveText;
                    questPlateToUpdate.UpdatedDate = DateTime.Now;
                    this.UpdateQuestPlate(questPlateToUpdate);
                  });
              hasPendingFoundObjectives = true;
              continue;
            }

            foundQuestPlate.TranslatedObjectives.TryAdd(
                stepKey,
                translatedQuestObjective);
            foundQuestPlate.Objectives.TryAdd(
                stepText,
                translatedQuestObjective);
            translatedStoredObjectives.Add(translatedQuestObjective);
            this.UpdateQuestPlate(foundQuestPlate);

            if (this.ToDoListShouldRemoveDiacritics)
            {
              translatedQuestObjective = this.NormalizeQuestText(
                  translatedQuestObjective ?? string.Empty);
            }

            if (this.ToDoListWritesNativeTranslation)
            {
              todoList->UldManager.NodeList[objective.IndexI]
                      ->GetAsAtkComponentNode()->Component->UldManager
                  .NodeList[objective.IndexJ]->GetAsAtkTextNode()
                  ->SetText(translatedQuestObjective);
            }
            this.RegisterToDoTooltip(
                todoList,
                objective.IndexI,
                objective.IndexJ,
                objective.NodeId,
                objective.Text,
                translatedQuestObjective,
                questTodoProgressKey);
          }

          if (hasPendingFoundObjectives)
          {
            continue;
          }

          QuestUiTranslationCache.Remember(
              questTodoProgressKey,
              questTodoProgressKey);

          continue;
        }

        var questNameCacheKey =
            $"ToDoListQuest|{questTodoProgressKey}|{effectiveQuestName}";
        if (!this.TryGetQueuedTranslation(
                questNameCacheKey,
                out var translatedNameText))
        {
          this.QueueTranslation(
              questNameCacheKey,
              () => this.Translate(effectiveQuestName),
              translatedQuestName =>
              {
                var translatedQuestPlate = this.CreateTranslatedQuestPlate(
                    effectiveQuestName,
                    string.Empty,
                    translatedQuestName,
                    string.Empty,
                    string.Empty);

                this.InsertQuestPlate(translatedQuestPlate);
              });
          continue;
        }

        var storedTranslatedNameText = translatedNameText;
        var translatedQuestPlate = this.CreateTranslatedQuestPlate(
            effectiveQuestName,
            string.Empty,
            storedTranslatedNameText,
            string.Empty,
            string.Empty);

        if (this.ToDoListShouldRemoveDiacritics)
        {
          translatedNameText = this.NormalizeQuestText(
              translatedNameText ?? string.Empty);
        }

        if (this.ToDoListWritesNativeTranslation)
        {
          todoList->UldManager.NodeList[quest.IndexI]->
                  GetAsAtkComponentNode()->Component->UldManager
              .NodeList[quest.IndexJ]->GetAsAtkTextNode()->SetText(
                  translatedNameText);
        }
        this.RegisterToDoTooltip(
            todoList,
            quest.IndexI,
            quest.IndexJ,
            quest.NodeId,
            quest.Text,
            translatedNameText,
            questTodoProgressKey);

        QuestUiTranslationCache.Remember(
            effectiveQuestName,
            storedTranslatedNameText);

        var newSnapshotSteps = questTodoProgressSnapshot?.QuestProgress.QuestSteps;
        var newObjIdx = 0;
        List<string> translatedObjectives = [];
        var hasPendingNewObjectives = false;
        foreach (var objective in objectives)
        {
          var newStepText = newSnapshotSteps != null &&
                            newObjIdx < newSnapshotSteps.Count
              ? newSnapshotSteps[newObjIdx].Text
              : objective.Text;
          var newStepKey = newSnapshotSteps != null &&
                           newObjIdx < newSnapshotSteps.Count
              ? newSnapshotSteps[newObjIdx].KeyText
              : objective.Text;
          newObjIdx++;

          if (objective.Text == EmptyObjective)
          {
            translatedObjectives.Add(EmptyObjective);
            continue;
          }

          if (IsValidTimeFormat(objective.Text))
          {
            continue;
          }

          var objectiveCacheKey =
              $"ToDoListObjective|{questTodoProgressKey}|{newStepKey}";
          if (!this.TryGetQueuedTranslation(
                  objectiveCacheKey,
                  out var translatedObjectiveText))
          {
            this.QueueTranslation(
                objectiveCacheKey,
                () => this.Translate(newStepText),
                translatedQuestObjective =>
                {
                  var existingQuestPlate = this.FindQuestPlateByName(
                      this.CreateQuestPlate(
                          effectiveQuestName,
                          string.Empty));
                  if (existingQuestPlate == null)
                  {
                    existingQuestPlate = this.CreateTranslatedQuestPlate(
                        effectiveQuestName,
                        string.Empty,
                        storedTranslatedNameText,
                        string.Empty,
                        string.Empty);
                  }

                  existingQuestPlate.TranslatedObjectives.TryAdd(
                      newStepKey,
                      translatedQuestObjective);
                  existingQuestPlate.Objectives.TryAdd(
                      newStepText,
                      translatedQuestObjective);
                  if (existingQuestPlate.Id == 0)
                  {
                    this.InsertQuestPlate(existingQuestPlate);
                  }
                  else
                  {
                    this.UpdateQuestPlate(existingQuestPlate);
                  }
                });
            hasPendingNewObjectives = true;
            continue;
          }

          translatedObjectives.Add(translatedObjectiveText);
          translatedQuestPlate.TranslatedObjectives.TryAdd(
              newStepKey,
              translatedObjectiveText);
          translatedQuestPlate.Objectives.TryAdd(
              newStepText,
              translatedObjectiveText);

          if (this.ToDoListShouldRemoveDiacritics)
          {
            translatedObjectiveText = this.NormalizeQuestText(
                translatedObjectiveText ?? string.Empty);
          }

          if (this.ToDoListWritesNativeTranslation)
          {
            todoList->UldManager.NodeList[objective.IndexI]
                    ->GetAsAtkComponentNode()->Component->UldManager
                .NodeList[objective.IndexJ]->GetAsAtkTextNode()
                ->SetText(translatedObjectiveText);
          }
          this.RegisterToDoTooltip(
              todoList,
              objective.IndexI,
              objective.IndexJ,
              objective.NodeId,
              objective.Text,
              translatedObjectiveText,
              questTodoProgressKey);
        }

        this.InsertQuestPlate(translatedQuestPlate);

        if (hasPendingNewObjectives)
        {
          continue;
        }

        QuestUiTranslationCache.Remember(
            questTodoProgressKey,
            questTodoProgressKey);
      }
    }
    catch (Exception e)
    {
      PluginLog.Error("Error translating todo items:", e);
    }
  }

  /// <summary>
  ///     Handles ToDoList refresh events.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnToDoListEvent(AddonEvent type, AddonArgs args)
  {
    if (this.DisableTranslationAccordingToState())
    {
      return;
    }

    this.TranslateToDoList();
  }

  /// <summary>
  ///     Refreshes ToDoList hover targets every draw using the most recently
  ///     resolved row payloads without queueing new translations.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnToDoListHoverRefreshEvent(AddonEvent type, AddonArgs args)
  {
    if (!this.Config.TranslateToDoList || !this.ToDoListUsesHoverTooltips)
    {
      return;
    }

    if (this.toDoHoverEntries.Count == 0)
    {
      return;
    }

    var todoList = AtkStage.Instance()->RaptureAtkUnitManager->GetAddonByName(
        ToDoListAddonName);
    if (todoList == null || !todoList->IsVisible)
    {
      return;
    }

    foreach (var hoverEntry in this.toDoHoverEntries.Values.ToList())
    {
      if (!this.TryGetToDoHoverBounds(
              todoList,
              hoverEntry.IndexI,
              hoverEntry.IndexJ,
              out var topLeft,
              out var bottomRight))
      {
        continue;
      }

      this.RegisterTranslatedHoverTooltip(
          hoverEntry.Key,
          topLeft,
          bottomRight,
          hoverEntry.OriginalText,
          hoverEntry.TranslatedText,
          swapEnabled: this.ToDoListHoverShowsOriginal,
          forceEnabled: true);
    }
  }

  /// <summary>
  ///     Clears ToDoList hover registrations when the addon closes.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private void OnToDoListCleanupEvent(AddonEvent type, AddonArgs args)
  {
    if (string.Equals(args.AddonName, ToDoListAddonName, StringComparison.Ordinal))
    {
      this.toDoHoverEntries.Clear();
      this.RemoveHoverTooltipsByPrefix(ToDoListHoverPrefix);
    }
  }

  /// <summary>
  ///     Captures the latest tooltip payload for one visible ToDoList row.
  /// </summary>
  /// <param name="Key">The stable hover key.</param>
  /// <param name="IndexI">The outer row index.</param>
  /// <param name="IndexJ">The inner text-node index.</param>
  /// <param name="NodeId">The backing node identifier.</param>
  /// <param name="OriginalText">The current original text.</param>
  /// <param name="TranslatedText">The current translated text.</param>
  private sealed record ToDoHoverEntry(
      string Key,
      int IndexI,
      int IndexJ,
      uint NodeId,
      string OriginalText,
      string TranslatedText);
}
