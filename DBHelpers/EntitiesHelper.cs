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
        rtlLangTranslationImageData: null,
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
        rtlLangTranslationImageData: null,
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

  /// <summary>
  /// Formats a <see cref="StringArrayDatas" />. for database storage.
  /// </summary>
  /// <param name="type"> The type of the string array (e.g., "AtkValue", "StringArray").</param>
  /// <param name="size"> The size of the string array.</param>
  /// <param name="rawData"> The raw data byte array.</param>
  /// <param name="formattedRawData"> The formatted raw data string.</param>
  /// <param name="originalLang"> The original language code.</param>
  /// <param name="originalStrings"> The original strings.</param>
  /// <param name="translationLang"> The translation language code.</param>
  /// <param name="translatedStrings"> The translated strings.</param>
  /// <param name="translatedStringsWithPayloads"> The translated strings with payloads.</param>
  /// <param name="translationEngine"> The translation engine ID.</param>
  /// <param name="gameVersion"> The game version.</param>
  /// <returns>A formatted <see cref="StringArrayDatas" /> for database storage.</returns>
  public StringArrayDatas FormatStringArrayDatas(string type, int size, byte[] rawData, string formattedRawData, string originalLang, string originalStrings, string translationLang, string translatedStrings, string translatedStringsWithPayloads, int translationEngine, string gameVersion)
  {
    return new StringArrayDatas(
        type,
        size,
        rawData,
        formattedRawData,
        originalLang,
        originalStrings,
        translationLang,
        translatedStrings,
        translatedStringsWithPayloads,
        translationEngine,
        GetGameVersion(),
        DateTime.Now,
        DateTime.Now);
  }
}