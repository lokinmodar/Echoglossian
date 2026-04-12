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
#if DEBUG
        PluginLog.Debug(
            $"[ToDoList] tooltip candidate key='{(progressKey == null ? $"ToDoList-{indexI}-{indexJ}-{nodeId}" : $"ToDoList-{progressKey}-{indexI}-{indexJ}-{nodeId}")}' " +
            $"index=({indexI},{indexJ}) nodeId={nodeId} progressKey='{progressKey ?? string.Empty}' " +
            $"mode={this.configuration.ToDoListTranslationDisplayMode} hover={this.ToDoListUsesHoverTooltips} " +
            $"native={this.ToDoListWritesNativeTranslation} swap={this.ToDoListHoverShowsOriginal}");
#endif

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

#if DEBUG
        PluginLog.Debug(
            $"[ToDoList] scan start mode={this.configuration.ToDoListTranslationDisplayMode} hover={this.ToDoListUsesHoverTooltips} " +
            $"native={this.ToDoListWritesNativeTranslation} swap={this.ToDoListHoverShowsOriginal} nodeCount={todoList->UldManager.NodeListCount}");
#endif

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
#if DEBUG
            PluginLog.Debug(
                $"[ToDoList] scan result questNames=0 objectives={objectivesToTranslate.Count} levelQuestObjectives={levelQuestObjectivesToTranslate.Count}");
#endif
            return;
        }

        objectivesToTranslate.Reverse();

#if DEBUG
        PluginLog.Debug(
            $"[ToDoList] scan result questNames={questNamesToTranslate.Count} objectives={objectivesToTranslate.Count} " +
            $"levelQuestObjectives={levelQuestObjectivesToTranslate.Count}");
#endif

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

                // If resolution failed, quest.Text may already be a translated
                // name written to the UI node in a previous frame. Recover the
                // original name via the per-quest reverse-translation mapping so
                // identity resolution can be retried with the right input.
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

                var questPlate = this.FormatQuestPlate(
                    effectiveQuestName,
                    string.Empty);

                // Use the stable progress key as the short-circuit guard so
                // that subsequent frames skip quests whose translation is already
                // fully applied. The key is stable per quest phase and is stored
                // in the Remember call at the end of each successful translation.
                if (QuestUiTranslationCache.TryGetAppliedSnapshot(
                        questTodoProgressKey,
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
                    if (this.ToDoListShouldRemoveDiacritics)
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

                    var snapshotSteps = questTodoProgressSnapshot?.QuestProgress.QuestSteps;
                    var objIdx = 0;
                    List<string> translatedStoredObjectives = new();
                    var hasPendingFoundObjectives = false;
                    foreach (var objective in objectives)
                    {
                        // Use the Lumina step text and key when the snapshot is
                        // available so the translation source is stable even if
                        // the UI node already shows a previously applied string.
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

                            // let's not store empty objectives on the database
                            continue;
                        }

                        if (IsValidTimeFormat(objective.Text))
                        {
                            // PluginLog.Debug("Skipping time format translation");
                            continue;
                        }

                        // Look up by stable Lumina key first, then fall back to
                        // the legacy UI-text key for backward compatibility.
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
#if DEBUG
                            // PluginLog.Debug(
                            //     $"Objective from database: {stepText} {storedObjectiveText}");
#endif
                            translatedStoredObjectives.Add(storedObjectiveText);

                            if (this.ToDoListShouldRemoveDiacritics)
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
                                    var questPlateToUpdate =
                                        foundQuestPlate.Clone();
                                    questPlateToUpdate.TranslatedObjectives[
                                        stepKey] = translatedObjectiveText;
                                    questPlateToUpdate.Objectives[
                                        stepText] = translatedObjectiveText;
                                    questPlateToUpdate.UpdatedDate =
                                        DateTime.Now;
                                    this.UpdateQuestPlate(
                                        questPlateToUpdate);
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
                        translatedStoredObjectives.Add(
                            translatedQuestObjective);
                        // PluginLog.Debug(
                        //     $"Objective translated: {stepText} {translatedQuestObjective}");
                        var resultUpdate =
                            this.UpdateQuestPlate(foundQuestPlate);
#if DEBUG
                        // PluginLog.Debug(
                        //     $"Using QuestPlate Replace - QuestPlate DB Update operation result: {resultUpdate}");
#endif
                        if (this.ToDoListShouldRemoveDiacritics)
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

                    // Mark the stable progress key as fully processed so
                    // subsequent frames skip this quest without re-running.
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
                            var translatedQuestPlate = new QuestPlate(
                                effectiveQuestName,
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
                    effectiveQuestName,
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

                if (this.ToDoListShouldRemoveDiacritics)
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
                // Store the per-quest reverse mapping so that if the UI node
                // is mutated and shows translated text on the next frame, the
                // original name can be recovered for identity re-resolution.
                QuestUiTranslationCache.Remember(
                    effectiveQuestName,
                    storedTranslatedNameText);

                var newSnapshotSteps =
                    questTodoProgressSnapshot?.QuestProgress.QuestSteps;
                var newObjIdx = 0;
                List<string> translatedObjectives = new();
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

                        // let's not store empty objectives on the database
                        continue;
                    }

                    if (IsValidTimeFormat(objective.Text))
                    {
                        // PluginLog.Debug("Skipping time format translation");
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
                                var existingQuestPlate =
                                    this.FindQuestPlateByName(
                                        this.FormatQuestPlate(
                                            effectiveQuestName,
                                            string.Empty));
                                if (existingQuestPlate == null)
                                {
                                    existingQuestPlate = new QuestPlate(
                                        effectiveQuestName,
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

                                existingQuestPlate.TranslatedObjectives.TryAdd(
                                    newStepKey,
                                    translatedQuestObjective);
                                existingQuestPlate.Objectives.TryAdd(
                                    newStepText,
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
                    translatedQuestPlate.TranslatedObjectives.TryAdd(
                        newStepKey,
                        translatedObjectiveText);
                    translatedQuestPlate.Objectives.TryAdd(
                        newStepText,
                        translatedObjectiveText);

                    if (this.ToDoListShouldRemoveDiacritics)
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

                // Mark the stable progress key as fully processed.
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

