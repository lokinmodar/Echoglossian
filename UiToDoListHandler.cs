// <copyright file="UiToDoListHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;
using Echoglossian.EFCoreSqlite.Models.Journal;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Humanizer;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private const string EmptyObjective = "???";

    private unsafe void TranslateToDoList()
    {
      if (!this.configuration.TranslateJournal)
      {
        return;
      }

      var atkStage = AtkStage.Instance();
      var todoList = atkStage->RaptureAtkUnitManager->GetAddonByName("_ToDoList");
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

        if (todoList->UldManager.NodeList[i]->Type == NodeType.Collision || todoList->UldManager.NodeList[i]->Type == NodeType.Res)
        {
          continue;
        }

        var nodeID = todoList->UldManager.NodeList[i]->NodeId;

        // don't translate unneeded fate information
        if (nodeID == 8 || nodeID == 9)
        {
          continue;
        }

        var component = todoList->UldManager.NodeList[i]->GetAsAtkComponentNode();
        for (var j = 0; j < component->Component->UldManager.NodeListCount; j++)
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
          var originalStep = component->Component->UldManager.NodeList[j]->GetAsAtkTextNode()->NodeText;
          if (originalStep.IsEmpty)
          {
            continue;
          }

          if (IsValidTimeFormat(MemoryHelper.ReadSeStringAsString(out _, (nint)originalStep.StringPtr.Value)))
          {
            // skip text if time format
#if DEBUG
            PluginLog.Debug($"Skipping time format translation");
#endif
            continue;
          }

          // don't translate unneeded levelquest information
          if (nodeID == 4 && childrenNodeID == 8)
          {
            continue;
          }

          if (nodeID > 60000 || (nodeID == 4 && childrenNodeID == 3) || (nodeID == 6 && childrenNodeID == 2))
          {
            questNamesToTranslate.Add(new ToDoItem(MemoryHelper.ReadSeStringAsString(out _, (nint)originalStep.StringPtr.Value), i, j, nodeID));
          }
          else
          {
            if (nodeID == 4 || nodeID == 5)
            {
              levelQuestObjectivesToTranslate.Add(new ToDoItem(MemoryHelper.ReadSeStringAsString(out _, (nint)originalStep.StringPtr.Value), i, j, nodeID));
            }
            else
            {
              objectivesToTranslate.Add(new ToDoItem(MemoryHelper.ReadSeStringAsString(out _, (nint)originalStep.StringPtr.Value), i, j, nodeID));
            }
          }
        }
      }

      if (questNamesToTranslate.Count == 0)
      {
        return;
      }

      objectivesToTranslate.Reverse();

      this.TranslateTodoItems(questNamesToTranslate, objectivesToTranslate, levelQuestObjectivesToTranslate, todoList);
    }

    private List<ToDoItem> GetQuestObjectives(uint currentObjectiveNode, int objectiveIndex, List<ToDoItem> objectivesToTranslate, List<ToDoItem> questObjectives)
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
      return this.GetQuestObjectives(objective.NodeId, currentIndex, objectivesToTranslate, questObjectives);
    }

    private unsafe void TranslateTodoItems(List<ToDoItem> questNamesToTranslate, List<ToDoItem> objectivesToTranslate, List<ToDoItem> levelQuestObjectivesToTranslate, AtkUnitBase* todoList)
    {
      try
      {
        var objectiveIndex = 0;
        foreach (var quest in questNamesToTranslate)
        {
          List<ToDoItem> objectives = new();
          if (objectiveIndex < objectivesToTranslate.Count)
          {
            var currentObjective = objectivesToTranslate[objectiveIndex];
            objectives.Add(currentObjective);
            objectives = this.GetQuestObjectives(currentObjective.NodeId, objectiveIndex, objectivesToTranslate, objectives);
          }

          objectiveIndex += objectives.Count;
          if (quest.NodeId == 4)
          {
            objectives.AddRange(levelQuestObjectivesToTranslate);
          }

          // because sometimes the quest name translation is the same as the original name but the objectives are not
          var questWithObjectives = quest.Text + string.Join<ToDoItem>(",", objectives);
          if (this.translatedQuestNames.ContainsKey(sanitizer.Sanitize(questWithObjectives)))
          {
            continue;
          }

          QuestPlate questPlate = this.FormatQuestPlate(quest.Text, string.Empty);
          QuestPlate foundQuestPlate = this.FindQuestPlateByName(questPlate);
          if (foundQuestPlate != null)
          {
#if DEBUG
            PluginLog.Debug($"Name from database: {quest.Text} -> {foundQuestPlate.TranslatedQuestName}");
#endif

            var foundTranslatedQuestName = foundQuestPlate.TranslatedQuestName;
            if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
            {
              foundTranslatedQuestName = this.RemoveDiacritics(foundTranslatedQuestName, this.SpecialCharsSupportedByGameFont);
            }

            todoList->UldManager.NodeList[quest.IndexI]->GetAsAtkComponentNode()->Component->UldManager.NodeList[quest.IndexJ]->GetAsAtkTextNode()->SetText(foundTranslatedQuestName);

            List<string> translatedStoredObjectives = new();
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
                PluginLog.Debug($"Skipping time format translation");
                continue;
              }

              if (foundQuestPlate.Objectives.TryGetValue(objective.Text, out var storedObjectiveText))
              {
#if DEBUG
                PluginLog.Debug($"Objective from database: {objective.Text} {storedObjectiveText}");
#endif
                translatedStoredObjectives.Add(storedObjectiveText);

                if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
                {
                  storedObjectiveText = this.RemoveDiacritics(storedObjectiveText, this.SpecialCharsSupportedByGameFont);
                }

                todoList->UldManager.NodeList[objective.IndexI]->GetAsAtkComponentNode()->Component->UldManager.NodeList[objective.IndexJ]->GetAsAtkTextNode()->SetText(storedObjectiveText);
                continue;
              }

              var translatedQuestObjective = this.Translate(objective.Text);
              foundQuestPlate.Objectives.TryAdd(objective.Text, translatedQuestObjective);
              translatedStoredObjectives.Add(translatedQuestObjective);
              PluginLog.Debug($"Objective translated: {objective.Text} {translatedQuestObjective}");
              string resultUpdate = this.UpdateQuestPlate(foundQuestPlate);
#if DEBUG
              PluginLog.Debug($"Using QuestPlate Replace - QuestPlate DB Update operation result: {resultUpdate}");
#endif
              if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
              {
                translatedQuestObjective = this.RemoveDiacritics(translatedQuestObjective, this.SpecialCharsSupportedByGameFont);
              }

              todoList->UldManager.NodeList[objective.IndexI]->GetAsAtkComponentNode()->Component->UldManager.NodeList[objective.IndexJ]->GetAsAtkTextNode()->SetText(translatedQuestObjective);
            }

            // because sometimes the quest name translation is the same as the original name but the objectives are not
            var translatedStoredQuestWithObjectives = foundQuestPlate.TranslatedQuestName + string.Join<string>(",", translatedStoredObjectives);
            this.translatedQuestNames.TryAdd(translatedStoredQuestWithObjectives, true);

            continue;
          }

          var translatedNameText = this.Translate(quest.Text);
#if DEBUG
          PluginLog.Debug($"Name translated: {quest.Text} -> {translatedNameText}");
#endif
          QuestPlate translatedQuestPlate = new(
            quest.Text,
            string.Empty,
            ClientStateInterface.ClientLanguage.Humanize(),
            translatedNameText,
            string.Empty,
            string.Empty,
            langDict[languageInt].Code,
            this.configuration.ChosenTransEngine,
            DateTime.Now,
            DateTime.Now);

          if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
          {
            translatedNameText = this.RemoveDiacritics(translatedNameText, this.SpecialCharsSupportedByGameFont);
          }

          todoList->UldManager.NodeList[quest.IndexI]->GetAsAtkComponentNode()->Component->UldManager.NodeList[quest.IndexJ]->GetAsAtkTextNode()->SetText(translatedNameText);

          List<string> translatedObjectives = new();
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
              PluginLog.Debug($"Skipping time format translation");
              continue;
            }

            var translatedObjectiveText = this.Translate(objective.Text);
