// <copyright file="UiTalkHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.IO;
using System.Threading.Tasks;

using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using Dalamud.Utility;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Properties;
using FFXIVClientStructs.FFXIV.Component.GUI;
using XivCommon.Functions;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private unsafe void BattleTalkHandler(string addonName, int index)
    {
      if (!this.configuration.TranslateBattleTalk)
      {
        return;
      }

      var battleTalk = GameGui.GetAddonByName(addonName, index);
      if (battleTalk != IntPtr.Zero)
      {
        var battleTalkMaster = (AtkUnitBase*)battleTalk;
        if (battleTalkMaster->IsVisible)
        {
          this.battleTalkDisplayTranslation = true;
          this.battleTalkTextDimensions.X = battleTalkMaster->RootNode->Width * battleTalkMaster->Scale * 2;
          this.battleTalkTextDimensions.Y = battleTalkMaster->RootNode->Height * battleTalkMaster->Scale;
          this.battleTalkTextPosition.X = battleTalkMaster->RootNode->X;
          this.battleTalkTextPosition.Y = battleTalkMaster->RootNode->Y;
        }
        else
        {
          this.battleTalkDisplayTranslation = false;
        }
      }
      else
      {
        this.battleTalkDisplayTranslation = false;
      }
    }

    private void GetBattleTalk(
      ref SeString sender,
      ref SeString message,
      ref BattleTalkOptions options,
      ref bool ishandled)
    {
      if (!this.configuration.TranslateBattleTalk)
      {
        return;
      }
#if DEBUG
      using StreamWriter logStream = new(this.configDir + "GetBattleTalkLog.txt", true);
#endif

      try
      {
#if DEBUG
        PluginLog.Log(sender.TextValue + ": " + message.TextValue);
#endif

        var senderToTranslate = !sender.TextValue.IsNullOrEmpty() ? sender.TextValue : "System Message";
        var battleTextToTranslate = message.TextValue;

        var battleTalkMessage = this.FormatBattleTalkMessage(senderToTranslate, battleTextToTranslate);

#if DEBUG
        PluginLog.LogVerbose($"Before DB Query attempt: {battleTalkMessage}");
#endif
        var findings = this.FindBattleTalkMessage(battleTalkMessage);
#if DEBUG
        PluginLog.LogVerbose(
          $"After DB Query attempt: {(findings ? "Message found in Db." : "Message not found in Db")}");
#endif

        // If the dialogue is not saved
        if (!findings)
        {
          if (!this.configuration.UseImGuiForBattleTalk)
          {
            var translatedBattleTalkMessage = Translate(battleTextToTranslate);
            var senderTranslation = Translate(senderToTranslate);
#if DEBUG
            PluginLog.LogVerbose(translatedBattleTalkMessage);
#endif
            if (this.configuration.TranslateNpcNames)
            {
              sender = senderTranslation == string.Empty ? sender : senderTranslation;
              message = translatedBattleTalkMessage;

              var translatedBattleTalkData = new BattleTalkMessage(
                senderToTranslate,
                battleTextToTranslate,
                LangIdentify(battleTextToTranslate),
                LangIdentify(senderToTranslate),
                senderTranslation,
                translatedBattleTalkMessage,
                this.LanguagesDictionary[this.configuration.Lang].Code,
                this.configuration.ChosenTransEngine,
                DateTime.Now,
                DateTime.Now);
#if DEBUG
              logStream.WriteLineAsync($"Before Talk Messages table data insertion:  {translatedBattleTalkData}");
#endif
              var result = this.InsertBattleTalkData(translatedBattleTalkData);
#if DEBUG
              PluginLog.LogVerbose($"BattleTalk Message DB Insert operation result: {result}");
#endif
            }
            else
            {
              message = translatedBattleTalkMessage;

              var translatedBattleTalkData = new BattleTalkMessage(
                senderToTranslate,
                battleTextToTranslate,
                LangIdentify(battleTextToTranslate),
                LangIdentify(senderToTranslate),
                string.Empty,
                translatedBattleTalkMessage,
                this.LanguagesDictionary[this.configuration.Lang].Code,
                this.configuration.ChosenTransEngine,
                DateTime.Now,
                DateTime.Now);

              var result = this.InsertBattleTalkData(translatedBattleTalkData);
#if DEBUG
              PluginLog.LogVerbose($"Using BattleTalk Overlay - BattleTalk Message DB Insert operation result: {result}");
#endif
            }
#if DEBUG
            PluginLog.Log($"Using BattleTalk Replace - {sender.TextValue}: {message.TextValue}");
#endif
          }
          else
          {
            if (this.configuration.TranslateNpcNames)
            {
              this.currentSenderTranslationId = Environment.TickCount;
              this.currentSenderTranslation = Resources.WaitingForTranslation;
              Task.Run(
                () =>
                {
                  var nameId = this.currentSenderTranslationId;
                  var senderTranslation = Translate(senderToTranslate);
                  this.senderTranslationSemaphore.Wait();
                  if (nameId == this.currentSenderTranslationId)
                  {
                    this.currentSenderTranslation = senderTranslation;
                  }

                  this.senderTranslationSemaphore.Release();
                });
            }

            this.currentBattleTalkTranslationId = Environment.TickCount;
            this.currentBattleTalkTranslation = Resources.WaitingForTranslation;
            Task.Run(
              () =>
              {
                var id = this.currentBattleTalkTranslationId;
                var translation = Translate(battleTextToTranslate);
                this.battleTalkTranslationSemaphore.Wait();
                if (id == this.currentBattleTalkTranslationId)
                {
                  this.currentBattleTalkTranslation = translation;
                }

                this.battleTalkTranslationSemaphore.Release();
#if DEBUG
                PluginLog.LogVerbose($"Before if BattleTalk translation: {this.currentBattleTalkTranslation}");
#endif
                if (this.currentSenderTranslation != Resources.WaitingForTranslation &&
                    this.currentBattleTalkTranslation != Resources.WaitingForTranslation)
                {
                  var translatedBattleTalkData = new BattleTalkMessage(
                    senderToTranslate,
                    battleTextToTranslate,
                    LangIdentify(battleTextToTranslate),
                    LangIdentify(senderToTranslate),
                    this.configuration.TranslateNpcNames ? this.currentSenderTranslation : string.Empty,
                    this.currentBattleTalkTranslation,
                    this.LanguagesDictionary[this.configuration.Lang].Code,
                    this.configuration.ChosenTransEngine,
                    DateTime.Now,
                    DateTime.Now);
                  var result = this.InsertBattleTalkData(translatedBattleTalkData);
#if DEBUG
                  PluginLog.LogVerbose($"BattleTalk Message DB Insert operation result: {result}");
#endif
                }
              });
          }
        }
        else
        {
          // if the data is already in the DB
          if (!this.configuration.UseImGuiForBattleTalk)
          {
            var translatedBattleMessage = this.FoundBattleTalkMessage.TranslatedBattleTalkMessage;
            var senderTranslation = this.FoundBattleTalkMessage.TranslatedSenderName;
#if DEBUG
            PluginLog.LogVerbose($"From database - Name: {senderTranslation}, Message: {translatedBattleMessage}");
#endif
            if (this.configuration.TranslateNpcNames)
            {
              sender = senderTranslation == string.Empty ? sender : senderTranslation;
              message = translatedBattleMessage;
            }
            else
            {
              message = translatedBattleMessage;
            }
#if DEBUG
            PluginLog.Log(sender.TextValue + ": " + message.TextValue);
#endif
          }
          else
          {
            if (this.configuration.TranslateNpcNames)
            {
              this.currentSenderTranslationId = Environment.TickCount;
              this.currentSenderTranslation = Resources.WaitingForTranslation;
              Task.Run(
                () =>
                {
                  var nameId = this.currentSenderTranslationId;
                  var senderTranslation = this.FoundBattleTalkMessage.TranslatedSenderName;
                  this.senderTranslationSemaphore.Wait();
                  if (nameId == this.currentSenderTranslationId)
                  {
                    this.currentSenderTranslation = senderTranslation;
#if DEBUG
                    PluginLog.Error($"Using overlay - name found in DB: {senderTranslation} ");
#endif
                  }

                  this.senderTranslationSemaphore.Release();
                });
            }

            this.currentBattleTalkTranslationId = Environment.TickCount;
            this.currentBattleTalkTranslation = Resources.WaitingForTranslation;
            Task.Run(
              () =>
              {
                var id = this.currentBattleTalkTranslationId;
                var translatedBattleTalkMessage = this.FoundBattleTalkMessage.TranslatedBattleTalkMessage;
                this.battleTalkTranslationSemaphore.Wait();
                if (id == this.currentBattleTalkTranslationId)
                {
                  this.currentBattleTalkTranslation = translatedBattleTalkMessage;
#if DEBUG
                  PluginLog.Error($"Using overlay - message found in DB: {translatedBattleTalkMessage} ");
#endif
                }

                this.battleTalkTranslationSemaphore.Release();
              });
          }
        }
      }
      catch (Exception e)
      {
        PluginLog.Log("Exception: " + e.StackTrace);
        throw;
      }
    }
  }
}