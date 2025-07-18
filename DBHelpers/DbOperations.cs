// <copyright file="DbOperations.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

public partial class Echoglossian
{
  public static TalkMessage? FoundTalkMessage { get; set; }

  public ToastMessage? FoundToastMessage { get; set; }

  public static BattleTalkMessage? FoundBattleTalkMessage { get; set; }

  public static TalkSubtitleMessage? FoundTalkSubtitleMessage { get; set; }

  public static GameWindow? FoundGameWindow { get; set; }

  /// <summary>
  ///     Creates or uses the database, applying any pending migrations.
  /// </summary>
  public async void CreateOrUseDb()
  {
    using (var context = new EchoglossianDbContext(this.configDir))
    {
      PluginLog.Debug($"Config dir path: {this.configDir}");
      try
      {
        PluginLog.Debug($"Config dir path: {this.configDir}");

        var pendingMigrations =
            await context.Database.GetPendingMigrationsAsync();

        if (pendingMigrations.Any())
        {
          PluginLog.Debug(
              $"Pending migrations: {pendingMigrations.Count()}");
          await context.Database.MigrateAsync();
        }

        var lastAppliedMigration =
            (await context.Database.GetAppliedMigrationsAsync()).Last();

        PluginLog.Debug(
            $"Last applied migration: {lastAppliedMigration}");
      }
      catch (Exception e)
      {
        PluginLog.Error($"Error creating or using Db: {e}");
      }
      finally
      {
        PluginLog.Debug("Db created or used successfully");
      }
    }
  }

  /// <summary>
  ///     Finds and returns a TalkMessage from the database.
  /// </summary>
  /// <param name="talkMessage">TalkMessage to be found on the Database.</param>
  /// <returns>The found <see cref="TalkMessage" />.</returns>
  public TalkMessage? FindAndReturnTalkMessage(TalkMessage talkMessage)
  {
    using var context = new EchoglossianDbContext(
        PluginInterface.GetPluginConfigDirectory() +
        Path.DirectorySeparatorChar);

    var pluginConfig = PluginInterface.GetPluginConfig() as Config;

    try
    {
      var existingTalkMessage = context.TalkMessage.Where(t =>
          t.SenderName == talkMessage.SenderName &&
          t.OriginalTalkMessage == talkMessage.OriginalTalkMessage &&
          t.TranslationLang == talkMessage.TranslationLang);
      if (pluginConfig?.TranslateAlreadyTranslatedTexts == true)
      {
        existingTalkMessage = existingTalkMessage.Where(t =>
            t.TranslationEngine == talkMessage.TranslationEngine);
      }

      var localFoundTalkMessage = existingTalkMessage?.FirstOrDefault();
      if (existingTalkMessage?.FirstOrDefault() == null ||
          localFoundTalkMessage?.OriginalTalkMessage !=
          talkMessage.OriginalTalkMessage)
      {
        return null;
      }

      return localFoundTalkMessage;
    }
    catch (Exception e)
    {
      return null;
    }
  }

  /*    public static bool FindTalkMessage(TalkMessage talkMessage)
      {
        using EchoglossianDbContext context = new EchoglossianDbContext(PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar);

        PluginLog.Debug($"TalkMessage to be found in DB: {talkMessage}");

        var pluginConfig = PluginInterface.GetPluginConfig() as Config;

        try
        {
          IQueryable<TalkMessage> existingTalkMessage =
            context.TalkMessage.Where(t =>
              t.SenderName == talkMessage.SenderName &&
              t.OriginalTalkMessage == talkMessage.OriginalTalkMessage &&
              t.TranslationLang == talkMessage.TranslationLang);
          if (pluginConfig?.TranslateAlreadyTranslatedTexts == true)
          {
            existingTalkMessage = existingTalkMessage.Where(t => t.TranslationEngine == talkMessage.TranslationEngine);
          }

          TalkMessage? localFoundTalkMessage = existingTalkMessage?.FirstOrDefault();
          if (existingTalkMessage?.FirstOrDefault() == null ||
              localFoundTalkMessage?.OriginalTalkMessage != talkMessage.OriginalTalkMessage)
          {
            FoundTalkMessage = talkMessage;
            return false;
          }

          FoundTalkMessage = localFoundTalkMessage;

          PluginLog.Debug($"FoundTalkMessage in DB: {FoundTalkMessage}");

          return true;
        }
        catch (Exception e)
        {
          return false;
        }
      }*/

