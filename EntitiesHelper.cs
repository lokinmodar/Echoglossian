// <copyright file="EntitiesHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public TalkMessage FormatTalkMessage(string sender, string text)
    {
      return new TalkMessage(sender, text, LangIdentify(text), LangIdentify(sender), string.Empty, string.Empty,
        this.LanguagesDictionary[this.configuration.Lang].Code, this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
    }

    public BattleTalkMessage FormatBattleTalkMessage(string sender, string text)
    {
      return new BattleTalkMessage(sender, text, LangIdentify(text), LangIdentify(sender), string.Empty, string.Empty,
        this.LanguagesDictionary[this.configuration.Lang].Code, this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
    }

    public ToastMessage FormatToastMessage(string type, string text)
    {
      return new ToastMessage(type, text, LangIdentify(text), string.Empty,
        this.LanguagesDictionary[this.configuration.Lang].Code, this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
    }
  }
}
