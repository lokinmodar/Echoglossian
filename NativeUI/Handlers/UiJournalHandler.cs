// <copyright file="UiJournalHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;
using Echoglossian.EFCoreSqlite.Models.Journal;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Humanizer;
using System.ComponentModel;
using Lumina.Data.Parsing.Uld;
using System.Collections;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private static readonly uint JournalDetailCanvasResNodeId = 43;
    private static readonly uint JournalDetailDescriptionResNodeId = 5;
    private static readonly uint JournalDetailObjectivesResNodeId = 9;
    private static readonly uint JournalDetailSummaryResNodeId = 49;
    private static readonly uint JournalQuestListNodeId = 25;
    private static readonly uint[] JournalDetailCanvasNodesToTranslate = { JournalDetailDescriptionResNodeId, JournalDetailObjectivesResNodeId, JournalDetailSummaryResNodeId };

    private static unsafe List<TextNodePointer> GetComponentTextNodes(AtkComponentBase* component, uint[] parentFilter)
    {
#if DEBUG
      PluginLog.Debug($"ChildCound: {component->UldManager.NodeListCount}");
#endif
      List<TextNodePointer> textNodesToTranslate = [];

      for (var i = 0; i < component->UldManager.NodeListCount; i++)
      {
        var childNode = component->UldManager.NodeList[i];
        var childComponent = childNode->GetComponent();

        if (Array.IndexOf(parentFilter, childNode->ParentNode->NodeId) == -1 || !childNode->NodeFlags.HasFlag(NodeFlags.Visible))
        {
          continue;
        }

        if (childNode->Type == NodeType.Text)
        {
          var nodePointer = new TextNodePointer(childNode->GetAsAtkTextNode());
          if (!nodePointer.IsEmpty())
          {
            textNodesToTranslate.Add(nodePointer);
          }
        }

        if (childComponent != null && childComponent->UldManager.NodeListCount > 0)
        {
          var childTextNodes = GetComponentTextNodes(childComponent, [childNode->NodeId]);
          textNodesToTranslate.AddRange(childTextNodes);
        }
      }

      return textNodesToTranslate;
    }

    // used to be sure we don't translate the same quest name twice
    private readonly ConcurrentDictionary<string, bool> translatedQuestNames = new();

    private unsafe bool TranslateJournalBox(AtkUnitBase* journalDetail)
    {
      try
      {
        var questNameNode = journalDetail->GetTextNodeById(38);
        if (questNameNode == null || questNameNode->NodeText.IsEmpty)
        {
          return false;
        }

        var journalDetailNode = journalDetail->GetNodeById(JournalDetailCanvasResNodeId);
        if (!journalDetailNode->IsVisible())
        {
          return false;
        }

        var journalDetailCanvasComponent = journalDetailNode->GetComponent();
        var textNodesToTranslate = GetComponentTextNodes(journalDetailCanvasComponent, JournalDetailCanvasNodesToTranslate);
        var questNameTextNodePointer = new TextNodePointer(questNameNode);
        var languageCode = this.languagesDictionary[this.configuration.Lang].Code;
        var questName = questNameTextNodePointer.GetNodeText();

        textNodesToTranslate.Add(questNameTextNodePointer);

        QuestPlate questPlate = this.FindQuestPlateByName(questName, this.configuration.ChosenTransEngine, languageCode);
        var shouldUpdateQuestPlate = false;

        if (questPlate == null)
        {
          questPlate = this.FormatQuestPlate(questName, string.Empty);
          this.InsertQuestPlate(questPlate);
        }

        for (var i = 0; i < textNodesToTranslate.Count; i++)
        {
          var node = textNodesToTranslate[i];
#if DEBUG
          PluginLog.Debug($"Node id: {node.Node->NodeId}; ParentId: {node.Node->ParentNode->NodeId}; Node Type: {node.Node->Type}; IsVisible: {node.Node->IsVisible()};");
#endif

          var text = node.GetNodeText();
          var translatedText = string.Empty;
          if (questPlate.Summaries.TryGetValue(text, out var storedTranslatedText))
          {
            translatedText = storedTranslatedText;
          }
          else
          {
            shouldUpdateQuestPlate = true;
            translatedText = this.Translate(text);
            if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
            {
              translatedText = this.RemoveDiacritics(translatedText, this.SpecialCharsSupportedByGameFont);
            }

            questPlate.Summaries.Add(text, translatedText);
          }

          node.SetNodeText(translatedText);
        }

        if (shouldUpdateQuestPlate)
        {
          this.UpdateQuestPlate(questPlate);
        }
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

  public unsafe class TextNodePointer
  {
    public AtkTextNode* Node { get; set; }

    public string GetNodeText()
    {
      return MemoryHelper.ReadSeStringAsString(out _, (nint)this.Node->NodeText.StringPtr.Value);
    }

    public bool IsEmpty()
    {
      return this.GetNodeText() == string.Empty;
    }

    public void SetNodeText(string text)
    {
      this.Node->SetText(text);
    }

    public TextNodePointer(AtkTextNode* node)
    {
      this.Node = node;
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
