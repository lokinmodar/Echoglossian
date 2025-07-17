// <copyright file="UiAreaMapHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Echoglossian;

using ValueType = ValueType;

public partial class Echoglossian
{
    private unsafe void UiAreaMapHandler(AddonEvent type, AddonArgs args)
    {
#if DEBUG
        PluginLog.Debug(
            $"UiAreaMapHandler AddonEvent: {type} {args.AddonName}");
#endif
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
            if (setupAtkValues[142].Type != ValueType.String ||
                setupAtkValues[142].String.ToString() == string.Empty)
            {
                return;
            }

            var questNameText = MemoryHelper.ReadSeStringAsString(
                out _,
                (nint)setupAtkValues[142].String.Value);
            if (questNameText == string.Empty)
            {
                return;
            }

            var questPlate = this.FormatQuestPlate(questNameText, string.Empty);
            var foundQuestPlate = this.FindQuestPlateByName(questPlate);
            if (foundQuestPlate != null)
            {
#if DEBUG
                PluginLog.Debug(
                    $"Name from database: {questNameText} -> {foundQuestPlate.TranslatedQuestName}");
#endif
                setupAtkValues[142]
                    .SetManagedString(foundQuestPlate.TranslatedQuestName);
            }
            else
            {
                var translatedNameText = this.Translate(questNameText);
#if DEBUG
                PluginLog.Debug(
                    $"Name translated: {questNameText} -> {translatedNameText}");
#endif
                QuestPlate translatedQuestPlate = new(
                    questNameText,
                    string.Empty,
                    ClientStateInterface.ClientLanguage.Humanize(),
                    translatedNameText,
                    string.Empty,
                    string.Empty,
                    LangDict[LanguageInt].Code,
                    this.configuration.ChosenTransEngine,
                    DateTime.Now,
                    DateTime.Now);

                var result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
                PluginLog.Debug(
                    $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
                setupAtkValues[142].SetManagedString(translatedNameText);
            }
        }
        catch (Exception e)
        {
            PluginLog.Error("Exception at UiAreaMapHandler: " + e);
        }
    }
}