#if DEBUG
            PluginLog.Debug($"Objective translated: {translatedObjectiveText}");
#endif
            translatedObjectives.Add(translatedObjectiveText);
            translatedQuestPlate.Objectives.TryAdd(objective.Text, translatedObjectiveText);

            if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
            {
              translatedObjectiveText = this.RemoveDiacritics(translatedObjectiveText, this.SpecialCharsSupportedByGameFont);
            }

            todoList->UldManager.NodeList[objective.IndexI]->GetAsAtkComponentNode()->Component->UldManager.NodeList[objective.IndexJ]->GetAsAtkTextNode()->SetText(translatedObjectiveText);
          }

          string result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
          PluginLog.Debug($"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif

          // because sometimes the quest name translation is the same as the original name but the objectives are not
          var translatedQuestWithObjectives = translatedNameText + string.Join<string>(",", translatedObjectives);
          this.translatedQuestNames.TryAdd(translatedQuestWithObjectives, true);
        }
      }
      catch (Exception e)
      {
        PluginLog.Error("Error translating todo items:", e);
      }
    }

    private unsafe void UiToDoListHandler(AddonEvent type, AddonArgs args)
    {
#if DEBUG
      PluginLog.Debug($"UiToDoListHandler AddonEvent: {type} {args.AddonName}");
#endif

      if (this.DisableTranslationAccordingToState())
      {
        return;
      }

      this.TranslateToDoList();
    }
  }

  public class ToDoItem
  {
    public string Text { get; set; }

    public int IndexI { get; set; }

    public int IndexJ { get; set; }

    public uint NodeId { get; set; }

    public ToDoItem(string text, int indexI, int indexJ, uint nodeID)
    {
      this.Text = text;
      this.IndexI = indexI;
      this.IndexJ = indexJ;
      this.NodeId = nodeID;
    }

    public override string ToString()
    {
      return this.Text;
    }
  }
}