  public bool FindToastMessage(ToastMessage toastMessage)
  {
    try
    {
      var cache = this.OtherToastsCache;
      if (cache == null || cache.Count == 0)
      {
        this.LoadAllOtherToasts();
        cache = this.OtherToastsCache;

        if (cache == null || cache.Count == 0)
        {
          return false;
        }
      }

      var existingToastMessage = cache.Where(t =>
          t.OriginalToastMessage == toastMessage.OriginalToastMessage &&
          t.TranslationLang == toastMessage.TranslationLang &&
          t.ToastType == toastMessage.ToastType);

      if (this.configuration.TranslateAlreadyTranslatedTexts)
      {
        existingToastMessage = existingToastMessage.Where(t =>
            t.TranslationEngine == toastMessage.TranslationEngine);
      }

      var localFoundToastMessage = existingToastMessage.FirstOrDefault();

      PluginLog.Debug($"localFoundToasMessage: {localFoundToastMessage}");

      if (localFoundToastMessage == null ||
          localFoundToastMessage.OriginalToastMessage !=
          toastMessage.OriginalToastMessage)
      {
        this.FoundToastMessage = null;
        return false;
      }

      this.FoundToastMessage = localFoundToastMessage;
      return true;
    }
    catch (Exception e)
    {
      PluginLog.Debug($"FindToastMessage exception {e}");
      return false;
    }
  }

  public bool FindErrorToastMessage(ToastMessage toastMessage)
  {
    try
    {
      var cache = this.ErrorToastsCache;
      if (cache == null || cache.Count == 0)
      {
        this.LoadAllErrorToasts();
        cache = this.ErrorToastsCache;

        if (cache == null || cache.Count == 0)
        {
          return false;
        }
      }

      var existingToastMessage = cache.Where(t =>
          t.OriginalToastMessage == toastMessage.OriginalToastMessage &&
          t.TranslationLang == toastMessage.TranslationLang &&
          t.ToastType == toastMessage.ToastType);

      if (this.configuration.TranslateAlreadyTranslatedTexts)
      {
        existingToastMessage = existingToastMessage.Where(t =>
            t.TranslationEngine == toastMessage.TranslationEngine);
      }

      var localFoundToastMessage = existingToastMessage.FirstOrDefault();

      if (localFoundToastMessage == null ||
          localFoundToastMessage.OriginalToastMessage !=
          toastMessage.OriginalToastMessage)
      {
        this.FoundToastMessage = null;
        return false;
      }

      this.FoundToastMessage = localFoundToastMessage;
      return true;
    }
    catch (Exception e)
    {
      PluginLog.Debug($"FindErrorToastMessage exception {e}");
      return false;
    }
  }

  /// <summary>
  ///     Finds and returns a BattleTalkMessage from the database.
  /// </summary>
  /// <param name="battleTalkMessage"></param>
  /// <returns></returns>
  public BattleTalkMessage? FindAndReturnBattleTalkMessage(
      BattleTalkMessage battleTalkMessage)
  {
    using var context = new EchoglossianDbContext(
        PluginInterface.GetPluginConfigDirectory() +
        Path.DirectorySeparatorChar);

    var pluginConfig = PluginInterface.GetPluginConfig() as Config;

    try
    {
      var existingBattleTalkMessage = context.BattleTalkMessage.Where(t =>
          t.SenderName == battleTalkMessage.SenderName &&
          t.OriginalBattleTalkMessage ==
          battleTalkMessage.OriginalBattleTalkMessage &&
          t.TranslationLang == battleTalkMessage.TranslationLang);

      if (pluginConfig?.TranslateAlreadyTranslatedTexts == true)
      {
        existingBattleTalkMessage = existingBattleTalkMessage.Where(t =>
            t.TranslationEngine == battleTalkMessage.TranslationEngine);
      }

      var localFoundBattleTalkMessage =
          existingBattleTalkMessage.FirstOrDefault();
      if (existingBattleTalkMessage.FirstOrDefault() == null ||
          localFoundBattleTalkMessage?.OriginalBattleTalkMessage !=
          battleTalkMessage.OriginalBattleTalkMessage)
      {
        return null;
      }

      FoundBattleTalkMessage = localFoundBattleTalkMessage;

      return localFoundBattleTalkMessage;
    }
    catch (Exception e)
    {
      PluginLog.Debug($"FindAndReturnBattleTalkMessage exception {e}");
      return null;
    }
  }

