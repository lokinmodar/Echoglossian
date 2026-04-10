// <copyright file="UiToDoListHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;

namespace Echoglossian;

public partial class Echoglossian
{
    private const string EmptyObjective = "???";

    private unsafe void RegisterToDoTooltip(
        AtkUnitBase* todoList,
        int indexI,
        int indexJ,
        uint nodeId,
        string originalText,
        string translatedText,
        string? progressKey = null)
    {
        PluginLog.Debug(
            $"[ToDoList] tooltip candidate key='{(progressKey == null ? $"ToDoList-{indexI}-{indexJ}-{nodeId}" : $"ToDoList-{progressKey}-{indexI}-{indexJ}-{nodeId}")}' " +
            $"index=({indexI},{indexJ}) nodeId={nodeId} progressKey='{progressKey ?? string.Empty}' " +
            $"mode={this.configuration.ToDoListTranslationDisplayMode} hover={this.ToDoListUsesHoverTooltips} " +
            $"native={this.ToDoListWritesNativeTranslation} swap={this.ToDoListHoverShowsOriginal}");

        if (!this.ToDoListUsesHoverTooltips)
        {
            return;
        }

        var textNode = todoList->UldManager.NodeList[indexI]
            ->GetAsAtkComponentNode()->Component->UldManager.NodeList[indexJ]
            ->GetAsAtkTextNode();
        this.RegisterTranslatedHoverTooltip(
            progressKey == null
                ? $"ToDoList-{indexI}-{indexJ}-{nodeId}-{(nint)textNode:X}"
                : $"ToDoList-{progressKey}-{indexI}-{indexJ}-{nodeId}-{(nint)textNode:X}",
            textNode,
            originalText,
            translatedText,
            swapEnabled: this.ToDoListHoverShowsOriginal,
            forceEnabled: true, denseHitbox: true);
    }

    private unsafe void TranslateToDoList()
    {
        if (!this.configuration.TranslateToDoList)
        {
            return;
        }

        var atkStage = AtkStage.Instance();
        var todoList =
            atkStage->RaptureAtkUnitManager->GetAddonByName("_ToDoList");
        if (todoList == null || !todoList->IsVisible)
        {
            return;
        }

        PluginLog.Debug(
            $"[ToDoList] scan start mode={this.configuration.ToDoListTranslationDisplayMode} hover={this.ToDoListUsesHoverTooltips} " +
            $"native={this.ToDoListWritesNativeTranslation} swap={this.ToDoListHoverShowsOriginal} nodeCount={todoList->UldManager.NodeListCount}");

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

            // don't translate unneeded fate information
            if (nodeID == 8 || nodeID == 9)
            {
                continue;
            }

            var component =
                todoList->UldManager.NodeList[i]->GetAsAtkComponentNode();
            for (var j = 0;
                 j < component->Component->UldManager.NodeListCount;
                 j++)
            {
                if (!component->Component->UldManager.NodeList[j]->IsVisible())
                {
                    continue;
                }

                if (component->Component->UldManager.NodeList[j]->Type !=
                    NodeType.Text)
                {
                    continue;
                }

                var childrenNodeID =
                    component->Component->UldManager.NodeList[j]->NodeId;
                var originalStep =
                    component->Component->UldManager.NodeList[j]->
                        GetAsAtkTextNode()->NodeText;
                if (originalStep.IsEmpty)
                {
                    continue;
                }

                if (IsValidTimeFormat(
                        MemoryHelper.ReadSeStringAsString(
                            out _,
                            (nint)originalStep.StringPtr.Value)))
                {
                    // skip text if time format
#if DEBUG
                    // PluginLog.Debug("Skipping time format translation");
#endif
                    continue;
                }

                var originalStepText = MemoryHelper.ReadSeStringAsString(
                    out _,
                    (nint)originalStep.StringPtr.Value);
                if (this.ToDoListUsesHoverTooltips)
                {
                    this.RegisterToDoTooltip(
                        todoList,
                        i,
                        j,
                        nodeID,
                        originalStepText,
                        originalStepText);
                }

                // don't translate unneeded levelquest information
                if (nodeID == 4 && childrenNodeID == 8)
                {
                    continue;
                }

                if (nodeID > 60000 || (nodeID == 4 && childrenNodeID == 3) ||
                    (nodeID == 6 && childrenNodeID == 2))
                {
                    questNamesToTranslate.Add(
                        new ToDoItem(
                            MemoryHelper.ReadSeStringAsString(
                                out _,
                                (nint)originalStep.StringPtr.Value),
                            i,
                            j,
                            nodeID));
                }
                else
                {
                    if (nodeID == 4 || nodeID == 5)
                    {
                        levelQuestObjectivesToTranslate.Add(
                            new ToDoItem(
                                MemoryHelper.ReadSeStringAsString(
                                    out _,
                                    (nint)originalStep.StringPtr.Value),
                                i,
                                j,
                                nodeID));
                    }
                    else
                    {
                        objectivesToTranslate.Add(
                            new ToDoItem(
                                MemoryHelper.ReadSeStringAsString(
                                    out _,
                                    (nint)originalStep.StringPtr.Value),
                                i,
                                j,
                                nodeID));
                    }
                }
            }
        }

