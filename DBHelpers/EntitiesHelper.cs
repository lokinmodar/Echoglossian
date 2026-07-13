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
  /// <returns>
  ///     The formatted <see cref="TalkMessage" />, or <see langword="null" />
  ///     when the current source language cannot be resolved.
  /// </returns>
  public TalkMessage? FormatTalkMessage(string sender, string text)
  {
    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      return null;
    }

    return new TalkMessage(
        sender,
        text,
        sourceLanguage.PersistenceCode,
        sourceLanguage.PersistenceCode,
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
  /// <returns>
  ///     The formatted <see cref="BattleTalkMessage" />, or
  ///     <see langword="null" /> when the current source language cannot be
  ///     resolved.
  /// </returns>
  public BattleTalkMessage? FormatBattleTalkMessage(string sender, string text)
  {
    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      return null;
    }

    return new BattleTalkMessage(
        sender,
        text,
        sourceLanguage.PersistenceCode,
        sourceLanguage.PersistenceCode,
        string.Empty,
        string.Empty,
        this.languagesDictionary[this.configuration.Lang].Code,
        this.configuration.ChosenTransEngine,
        rtlLangTranslationImageData: null,
        DateTime.Now,
        DateTime.Now);
  }

  /// <summary>
  ///     Formats a <see cref="ToastMessage" /> for the database.
  /// </summary>
  /// <param name="type">The toast type.</param>
  /// <param name="text">The original toast text.</param>
  /// <returns>
  ///     The formatted <see cref="ToastMessage" />, or <see langword="null" />
  ///     when the current source language cannot be resolved.
  /// </returns>
  public ToastMessage? FormatToastMessage(string type, string text)
  {
    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      return null;
    }

    return new ToastMessage(
        type,
        text,
        sourceLanguage.PersistenceCode,
        string.Empty,
        this.languagesDictionary[this.configuration.Lang].Code,
        this.configuration.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now);
  }

  /// <summary>
  ///     Formats a <see cref="QuestPlate" /> for the database.
  /// </summary>
  /// <param name="questName">The original quest name.</param>
  /// <param name="questMessage">The original quest message.</param>
  /// <returns>
  ///     The formatted <see cref="QuestPlate" />, or <see langword="null" />
  ///     when the current source language cannot be resolved.
  /// </returns>
  public QuestPlate? FormatQuestPlate(string questName, string questMessage)
  {
    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      return null;
    }

    return new QuestPlate(
        questName,
        questMessage,
        sourceLanguage.PersistenceCode,
        string.Empty,
        string.Empty,
        string.Empty,
        this.languagesDictionary[this.configuration.Lang].Code,
        this.configuration.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now,
        GetGameVersion());
  }

  /// <summary>
  ///     Formats a <see cref="TalkSubtitleMessage" /> for the database.
  /// </summary>
  /// <param name="text">The original subtitle text.</param>
  /// <returns>
  ///     The formatted <see cref="TalkSubtitleMessage" />, or
  ///     <see langword="null" /> when the current source language cannot be
  ///     resolved.
  /// </returns>
  public TalkSubtitleMessage? FormatTalkSubtitleMessage(string text)
  {
    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      return null;
    }

    return new TalkSubtitleMessage(
        text,
        sourceLanguage.PersistenceCode,
        string.Empty,
        this.languagesDictionary[this.configuration.Lang].Code,
        this.configuration.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now);
  }

  /// <summary>
  ///     Formats a <see cref="TextGimmickHintMessage" /> for the database.
  /// </summary>
  /// <param name="text">The original text gimmick hint message.</param>
  /// <returns>
  ///     The formatted <see cref="TextGimmickHintMessage" />, or
  ///     <see langword="null" /> when the current source language cannot be
  ///     resolved.
  /// </returns>
  public TextGimmickHintMessage? FormatTextGimmickHintMessage(string text)
  {
    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      return null;
    }

    return new TextGimmickHintMessage(
        text,
        sourceLanguage.PersistenceCode,
        string.Empty,
        this.languagesDictionary[this.configuration.Lang].Code,
        this.configuration.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now);
  }

  /// <summary>
  ///     Formats a <see cref="SelectString" /> for cutscene select-string storage.
  /// </summary>
  /// <param name="question">The original question/title text.</param>
  /// <param name="options">The original options rendered by the addon.</param>
  /// <returns>
  ///     The formatted <see cref="SelectString" />, or <see langword="null" />
  ///     when the current source language cannot be resolved.
  /// </returns>
  public SelectString? FormatCutSceneSelectString(
      string question,
      List<string> options)
  {
    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      return null;
    }

    return new SelectString(
        question,
        sourceLanguage.PersistenceCode,
        JsonConvert.SerializeObject(options),
        string.Empty,
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
