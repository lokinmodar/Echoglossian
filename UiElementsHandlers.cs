// <copyright file="UiElementsHandlers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.ComponentModel.Design;
using System.IO;
using System.Threading.Tasks;

using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Properties;
using FFXIVClientStructs.FFXIV.Component.GUI;
using XivCommon.Functions;

namespace Echoglossian
{
  // TODO: Add logic to invert translation and original texts
  public partial class Echoglossian
  {
    private unsafe void TalkHandler(string addonName, int index)
    {
      var talk = this.gameGui.GetAddonByName(addonName, index);
      if (talk != IntPtr.Zero)
      {
        var talkMaster = (AtkUnitBase*)talk;
        if (talkMaster->IsVisible)
        {
          this.talkDisplayTranslation = true;
          this.talkTextDimensions.X = talkMaster->RootNode->Width * talkMaster->Scale;
          this.talkTextDimensions.Y = talkMaster->RootNode->Height * talkMaster->Scale;
          this.talkTextPosition.X = talkMaster->RootNode->X;
          this.talkTextPosition.Y = talkMaster->RootNode->Y;
        }
        else
        {
          this.talkDisplayTranslation = false;
        }
      }
      else
      {
        this.talkDisplayTranslation = false;
      }
    }

    private unsafe void BattleTalkHandler(string addonName, int index)
    {
      var battleTalk = this.gameGui.GetAddonByName(addonName, index);
      if (battleTalk != IntPtr.Zero)
      {
        var talkMaster = (AtkUnitBase*)battleTalk;
        if (talkMaster->IsVisible)
        {
          this.battleTalkDisplayTranslation = true;
          this.battleTalkTextDimensions.X = talkMaster->RootNode->Width * talkMaster->Scale;
          this.battleTalkTextDimensions.Y = talkMaster->RootNode->Height * talkMaster->Scale;
          this.battleTalkTextPosition.X = talkMaster->RootNode->X;
          this.battleTalkTextPosition.Y = talkMaster->RootNode->Y;
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

    private unsafe void ToastHandler(string toastName, int index)
    {
      var toastByName = this.gameGui.GetAddonByName(toastName, index);
      if (toastByName != IntPtr.Zero)
      {
        var toastByNameMaster = (AtkUnitBase*)toastByName;
        if (toastByNameMaster->IsVisible)
        {
          this.toastDisplayTranslation = true;
          this.toastTranslationTextDimensions.X = toastByNameMaster->RootNode->Width * toastByNameMaster->Scale * 2;
          this.toastTranslationTextDimensions.Y = toastByNameMaster->RootNode->Height * toastByNameMaster->Scale;
          this.toastTranslationTextPosition.X = toastByNameMaster->RootNode->X;
          this.toastTranslationTextPosition.Y = toastByNameMaster->RootNode->Y;
        }
        else
        {
          this.toastDisplayTranslation = false;
        }
      }
      else
      {
        this.toastDisplayTranslation = false;
      }
    }

    private unsafe void QuestToastHandler(string questToastName, int index)
    {
      var questToastByName = this.gameGui.GetAddonByName(questToastName, index);
      if (questToastByName != IntPtr.Zero)
      {
        var questToastByNameMaster = (AtkUnitBase*)questToastByName;
        if (questToastByNameMaster->IsVisible)
        {
          this.questToastDisplayTranslation = true;
          this.questToastTranslationTextDimensions.X = questToastByNameMaster->RootNode->Width * questToastByNameMaster->Scale * 2;
          this.questToastTranslationTextDimensions.Y = questToastByNameMaster->RootNode->Height * questToastByNameMaster->Scale;
          this.questToastTranslationTextPosition.X = questToastByNameMaster->RootNode->X;
          this.questToastTranslationTextPosition.Y = questToastByNameMaster->RootNode->Y;
        }
        else
        {
          this.questToastDisplayTranslation = false;
        }
      }
      else
      {
        this.questToastDisplayTranslation = false;
      }
    }

    private unsafe void ClassChangeToastHandler(string classChangeToastName, int index)
    {
      var classChangeToastByName = this.gameGui.GetAddonByName(classChangeToastName, index);
      if (classChangeToastByName != IntPtr.Zero)
      {
        var classChangeToastByNameMaster = (AtkUnitBase*)classChangeToastByName;
        if (classChangeToastByNameMaster->IsVisible)
        {
          this.classChangeToastDisplayTranslation = true;
          this.classChangeToastTranslationTextDimensions.X = classChangeToastByNameMaster->RootNode->Width * classChangeToastByNameMaster->Scale * 2;
          this.classChangeToastTranslationTextDimensions.Y = classChangeToastByNameMaster->RootNode->Height * classChangeToastByNameMaster->Scale;
          this.classChangeToastTranslationTextPosition.X = classChangeToastByNameMaster->RootNode->X;
          this.classChangeToastTranslationTextPosition.Y = classChangeToastByNameMaster->RootNode->Y;
        }
        else
        {
          this.classChangeToastDisplayTranslation = false;
        }
      }
      else
      {
        this.classChangeToastDisplayTranslation = false;
      }
    }

    private unsafe void ErrorToastHandler(string toastName, int index)
    {
      var errorToastByName = this.gameGui.GetAddonByName(toastName, index);
      if (errorToastByName != IntPtr.Zero)
      {
        var errorToastByNameMaster = (AtkUnitBase*)errorToastByName;
        if (errorToastByNameMaster->IsVisible)
        {
          this.errorToastDisplayTranslation = true;
          this.errorToastTranslationTextDimensions.X = errorToastByNameMaster->RootNode->Width * errorToastByNameMaster->Scale * 2;
          this.errorToastTranslationTextDimensions.Y = errorToastByNameMaster->RootNode->Height * errorToastByNameMaster->Scale;
          this.errorToastTranslationTextPosition.X = errorToastByNameMaster->RootNode->X;
          this.errorToastTranslationTextPosition.Y = errorToastByNameMaster->RootNode->Y;
        }
        else
        {
          this.errorToastDisplayTranslation = false;
        }
      }
      else
      {
        this.errorToastDisplayTranslation = false;
      }
    }

    private unsafe void AreaToastHandler(string areaToastName, int index)
    {
      var areaToastByName = this.gameGui.GetAddonByName(areaToastName, index);
      if (areaToastByName != IntPtr.Zero)
      {
        var areaToastByNameMaster = (AtkUnitBase*)areaToastByName;
        if (areaToastByNameMaster->IsVisible)
        {
          this.areaToastDisplayTranslation = true;
          this.areaToastTranslationTextDimensions.X = areaToastByNameMaster->RootNode->Width * areaToastByNameMaster->Scale * 2;
          this.areaToastTranslationTextDimensions.Y = areaToastByNameMaster->RootNode->Height * areaToastByNameMaster->Scale;
          this.areaToastTranslationTextPosition.X = areaToastByNameMaster->RootNode->X;
          this.areaToastTranslationTextPosition.Y = areaToastByNameMaster->RootNode->Y;
        }
        else
        {
          this.areaToastDisplayTranslation = false;
        }
      }
      else
      {
        this.areaToastDisplayTranslation = false;
      }
    }

    private unsafe void WideTextToastHandler(string wideTextToastName, int index)
    {
      var wideTextToastByName = this.gameGui.GetAddonByName(wideTextToastName, index);
      if (wideTextToastByName != IntPtr.Zero)
      {
        var wideTextToastByNameMaster = (AtkUnitBase*)wideTextToastByName;
        if (wideTextToastByNameMaster->IsVisible)
        {
          this.wideTextToastDisplayTranslation = true;
          this.wideTextToastTranslationTextDimensions.X = wideTextToastByNameMaster->RootNode->Width * wideTextToastByNameMaster->Scale * 2;
          this.wideTextToastTranslationTextDimensions.Y = wideTextToastByNameMaster->RootNode->Height * wideTextToastByNameMaster->Scale;
          this.wideTextToastTranslationTextPosition.X = wideTextToastByNameMaster->RootNode->X;
          this.wideTextToastTranslationTextPosition.Y = wideTextToastByNameMaster->RootNode->Y;
        }
        else
        {
          this.wideTextToastDisplayTranslation = false;
        }
      }
      else
      {
        this.wideTextToastDisplayTranslation = false;
      }
    }

    private unsafe void AddonHandlers(string addonName, int index)
    {
      var addonByName = this.gameGui.GetAddonByName(addonName, index);
      if (addonByName != IntPtr.Zero)
      {
        var addonByNameMaster = (AtkUnitBase*)addonByName;
        if (addonByNameMaster->IsVisible)
        {
          this.addonDisplayTranslation = true;
          this.addonTranslationTextDimensions.X = addonByNameMaster->RootNode->Width * addonByNameMaster->Scale * 2;
          this.addonTranslationTextDimensions.Y = addonByNameMaster->RootNode->Height * addonByNameMaster->Scale;
          this.addonTranslationTextPosition.X = addonByNameMaster->RootNode->X;
          this.addonTranslationTextPosition.Y = addonByNameMaster->RootNode->Y;
        }
        else
        {
          this.addonDisplayTranslation = false;
        }
      }
      else
      {
        this.addonDisplayTranslation = false;
      }
    }

    private void OnQuestToast(ref SeString message, ref QuestToastOptions options, ref bool ishandled)
    {
      if (!this.configuration.TranslateToast && !this.configuration.TranslateQuestToast)
      {
        return;
      }

      try
      {
        var messageTextToTranslate = message.TextValue;

        if (!this.configuration.UseImGui && this.configuration.TranslateQuestToast)
        {
          var messageTranslatedText = Translate(messageTextToTranslate);

          message = messageTranslatedText;
        }
        else
        {
          this.currentQuestToastTranslationId = Environment.TickCount;
          this.currentQuestToastTranslation = Resources.WaitingForTranslation;
          Task.Run(() =>
          {
            var messageId = this.currentQuestToastTranslationId;
            var messageTranslation = Translate(messageTextToTranslate);
            this.questToastTranslationSemaphore.Wait();
            if (messageId == this.currentQuestToastTranslationId)
            {
              this.currentQuestToastTranslation = messageTranslation;
            }

            this.questToastTranslationSemaphore.Release();
          });
        }
      }
      catch (Exception e)
      {
        PluginLog.Log("Exception: " + e.StackTrace);
        throw;
      }
    }

    private void OnErrorToast(ref SeString message, ref bool ishandled)
    {
      if (!this.configuration.TranslateToast && !this.configuration.TranslateErrorToast)
      {
        return;
      }

      try
      {
        var messageTextToTranslate = message.TextValue;

        if (!this.configuration.UseImGui)
        {
          var messageTranslatedText = Translate(messageTextToTranslate);

          message = messageTranslatedText;
        }
        else
        {
          this.currentToastTranslationId = Environment.TickCount;
          this.currentToastTranslation = Resources.WaitingForTranslation;
          Task.Run(() =>
          {
            var messageId = this.currentToastTranslationId;
            var messageTranslation = Translate(messageTextToTranslate);
            this.toastTranslationSemaphore.Wait();
            if (messageId == this.currentToastTranslationId)
            {
              this.currentToastTranslation = messageTranslation;
            }

            this.toastTranslationSemaphore.Release();
          });
        }
      }
      catch (Exception e)
      {
        PluginLog.Log("Exception: " + e.StackTrace);
        throw;
      }
    }

    private void OnToast(ref SeString message, ref ToastOptions options, ref bool ishandled)
    {
#if DEBUG
      using StreamWriter logStream = new (this.DbOperationsLogPath + "GetToastLog.txt", append: true);
#endif
      if (!this.configuration.TranslateToast && (!this.configuration.TranslateAreaToast ||
                                                 !this.configuration.TranslateClassChangeToast ||
                                                 !this.configuration.TranslateWideTextToast))
      {
        return;
      }

      try
      {
        var messageTextToTranslate = message.TextValue;

        if (!this.configuration.UseImGui)
        {
          var messageTranslatedText = Translate(messageTextToTranslate);

          message = messageTranslatedText;
        }
        else
        {
          this.currentToastTranslationId = Environment.TickCount;
          this.currentToastTranslation = Resources.WaitingForTranslation;
          Task.Run(() =>
          {
            var messageId = this.currentToastTranslationId;
            var messageTranslation = Translate(messageTextToTranslate);
            this.toastTranslationSemaphore.Wait();
            if (messageId == this.currentToastTranslationId)
            {
              this.currentToastTranslation = messageTranslation;
            }

            this.toastTranslationSemaphore.Release();
          });
        }
      }
      catch (Exception e)
      {
        PluginLog.Log("Exception: " + e.StackTrace);
        throw;
      }
    }

    private void GetTalk(ref SeString name, ref SeString text, ref TalkStyle style)
    {
      if (!this.configuration.TranslateTalk)
      {
        return;
      }
#if DEBUG
      using StreamWriter logStream = new (this.DbOperationsLogPath + "GetTalkLog.txt", append: true);
#endif

      try
      {
#if DEBUG
        PluginLog.Log(name.TextValue + ": " + text.TextValue);
#endif

        var nameToTranslate = name.TextValue;
        var textToTranslate = text.TextValue;

        TalkMessage talkMessage = this.FormatTalkMessage(nameToTranslate, textToTranslate);

#if DEBUG
        PluginLog.LogFatal($"Before DB Query attempt: {talkMessage}");
#endif
        var findings = this.FindTalkMessage(talkMessage);
#if DEBUG
        PluginLog.LogFatal(
          $"After DB Query attempt: {(findings ? "Message found in Db." : "Message not found in Db")}");
#endif

        // If the dialogue is not saved
        if (!findings)
        {
          if (!this.configuration.UseImGui)
          {
            var translatedText = Translate(textToTranslate);
            var nameTranslation = Translate(nameToTranslate);
#if DEBUG
            PluginLog.LogWarning(translatedText);
#endif
            if (this.configuration.TranslateNPCNames)
            {
              name = nameTranslation == string.Empty ? name : nameTranslation;
              text = translatedText;

              var translatedTalkData = new TalkMessage(nameToTranslate, textToTranslate, LangIdentify(textToTranslate),
                LangIdentify(nameToTranslate), nameTranslation, translatedText, Codes[languageInt],
                this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
#if DEBUG
              logStream.WriteLineAsync($"Before Talk Messages table data insertion:  {translatedTalkData}");
#endif
              var result = this.InsertTalkData(translatedTalkData);
#if DEBUG
              PluginLog.LogError($"Talk Message DB Insert operation result: {result}");
#endif
            }
            else
            {
              text = translatedText;

              var translatedTalkData = new TalkMessage(nameToTranslate, textToTranslate, LangIdentify(textToTranslate),
                LangIdentify(nameToTranslate), string.Empty, translatedText, Codes[languageInt],
                this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);

              var result = this.InsertTalkData(translatedTalkData);
#if DEBUG
              PluginLog.LogError(result);
#endif
            }
#if DEBUG
            PluginLog.Log($"Using Talk Replace - {name.TextValue}: {text.TextValue}");
#endif
          }
          else
          {
            if (!this.configuration.SwapTextsUsingImGui)
            {
              // TODO: format element to insert whenTranslation Overlay is active
              if (this.configuration.TranslateNPCNames)
              {
                this.currentNameTranslationId = Environment.TickCount;
                this.currentNameTranslation = Resources.WaitingForTranslation;
                Task.Run(() =>
                {
                  var nameId = this.currentNameTranslationId;
                  var nameTranslation = Translate(nameToTranslate);
                  this.nameTranslationSemaphore.Wait();
                  if (nameId == this.currentNameTranslationId)
                  {
                    this.currentNameTranslation = nameTranslation;
                  }

                  this.nameTranslationSemaphore.Release();
                });
              }

              this.currentTalkTranslationId = Environment.TickCount;
              this.currentTalkTranslation = Resources.WaitingForTranslation;
              Task.Run(() =>
              {
                var id = this.currentTalkTranslationId;
                var translation = Translate(textToTranslate);
                this.talkTranslationSemaphore.Wait();
                if (id == this.currentTalkTranslationId)
                {
                  this.currentTalkTranslation = translation;
                }

                this.talkTranslationSemaphore.Release();

#if DEBUG
                PluginLog.LogError($"Before if talk translation: {this.currentTalkTranslation}");
#endif
                if (this.currentNameTranslation != Resources.WaitingForTranslation &&
                    this.currentTalkTranslation != Resources.WaitingForTranslation)
                {
                  var translatedTalkData = new TalkMessage(nameToTranslate, textToTranslate,
                    LangIdentify(textToTranslate),
                    LangIdentify(nameToTranslate),
                    this.configuration.TranslateNPCNames ? this.currentNameTranslation : string.Empty,
                    this.currentTalkTranslation, Codes[languageInt],
                    this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
                  var result = this.InsertTalkData(translatedTalkData);
#if DEBUG
                  PluginLog.LogError($"Talk Message DB Insert operation result: {result}");
#endif
                }
              });
            }
            else
            {
              if (this.configuration.TranslateNPCNames)
              {
                this.currentNameTranslationId = Environment.TickCount;
                this.currentNameTranslation = Resources.WaitingForTranslation;
                Task.Run(() =>
                {
                  var nameId = this.currentNameTranslationId;
                  this.nameTranslationSemaphore.Wait();
                  if (nameId == this.currentNameTranslationId)
                  {
                    this.currentNameTranslation = nameToTranslate;
                  }

                  this.nameTranslationSemaphore.Release();
                });
              }

              this.currentTalkTranslationId = Environment.TickCount;
              this.currentTalkTranslation = Resources.WaitingForTranslation;
              Task.Run(() =>
              {
                var id = this.currentTalkTranslationId;
                this.talkTranslationSemaphore.Wait();
                if (id == this.currentTalkTranslationId)
                {
                  this.currentTalkTranslation = textToTranslate;
                }

                this.talkTranslationSemaphore.Release();

#if DEBUG
                PluginLog.LogError($"Before if talk translation: {this.currentTalkTranslation}");
#endif
                if (this.currentNameTranslation != Resources.WaitingForTranslation &&
                    this.currentTalkTranslation != Resources.WaitingForTranslation)
                {
                  var translatedTalkData = new TalkMessage(this.currentNameTranslation, this.currentTalkTranslation,
                    LangIdentify(this.currentTalkTranslation),
                    LangIdentify(this.currentNameTranslation),
                    this.configuration.TranslateNPCNames ? Translate(this.currentNameTranslation) : string.Empty,
                    Translate(this.currentTalkTranslation), Codes[languageInt],
                    this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
                  var result = this.InsertTalkData(translatedTalkData);
#if DEBUG
                  PluginLog.LogError($"Talk Message DB Insert operation result: {result}");
#endif
                }
              });
              name = Translate(name.TextValue);
              text = Translate(text.TextValue);
            }
          }
        }
        else
        {
          if (!this.configuration.UseImGui)
          {
            var translatedText = this.FoundTalkMessage.TranslatedTalkMessage;
            var nameTranslation = this.FoundTalkMessage.TranslatedSenderName;
#if DEBUG
            PluginLog.LogWarning($"From database - Name: {nameTranslation}, Message: {translatedText}");
#endif
            if (this.configuration.TranslateNPCNames)
            {
              name = nameTranslation == string.Empty ? name : nameTranslation;
              text = translatedText;
            }
            else
            {
              text = translatedText;
            }
#if DEBUG
            PluginLog.Log(name.TextValue + ": " + text.TextValue);
#endif
          }
          else
          {
            if (!this.configuration.SwapTextsUsingImGui)
            {
              if (this.configuration.TranslateNPCNames)
              {
                this.currentNameTranslationId = Environment.TickCount;
                this.currentNameTranslation = Resources.WaitingForTranslation;
                Task.Run(() =>
                {
                  var nameId = this.currentNameTranslationId;
                  var nameTranslation = this.FoundTalkMessage.TranslatedSenderName;
                  this.nameTranslationSemaphore.Wait();
                  if (nameId == this.currentNameTranslationId)
                  {
                    this.currentNameTranslation = nameTranslation;
#if DEBUG
                    PluginLog.Error($"Using overlay - name found in DB: {nameTranslation} ");
#endif
                  }

                  this.nameTranslationSemaphore.Release();
                });
              }

              this.currentTalkTranslationId = Environment.TickCount;
              this.currentTalkTranslation = Resources.WaitingForTranslation;
              Task.Run(() =>
              {
                var id = this.currentTalkTranslationId;
                var translatedTalkMessage = this.FoundTalkMessage.TranslatedTalkMessage;
                this.talkTranslationSemaphore.Wait();
                if (id == this.currentTalkTranslationId)
                {
                  this.currentTalkTranslation = translatedTalkMessage;
#if DEBUG
                  PluginLog.Error($"Using overlay - message found in DB: {translatedTalkMessage} ");
#endif
                }

                this.talkTranslationSemaphore.Release();
              });
            }
            else
            {
              if (this.configuration.TranslateNPCNames)
              {
                this.currentNameTranslationId = Environment.TickCount;
                this.currentNameTranslation = Resources.WaitingForTranslation;
                Task.Run(() =>
                {
                  var nameId = this.currentNameTranslationId;
                  var nameTranslation = this.FoundTalkMessage.SenderName;
                  this.nameTranslationSemaphore.Wait();
                  if (nameId == this.currentNameTranslationId)
                  {
                    this.currentNameTranslation = nameTranslation;
#if DEBUG
                    PluginLog.Error($"Using overlay - name found in DB: {nameTranslation} ");
#endif
                  }

                  this.nameTranslationSemaphore.Release();
                });
              }

              this.currentTalkTranslationId = Environment.TickCount;
              this.currentTalkTranslation = Resources.WaitingForTranslation;
              Task.Run(() =>
              {
                var id = this.currentTalkTranslationId;
                var translatedTalkMessage = this.FoundTalkMessage.OriginalTalkMessage;
                this.talkTranslationSemaphore.Wait();
                if (id == this.currentTalkTranslationId)
                {
                  this.currentTalkTranslation = translatedTalkMessage;
#if DEBUG
                  PluginLog.Error($"Using overlay - message found in DB: {translatedTalkMessage} ");
#endif
                }

                this.talkTranslationSemaphore.Release();
              });
              name = this.FoundTalkMessage.TranslatedSenderName;
              text = this.FoundTalkMessage.TranslatedTalkMessage;
            }
          }
        }
      }
      catch (Exception e)
      {
        PluginLog.Log("Exception: " + e.StackTrace);
        throw;
      }
    }

    private void GetBattleTalk(ref SeString sender, ref SeString message, ref BattleTalkOptions options,
      ref bool ishandled)
    {
      if (!this.configuration.TranslateBattleTalk)
      {
        return;
      }
#if DEBUG
      using StreamWriter logStream = new (this.DbOperationsLogPath + "GetBattleTalkLog.txt", append: true);
#endif

      try
      {
#if DEBUG
        PluginLog.Log(sender.TextValue + ": " + message.TextValue);
#endif

        var senderToTranslate = sender.TextValue;
        var battleTextToTranslate = message.TextValue;

        BattleTalkMessage battleTalkMessage = this.FormatBattleTalkMessage(senderToTranslate, battleTextToTranslate);

#if DEBUG
        PluginLog.LogFatal($"Before DB Query attempt: {battleTalkMessage}");
#endif
        var findings = this.FindBattleTalkMessage(battleTalkMessage);
#if DEBUG
        PluginLog.LogFatal(
          $"After DB Query attempt: {(findings ? "Message found in Db." : "Message not found in Db")}");
#endif

        // If the dialogue is not saved
        if (!findings)
        {
          if (!this.configuration.UseImGui)
          {
            var translatedBattleTalkMessage = Translate(battleTextToTranslate);
            var senderTranslation = Translate(senderToTranslate);
#if DEBUG
            PluginLog.LogWarning(translatedBattleTalkMessage);
#endif
            if (this.configuration.TranslateNPCNames)
            {
              sender = senderTranslation == string.Empty ? sender : senderTranslation;
              message = translatedBattleTalkMessage;

              var translatedBattleTalkData = new BattleTalkMessage(senderToTranslate, battleTextToTranslate,
                LangIdentify(battleTextToTranslate),
                LangIdentify(senderToTranslate), senderTranslation, translatedBattleTalkMessage, Codes[languageInt],
                this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
#if DEBUG
              logStream.WriteLineAsync($"Before Talk Messages table data insertion:  {translatedBattleTalkData}");
#endif
              var result = this.InsertBattleTalkData(translatedBattleTalkData);
#if DEBUG
              PluginLog.LogError($"BattleTalk Message DB Insert operation result: {result}");
#endif
            }
            else
            {
              message = translatedBattleTalkMessage;

              var translatedBattleTalkData = new BattleTalkMessage(senderToTranslate, battleTextToTranslate,
                LangIdentify(battleTextToTranslate),
                LangIdentify(senderToTranslate), string.Empty, translatedBattleTalkMessage, Codes[languageInt],
                this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);

              var result = this.InsertBattleTalkData(translatedBattleTalkData);
#if DEBUG
              PluginLog.LogError($"Using BattleTalk Overlay - BattleTalk Message DB Insert operation result: {result}");
#endif
            }
#if DEBUG
            PluginLog.Log($"Using BattleTalk Replace - {sender.TextValue}: {message.TextValue}");
#endif
          }
          else
          {
            // TODO: format element to insert whenTranslation Overlay is active
            if (this.configuration.TranslateNPCNames)
            {
              this.currentSenderTranslationId = Environment.TickCount;
              this.currentSenderTranslation = Resources.WaitingForTranslation;
              Task.Run(() =>
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
            Task.Run(() =>
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
              PluginLog.LogError($"Before if BattleTalk translation: {this.currentBattleTalkTranslation}");
#endif
              if (this.currentSenderTranslation != Resources.WaitingForTranslation && this.currentBattleTalkTranslation != Resources.WaitingForTranslation)
              {
                var translatedBattleTalkData = new BattleTalkMessage(senderToTranslate, battleTextToTranslate,
                  LangIdentify(battleTextToTranslate),
                  LangIdentify(senderToTranslate),
                  this.configuration.TranslateNPCNames ? this.currentSenderTranslation : string.Empty,
                  this.currentBattleTalkTranslation, Codes[languageInt],
                  this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
                var result = this.InsertBattleTalkData(translatedBattleTalkData);
#if DEBUG
                PluginLog.LogError($"BattleTalk Message DB Insert operation result: {result}");
#endif
              }
            });
          }
        }
        else
        { // if the data is already in the DB
          if (!this.configuration.UseImGui)
          {
            var translatedBattleMessage = this.FoundBattleTalkMessage.TranslatedBattleTalkMessage;
            var senderTranslation = this.FoundBattleTalkMessage.TranslatedSenderName;
#if DEBUG
            PluginLog.LogWarning($"From database - Name: {senderTranslation}, Message: {translatedBattleMessage}");
#endif
            if (this.configuration.TranslateNPCNames)
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
            if (this.configuration.TranslateNPCNames)
            {
              this.currentSenderTranslationId = Environment.TickCount;
              this.currentSenderTranslation = Resources.WaitingForTranslation;
              Task.Run(() =>
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
            Task.Run(() =>
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
