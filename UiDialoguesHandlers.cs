// <copyright file="UiDialoguesHandlers.cs" company="lokinmodar">
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
#if DEBUG
          // PluginLog.LogVerbose("Inside Talk Handler.");
#endif
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

    private void TalkSubtitleHandler(string addonName, int index)
    {
      // Pointer: 23FDB6C9040 or 23FDB6C91C0
      try
      {
        var talkSubtitlePtr = this.gameGui.GetAddonByName(addonName, index);
        if (talkSubtitlePtr == IntPtr.Zero)
        {
          return;
        }

      }
      catch (Exception e)
      {
        PluginLog.Error($"The routine has crashed: {e}");
      }
    }


    private unsafe void BattleTalkHandler(string addonName, int index)
    {
      var battleTalk = this.gameGui.GetAddonByName(addonName, index);
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

    private void GetTalk(ref SeString name, ref SeString text, ref TalkStyle style)
    {
      if (!this.configuration.TranslateTalk)
      {
        return;
      }
#if DEBUG
      using StreamWriter logStream = new(this.configDir + "GetTalkLog.txt", append: true);
#endif

      try
      {
#if DEBUG
        PluginLog.Log(name.TextValue + ": " + text.TextValue);
#endif

        var nameToTranslate = !name.TextValue.IsNullOrEmpty() ? name.TextValue : "System Message";
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
          if (!this.configuration.UseImGuiForTalk)
          {
            var translatedText = Translate(textToTranslate);
            var nameTranslation = nameToTranslate.IsNullOrEmpty() ? string.Empty : Translate(nameToTranslate);
#if DEBUG
            PluginLog.LogWarning(translatedText);
#endif
            if (this.configuration.TranslateNpcNames)
            {
              name = nameTranslation == string.Empty ? name : nameTranslation;
              text = translatedText;

              var translatedTalkData = new TalkMessage(
                nameToTranslate,
                textToTranslate,
                LangIdentify(textToTranslate),
                LangIdentify(nameToTranslate),
                nameTranslation,
                translatedText,
                langDict[languageInt].Code,
                this.configuration.ChosenTransEngine,
                DateTime.Now,
                DateTime.Now);
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

              var translatedTalkData = new TalkMessage(
                nameToTranslate,
                textToTranslate,
                LangIdentify(textToTranslate),
                LangIdentify(nameToTranslate),
                string.Empty,
                translatedText,
                langDict[languageInt].Code,
                this.configuration.ChosenTransEngine,
                DateTime.Now,
                DateTime.Now);

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
              if (this.configuration.TranslateNpcNames)
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
                    if (this.configuration.Lang == 2)
                    {
                      this.currentNameTranslationTexture =
                        this.pluginInterface.UiBuilder.LoadImage(this.TranslationImageConverter(this.DrawText(this.currentNameTranslation)));
                    }
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
                  // TODO: check image creation logic for RTL
                  /*                  if (this.configuration.Lang == 2)
                                    {
                  #if DEBUG
                                      PluginLog.LogWarning("Lang is 2!");
                  #endif
                                      var textAsImage = this.DrawText(this.currentTalkTranslation);
                                      var textImageAsBytes = this.TranslationImageConverter(textAsImage);
                  #if DEBUG
                                      PluginLog.LogWarning($"image bytes: {textImageAsBytes}");
                  #endif
                                      this.currentTalkTranslationTexture =
                                        this.pluginInterface.UiBuilder.LoadImage(textImageAsBytes);
                                    }*/
                }

                this.talkTranslationSemaphore.Release();

#if DEBUG
                PluginLog.LogError($"Before if talk translation: {this.currentTalkTranslation}");
#endif
                if (this.currentNameTranslation != Resources.WaitingForTranslation &&
                    this.currentTalkTranslation != Resources.WaitingForTranslation)
                {
                  var translatedTalkData = new TalkMessage(
                    nameToTranslate,
                    textToTranslate,
                    LangIdentify(textToTranslate),
                    LangIdentify(nameToTranslate),
                    this.configuration.TranslateNpcNames ? this.currentNameTranslation : string.Empty,
                    this.currentTalkTranslation,
                    langDict[languageInt].Code,
                    this.configuration.ChosenTransEngine,
                    DateTime.Now,
                    DateTime.Now);
                  var result = this.InsertTalkData(translatedTalkData);
#if DEBUG
                  PluginLog.LogError($"Talk Message DB Insert operation result: {result}");
#endif
                }
              });
            }
            else
            {
#if DEBUG
              PluginLog.LogWarning("Using Swap text for translation");
#endif
              var translatedText = Translate(textToTranslate);
              var nameTranslation = nameToTranslate.IsNullOrEmpty() ? string.Empty : Translate(nameToTranslate);
              if (this.configuration.TranslateNpcNames)
              {
                name = nameTranslation == string.Empty ? name : nameTranslation;
                text = translatedText;

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
                });

                var translatedTalkData = new TalkMessage(
                  nameToTranslate,
                  textToTranslate,
                  LangIdentify(textToTranslate),
                  LangIdentify(nameToTranslate),
                  nameTranslation,
                  translatedText,
                  langDict[languageInt].Code,
                  this.configuration.ChosenTransEngine,
                  DateTime.Now,
                  DateTime.Now);
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
                  /*#if DEBUG
                                    PluginLog.LogError($"Before if talk translation: {this.currentTalkTranslation}");
                  #endif*/
                  /*if (this.currentNameTranslation != Resources.WaitingForTranslation &&
                      this.currentTalkTranslation != Resources.WaitingForTranslation)
                  {
                    var translatedTalkData = new TalkMessage(this.currentNameTranslation, this.currentTalkTranslation,
                      LangIdentify(this.currentTalkTranslation),
                      LangIdentify(this.currentNameTranslation),
                      this.configuration.TranslateNPCNames ? Translate(this.currentNameTranslation) : string.Empty,
                      Translate(this.currentTalkTranslation), langDict[languageInt].Code,
                      this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
                    var result = this.InsertTalkData(translatedTalkData);
#if DEBUG
                    PluginLog.LogError($"Talk Message DB Insert operation result: {result}");
#endif
                  }*/
                });

                var translatedTalkData = new TalkMessage(
                  nameToTranslate,
                  textToTranslate,
                  LangIdentify(textToTranslate),
                  LangIdentify(nameToTranslate),
                  string.Empty,
                  translatedText,
                  langDict[languageInt].Code,
                  this.configuration.ChosenTransEngine,
                  DateTime.Now,
                  DateTime.Now);

                var result = this.InsertTalkData(translatedTalkData);