        if (questNamesToTranslate.Count == 0)
        {
            PluginLog.Debug(
                $"[ToDoList] scan result questNames=0 objectives={objectivesToTranslate.Count} levelQuestObjectives={levelQuestObjectivesToTranslate.Count}");
            return;
        }

        objectivesToTranslate.Reverse();

        PluginLog.Debug(
            $"[ToDoList] scan result questNames={questNamesToTranslate.Count} objectives={objectivesToTranslate.Count} " +
            $"levelQuestObjectives={levelQuestObjectivesToTranslate.Count}");

        this.TranslateTodoItems(
            questNamesToTranslate,
            objectivesToTranslate,
            levelQuestObjectivesToTranslate,
            todoList);
    }

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

        // objectives of the same quest use adjacent node ids
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
                List<ToDoItem> objectives = new();
                if (objectiveIndex < objectivesToTranslate.Count)
                {
                    var currentObjective =
                        objectivesToTranslate[objectiveIndex];
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

                var questPlate = this.FormatQuestPlate(
                    quest.Text,
                    string.Empty);
                var questTodoProgressSnapshot = default(
                    QuestTodoProgressSnapshot?);
                if (QuestTodoProgressResolver.TryResolveQuestTodoProgress(
                        quest.Text,
                        out var resolvedTodoProgressSnapshot))
                {
                    questTodoProgressSnapshot = resolvedTodoProgressSnapshot;
                }

                var questTodoProgressKey =
                    questTodoProgressSnapshot?.CacheKey ?? quest.Text;

                // because sometimes the quest name translation is the same as the original name but the objectives are not
                var questWithObjectives =
                    $"{questTodoProgressKey}|{quest.Text}|{string.Join(",", objectives)}";
                if (QuestUiTranslationCache.TryGetAppliedSnapshot(
                        Sanitizer.Sanitize(questWithObjectives),
                        out _))
                {
                    continue;
                }

                var foundQuestPlate = this.FindQuestPlateByName(questPlate);
                if (foundQuestPlate != null)
                {
#if DEBUG
                    // PluginLog.Debug(
                    //     $"Name from database: {quest.Text} -> {foundQuestPlate.TranslatedQuestName}");
#endif

                    var foundTranslatedQuestName =
                        foundQuestPlate.TranslatedQuestName;
                    if (this.configuration
                        .RemoveDiacriticsWhenUsingReplacementQuest)
                    {
                        foundTranslatedQuestName = this.RemoveDiacritics(
                            foundTranslatedQuestName,
                            this.SpecialCharsSupportedByGameFont);
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

                    List<string> translatedStoredObjectives = new();
                    var hasPendingFoundObjectives = false;
                    foreach (var objective in objectives)
                    {
                        if (objective.Text == EmptyObjective)
                        {
                            translatedStoredObjectives.Add(EmptyObjective);

                            // let's not store empty objectives on the database
                            continue;
                        }

                        if (IsValidTimeFormat(objective.Text))
                        {
                            // PluginLog.Debug("Skipping time format translation");
                            continue;
                        }

                        if (foundQuestPlate.Objectives.TryGetValue(
                                objective.Text,
                                out var storedObjectiveText))
                        {
#if DEBUG
                            // PluginLog.Debug(
                            //     $"Objective from database: {objective.Text} {storedObjectiveText}");
#endif
                            translatedStoredObjectives.Add(storedObjectiveText);

                            if (this.configuration
                                .RemoveDiacriticsWhenUsingReplacementQuest)
                            {
                                storedObjectiveText = this.RemoveDiacritics(
                                    storedObjectiveText,
                                    this.SpecialCharsSupportedByGameFont);
                            }

                            if (this.ToDoListWritesNativeTranslation)
                            {
                                todoList->UldManager.NodeList[
                                            objective.IndexI]->
                                        GetAsAtkComponentNode()->Component->
                                    UldManager
                                    .NodeList[objective.IndexJ]->
                                    GetAsAtkTextNode()->SetText(
                                        storedObjectiveText);
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
                            $"ToDoListObjective|{questTodoProgressKey}|{objective.Text}";
                        if (!this.TryGetQueuedTranslation(
                                objectiveCacheKey,
                                out var translatedQuestObjective))
                        {
                            this.QueueTranslation(
                                objectiveCacheKey,
                                () => this.Translate(objective.Text),
                                translatedObjectiveText =>
                                {
                                    var questPlateToUpdate =
                                        foundQuestPlate.Clone();
                                    questPlateToUpdate.Objectives[
                                        objective.Text] =
                                        translatedObjectiveText;
                                    questPlateToUpdate.UpdatedDate =
                                        DateTime.Now;
                                    this.UpdateQuestPlate(
                                        questPlateToUpdate);
                                });
                            hasPendingFoundObjectives = true;
                            continue;
                        }
                        foundQuestPlate.Objectives.TryAdd(
                            objective.Text,
                            translatedQuestObjective);
                        translatedStoredObjectives.Add(
                            translatedQuestObjective);
                        // PluginLog.Debug(
                        //     $"Objective translated: {objective.Text} {translatedQuestObjective}");
                        var resultUpdate =
                            this.UpdateQuestPlate(foundQuestPlate);
#if DEBUG
                        // PluginLog.Debug(
                        //     $"Using QuestPlate Replace - QuestPlate DB Update operation result: {resultUpdate}");
#endif
                        if (this.configuration
                            .RemoveDiacriticsWhenUsingReplacementQuest)
                        {
                            translatedQuestObjective = this.RemoveDiacritics(
                                translatedQuestObjective,
                                this.SpecialCharsSupportedByGameFont);
                        }

                        todoList->UldManager.NodeList[objective.IndexI]->
                                    GetAsAtkComponentNode()->Component->
                                UldManager
                                .NodeList[objective.IndexJ]->GetAsAtkTextNode()
                            ->
                            SetText(translatedQuestObjective);
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

                    // because sometimes the quest name translation is the same as the original name but the objectives are not
                    var translatedStoredQuestWithObjectives =
                        foundQuestPlate.TranslatedQuestName +
                        string.Join<string>(",", translatedStoredObjectives);
                    QuestUiTranslationCache.Remember(
                        $"{questTodoProgressKey}|{quest.Text}",
                        Sanitizer.Sanitize(translatedStoredQuestWithObjectives));

                    continue;
                }

                var questNameCacheKey =
                    $"ToDoListQuest|{questTodoProgressKey}|{quest.Text}";
                if (!this.TryGetQueuedTranslation(
                        questNameCacheKey,
                        out var translatedNameText))
                {
                    this.QueueTranslation(
                        questNameCacheKey,
                        () => this.Translate(quest.Text),
                        translatedQuestName =>
                        {
                            var translatedQuestPlate = new QuestPlate(
                                quest.Text,
                                string.Empty,
                                ClientStateInterface.ClientLanguage.Humanize(),
                                translatedQuestName,
                                string.Empty,
                                string.Empty,
                                LangDict[LanguageInt].Code,
                                this.configuration.ChosenTransEngine,
                                DateTime.Now,
                                DateTime.Now,
                                GetGameVersion());

                            var result = this.InsertQuestPlate(
                                translatedQuestPlate);
#if DEBUG
                            // PluginLog.Debug(
                            //     $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
                        });
                    continue;
                }
#if DEBUG
                    // PluginLog.Debug(
                    //     $"Name translated: {quest.Text} -> {translatedNameText}");
#endif
                var storedTranslatedNameText = translatedNameText;
                QuestPlate translatedQuestPlate = new(
                    quest.Text,
                    string.Empty,
                    ClientStateInterface.ClientLanguage.Humanize(),
                    storedTranslatedNameText,
                    string.Empty,
                    string.Empty,
                    LangDict[LanguageInt].Code,
                    this.configuration.ChosenTransEngine,
                    DateTime.Now,
                    DateTime.Now,
                    GetGameVersion());

                if (this.configuration
                    .RemoveDiacriticsWhenUsingReplacementQuest)
                {
                    translatedNameText = this.RemoveDiacritics(
                        translatedNameText,
                        this.SpecialCharsSupportedByGameFont);
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

                List<string> translatedObjectives = new();
                var hasPendingNewObjectives = false;
                foreach (var objective in objectives)
                {
                    if (objective.Text == EmptyObjective)
                    {
                        translatedObjectives.Add(EmptyObjective);

                        // let's not store empty objectives on the database
                        continue;
                    }

                    if (IsValidTimeFormat(objective.Text))
                    {
                        // PluginLog.Debug("Skipping time format translation");
                        continue;
                    }

                    var objectiveCacheKey =
                        $"ToDoListObjective|{questTodoProgressKey}|{objective.Text}";
                    if (!this.TryGetQueuedTranslation(
                            objectiveCacheKey,
                            out var translatedObjectiveText))
                    {
                        this.QueueTranslation(
                            objectiveCacheKey,
                            () => this.Translate(objective.Text),
                            translatedQuestObjective =>
                            {
                                var existingQuestPlate =
                                    this.FindQuestPlateByName(
                                        this.FormatQuestPlate(
                                            storedTranslatedNameText,
                                            string.Empty));
                                if (existingQuestPlate == null)
                                {
                                    existingQuestPlate = new QuestPlate(
                                        quest.Text,
                                        string.Empty,
                                        ClientStateInterface.ClientLanguage.Humanize(),
                                        storedTranslatedNameText,
                                        string.Empty,
                                        string.Empty,
                                        LangDict[LanguageInt].Code,
                                        this.configuration.ChosenTransEngine,
                                        DateTime.Now,
                                        DateTime.Now,
                                        GetGameVersion());
                                }

                                existingQuestPlate.Objectives.TryAdd(
                                    objective.Text,
                                    translatedQuestObjective);
                                var result = existingQuestPlate.Id == 0
                                    ? this.InsertQuestPlate(existingQuestPlate)
                                    : this.UpdateQuestPlate(
                                        existingQuestPlate);
#if DEBUG
                                // PluginLog.Debug(
                                //     $"Using QuestPlate Replace - QuestPlate DB Update operation result: {result}");
#endif
                            });
                        hasPendingNewObjectives = true;
                        continue;
                    }
#if DEBUG
                    // PluginLog.Debug(
                    //     $"Objective translated: {translatedObjectiveText}");
#endif
                    translatedObjectives.Add(translatedObjectiveText);
                    translatedQuestPlate.Objectives.TryAdd(
                        objective.Text,
                        translatedObjectiveText);

                    if (this.configuration
                        .RemoveDiacriticsWhenUsingReplacementQuest)
                    {
                        translatedObjectiveText = this.RemoveDiacritics(
                            translatedObjectiveText,
                            this.SpecialCharsSupportedByGameFont);
                    }

                    if (this.ToDoListWritesNativeTranslation)
                    {
                        todoList->UldManager.NodeList[objective.IndexI]->
                                GetAsAtkComponentNode()->Component->UldManager
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

                var result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
                // PluginLog.Debug(
                //     $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif

                if (hasPendingNewObjectives)
                {
                    continue;
                }

                // because sometimes the quest name translation is the same as the original name but the objectives are not
                var translatedQuestWithObjectives = translatedNameText +
                                                    string.Join<string>(
                                                        ",",
                                                        translatedObjectives);
                QuestUiTranslationCache.Remember(
                    $"{questTodoProgressKey}|{quest.Text}",
                    Sanitizer.Sanitize(translatedQuestWithObjectives));
            }
        }
        catch (Exception e)
        {
            PluginLog.Error("Error translating todo items:", e);
        }
    }

    private void UiToDoListHandler(AddonEvent type, AddonArgs args)
    {
#if DEBUG
        // PluginLog.Debug(
        //     $"UiToDoListHandler AddonEvent: {type} {args.AddonName}");
#endif

        if (this.DisableTranslationAccordingToState())
        {
            return;
        }

        this.TranslateToDoList();
    }
}

