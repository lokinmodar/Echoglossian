// <copyright file="EntitiesHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.EFCoreSqlite.Models.Journal;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public TalkMessage FormatTalkMessage(string sender, string text)
    {
      return new TalkMessage(sender, text, ClientStateInterface.ClientLanguage.Humanize(), sender, string.Empty, string.Empty,
        this.languagesDictionary[this.configuration.Lang].Code, this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
    }

    public BattleTalkMessage FormatBattleTalkMessage(string sender, string text)
    {
      return new BattleTalkMessage(sender, text, ClientStateInterface.ClientLanguage.Humanize(), sender, string.Empty, string.Empty,
        this.languagesDictionary[this.configuration.Lang].Code, this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
    }

    public ToastMessage FormatToastMessage(string type, string text)
    {
      return new ToastMessage(type, text, ClientStateInterface.ClientLanguage.Humanize(), string.Empty,
        this.languagesDictionary[this.configuration.Lang].Code, this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
    }

    public QuestPlate FormatQuestPlate(string questName, string questMessage)
    {
      return new QuestPlate(questName, questMessage, ClientStateInterface.ClientLanguage.Humanize(), string.Empty, string.Empty, string.Empty,
        this.languagesDictionary[this.configuration.Lang].Code, this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
    }

    public TalkSubtitleMessage FormatTalkSubtitleMessage(string text)
    {
      return new TalkSubtitleMessage(text, ClientStateInterface.ClientLanguage.Humanize(), string.Empty,
               this.languagesDictionary[this.configuration.Lang].Code, this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
    }
  }
}
