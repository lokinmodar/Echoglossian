// <copyright file="DbOperations.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Linq;

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.EFCoreSqlite.Models.Journal;
using Microsoft.EntityFrameworkCore;

namespace Echoglossian
{
  public partial class Echoglossian
  {
#if DEBUG
    private static readonly string V =
      $"{Directory.GetParent(Assembly.GetExecutingAssembly().Location)}{Path.DirectorySeparatorChar}";

    public string DbOperationsLogPath = V;
#endif

    public TalkMessage FoundTalkMessage { get; set; }

    public ToastMessage FoundToastMessage { get; set; }

    public BattleTalkMessage FoundBattleTalkMessage { get; set; }

    public QuestPlate FoundQuestPlate { get; set; }

    public void CreateOrUseDb()
    {
      using var context = new EchoglossianDbContext();
      context.Database.MigrateAsync();
    }

    public bool FindTalkMessage(TalkMessage talkMessage)
    {
      using var context = new EchoglossianDbContext();
#if DEBUG
      using StreamWriter logStream = new(this.DbOperationsLogPath + "DbFindTalkOperationsLog.txt", append: true);
#endif
      try
      {
#if DEBUG
        logStream.WriteLineAsync($"Before Talk Messages table query: {talkMessage}");
#endif

        var existingTalkMessage =
          context.TalkMessage.Where(t =>
            t.OriginalTalkMessage == talkMessage.OriginalTalkMessage &&
            t.TranslationLang == talkMessage.TranslationLang);
        var localFoundTalkMessage = existingTalkMessage.FirstOrDefault();
#if DEBUG
        logStream.WriteLineAsync($"After Talk Messages table query: {localFoundTalkMessage}");
#endif
        if (existingTalkMessage.FirstOrDefault() == null ||
            localFoundTalkMessage?.OriginalTalkMessage != talkMessage.OriginalTalkMessage)
        {
          this.FoundTalkMessage = talkMessage;
          return false;
        }

        this.FoundTalkMessage = localFoundTalkMessage;
        return true;
      }
      catch (Exception e)
      {
#if DEBUG
        logStream.WriteLineAsync($"Query operation error: {e}");
#endif
        return false;
      }
    }

    public bool FindToastMessage(ToastMessage toastMessage)
    {
      using var context = new EchoglossianDbContext();
#if DEBUG
      using StreamWriter logStream = new(this.DbOperationsLogPath + "DbFindToastOperationsLog.txt", append: true);
#endif
      try
      {
#if DEBUG
        logStream.WriteLineAsync($"Before Toast Messages table query: {toastMessage}");
#endif

        var existingToastMessage =
          context.ToastMessage.Where(t => t.OriginalToastMessage == toastMessage.OriginalToastMessage);

        var localFoundToastMessage = existingToastMessage.FirstOrDefault();
#if DEBUG
        logStream.WriteLineAsync($"After Toast Messages table query: {localFoundToastMessage}");
#endif
        if (existingToastMessage.FirstOrDefault() == null ||
            localFoundToastMessage?.OriginalToastMessage != toastMessage.OriginalToastMessage)
        {
          this.FoundToastMessage = toastMessage;
          return false;
        }

        this.FoundToastMessage = localFoundToastMessage;
        return true;
      }
      catch (Exception e)
      {
#if DEBUG
        logStream.WriteLineAsync($"Query operation error: {e}");
#endif
        return false;
      }
    }

    public bool FindBattleTalkMessage(BattleTalkMessage battleTalkMessage)
    {
      using var context = new EchoglossianDbContext();
#if DEBUG
      using StreamWriter logStream = new(this.DbOperationsLogPath + "DbFindBattleTalkOperationsLog.txt", append: true);
#endif
      try
      {
#if DEBUG
        logStream.WriteLineAsync($"Before BattleTalk Messages table query: {battleTalkMessage}");
#endif

        var existingBattleTalkMessage =
          context.BattleTalkMessage.Where(t =>
            t.OriginalBattleTalkMessage == battleTalkMessage.OriginalBattleTalkMessage);

        var localFoundBattleTalkMessage = existingBattleTalkMessage.FirstOrDefault();
#if DEBUG
        logStream.WriteLineAsync($"After BattleTalk Messages table query: {localFoundBattleTalkMessage}");
#endif
        if (existingBattleTalkMessage.FirstOrDefault() == null ||
            localFoundBattleTalkMessage?.OriginalBattleTalkMessage != battleTalkMessage.OriginalBattleTalkMessage)
        {
          this.FoundBattleTalkMessage = battleTalkMessage;
          return false;
        }

        this.FoundBattleTalkMessage = localFoundBattleTalkMessage;
        return true;
      }
      catch (Exception e)
      {
#if DEBUG
        logStream.WriteLineAsync($"Query operation error: {e}");
#endif
        return false;
      }
    }

