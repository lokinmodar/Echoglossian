// <copyright file="DbOperations.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Reflection;

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Microsoft.EntityFrameworkCore;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private static readonly string V = $"{Directory.GetParent(Assembly.GetExecutingAssembly().Location)}{Path.DirectorySeparatorChar}dantelog.txt";
    public string DbPath = V;

    public TalkMessage FoundTalkMessage { get; set; }

    public void CreateOrUseDb()
    {
      using var context = new EchoglossianDbContext();
      context.Database.MigrateAsync();
    }

    public bool FindTalkMessage(TalkMessage talkMessage)
    {
      using var context = new EchoglossianDbContext();
      try
      {
        File.AppendAllLines(this.DbPath, new[] { "antes da consulta: ", talkMessage.ToString() });
        var existingTalkMessage =
          context.TalkMessage.Where(t =>
            t.OriginalTalkMessage == talkMessage.OriginalTalkMessage &&
            t.TranslationLang == talkMessage.TranslationLang);
        var localFoundTalkMessage = existingTalkMessage.FirstOrDefault();

        File.AppendAllLines(
            this.DbPath,
            new[] { "depois da consulta: ", localFoundTalkMessage?.ToString() });
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
        File.AppendAllLinesAsync(this.DbPath, new[] { "Query operation error:", e.ToString() });
        return false;
      }
    }

    public ToastMessage FindToastMessage(ToastMessage toastMessage)
    {
      using var context = new EchoglossianDbContext();
      var existingToastMessage =
        context.ToastMessage.Where(t => t.OriginalToastMessage == toastMessage.OriginalToastMessage);
      return existingToastMessage.First().TranslatedToastMessage != null ? existingToastMessage.First() : toastMessage;
    }

    public BattleTalkMessage FindBattleTalkMessage(BattleTalkMessage battleTalkMessage)
    {
      using var context = new EchoglossianDbContext();
      var existingBattleTalkMessage =
        context.BattleTalkMessage.Where(t => t.OriginalBattleTalkMessage == battleTalkMessage.OriginalBattleTalkMessage);
      return existingBattleTalkMessage.First().TranslatedBattleTalkMessage != string.Empty ? existingBattleTalkMessage.First() : battleTalkMessage;
    }

    public string InsertTalkData(TalkMessage talkMessage)
    {
      using var context = new EchoglossianDbContext();
      try
      {
        File.AppendAllLines(this.DbPath, new[] { "antes de inserir:", talkMessage.ToString() });

        // 1. Attach an entity to context with Added EntityState
        context.TalkMessage.Attach(talkMessage);

        File.AppendAllLines(this.DbPath, new[] { "dentro do context:", context.TalkMessage.Local.ToString() });

        // or the followings are also valid
        // context.Students.Add(std);
        // context.Entry<Student>(std).State = EntityState.Added;
        // context.Attach<Student>(std);

        // 2. Calling SaveChanges to insert a new record into table
        context.SaveChanges();
        File.AppendAllLines(this.DbPath, new[] { "depois do save changes:", context.TalkMessage.Local.ToString() });
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
      try
      {
        // 1. Attach an entity to context with Added EntityState
        context.Add(battleTalkMessage);

        // or the followings are also valid
        // context.Students.Add(std);
        // context.Entry<Student>(std).State = EntityState.Added;
        // context.Attach<Student>(std);

        // 2. Calling SaveChanges to insert a new record into Students table
        context.SaveChanges();
        return "Data inserted to BattleTalkMessages table.";
      }
      catch (Exception e)
      {
        return $"Error: {e.StackTrace}";
      }
    }

    public string InsertToastMessageData(ToastMessage toastMessage)
    {
      using var context = new EchoglossianDbContext();
      try
      {
        // 1. Attach an entity to context with Added EntityState
        context.Add(toastMessage);

        /* or the followings are also valid
         context.Students.Add(std);
         context.Entry<Student>(std).State = EntityState.Added;
         context.Attach<Student>(std); */

        // 2. Calling SaveChanges to insert a new record into Students table
        context.SaveChanges();
        return "Data inserted to ToastMessages table.";
      }
      catch (Exception e)
      {
        return $"Error: {e.StackTrace}";
      }
    }
  }
}