  /*    public static bool FindBattleTalkMessage(BattleTalkMessage battleTalkMessage)
      {
        using EchoglossianDbContext context = new EchoglossianDbContext(PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar);

        PluginLog.Debug($"BattleTalkMessage to be found in DB: {battleTalkMessage}");

        var pluginConfig = PluginInterface.GetPluginConfig() as Config;

        try
        {
          IQueryable<BattleTalkMessage> existingBattleTalkMessage =
            context.BattleTalkMessage.Where(t =>
              t.SenderName == battleTalkMessage.SenderName &&
              t.OriginalBattleTalkMessage == battleTalkMessage.OriginalBattleTalkMessage &&
              t.TranslationLang == battleTalkMessage.TranslationLang);

          if (pluginConfig?.TranslateAlreadyTranslatedTexts == true)
          {
            existingBattleTalkMessage = existingBattleTalkMessage.Where(t => t.TranslationEngine == battleTalkMessage.TranslationEngine);
          }

          BattleTalkMessage? localFoundBattleTalkMessage = existingBattleTalkMessage.FirstOrDefault();
          if (existingBattleTalkMessage.FirstOrDefault() == null ||
              localFoundBattleTalkMessage?.OriginalBattleTalkMessage != battleTalkMessage.OriginalBattleTalkMessage)
          {
            FoundBattleTalkMessage = battleTalkMessage;
            return false;
          }

          FoundBattleTalkMessage = localFoundBattleTalkMessage;

          PluginLog.Debug($"FoundBattleTalkMessage in DB: {FoundBattleTalkMessage}");
          return true;
        }
        catch (Exception e)
        {
          return false;
        }
      }*/

  /// <summary>
  ///     Finds and returns a QuestPlate from the database.
  /// </summary>
  /// <param name="questPlate"></param>
  /// <returns></returns>
  public QuestPlate? FindQuestPlate(QuestPlate questPlate)
  {
    using var context = new EchoglossianDbContext(this.configDir);
    try
    {
      var existingQuestPlate = context.QuestPlate.Where(t =>
          t.QuestName == questPlate.QuestName &&
          t.OriginalQuestMessage == questPlate.OriginalQuestMessage &&
          t.TranslationLang == questPlate.TranslationLang);

      if (this.configuration?.TranslateAlreadyTranslatedTexts == true)
      {
        existingQuestPlate = existingQuestPlate.Where(t =>
            t.TranslationEngine == questPlate.TranslationEngine);
      }

      var localFoundQuestPlate = existingQuestPlate.FirstOrDefault();
      if (localFoundQuestPlate == null ||
          localFoundQuestPlate.OriginalQuestMessage !=
          questPlate.OriginalQuestMessage)
      {
        return null;
      }

      localFoundQuestPlate.UpdateFieldsFromText();
      return localFoundQuestPlate;
    }
    catch (Exception e)
    {
      return null;
    }
  }

  /// <summary>
  ///   Finds a QuestPlate by its name and translation language.
  /// </summary>
  /// <param name="questPlate"></param>
  /// <returns></returns>
  public QuestPlate? FindQuestPlateByName(QuestPlate questPlate)
  {
    using var context = new EchoglossianDbContext(this.configDir);
    try
    {
      var existingQuestPlate = context.QuestPlate.Where(t =>
          t.QuestName == questPlate.QuestName && t.TranslationLang ==
          questPlate.TranslationLang);

      if (this.configuration?.TranslateAlreadyTranslatedTexts == true)
      {
        existingQuestPlate = existingQuestPlate.Where(t =>
            t.TranslationEngine == questPlate.TranslationEngine);
      }

      var localFoundQuestPlate = existingQuestPlate.FirstOrDefault();

      if (localFoundQuestPlate == null ||
          localFoundQuestPlate.QuestName != questPlate.QuestName)
      {
        return null;
      }

      localFoundQuestPlate.UpdateFieldsFromText();
      return localFoundQuestPlate;
    }
    catch (Exception e)
    {
      return null;
    }
  }

