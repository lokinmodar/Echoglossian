// <copyright file="DbOperations.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Dalamud.Logging;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.EFCoreSqlite.Models.Journal;
using Microsoft.EntityFrameworkCore;

namespace Echoglossian
{
  public partial class Echoglossian
  {
#if DEBUG

#endif
    public TalkMessage FoundTalkMessage { get; set; }

    public ToastMessage FoundToastMessage { get; set; }

    public BattleTalkMessage FoundBattleTalkMessage { get; set; }

    public QuestPlate FoundQuestPlate { get; set; }

    public void CreateOrUseDb()
    {
      using var context = new EchoglossianDbContext(this.ConfigDir);
      context.Database.MigrateAsync();
    }

    public bool FindTalkMessage(TalkMessage talkMessage)
    {
      using var context = new EchoglossianDbContext(this.ConfigDir);
#if DEBUG
      using StreamWriter logStream = new($"{this.ConfigDir}DbFindTalkOperationsLog.txt", append: true);
#endif
      try
      {
#if DEBUG
        PluginLog.Error(this.ConfigDir);
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
      
#if DEBUG
      using StreamWriter logStream = new($"{this.ConfigDir}DbFindToastOperationsLog.txt", append: true);
#endif
      try
      {
        var cache = this.ErrorToastsCache;
        if (cache.Count == 0 || cache == null)
        {
          this.LoadAllErrorToasts();
          cache = this.ErrorToastsCache;

          if (cache.Count == 0 || cache == null)
          {
            return false;
          }
        }

#if DEBUG
        logStream.WriteLineAsync($"Before Toast Messages table query: {toastMessage}");
#endif
        var existingToastMessage =
          cache.Where(t => t.OriginalToastMessage == toastMessage.OriginalToastMessage &&
                                          t.TranslationLang == toastMessage.TranslationLang &&
                                          t.ToastType == toastMessage.ToastType);

        var localFoundToastMessage = existingToastMessage.SingleOrDefault();
#if DEBUG
        logStream.WriteLineAsync($"After Toast Messages table query: {localFoundToastMessage}");
#endif
        if (localFoundToastMessage == null ||
            localFoundToastMessage.OriginalToastMessage != toastMessage.OriginalToastMessage)
        {
          this.FoundToastMessage = null;
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
      using var context = new EchoglossianDbContext(this.ConfigDir);
#if DEBUG
      using StreamWriter logStream = new($"{this.ConfigDir}DbFindBattleTalkOperationsLog.txt", append: true);
#endif
      try
      {
#if DEBUG
        logStream.WriteLineAsync($"Before BattleTalk Messages table query: {battleTalkMessage}");
#endif

        var existingBattleTalkMessage =
          context.BattleTalkMessage.Where(t =>
            t.OriginalBattleTalkMessage == battleTalkMessage.OriginalBattleTalkMessage &&
            t.TranslationLang == battleTalkMessage.TranslationLang);

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
      using var context = new EchoglossianDbContext(this.ConfigDir);
#if DEBUG
      using StreamWriter logStream = new($"{this.ConfigDir}DbInsertTalkOperationsLog.txt", append: true);
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
      using var context = new EchoglossianDbContext(this.ConfigDir);
#if DEBUG
      using StreamWriter logStream = new($"{this.ConfigDir}DbInsertBattleTalkOperationsLog.txt", append: true);
#endif
      try
      {

        context.BattleTalkMessage.Attach(battleTalkMessage);
#if DEBUG
        if (!this.configuration.UseImGui)
        {
          logStream.WriteLineAsync($"Inside Context: {context.BattleTalkMessage.Local}");
        }
#endif


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
      using var context = new EchoglossianDbContext(this.ConfigDir);
#if DEBUG
      using StreamWriter logStream = new($"{this.ConfigDir}DbInsertToastOperationsLog.txt", append: true);
#endif
      try
      {
        bool isInThere;
        if (this.ErrorToastsCache.Count > 0 && this.ErrorToastsCache != null)
        {
#if DEBUG
          foreach (var t in this.ErrorToastsCache)
          {
            PluginLog.LogError($"{this.ErrorToastsCache.GetEnumerator().Current} :{t}");
          }
#endif
          isInThere = this.ErrorToastsCache.Any(t => toastMessage.ToastType == t.ToastType && toastMessage.TranslationLang == t.TranslationLang &&
                                                     toastMessage.OriginalToastMessage == t.OriginalToastMessage);
        }
        else
        {
          isInThere = false;
        }

        if (isInThere)
        {
          return "Data already in the Db.";
        }

        context.ToastMessage.Attach(toastMessage);
#if DEBUG
        logStream.WriteLineAsync($"Inside Context: {context.ToastMessage.Local}");
#endif

        // or the followings are also valid
        // context.Students.Add(std);
        // context.Entry<Student>(std).State = EntityState.Added;
        // context.Attach<Student>(std);

        // 2. Calling SaveChanges to insert a new record into Students table
        context.SaveChangesAsync();
#if DEBUG
        logStream.WriteLineAsync($"After 'SaveChanges': {context.ToastMessage.Local}");
#endif

        this.LoadAllErrorToasts();

        return "Data inserted to ToastMessages table.";

      }
      catch (Exception e)
      {
        return $"ErrorSavingData: {e}";
      }
    }


    public void LoadAllErrorToasts()
    {
      using var context = new EchoglossianDbContext(this.ConfigDir);
      this.ErrorToastsCache = new List<ToastMessage>();
#if DEBUG
      using StreamWriter logStream = new($"{this.ConfigDir}DbErrorToastListQueryOperationsLog.txt", append: true);
#endif
      try
      {
        var existingToastMessages =
          context.ToastMessage
            .Where(t => t.ToastType == "Error");

        foreach (ToastMessage t in existingToastMessages)
        {
          this.ErrorToastsCache.Add(t);
        }
#if DEBUG
        logStream.WriteLineAsync($"After Toast Messages table query: {this.ErrorToastsCache.ToArray()}");
#endif
      }
      catch (Exception e)
      {
#if DEBUG
        logStream.WriteLineAsync($"Query operation error: {e}");
        PluginLog.LogWarning("Could not find any Error Toasts in Database");
#endif
      }

    }
  }
}