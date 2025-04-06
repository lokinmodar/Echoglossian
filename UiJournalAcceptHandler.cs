// <copyright file="UiJournalAcceptHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;

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
    private unsafe void UiJournalAcceptHandler(AddonEvent type, AddonArgs args)
    {
      if (!this.configuration.TranslateJournal)
      {
        return;
      }

      if (args is not AddonSetupArgs setupArgs)
      {
        return;
      }

      var setupAtkValues = (AtkValue*)setupArgs.AtkValues;
      if (setupAtkValues == null)
      {
        return;
      }

      try
      {
        string questName = MemoryHelper.ReadSeStringAsString(out _, (nint)setupAtkValues[5].String.Value);
        string questMessage = MemoryHelper.ReadSeStringAsString(out _, (nint)setupAtkValues[12].String.Value);

#if DEBUG
        PluginLog.Debug($"Language: {ClientStateInterface.ClientLanguage.Humanize()}");
        PluginLog.Debug($"Quest name: {questName}");
        PluginLog.Debug($"Quest message: {questMessage}");
#endif

        QuestPlate questPlate = this.FormatQuestPlate(questName, questMessage);
        QuestPlate foundQuestPlate = this.FindQuestPlate(questPlate);

        string translatedQuestName;
        string translatedQuestMessage;

        // If the quest is not saved
        if (foundQuestPlate == null)
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
        else
        { // if the data is already in the DB
          translatedQuestName = foundQuestPlate.TranslatedQuestName;
          translatedQuestMessage = foundQuestPlate.TranslatedQuestMessage;
#if DEBUG
          PluginLog.Debug($"From database - Name: {translatedQuestName}, Message: {translatedQuestMessage}");
#endif
        }
#if DEBUG
        PluginLog.Debug($"Using QuestPlate Replace - {translatedQuestName}: {translatedQuestMessage}");
#endif
        if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
        {
          translatedQuestName = this.RemoveDiacritics(translatedQuestName, this.SpecialCharsSupportedByGameFont);
          translatedQuestMessage = this.RemoveDiacritics(translatedQuestMessage, this.SpecialCharsSupportedByGameFont);
        }

        setupAtkValues[5].SetManagedString(translatedQuestName);
        setupAtkValues[12].SetManagedString(translatedQuestMessage);
      }
      catch (Exception e)
      {
        PluginLog.Error("Exception: " + e.StackTrace);
      }
    }
  }
}