  public TalkSubtitleMessage? FindAndReturnTalkSubtitleMessage(
      TalkSubtitleMessage talkSubtitleMessage)
  {
    using var context = new EchoglossianDbContext(this.configDir);
    try
    {
      var existingTalkSubtitleMessage =
          context.TalkSubtitleMessage.Where(t =>
              t.OriginalTalkSubtitleMessage == talkSubtitleMessage
                  .OriginalTalkSubtitleMessage && t.TranslationLang ==
              talkSubtitleMessage.TranslationLang);

      if (this.configuration?.TranslateAlreadyTranslatedTexts == true)
      {
        existingTalkSubtitleMessage =
            existingTalkSubtitleMessage.Where(t =>
                t.TranslationEngine ==
                talkSubtitleMessage.TranslationEngine);
      }

      var localFoundTalkSubtitleMessage =
          existingTalkSubtitleMessage.FirstOrDefault();
      if (localFoundTalkSubtitleMessage == null ||
          localFoundTalkSubtitleMessage.OriginalTalkSubtitleMessage !=
          talkSubtitleMessage.OriginalTalkSubtitleMessage)
      {
        return null;
      }

      return localFoundTalkSubtitleMessage;
    }
    catch (Exception e)
    {
      PluginLog.Debug($"FindAndReturnTalkSubtitleMessage exception {e}");
      return null;
    }
  }

  /*    public static bool FindTalkSubtitleMessage(TalkSubtitleMessage talkSubtitleMessage)
      {
        using EchoglossianDbContext context = new EchoglossianDbContext(PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar);

        PluginLog.Debug($"TalkSubtitleMessage to be found in DB: {talkSubtitleMessage}");

        var pluginConfig = PluginInterface.GetPluginConfig() as Config;

        try
        {
          IQueryable<TalkSubtitleMessage> existingTalkSubtitleMessage =
            context.TalkSubtitleMessage.Where(t =>
                                t.OriginalTalkSubtitleMessage == talkSubtitleMessage.OriginalTalkSubtitleMessage &&
                                                           t.TranslationLang == talkSubtitleMessage.TranslationLang);

          if (pluginConfig?.TranslateAlreadyTranslatedTexts == true)
          {
            existingTalkSubtitleMessage = existingTalkSubtitleMessage.Where(t => t.TranslationEngine == talkSubtitleMessage.TranslationEngine);
          }

          TalkSubtitleMessage? localFoundTalkSubtitleMessage = existingTalkSubtitleMessage.FirstOrDefault();
          if (existingTalkSubtitleMessage.FirstOrDefault() == null ||
                                localFoundTalkSubtitleMessage?.OriginalTalkSubtitleMessage != talkSubtitleMessage.OriginalTalkSubtitleMessage)
          {
            FoundTalkSubtitleMessage = talkSubtitleMessage;
            return false;
          }

          FoundTalkSubtitleMessage = localFoundTalkSubtitleMessage;

          PluginLog.Debug($"FoundTalkSubtitleMessage in DB: {FoundTalkMessage}");
          return true;
        }
        catch (Exception e)
        {
          return false;
        }
      }*/

  public static GameWindow? FindAndReturnGameWindow(GameWindow gameWindow)
  {
    using var context = new EchoglossianDbContext(
        PluginInterface.GetPluginConfigDirectory() +
        Path.DirectorySeparatorChar);
    try
    {
      var existingGameWindow = context.GameWindow.Where(t =>
          t.WindowAddonName == gameWindow.WindowAddonName &&
          t.TranslationLang == gameWindow.TranslationLang);
      if (existingGameWindow.FirstOrDefault() == null)
      {
        return null;
      }

      var localFoundGameWindow = existingGameWindow.FirstOrDefault();
      if (localFoundGameWindow?.WindowAddonName !=
          gameWindow.WindowAddonName)
      {
        return null;
      }

      return localFoundGameWindow;
    }
    catch (Exception e)
    {
      PluginLog.Debug($"FindAndReturnGameWindow exception {e}");
      return null;
    }
  }

