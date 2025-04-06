// <copyright file="UiRecommendListHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Threading.Tasks;

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
    private unsafe void UpdateRecommendList()
    {
      var atkStage = AtkStage.Instance();
      var recommendList = atkStage->RaptureAtkUnitManager->GetAddonByName("RecommendList");
      if (recommendList == null || !recommendList->IsVisible)
      {
        return;
      }

      try
      {
        // Replace the text in the nodes reading from the DB
        var questListNode = recommendList->GetNodeById(5);
        if (questListNode == null || !questListNode->IsVisible())
        {
          return;
        }

        var questListComponent = questListNode->GetAsAtkComponentNode()->Component;
        for (var i = 0; i < questListComponent->UldManager.NodeListCount; i++)
        {
          if (!questListComponent->UldManager.NodeList[i]->IsVisible())
          {
            continue;
          }

          if (questListComponent->UldManager.NodeList[i]->Type == NodeType.Collision || questListComponent->UldManager.NodeList[i]->Type == NodeType.Res)
          {
            continue;
          }

          var questItemNode = questListComponent->UldManager.NodeList[i]->GetAsAtkComponentNode();
          var questNameNode = questItemNode->Component->UldManager.SearchNodeById(5);
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
            PluginLog.Debug($"Name from database: {questNameText} -> {foundQuestPlate.TranslatedQuestName}");
#endif
            var translatedQuestName = foundQuestPlate.TranslatedQuestName;

            if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
            {
              translatedQuestName = this.RemoveDiacritics(translatedQuestName, this.SpecialCharsSupportedByGameFont);
            }

            // because we are translating names, it's safer to use SetString instead of SetText
            questName->NodeText.SetString(translatedQuestName);
            this.translatedQuestNames.TryAdd(translatedQuestName, true);
          }
        }
      }
      catch (Exception e)
      {
        PluginLog.Error($"Error: {e}");
      }
    }

    private unsafe void TranslateRecommendListHandler()
    {
      var atkStage = AtkStage.Instance();
      var recommendList = atkStage->RaptureAtkUnitManager->GetAddonByName("RecommendList");
      if (recommendList == null || !recommendList->IsVisible)
      {
        return;
      }

      try
      {
        // First we store the non translated quest names in the DB
        var questListNode = recommendList->GetNodeById(5);
        if (questListNode == null || !questListNode->IsVisible())
        {
          return;
        }

        var questListComponent = questListNode->GetAsAtkComponentNode()->Component;
        for (var i = 0; i < questListComponent->UldManager.NodeListCount; i++)
        {
          if (!questListComponent->UldManager.NodeList[i]->IsVisible())
          {
            continue;
          }

          if (questListComponent->UldManager.NodeList[i]->Type == NodeType.Collision || questListComponent->UldManager.NodeList[i]->Type == NodeType.Res)
          {
            continue;
          }

          var questItemNode = questListComponent->UldManager.NodeList[i]->GetAsAtkComponentNode();
          var questNameNode = questItemNode->Component->UldManager.SearchNodeById(5);
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
        }

        // Then we replace the text in the nodes
        this.UpdateRecommendList();
      }
      catch (Exception e)
      {
        PluginLog.Error($"Error in UIRecommendListHandler: {e}");
      }
    }

    private unsafe void UiRecommendListHandler(AddonEvent type, AddonArgs args)
    {
#if DEBUG
      PluginLog.Debug($"UiRecommendListHandler AddonEvent: {type} {args.AddonName}");
#endif

      if (this.DisableTranslationAccordingToState())
      {
        return;
      }

      if (!this.configuration.TranslateJournal)
      {
        return;
      }

      this.TranslateRecommendListHandler();
    }

    private unsafe void UiRecommendListHandlerAsync(AddonEvent type, AddonArgs args)
    {
#if DEBUG
      PluginLog.Debug($"UiRecommendListHandlerAsync AddonEvent: {type} {args.AddonName}");
#endif
      if (!this.configuration.TranslateJournal)
      {
        return;
      }

      // delay added to be sure the nodes are loaded when the player changes zones
      Task.Delay(200).ContinueWith(t => this.TranslateRecommendListHandler());
    }
  }
}