    public string InsertTalkData(TalkMessage talkMessage)
    {
      using var context = new EchoglossianDbContext();
#if DEBUG
      using StreamWriter logStream = new(this.DbOperationsLogPath + "DbInsertTalkOperationsLog.txt", append: true);
#endif
      try
      {
#if DEBUG
        if (!this.configuration.UseImGui)
        {
          logStream.WriteLineAsync($"Before SaveChanges: {talkMessage}");
        }
#endif

        // 1. Attach an entity to context with Added EntityState
        context.TalkMessage.Attach(talkMessage);
#if DEBUG
        if (!this.configuration.UseImGui)
        {
          logStream.WriteLineAsync($"Inside Context: {context.TalkMessage.Local}");
        }
#endif

        // or the followings are also valid
        // context.Students.Add(std);
        // context.Entry<Student>(std).State = EntityState.Added;
        // context.Attach<Student>(std);

        // 2. Calling SaveChanges to insert a new record into table
        context.SaveChangesAsync();
#if DEBUG
        if (!this.configuration.UseImGui)
        {
          logStream.WriteLineAsync($"After 'SaveChanges': {context.TalkMessage.Local}");
        }
#endif
        return "Data inserted to TalkMessages table.";
      }
      catch (Exception e)
      {
        return $"ErrorSavingData: {e}";
      }
    }

    public string InsertBattleTalkData(BattleTalkMessage battleTalkMessage)
    {
      using var context = new EchoglossianDbContext();
#if DEBUG
      using StreamWriter logStream = new(this.DbOperationsLogPath + "DbInsertBattleTalkOperationsLog.txt",
        append: true);
#endif
      try
      {
        // 1. Attach an entity to context with Added EntityState
        context.BattleTalkMessage.Attach(battleTalkMessage);
#if DEBUG
        if (!this.configuration.UseImGui)
        {
          logStream.WriteLineAsync($"Inside Context: {context.BattleTalkMessage.Local}");
        }
#endif

        // or the followings are also valid
        // context.Students.Add(std);
        // context.Entry<Student>(std).State = EntityState.Added;
        // context.Attach<Student>(std);

        // 2. Calling SaveChanges to insert a new record into Students table
        context.SaveChangesAsync();
#if DEBUG
        if (!this.configuration.UseImGui)
        {
          logStream.WriteLineAsync($"After 'SaveChanges': {context.BattleTalkMessage.Local}");
        }
#endif
        return "Data inserted to BattleTalkMessages table.";
      }
      catch (Exception e)
      {
        return $"ErrorSavingData: {e}";
      }
    }

    public string InsertToastMessageData(ToastMessage toastMessage)
    {
      using var context = new EchoglossianDbContext();
#if DEBUG
      using StreamWriter logStream = new(this.DbOperationsLogPath + "DbInsertToastOperationsLog.txt", append: true);
#endif
      try
      {
        // 1. Attach an entity to context with Added EntityState
        context.ToastMessage.Attach(toastMessage);
#if DEBUG
        if (!this.configuration.UseImGui)
        {
          logStream.WriteLineAsync($"Inside Context: {context.ToastMessage.Local}");
        }
#endif

        // or the followings are also valid
        // context.Students.Add(std);
        // context.Entry<Student>(std).State = EntityState.Added;
        // context.Attach<Student>(std);

        // 2. Calling SaveChanges to insert a new record into Students table
        context.SaveChangesAsync();
#if DEBUG
        if (!this.configuration.UseImGui)
        {
          logStream.WriteLineAsync($"After 'SaveChanges': {context.ToastMessage.Local}");
        }
#endif
        return "Data inserted to ToastMessages table.";
      }
      catch (Exception e)
      {
        return $"ErrorSavingData: {e}";
      }
    }
  }
}