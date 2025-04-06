// <copyright file="UiScenarioTreeHandler.cs" company="lokinmodar">
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
    private unsafe void TranslateQuestOnScenarioTree(AtkValue* setupAtkValues, int valueIndex)
    {
      if (setupAtkValues[valueIndex].Type != FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String || setupAtkValues[valueIndex].String == null)
      {
        return;
      }

      var questNameText = MemoryHelper.ReadSeStringAsString(out _, (nint)setupAtkValues[valueIndex].String.Value);
      if (questNameText == null || questNameText.Length == 0)
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
        var translatedQuestName = foundQuestPlate.TranslatedQuestName;

        if (this.configuration.RemoveDiacriticsWhenUsingReplacementQuest)
        {
          translatedQuestName = this.RemoveDiacritics(translatedQuestName, this.SpecialCharsSupportedByGameFont);
        }

        setupAtkValues[valueIndex].SetManagedString(translatedQuestName);
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

        setupAtkValues[valueIndex].SetManagedString(translatedNameText);
      }
    }

    private unsafe void UiScenarioTreeHandler(AddonEvent type, AddonArgs args)
    {
      // PluginLog.Debug($"UiScenarioTreeHandler AddonEvent: {type} {args.AddonName}");
      if (!this.configuration.TranslateJournal)
      {
        return;
      }

      if (args is not AddonRefreshArgs setupArgs)
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
        // Translate MSQ
        this.TranslateQuestOnScenarioTree(setupAtkValues, 7);

        // Translate SubQuest
        this.TranslateQuestOnScenarioTree(setupAtkValues, 2);
      }
      catch (Exception e)
      {
        PluginLog.Error("Exception at UiScenarioTreeHandler: " + e.StackTrace);
      }
    }
  }
}
