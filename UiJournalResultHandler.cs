// <copyright file="UiJournalResultHandler.cs" company="lokinmodar">
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
    private unsafe void UiJournalResultHandler(AddonEvent type, AddonArgs args)
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
        if (setupAtkValues[1].Type != FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String || setupAtkValues[1].String == null)
        {
          return;
        }

        var questNameText = MemoryHelper.ReadSeStringAsString(out _, (nint)setupAtkValues[1].String.Value);
        if (questNameText == string.Empty)
        {
          return;
        }

        QuestPlate questPlate = this.FormatQuestPlate(questNameText, string.Empty);
        QuestPlate foundQuestPlate = this.FindQuestPlateByName(questPlate);
        if (foundQuestPlate != null)
        {
#if DEBUG
          PluginLog.Debug($"Name from database: {questNameText} -> {foundQuestPlate.TranslatedQuestName}");
#endif
          if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
          {
            foundQuestPlate.TranslatedQuestName = this.RemoveDiacritics(foundQuestPlate.TranslatedQuestName, this.SpecialCharsSupportedByGameFont);
          }

          setupAtkValues[1].SetManagedString(foundQuestPlate.TranslatedQuestName);
        }
        else
        {
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

          setupAtkValues[1].SetManagedString(translatedNameText);
        }
      }
      catch (Exception e)
      {
        PluginLog.Error("UiJournalResultHandler Exception: " + e.StackTrace);
      }
    }
  }
}
