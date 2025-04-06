// <copyright file="UiJournalHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Concurrent;
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
    // used to be sure we don't translate the same quest name twice
    private readonly ConcurrentDictionary<string, bool> translatedQuestNames = new();

    private unsafe List<SummaryQuest> TranslateSummaries(
      AtkComponentBase* journalBox,
      QuestPlate foundQuestPlate,
      string summaryText)
    {
      List<SummaryQuest> summaries = new();
      if (summaryText == string.Empty)
      {
        return summaries;
      }

      for (var i = 0; i < journalBox->UldManager.NodeListCount; i++)
      {
        if (journalBox->UldManager.NodeList[i]->NodeId < 480700 || journalBox->UldManager.NodeList[i]->NodeId > 480750)
        {
          continue;
        }

        if (!journalBox->UldManager.NodeList[i]->IsVisible())
        {
          continue;
        }

        var summaryItemNode = journalBox->UldManager.NodeList[i]->GetAsAtkComponentNode();
        var summaryNode = summaryItemNode->Component->UldManager.SearchNodeById(2);
        if (summaryNode == null || summaryNode->Type != NodeType.Text || !summaryNode->IsVisible())
        {
          continue;
        }

        var summaryTextNode = summaryNode->GetAsAtkTextNode();
        if (summaryTextNode->NodeText.IsEmpty)
        {
          continue;
        }

        var originalText = MemoryHelper.ReadSeStringAsString(out _, (nint)summaryTextNode->NodeText.StringPtr.Value);
        if (foundQuestPlate != null && foundQuestPlate.Summaries.TryGetValue(originalText, out var storedSummaryText))
        {
          summaries.Add(new(originalText, storedSummaryText, summaryTextNode, false));
          continue;
        }

        var translatedText = this.Translate(originalText);
        summaries.Add(new(originalText, translatedText, summaryTextNode, true));
      }

      return summaries;
    }

    private unsafe void TranslateQuestOnJournalBox(
      AtkComponentBase* journalBox,
      QuestPlate foundQuestPlate,
      string questName,
      string questMessage,
      string objectiveText,
      string summaryText,
      AtkTextNode* questNameNode,
      AtkTextNode* descriptionNode,
      AtkTextNode* objectiveNode,
      AtkTextNode* summaryNode)
    {
      string translatedQuestName;
      string translatedQuestMessage;
      string translatedQuestObjective;
      string translatedQuestSummary = string.Empty;
      List<SummaryQuest> summaries;

      if (foundQuestPlate != null)
      {
        translatedQuestName = foundQuestPlate.TranslatedQuestName;
        translatedQuestMessage = foundQuestPlate.TranslatedQuestMessage;
        var shouldUpdateQuest = false;

        if (foundQuestPlate.Objectives.TryGetValue(objectiveText, out var storedObjectiveText))
        {
          translatedQuestObjective = storedObjectiveText;
        }
        else
        {
          translatedQuestObjective = this.Translate(objectiveText);
          foundQuestPlate.Objectives.Add(objectiveText, translatedQuestObjective);
          shouldUpdateQuest = true;
        }

        if (summaryText != string.Empty)
        {
          if (foundQuestPlate.Summaries.TryGetValue(summaryText, out var storedSummaryText))
          {
            translatedQuestSummary = storedSummaryText;
          }
          else
          {
            translatedQuestSummary = this.Translate(summaryText);
            foundQuestPlate.Summaries.Add(summaryText, translatedQuestSummary);
            shouldUpdateQuest = true;
          }
        }

        summaries = this.TranslateSummaries(journalBox, foundQuestPlate, summaryText);
        foreach (var summary in summaries)
        {
          if (summary.IsTranslated)
          {
            foundQuestPlate.Summaries.Add(summary.OriginalText, summary.TranslatedText);
            shouldUpdateQuest = true;
          }
        }

        if (shouldUpdateQuest)
        {
          string result = this.UpdateQuestPlate(foundQuestPlate);
#if DEBUG
          PluginLog.Debug($"Using QuestPlate Replace - QuestPlate DB Update operation result: {result}");
#endif
        }
#if DEBUG
        PluginLog.Debug($"From database - Name: {foundQuestPlate.TranslatedQuestName}, Message: {foundQuestPlate.TranslatedQuestMessage}");
#endif
      }
      else
      {
        translatedQuestName = this.Translate(questName);
        translatedQuestMessage = this.Translate(questMessage);
        translatedQuestObjective = this.Translate(objectiveText);

        QuestPlate translatedQuestPlate = new(
          questName,
          questMessage,
          ClientStateInterface.ClientLanguage.Humanize(),
          translatedQuestName,
          translatedQuestMessage,
          string.Empty,
          langDict[languageInt].Code,
          this.configuration.ChosenTransEngine,
          DateTime.Now,
          DateTime.Now);

        if (summaryText != string.Empty)
        {
          translatedQuestSummary = this.Translate(summaryText);
          translatedQuestPlate.Summaries.Add(summaryText, translatedQuestSummary);
        }

        summaries = this.TranslateSummaries(journalBox, foundQuestPlate, summaryText);
        foreach (var summary in summaries)
        {
          translatedQuestPlate.Summaries.Add(summary.OriginalText, summary.TranslatedText);
        }

        translatedQuestPlate.Objectives.Add(objectiveText, translatedQuestObjective);
        string result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
        PluginLog.Debug($"Translated quest name: {translatedQuestName}");
        PluginLog.Debug($"Translated quest message: {translatedQuestMessage}");
        PluginLog.Debug($"Translated quest objective: {translatedQuestObjective}");
        PluginLog.Debug($"Translated quest summary: {translatedQuestSummary}");
        PluginLog.Debug($"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
      }

      if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
      {
        translatedQuestName = this.RemoveDiacritics(translatedQuestName, this.SpecialCharsSupportedByGameFont);
        translatedQuestMessage = this.RemoveDiacritics(translatedQuestMessage, this.SpecialCharsSupportedByGameFont);
        translatedQuestObjective = this.RemoveDiacritics(translatedQuestObjective, this.SpecialCharsSupportedByGameFont);
        translatedQuestSummary = this.RemoveDiacritics(translatedQuestSummary, this.SpecialCharsSupportedByGameFont);

        foreach (var summary in summaries)
        {
          summary.TranslatedText = this.RemoveDiacritics(summary.TranslatedText, this.SpecialCharsSupportedByGameFont);
        }
      }

      questNameNode->SetText(translatedQuestName);
      descriptionNode->SetText(translatedQuestMessage);
      objectiveNode->SetText(translatedQuestObjective);
      if (summaryText != string.Empty && summaryNode != null)
      {
        summaryNode->SetText(translatedQuestSummary);
      }

      foreach (var summary in summaries)
      {
        summary.Node->SetText(summary.TranslatedText);
      }
    }

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

        var objectiveResNode = journalBox->UldManager.SearchNodeById(12)->GetComponent()->UldManager.SearchNodeById(3);
        if (objectiveResNode == null || objectiveResNode->Type != NodeType.Text)
        {
          return true;
        }

        var summaryText = string.Empty;
        AtkTextNode* summaryNode = null;
        var summaryBox = journalBox->UldManager.SearchNodeById(52);
        if (summaryBox != null && summaryBox->IsVisible())
        {
          var summaryResNode = summaryBox->GetComponent()->UldManager.SearchNodeById(2);
          if (summaryResNode != null && summaryResNode->Type == NodeType.Text)
          {
            summaryNode = summaryResNode->GetAsAtkTextNode();
            summaryText = MemoryHelper.ReadSeStringAsString(out _, (nint)summaryNode->NodeText.StringPtr.Value);
          }
        }

        var questName = MemoryHelper.ReadSeStringAsString(out _, (nint)questNameNode->NodeText.StringPtr.Value);
        var descriptionNode = description->GetAsAtkTextNode();
        var questMessage = MemoryHelper.ReadSeStringAsString(out _, (nint)descriptionNode->NodeText.StringPtr.Value);
        var objectiveNode = objectiveResNode->GetAsAtkTextNode();
        var objectiveText = MemoryHelper.ReadSeStringAsString(out _, (nint)objectiveNode->NodeText.StringPtr.Value);
        QuestPlate questPlate = this.FormatQuestPlate(questName, questMessage);
        QuestPlate foundQuestPlate = this.FindQuestPlate(questPlate);

#if DEBUG
        PluginLog.Debug($"Quest name: {questName}");
        PluginLog.Debug($"Quest message: {questMessage}");
        PluginLog.Debug($"Objective text: {objectiveText}");
        PluginLog.Debug($"Summary text: {summaryText}");
#endif
        this.TranslateQuestOnJournalBox(journalBox, foundQuestPlate, questName, questMessage, objectiveText, summaryText, questNameNode, descriptionNode, objectiveNode, summaryNode);
      }
      catch (Exception e)
      {
        PluginLog.Error($"Error in UIJournalHandler: {e}");
      }

      return true;
    }

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

        var questName = MemoryHelper.ReadSeStringAsString(out _, (nint)questNameNode->NodeText.StringPtr.Value);
        var descriptionNode = description->GetAsAtkTextNode();
        var questMessage = MemoryHelper.ReadSeStringAsString(out _, (nint)descriptionNode->NodeText.StringPtr.Value);
        QuestPlate questPlate = this.FormatQuestPlate(questName, questMessage);
        QuestPlate foundQuestPlate = this.FindQuestPlate(questPlate);
