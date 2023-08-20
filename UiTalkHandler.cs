// <copyright file="UiTalkHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
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
    public bool talkTracker { get; set; } = false;

    private ConcurrentQueue<TalkMessage> _talkMessageQueue = new();
    private ConcurrentDictionary<int, TalkMessage> _talkConcurrentDictionary = new();
    private void HandleTalkAsync()
    {
      Thread t = new(new ThreadStart(this.TalkProc));
      t.Start();
    }

    public void TalkProc()
    {
      PluginLog.Log("Translation Engine Started");
      while (this.configuration.TranslateTalk)
      {
        if (this._talkMessageQueue.Count > 0)
        {
          this._talkMessageQueue.TryDequeue(out TalkMessage dialogue);
          this.talkTracker = true;
          string nameTranslation;
          string messageTranslation;
          if (this.configuration.TranslateNpcNames)
          {
            nameTranslation = Translate(dialogue?.SenderName);
            PluginLog.LogWarning($"Name translation async: {nameTranslation}");
          }

          messageTranslation = Translate(dialogue?.OriginalTalkMessage);
          PluginLog.LogWarning($"Message translation async: {messageTranslation}");
        }
      }
    }

    /*    IntPtr talk = GameGui.GetAddonByName("Talk", 1);
          if (talk != IntPtr.Zero)
        {
          unsafe
          {
            AtkUnitBase* talkMaster = (AtkUnitBase*)talk;
            if (talkMaster->IsVisible)
            {
              AtkTextNode* textNode = null;
              for (int i = 0; i<talkMaster->UldManager.NodeListCount; i++)
              {
                if (talkMaster->UldManager.NodeList[i]->Type != NodeType.Text)
                {
                  continue;
                }

                textNode = (AtkTextNode*) talkMaster->UldManager.NodeList[i];
                break;
              }

              textNode->SetText(messageTranslation);
            }
          }
        }*/

    private unsafe void TalkHandler(string addonName, int index)
    {
      IntPtr talk = GameGui.GetAddonByName(addonName, index);
      if (talk != IntPtr.Zero)
      {
        AtkUnitBase* talkMaster = (AtkUnitBase*)talk;
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

        string nameToTranslate = !name.TextValue.IsNullOrEmpty() ? name.TextValue : string.Empty;
        string textToTranslate = text.TextValue;

        TalkMessage talkMessage = this.FormatTalkMessage(nameToTranslate, textToTranslate);
        this._talkMessageQueue.Enqueue(talkMessage);

#if DEBUG
        PluginLog.LogFatal($"Before DB Query attempt: {talkMessage}");
#endif
        bool findings = this.FindTalkMessage(talkMessage);
#if DEBUG
        PluginLog.LogFatal(
          $"After DB Query attempt: {(findings ? "Message found in Db." : "Message not found in Db")}");
#endif

        // If the dialogue is not saved
        if (!findings)
        {
          if (!this.configuration.UseImGuiForTalk)
          {
            string translatedText = Translate(textToTranslate);
            string nameTranslation = nameToTranslate.IsNullOrEmpty() ? string.Empty : Translate(nameToTranslate);
#if DEBUG
            PluginLog.LogWarning(translatedText);
#endif
            if (this.configuration.TranslateNpcNames)
            {
              name = nameTranslation == string.Empty ? name : nameTranslation;
              text = translatedText;

              TalkMessage translatedTalkData = new TalkMessage(
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
              string result = this.InsertTalkData(translatedTalkData);
#if DEBUG
              PluginLog.LogError($"Talk Message DB Insert operation result: {result}");
#endif
            }
            else
            {
              text = translatedText;

              TalkMessage translatedTalkData = new TalkMessage(
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

              string result = this.InsertTalkData(translatedTalkData);
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
                  int nameId = this.currentNameTranslationId;
                  string nameTranslation = Translate(nameToTranslate);
                  this.nameTranslationSemaphore.Wait();
                  if (nameId == this.currentNameTranslationId)
                  {
                    this.currentNameTranslation = nameTranslation;
                    if (this.configuration.Lang == 2)
                    {
                      this.currentNameTranslationTexture =
                        PluginInterface.UiBuilder.LoadImage(this.TranslationImageConverter(this.DrawText(this.currentNameTranslation)));
                    }
                  }

                  this.nameTranslationSemaphore.Release();
                });
              }

              this.currentTalkTranslationId = Environment.TickCount;
              this.currentTalkTranslation = Resources.WaitingForTranslation;
              Task.Run(() =>
              {
                int id = this.currentTalkTranslationId;
                string translation = Translate(textToTranslate);
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
                                        pluginInterface.UiBuilder.LoadImage(textImageAsBytes);
                                    }*/
                }

                this.talkTranslationSemaphore.Release();

#if DEBUG
                PluginLog.LogError($"Before if talk translation: {this.currentTalkTranslation}");
#endif
                if (this.currentNameTranslation != Resources.WaitingForTranslation &&
                    this.currentTalkTranslation != Resources.WaitingForTranslation)
                {
                  TalkMessage translatedTalkData = new TalkMessage(
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
                  string result = this.InsertTalkData(translatedTalkData);
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
              string translatedText = Translate(textToTranslate);
              string nameTranslation = nameToTranslate.IsNullOrEmpty() ? string.Empty : Translate(nameToTranslate);
              if (this.configuration.TranslateNpcNames)
              {
                name = nameTranslation == string.Empty ? name : nameTranslation;
                text = translatedText;

                this.currentNameTranslationId = Environment.TickCount;
                this.currentNameTranslation = Resources.WaitingForTranslation;
                Task.Run(() =>
                {
                  int nameId = this.currentNameTranslationId;
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
                  int id = this.currentTalkTranslationId;
                  this.talkTranslationSemaphore.Wait();
                  if (id == this.currentTalkTranslationId)
                  {
                    this.currentTalkTranslation = textToTranslate;
                  }

                  this.talkTranslationSemaphore.Release();
                });

                TalkMessage translatedTalkData = new TalkMessage(
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
                string result = this.InsertTalkData(translatedTalkData);
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
                  int id = this.currentTalkTranslationId;
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

                TalkMessage translatedTalkData = new TalkMessage(
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

                string result = this.InsertTalkData(translatedTalkData);
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
            string translatedText = this.FoundTalkMessage.TranslatedTalkMessage;
            string nameTranslation = this.FoundTalkMessage.TranslatedSenderName;
#if DEBUG
            PluginLog.LogWarning($"From database - Name: {nameTranslation}, Message: {translatedText}");
#endif
            if (this.configuration.TranslateNpcNames)
            {
              name = nameTranslation == string.Empty || nameTranslation == null || nameTranslation == "" ? name : nameTranslation;
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
                  int nameId = this.currentNameTranslationId;
                  string nameTranslation = this.FoundTalkMessage.TranslatedSenderName;
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
                int id = this.currentTalkTranslationId;
                string translatedTalkMessage = this.FoundTalkMessage.TranslatedTalkMessage;
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
                  int nameId = this.currentNameTranslationId;
                  string nameTranslation = this.FoundTalkMessage.SenderName;
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
                int id = this.currentTalkTranslationId;
                string translatedTalkMessage = this.FoundTalkMessage.OriginalTalkMessage;
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
  }
}