  public static async Task<string> InsertTalkData(TalkMessage talkMessage)
  {
    using var context = new EchoglossianDbContext(
        PluginInterface.GetPluginConfigDirectory() +
        Path.DirectorySeparatorChar);
#if DEBUG

    // using StreamWriter logStream = new($"{this.configDir}DbInsertTalkOperationsLog.txt", append: true);
    PluginLog.Debug($"TalkMessage to be saved in DB: {talkMessage}");
#endif

    var pluginConfig = PluginInterface.GetPluginConfig() as Config;

    try
    {
      if (!ShouldSaveToDB(talkMessage.TranslatedTalkMessage))
      {
        return "No data to save.";
      }

      if (pluginConfig?.CopyTranslationToClipboard == true)
      {
        ImGui.SetClipboardText(talkMessage.ToString());
      }

      context.TalkMessage.Add(talkMessage);

      await context.SaveChangesAsync();

      return "Data inserted to TalkMessages table.";
    }
    catch (Exception e)
    {
      PluginLog.Error($"DB Save Failed: {e.Message}\n{e.StackTrace}");
      return $"ErrorSavingData: {e}";
    }
  }

  public static string InsertBattleTalkData(
      BattleTalkMessage battleTalkMessage)
  {
    using var context = new EchoglossianDbContext(
        PluginInterface.GetPluginConfigDirectory() +
        Path.DirectorySeparatorChar);
    /*#if DEBUG
          using StreamWriter logStream = new($"{this.configDir}DbInsertBattleTalkOperationsLog.txt", append: true);
    #endif*/

    var pluginConfig = PluginInterface.GetPluginConfig() as Config;

    try
    {
      if (!ShouldSaveToDB(battleTalkMessage.TranslatedBattleTalkMessage))
      {
        return "No data to save.";
      }

      context.BattleTalkMessage.Attach(battleTalkMessage);

      if (pluginConfig?.CopyTranslationToClipboard == true)
      {
        ImGui.SetClipboardText(battleTalkMessage.ToString());
      }

      context.SaveChangesAsync();

      return "Data inserted to BattleTalkMessages table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  public static string InsertTalkSubtitleData(
      TalkSubtitleMessage talkSubtitleMessage)
  {
    using var context = new EchoglossianDbContext(
        PluginInterface.GetPluginConfigDirectory() +
        Path.DirectorySeparatorChar);
    /*#if DEBUG
     *            using StreamWriter logStream = new($"{this.configDir}DbInsertTalkSubtitleOperationsLog.txt", append: true);
     *                 #endif*/

    var pluginConfig = PluginInterface.GetPluginConfig() as Config;

    try
    {
      if (!ShouldSaveToDB(
              talkSubtitleMessage.TranslatedTalkSubtitleMessage))
      {
        return "No data to save.";
      }

      context.TalkSubtitleMessage.Attach(talkSubtitleMessage);

      if (pluginConfig?.CopyTranslationToClipboard == true)
      {
        ImGui.SetClipboardText(talkSubtitleMessage.ToString());
      }

      context.SaveChangesAsync();

      return "Data inserted to TalkSubtitleMessages table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  public string InsertErrorToastMessageData(ToastMessage toastMessage)
  {
    using var context = new EchoglossianDbContext(this.configDir);
    /*#if DEBUG
          using StreamWriter logStream = new($"{this.configDir}DbInsertToastOperationsLog.txt", append: true);
    #endif*/
    try
    {
      bool isInThere;
      if (this.ErrorToastsCache != null &&
          this.ErrorToastsCache.Count > 0)
      {
#if DEBUG
        PluginLog.Debug(
            $"Total ErrorToasts in cache: {this.ErrorToastsCache.Count}");
        /* foreach (ToastMessage t in this.ErrorToastsCache)
         {
           PluginLog.Debug($"{this.ErrorToastsCache.GetEnumerator().Current} :{t}");
         }*/
#endif
        isInThere = this.ErrorToastsCache.Exists(t =>
            toastMessage.ToastType == t.ToastType &&
            toastMessage.TranslationLang == t.TranslationLang &&
            toastMessage.OriginalToastMessage ==
            t.OriginalToastMessage && toastMessage.TranslationEngine ==
            t.TranslationEngine);
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

      context.SaveChangesAsync();

      this.LoadAllErrorToasts();

      return "Data inserted to ToastMessages table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  public string InsertOtherToastMessageData(ToastMessage toastMessage)
  {
    using var context = new EchoglossianDbContext(this.configDir);
    /*#if DEBUG
          using StreamWriter logStream = new($"{this.configDir}DbInsertToastOperationsLog.txt", append: true);
    #endif*/
    try
    {
      bool isInThere;
      if (this.OtherToastsCache != null &&
          this.OtherToastsCache.Count > 0)
      {
#if DEBUG
        PluginLog.Debug(
            $"Total ErrorToasts in cache: {this.OtherToastsCache.Count}");
        /* foreach (ToastMessage t in this.OtherToastsCache)
         {
           PluginLog.Debug($"{this.OtherToastsCache.GetEnumerator().Current} :{t}");
         }*/
#endif
        isInThere = this.OtherToastsCache.Exists(t =>
            toastMessage.ToastType == t.ToastType &&
            toastMessage.TranslationLang == t.TranslationLang &&
            toastMessage.OriginalToastMessage ==
            t.OriginalToastMessage && toastMessage.TranslationEngine ==
            t.TranslationEngine);
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

      context.SaveChangesAsync();

      this.LoadAllOtherToasts();

      return "Data inserted to ToastMessages table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  public string InsertQuestPlate(QuestPlate questPlate)
  {
    using var context = new EchoglossianDbContext(this.configDir);
    /*#if DEBUG
          using StreamWriter logStream = new($"{this.configDir}DbInsertQuestPlateOperationsLog.txt", append: true);
    #endif*/
    try
    {
      questPlate.UpdateFieldsAsText();
      context.QuestPlate.Attach(questPlate);
      /*#if DEBUG
              logStream.WriteLineAsync($"Inside Context: {context.QuestPlate.Local}");
      #endif*/
      if (this.configuration.CopyTranslationToClipboard)
      {
        ImGui.SetClipboardText(questPlate.ToString());
      }

      context.SaveChangesAsync();
      /*#if DEBUG
              logStream.WriteLineAsync($"After 'SaveChanges': {context.QuestPlate.Local}");
      #endif*/
      return "Data inserted to QuestPlate table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  public string UpdateQuestPlate(QuestPlate questPlate)
  {
    using var context = new EchoglossianDbContext(this.configDir);
    /*#if DEBUG
          using StreamWriter logStream = new($"{this.configDir}DbUpdateQuestPlateOperationsLog.txt", append: true);
    #endif*/
    try
    {
      questPlate.UpdateFieldsAsText();
      context.QuestPlate.Update(questPlate);
      /*#if DEBUG
              logStream.WriteLineAsync($"Inside Context: {context.QuestPlate.Local}");
      #endif*/
      if (this.configuration.CopyTranslationToClipboard)
      {
        ImGui.SetClipboardText(questPlate.ToString());
      }

      context.SaveChangesAsync();
      /*#if DEBUG
              logStream.WriteLineAsync($"After 'SaveChanges': {context.QuestPlate.Local}");
      #endif*/
      return "Data updated on QuestPlate table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  public string InsertGameWindow(GameWindow gameWindow)
  {
    using var context = new EchoglossianDbContext(this.configDir);

    try
    {
      context.GameWindow.Attach(gameWindow);

      context.SaveChangesAsync();

      return "Data inserted to GameWindow table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  public string UpdateGameWindow(GameWindow gameWindow)
  {
    using var context = new EchoglossianDbContext(this.configDir);

    try
    {
      context.GameWindow.Update(gameWindow);

      context.SaveChangesAsync();

      return "Data updated on GameWindow table.";
    }
    catch (Exception e)
    {
      return $"ErrorSavingData: {e}";
    }
  }

  public void LoadAllErrorToasts()
  {
    using var context = new EchoglossianDbContext(this.configDir);
    this.ErrorToastsCache = new List<ToastMessage>();
    /*#if DEBUG
          using StreamWriter logStream = new($"{this.configDir}DbErrorToastListQueryOperationsLog.txt", append: true);
    #endif*/
    try
    {
      var existingToastMessages =
          context.ToastMessage.Where(t => t.ToastType == "Error");

      foreach (var t in existingToastMessages)
      {
        this.ErrorToastsCache.Add(t);
      }

      /*#if DEBUG
              logStream.WriteLineAsync($"After Toast Messages table query: {this.ErrorToastsCache.ToArray()}");
      #endif*/
    }
    catch (Exception e)
    {
      /*#if DEBUG
              logStream.WriteLineAsync($"Query operation error: {e}");
      #endif*/
      PluginLog.Debug("Could not find any Error Toasts in Database");
    }
  }

  public void LoadAllOtherToasts()
  {
    using var context = new EchoglossianDbContext(this.configDir);
    this.OtherToastsCache = new List<ToastMessage>();
    /*#if DEBUG
          using StreamWriter logStream = new($"{this.configDir}DbOtherToastListQueryOperationsLog.txt", append: true);
    #endif*/
    try
    {
      var existingToastMessages =
          context.ToastMessage.Where(t => t.ToastType == "NonError");

      foreach (var t in existingToastMessages)
      {
        this.OtherToastsCache.Add(t);
      }

      /*#if DEBUG
              logStream.WriteLineAsync($"After Toast Messages table query: {this.OtherToastsCache.ToArray()}");
      #endif*/
    }
    catch (Exception e)
    {
      /*#if DEBUG
              logStream.WriteLineAsync($"Query operation error: {e}");
      #endif*/
      PluginLog.Debug("Could not find any Other Toasts in Database");
    }
  }

  public static bool ShouldSaveToDB(string text)
  {
    if (text.Contains("[Translation Error: HTTP 400") ||
        text.Contains("[Translation Error: HTTP 401") ||
        text.Contains("[Translation Error: HTTP 403") ||
        text.Contains("[Translation Error: HTTP 404") ||
        text.Contains("[Translation Error: HTTP 429") ||
        text.Contains("[Translation Error: HTTP 500"))
    {
      return false;
    }

    return true;
  }

  /// <summary>
  ///     Finds an entity in the database matching the given filter.
  /// </summary>
  /// <typeparam name="T">Type of entity.</typeparam>
  /// <param name="predicate">Predicate to match.</param>
  /// <returns>Matching entity or null.</returns>
  public static T? FindEntity<T>(Func<T, bool> predicate)
      where T : class, IGenericEntity
  {
    using var context = new EchoglossianDbContext(
        PluginInterface.GetPluginConfigDirectory() +
        Path.DirectorySeparatorChar);
    try
    {
      return context.Set<T>().AsEnumerable().FirstOrDefault(predicate);
    }
    catch (Exception ex)
    {
      PluginLog.Error(
          $"FindEntity<{typeof(T).Name}> failed: {ex.Message}");
      return null;
    }
  }

  /// <summary>
  ///     Inserts an entity into the database.
  /// </summary>
  /// <typeparam name="T">Entity type.</typeparam>
  /// <param name="entity">Entity to insert.</param>
  /// <returns>Result message.</returns>
  public static async Task<string> InsertEntity<T>(T entity)
        where T : class
  {
    using var context = new EchoglossianDbContext(
        PluginInterface.GetPluginConfigDirectory() +
        Path.DirectorySeparatorChar);
    try
    {
      context.Set<T>().Add(entity);
      await context.SaveChangesAsync();
      return "Entity inserted.";
    }
    catch (Exception ex)
    {
      PluginLog.Error(
          $"InsertEntity<{typeof(T).Name}> failed: {ex.Message}");
      return $"Insert failed: {ex.Message}";
    }
  }
}