#if DEBUG
                PluginLog.LogError(result);
#endif
              }
            }
          }
        }
        else
        {
          if (!this.configuration.UseImGuiForTalk)
          {
            var translatedText = this.FoundTalkMessage.TranslatedTalkMessage;
            var nameTranslation = this.FoundTalkMessage.TranslatedSenderName;
#if DEBUG
            PluginLog.LogWarning($"From database - Name: {nameTranslation}, Message: {translatedText}");
#endif
            if (this.configuration.TranslateNpcNames)
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
              if (this.configuration.TranslateNpcNames)
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
              if (this.configuration.TranslateNpcNames)
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
      using StreamWriter logStream = new(this.configDir + "GetBattleTalkLog.txt", append: true);
#endif

      try
      {
#if DEBUG
        PluginLog.Log(sender.TextValue + ": " + message.TextValue);
#endif

        var senderToTranslate = !sender.TextValue.IsNullOrEmpty() ? sender.TextValue : "System Message";
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
          if (!this.configuration.UseImGuiForBattleTalk)
          {
            var translatedBattleTalkMessage = Translate(battleTextToTranslate);
            var senderTranslation = Translate(senderToTranslate);
#if DEBUG
            PluginLog.LogWarning(translatedBattleTalkMessage);
#endif
            if (this.configuration.TranslateNpcNames)
            {
              sender = senderTranslation == string.Empty ? sender : senderTranslation;
              message = translatedBattleTalkMessage;

              var translatedBattleTalkData = new BattleTalkMessage(senderToTranslate, battleTextToTranslate,
                LangIdentify(battleTextToTranslate),
                LangIdentify(senderToTranslate), senderTranslation, translatedBattleTalkMessage, langDict[languageInt].Code,
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
                LangIdentify(senderToTranslate), string.Empty, translatedBattleTalkMessage, langDict[languageInt].Code,
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
            if (this.configuration.TranslateNpcNames)
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
                  this.configuration.TranslateNpcNames ? this.currentSenderTranslation : string.Empty,
                  this.currentBattleTalkTranslation, langDict[languageInt].Code,
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
          if (!this.configuration.UseImGuiForBattleTalk)
          {
            var translatedBattleMessage = this.FoundBattleTalkMessage.TranslatedBattleTalkMessage;
            var senderTranslation = this.FoundBattleTalkMessage.TranslatedSenderName;
#if DEBUG
            PluginLog.LogWarning($"From database - Name: {senderTranslation}, Message: {translatedBattleMessage}");
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
