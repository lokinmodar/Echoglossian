// <copyright file="EntitiesHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

public partial class Echoglossian
{
    /// <summary>
    ///     Formats a <see cref="TalkMessage" /> for the database.
    /// </summary>
    /// <param name="sender">Message sender name.</param>
    /// <param name="text">Message text.</param>
    /// <returns>Returns <see cref="TalkMessage" />.</returns>
    public TalkMessage FormatTalkMessage(string sender, string text)
    {
        return new TalkMessage(
            sender,
            text,
            ClientStateInterface.ClientLanguage.Humanize(),
            ClientStateInterface.ClientLanguage.Humanize(),
            string.Empty,
            string.Empty,
            this.languagesDictionary[this.configuration.Lang].Code,
            this.configuration.ChosenTransEngine,
            DateTime.Now,
            DateTime.Now);
    }

    /// <summary>
    ///     Formats a <see cref="BattleTalkMessage" /> for the database.
    /// </summary>
    /// <param name="sender">Message sender name.</param>
    /// <param name="text">Message text.</param>
    /// <returns>Returns <see cref="BattleTalkMessage" />.</returns>
    public BattleTalkMessage FormatBattleTalkMessage(string sender, string text)
    {
        return new BattleTalkMessage(
            sender,
            text,
            ClientStateInterface.ClientLanguage.Humanize(),
            ClientStateInterface.ClientLanguage.Humanize(),
            string.Empty,
            string.Empty,
            this.languagesDictionary[this.configuration.Lang].Code,
            this.configuration.ChosenTransEngine,
            DateTime.Now,
            DateTime.Now);
    }

    public ToastMessage FormatToastMessage(string type, string text)
    {
        return new ToastMessage(
            type,
            text,
            ClientStateInterface.ClientLanguage.Humanize(),
            string.Empty,
            this.languagesDictionary[this.configuration.Lang].Code,
            this.configuration.ChosenTransEngine,
            DateTime.Now,
            DateTime.Now);
    }

    public QuestPlate FormatQuestPlate(string questName, string questMessage)
    {
        return new QuestPlate(
            questName,
            questMessage,
            ClientStateInterface.ClientLanguage.Humanize(),
            string.Empty,
            string.Empty,
            string.Empty,
            this.languagesDictionary[this.configuration.Lang].Code,
            this.configuration.ChosenTransEngine,
            DateTime.Now,
            DateTime.Now);
    }

    public TalkSubtitleMessage FormatTalkSubtitleMessage(string text)
    {
        return new TalkSubtitleMessage(
            text,
            ClientStateInterface.ClientLanguage.Humanize(),
            string.Empty,
            this.languagesDictionary[this.configuration.Lang].Code,
            this.configuration.ChosenTransEngine,
            DateTime.Now,
            DateTime.Now);
    }

    /// <summary>
    ///     Formats a <see cref="GameWindow" />.
    /// </summary>
    /// <param name="windowAddonName"></param>
    /// <param name="originalWindowStrings"></param>
    /// <param name="originalWindowStringsLang"></param>
    /// <param name="translatedWindowStrings"></param>
    /// <param name="translationLang"></param>
    /// <param name="translationEngine"></param>
    /// <returns>Returns a formatted <see cref="GameWindow" />.</returns>
    public GameWindow FormatGameWindow(
        string windowAddonName,
        string originalWindowStrings,
        string originalWindowStringsLang,
        string translatedWindowStrings,
        string translationLang,
        int? translationEngine)
    {
        return new GameWindow(
            windowAddonName,
            originalWindowStrings,
            originalWindowStringsLang,
            translatedWindowStrings,
            translationLang,
            translationEngine,
            GetGameVersion(),
            DateTime.Now,
            DateTime.Now);
    }
}