// <copyright file="EntitiesHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;

using Dalamud.Logging;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private TalkMessage FormatTalkMessage(string sender, string text)
    {
#if DEBUG
      PluginLog.LogWarning("Formatting node texts into TalkMessage");
      PluginLog.LogVerbose($"Client language: {this.clientLanguage}");
#endif
      return new TalkMessage(
        sender,
        text,
        ConvertClientLanguageToLangCode(this.clientLanguage),
        LangIdentify(sender),
        string.Empty,
        string.Empty,
        this.LanguagesDictionary[this.configuration.Lang].Code,
        this.configuration.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now);
    }

    private BattleTalkMessage FormatBattleTalkMessage(string sender, string text)
    {
      return new BattleTalkMessage(
        sender,
        text,
        ConvertClientLanguageToLangCode(this.clientLanguage),
        LangIdentify(sender),
        string.Empty,
        string.Empty,
        this.LanguagesDictionary[this.configuration.Lang].Code,
        this.configuration.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now);
    }

    private ToastMessage FormatToastMessage(string type, string text)
    {
      return new ToastMessage(
        type,
        text,
        ConvertClientLanguageToLangCode(this.clientLanguage),
        string.Empty,
        this.LanguagesDictionary[this.configuration.Lang].Code,
        this.configuration.ChosenTransEngine,
        DateTime.Now,
        DateTime.Now);
    }
  }
}