#if DEBUG
        PluginLog.Debug($"Quest name: {questName}");
        PluginLog.Debug($"Quest message: {questMessage}");
#endif

        string translatedQuestName;
        string translatedQuestMessage;

        if (foundQuestPlate != null)
        {
          translatedQuestName = foundQuestPlate.TranslatedQuestName;
          translatedQuestMessage = foundQuestPlate.TranslatedQuestMessage;
#if DEBUG
          PluginLog.Debug($"From database - Name: {foundQuestPlate.TranslatedQuestName}, Message: {foundQuestPlate.TranslatedQuestMessage}");
#endif
        }
        else
        {
          translatedQuestName = this.Translate(questName);
          translatedQuestMessage = this.Translate(questMessage);

#if DEBUG
          PluginLog.Debug($"Translated quest name: {translatedQuestName}");
          PluginLog.Debug($"Translated quest message: {translatedQuestMessage}");
#endif

          QuestPlate translatedQuestPlate = new(
            questName,
            questMessage,
            ClientStateInterface.ClientLanguage.Humanize(),
            translatedQuestName,
            translatedQuestMessage,
            string.Empty,
            langDict[languageInt].Code,
            this.configuration.ChosenTransEngine,
            DateTime.Now,
            DateTime.Now);
          string result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
          PluginLog.Debug($"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
        }

        if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
        {
          translatedQuestName = this.RemoveDiacritics(translatedQuestName, this.SpecialCharsSupportedByGameFont);
          translatedQuestMessage = this.RemoveDiacritics(translatedQuestMessage, this.SpecialCharsSupportedByGameFont);
        }

        questNameNode->SetText(translatedQuestName);
        descriptionNode->SetText(translatedQuestMessage);
      }
      catch (Exception e)
      {
        PluginLog.Error($"Error in UiJournalHandler: {e}");
      }
    }

    private unsafe void TranslateJournalDetail()
    {
      if (!this.configuration.TranslateJournal)
      {
        return;
      }

      var atkStage = AtkStage.Instance();
      var journalDetail = atkStage->RaptureAtkUnitManager->GetAddonByName("JournalDetail");
      if (journalDetail == null || !journalDetail->IsVisible)
      {
        return;
      }
#if DEBUG
      PluginLog.Debug($"Language: {ClientStateInterface.ClientLanguage.Humanize()}");
      PluginLog.Debug($"Translate JournalDetail");
#endif

      if (!this.TranslateJournalBox(journalDetail))
      {
        this.TranslateCompletedQuest(journalDetail);
      }
    }

    private unsafe void TranslateJournalQuests()
    {
      if (!this.configuration.TranslateJournal)
      {
        return;
      }

      var atkStage = AtkStage.Instance();
      var journal = atkStage->RaptureAtkUnitManager->GetAddonByName("Journal");
      if (journal == null || !journal->IsVisible)
      {
        return;
      }

#if DEBUG
      PluginLog.Debug($"Language: {ClientStateInterface.ClientLanguage.Humanize()}");
      PluginLog.Debug($"Translate JournalQuests");
#endif
      try
      {
        var questListNode = journal->GetNodeById(25)->GetAsAtkComponentNode()->Component;
        for (var i = 0; i < questListNode->UldManager.NodeListCount; i++)
        {
          if (!questListNode->UldManager.NodeList[i]->IsVisible() || questListNode->UldManager.NodeList[i]->NodeId == 5)
          {
            continue;
          }

          if (questListNode->UldManager.NodeList[i]->Type == NodeType.Collision || questListNode->UldManager.NodeList[i]->Type == NodeType.Res)
          {
            continue;
          }

          var questItemNode = questListNode->UldManager.NodeList[i]->GetAsAtkComponentNode();
          var questNameNode = questItemNode->Component->UldManager.SearchNodeById(3);
          if (questNameNode == null || !questNameNode->IsVisible() || questNameNode->Type != NodeType.Text)
          {
            continue;
          }

          var questName = questNameNode->GetAsAtkTextNode();
          if (questName->NodeText.IsEmpty)
          {
            continue;
          }

          var questNameText = MemoryHelper.ReadSeStringAsString(out _, (nint)questName->NodeText.StringPtr.Value);
          if (this.translatedQuestNames.ContainsKey(questNameText))
          {
            continue;
          }

          QuestPlate questPlate = this.FormatQuestPlate(questNameText, string.Empty);
          QuestPlate foundQuestPlate = this.FindQuestPlateByName(questPlate);
          if (foundQuestPlate != null)
          {
#if DEBUG
            PluginLog.Debug($"Name from database: {questName->NodeText} -> {foundQuestPlate.TranslatedQuestName}");
#endif
            var translQuestName = foundQuestPlate.TranslatedQuestName;
            if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
            {
              translQuestName = this.RemoveDiacritics(foundQuestPlate.TranslatedQuestName, this.SpecialCharsSupportedByGameFont);
            }

            questName->SetText(translQuestName);

            this.translatedQuestNames.TryAdd(foundQuestPlate.TranslatedQuestName, true);
            continue;
          }

          var translatedNameText = this.Translate(questNameText);
#if DEBUG
          PluginLog.Debug($"Name translated: {questNameText} -> {translatedNameText}");
#endif
          QuestPlate translatedQuestPlate = new(
            questNameText,
            string.Empty,
            ClientStateInterface.ClientLanguage.Humanize(),
            translatedNameText,
            string.Empty,
            string.Empty,
            langDict[languageInt].Code,
            this.configuration.ChosenTransEngine,
            DateTime.Now,
            DateTime.Now);

          string result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
          PluginLog.Debug($"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
          if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
          {
            translatedNameText = this.RemoveDiacritics(translatedNameText, this.SpecialCharsSupportedByGameFont);
          }

          questName->SetText(translatedNameText);
          this.translatedQuestNames.TryAdd(translatedNameText, true);
        }
      }
      catch (Exception e)
      {
        PluginLog.Error($"Error: {e}");
      }
    }

    private unsafe void UiJournalDetailHandler(AddonEvent type, AddonArgs args)
    {
#if DEBUG
      PluginLog.Debug($"UiJournalDetailHandler AddonEvent: {type} {args.AddonName}");
#endif
      this.TranslateJournalDetail();
    }

    private unsafe void UiJournalQuestHandler(AddonEvent type, AddonArgs args)
    {
#if DEBUG
      PluginLog.Debug($"UiJournalQuestHandler AddonEvent: {type} {args.AddonName}");
#endif
      this.TranslateJournalQuests();
    }
  }

  public unsafe class SummaryQuest
  {
    public string OriginalText { get; set; }

    public string TranslatedText { get; set; }

    public AtkTextNode* Node { get; set; }

    public bool IsTranslated { get; set; }

    public SummaryQuest(string originalText, string translatedText, AtkTextNode* node, bool isTranslated)
    {
      this.OriginalText = originalText;
      this.TranslatedText = translatedText;
      this.Node = node;
      this.IsTranslated = isTranslated;
    }
  }
}
