﻿// <copyright file="UiTalkHandler.cs" company="lokinmodar">
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
    private readonly ConcurrentQueue<TalkMessage> talkMessageQueue = new();
    private readonly ConcurrentQueue<TalkMessage> translatedTalkMessageQueue = new();
    private ConcurrentDictionary<int, TalkMessage> talkConcurrentDictionary = new(); // not using this right now

    private TalkMessage CurrentTalkMessage { get; set; }

    public bool TalkTracker { get; set; } = false;

    private void HandleTalkAsync()
    {
      try
      {
        Thread t = new(this.TalkProc);
        t.Start();
      }
      catch (Exception e)
      {
        PluginLog.LogVerbose($"Talk Translation Thread Error: {e}");
        throw;
      }
    }

    // TODO: clear queues before use!
    private void TalkProc()
    {
      PluginLog.Log("Translation Engine Started");
      while (this.configuration.TranslateTalk)
      {
        if (this.talkMessageQueue.IsEmpty)
        {
          PluginLog.LogVerbose("4");
          this.talkMessageQueue.TryPeek(out var dialogue);
          var talkMessageToTranslate = dialogue;

          if (talkMessageToTranslate != null)
          {
            if (this.configuration.TranslateNpcNames)
            {
#if DEBUG
              PluginLog.LogVerbose("5");
#endif
              var nameTranslation = Translate(talkMessageToTranslate.SenderName);
#if DEBUG
              PluginLog.LogVerbose($"Name translation async: {nameTranslation}");
#endif
              talkMessageToTranslate.TranslatedSenderName = nameTranslation;
#if DEBUG
              PluginLog.LogVerbose("6");
#endif
            }
#if DEBUG
            PluginLog.LogVerbose("7");
#endif
            var messageTranslation = Translate(talkMessageToTranslate.OriginalTalkMessage);
#if DEBUG
            PluginLog.LogVerbose($"Message translation async: {messageTranslation}");
            PluginLog.LogVerbose("8");
#endif
            talkMessageToTranslate.TranslatedTalkMessage = messageTranslation;
#if DEBUG
            PluginLog.LogVerbose("9");
#endif
            this.translatedTalkMessageQueue.Enqueue(talkMessageToTranslate);
#if DEBUG
            PluginLog.LogVerbose("10");
#endif
          }
        }
      }
    }

    // not working but it is not retrying to translate all the time. Needs tracking logic
    private unsafe void TalkHandler()
    {
#if DEBUG
      using StreamWriter logStream = new(this.configDir + "TalkHandlerLog.txt", true);
#endif
      bool findings;
      if (!this.configuration.TranslateTalk)
      {
        PluginLog.LogWarning("Talk translation is currently disabled");
        return;
      }

      try
      {
        var talk = GameGui.GetAddonByName("Talk", 1);
        if (talk != IntPtr.Zero)
        {
          var talkMaster = (AtkUnitBase*)talk;
          if (talkMaster->IsVisible)
          {
            PluginLog.LogWarning("Node is visible!");
            var nameTextNode = talkMaster->GetTextNodeById(2);
            var nameNodeText = nameTextNode->NodeText.ToString();
            var textTextNode = talkMaster->GetTextNodeById(3);
            var textNodeText = textTextNode->NodeText.ToString();
#if DEBUG
            PluginLog.LogVerbose($"Name Node text: {nameNodeText}");
            PluginLog.LogVerbose($"Text Node text: {textNodeText}");
#endif
            // are TextNodes empty
            if (nameNodeText.IsNullOrEmpty() && textNodeText.IsNullOrEmpty())
            {
              PluginLog.LogWarning("TextNodes empty");
              return;
            }
            PluginLog.LogWarning("TextNodes Have text!");
            var nameToTranslate = !nameNodeText.IsNullOrEmpty() ? nameNodeText : string.Empty;
            var textToTranslate = sanitizer.Sanitize(textNodeText);

            this.CurrentTalkMessage = this.FormatTalkMessage(nameToTranslate, textToTranslate);
#if DEBUG
            PluginLog.LogVerbose($"Before DB Query attempt: {CurrentTalkMessage}");
#endif
            if (this.FoundTalkMessage != null)
            {
              if (this.FoundTalkMessage.OriginalTalkMessage == textNodeText)
              {
                if (this.FoundTalkMessage.TranslatedTalkMessage == null)
                {
                  return;
                }
                if (this.FoundTalkMessage.TranslatedTalkMessage == textNodeText)
                {
                  return;
                }
                FoundTalkMessage = null;

              }
              else
              {
                return;
              }
              return;
            }

            findings = this.FindTalkMessage(CurrentTalkMessage);
#if DEBUG
            PluginLog.LogVerbose(
              $"After DB Query attempt: {(findings ? "Message found in Db." : "Message not found in Db")}");
            PluginLog.LogInformation("Findings? " + findings);
            PluginLog.Error($"Found TalkMessage: {(this.FoundTalkMessage != null ? this.FoundTalkMessage.ToString() : "No data found!")}");
#endif
            if (findings)
            {
              this.CurrentTalkMessage = this.FoundTalkMessage;
              var translatedText = this.FoundTalkMessage?.TranslatedTalkMessage;
              var nameTranslation = this.FoundTalkMessage?.TranslatedSenderName;

              if (!this.configuration.UseImGuiForTalk)
              {
#if DEBUG
                PluginLog.LogVerbose($"From database - Name: {this.FoundTalkMessage?.TranslatedSenderName}, Message: {this.FoundTalkMessage?.TranslatedTalkMessage}");
#endif
                if (this.configuration.TranslateNpcNames)
                {
                  nameTextNode->SetText(nameTranslation == string.Empty ? nameNodeText : this.FoundTalkMessage?.TranslatedSenderName);
                  var formattedTranslatedText = FormatText(this.FoundTalkMessage?.TranslatedTalkMessage);
                  PluginLog.LogWarning($"Formatted node text: {formattedTranslatedText}");
                  textTextNode->SetText(formattedTranslatedText);
                }
                else
                {
                  var formattedTranslatedText = FormatText(translatedText);
                  PluginLog.LogWarning($"Formatted node text: {formattedTranslatedText}");
                  textTextNode->SetText(formattedTranslatedText);
                }
              }
              else
              {
                if (!this.configuration.SwapTextsUsingImGui)
                {
                  if (this.configuration.TranslateNpcNames)
                  {
                    this.currentNameTranslationId = Environment.TickCount;
                    this.currentNameTranslation = Resources.WaitingForTranslation;
                    Task.Run(
                      () =>
                      {
                        var nameId = this.currentNameTranslationId;
                        var senderNameTranslation = this.FoundTalkMessage.TranslatedSenderName;
                        this.nameTranslationSemaphore.Wait();
                        if (nameId == this.currentNameTranslationId)
                        {
                          this.currentNameTranslation = senderNameTranslation;
#if DEBUG
                          PluginLog.Error($"Using overlay - name found in DB: {senderNameTranslation} ");
#endif
                        }

                        this.nameTranslationSemaphore.Release();
                      });
                  }

                  this.currentTalkTranslationId = Environment.TickCount;
                  this.currentTalkTranslation = Resources.WaitingForTranslation;
                  Task.Run(
                    () =>
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
                    Task.Run(
                      () =>
                      {
                        var nameId = this.currentNameTranslationId;
                        var senderName = this.FoundTalkMessage.SenderName;
                        this.nameTranslationSemaphore.Wait();
                        if (nameId == this.currentNameTranslationId)
                        {
                          this.currentNameTranslation = senderName;
#if DEBUG
                          PluginLog.Error($"Using overlay - name found in DB: {senderName} ");
#endif
                        }
                        this.nameTranslationSemaphore.Release();
                      });
                  }

                  this.currentTalkTranslationId = Environment.TickCount;
                  this.currentTalkTranslation = Resources.WaitingForTranslation;
                  Task.Run(
                    () =>
                    {
                      var id = this.currentTalkTranslationId;
                      var originalTalkMessage = this.FoundTalkMessage.OriginalTalkMessage;
                      this.talkTranslationSemaphore.Wait();
                      if (id == this.currentTalkTranslationId)
                      {
                        this.currentTalkTranslation = originalTalkMessage;
#if DEBUG
                        PluginLog.Error($"Using overlay - message found in DB: {originalTalkMessage} ");
#endif
                      }

                      this.talkTranslationSemaphore.Release();
                    });

                  // TODO: fix to use detected node elements
                  nameTextNode->SetText(this.FoundTalkMessage?.TranslatedSenderName);
                  textTextNode->SetText(FormatText(this.FoundTalkMessage?.TranslatedTalkMessage));
                }
              }
            }/*
            else
            {
              if (translatedTalkMessage != null)
              {
                if (this.configuration.UseImGuiForTalk)
                {
                  if (this.configuration.TranslateNpcNames)
                  {
                    this.currentNameTranslation = translatedTalkMessage.TranslatedSenderName;
                  }

                  this.currentTalkTranslation =
                    translatedTalkMessage.TranslatedTalkMessage;

                  this.talkDisplayTranslation = true;
                  this.talkTextDimensions.X =
                    talkMaster->RootNode->Width * talkMaster->Scale;
                  this.talkTextDimensions.Y =
                    talkMaster->RootNode->Height * talkMaster->Scale;
                  this.talkTextPosition.X = talkMaster->RootNode->X;
                  this.talkTextPosition.Y = talkMaster->RootNode->Y;
                }
                else
                {
                  // TODO: handle the payloads the game uses
                  if (nameNodeText == translatedTalkMessage.SenderName)
                  {
                    nameTextNode->SetText(translatedTalkMessage.TranslatedSenderName);
                  }

                  if (textNodeText == translatedTalkMessage.OriginalTalkMessage)
                  {
                    textTextNode->SetText(translatedTalkMessage.TranslatedTalkMessage);
                    textTextNode->ResizeNodeForCurrentText();
                  }
                }

#if DEBUG
                PluginLog.LogVerbose("13");
#endif
              }
              else
              {
                this._talkMessageQueue.Enqueue(
                  new TalkMessage(
                    nameNodeText,
                    textNodeText,
                    LangIdentify(textNodeText),
                    LangIdentify(nameNodeText),
                    string.Empty,
                    string.Empty,
                    this.LanguagesDictionary[this.configuration.Lang].Code,
                    chosenTransEngine,
                    DateTime.UtcNow,
                    null));
              }
            }*/
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
      catch (Exception ex)
      {
        PluginLog.Error($"Error in TalkDisplayTranslation: {ex}");
      }
    }

    // TODO: probably won't use this anymore
    private void GetTalk(ref SeString name, ref SeString text, ref TalkStyle style)
    {
      if (!this.configuration.TranslateTalk)
      {
        return;
      }
#if DEBUG
      using StreamWriter logStream = new(this.configDir + "GetTalkLog.txt", true);
#endif

      try
      {
#if DEBUG
        PluginLog.Log(name.TextValue + ": " + text.TextValue);
#endif

        var nameToTranslate = !name.TextValue.IsNullOrEmpty() ? name.TextValue : "System Message";
        var textToTranslate = text.TextValue;

        var talkMessage = this.FormatTalkMessage(nameToTranslate, textToTranslate);
        this.talkMessageQueue.Enqueue(talkMessage);

#if DEBUG
        PluginLog.LogVerbose($"Before DB Query attempt: {talkMessage}");
#endif
        var findings = this.FindTalkMessage(talkMessage);
#if DEBUG
        PluginLog.LogVerbose(
          $"After DB Query attempt: {(findings ? "Message found in Db." : "Message not found in Db")}");
#endif

        // If the dialogue is not saved
        if (!findings)
        {
          if (!this.configuration.UseImGuiForTalk)
          {
            /*
#if DEBUG

//tentative async
PluginLog.LogVerbose("1");
this._talkMessageQueue.Enqueue(new TalkMessage(nameToTranslate, textToTranslate, LangIdentify(textToTranslate),
  LangIdentify(nameToTranslate), string.Empty, string.Empty, this.LanguagesDictionary[this.configuration.Lang].Code,
  this.configuration.ChosenTransEngine, DateTime.Now,
  DateTime.Now));

if (this._translatedTalkMessageQueue.IsEmpty)
{
  PluginLog.LogVerbose("Translation Queue is empty");
  //name = Resources.WaitingForTranslation;
  //text = Resources.WaitingForTranslation;
}
else
{
  PluginLog.LogVerbose("21");
  this._translatedTalkMessageQueue.TryDequeue(out var dialogue);
  PluginLog.LogVerbose("22");
  name = dialogue?.TranslatedSenderName;
  text = dialogue?.TranslatedTalkMessage;
  PluginLog.LogVerbose("23");
}

// end tentative

#else*/
            PluginLog.LogVerbose("14");
            var translatedText = Translate(textToTranslate);
            var nameTranslation = nameToTranslate.IsNullOrEmpty() ? string.Empty : Translate(nameToTranslate);
#if DEBUG
            PluginLog.LogVerbose(translatedText);
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
                this.LanguagesDictionary[this.configuration.Lang].Code,
                this.configuration.ChosenTransEngine,
                DateTime.Now,
                DateTime.Now);
#if DEBUG
              logStream.WriteLineAsync($"Before Talk Messages table data insertion:  {translatedTalkData}");
#endif
              var result = this.InsertTalkData(translatedTalkData);
#if DEBUG
              PluginLog.LogVerbose($"Talk Message DB Insert operation result: {result}");
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
                this.LanguagesDictionary[this.configuration.Lang].Code,
                this.configuration.ChosenTransEngine,
                DateTime.Now,
                DateTime.Now);

              var result = this.InsertTalkData(translatedTalkData);
#if DEBUG
              PluginLog.LogVerbose(result);
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
                Task.Run(
                  () =>
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
                          PluginInterface.UiBuilder.LoadImage(
                            TranslationImageConverter(this.DrawText(this.currentNameTranslation)));
                      }
                    }

                    this.nameTranslationSemaphore.Release();
                  });
              }

              this.currentTalkTranslationId = Environment.TickCount;
              this.currentTalkTranslation = Resources.WaitingForTranslation;
              Task.Run(
                () =>
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
                                        PluginLog.LogVerbose("Lang is 2!");
  #endif
                                        var textAsImage = this.DrawText(this.currentTalkTranslation);
                                        var textImageAsBytes = this.TranslationImageConverter(textAsImage);
  #if DEBUG
                                        PluginLog.LogVerbose($"image bytes: {textImageAsBytes}");
  #endif
                                        this.currentTalkTranslationTexture =
                                          pluginInterface.UiBuilder.LoadImage(textImageAsBytes);
                                      }*/
                  }

                  this.talkTranslationSemaphore.Release();

#if DEBUG
                  PluginLog.LogVerbose($"Before if talk translation: {this.currentTalkTranslation}");
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
                      this.LanguagesDictionary[this.configuration.Lang].Code,
                      this.configuration.ChosenTransEngine,
                      DateTime.Now,
                      DateTime.Now);
                    var result = this.InsertTalkData(translatedTalkData);
#if DEBUG
                    PluginLog.LogVerbose($"Talk Message DB Insert operation result: {result}");
#endif
                  }
                });
            }
            else
            {
#if DEBUG
              PluginLog.LogVerbose("Using Swap text for translation");
#endif
              var translatedText = Translate(textToTranslate);
              var nameTranslation = nameToTranslate.IsNullOrEmpty() ? string.Empty : Translate(nameToTranslate);
              if (this.configuration.TranslateNpcNames)
              {
                name = nameTranslation == string.Empty ? name : nameTranslation;
                text = translatedText;

                this.currentNameTranslationId = Environment.TickCount;
                this.currentNameTranslation = Resources.WaitingForTranslation;
                Task.Run(
                  () =>
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
                Task.Run(
                  () =>
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
                  this.LanguagesDictionary[this.configuration.Lang].Code,
                  this.configuration.ChosenTransEngine,
                  DateTime.Now,
                  DateTime.Now);
#if DEBUG
                logStream.WriteLineAsync($"Before Talk Messages table data insertion:  {translatedTalkData}");
#endif
                var result = this.InsertTalkData(translatedTalkData);
#if DEBUG
                PluginLog.LogVerbose($"Talk Message DB Insert operation result: {result}");
#endif
              }
              else
              {
                text = translatedText;

                this.currentTalkTranslationId = Environment.TickCount;
                this.currentTalkTranslation = Resources.WaitingForTranslation;
                Task.Run(
                  () =>
                  {
                    var id = this.currentTalkTranslationId;
                    this.talkTranslationSemaphore.Wait();
                    if (id == this.currentTalkTranslationId)
                    {
                      this.currentTalkTranslation = textToTranslate;
                    }

                    this.talkTranslationSemaphore.Release();
                    /*#if DEBUG
                                      PluginLog.LogVerbose($"Before if talk translation: {this.currentTalkTranslation}");
                    //#endif*/
                    /*if (this.currentNameTranslation != Resources.WaitingForTranslation &&
                        this.currentTalkTranslation != Resources.WaitingForTranslation)
                    {
                      var translatedTalkData = new TalkMessage(this.currentNameTranslation, this.currentTalkTranslation,
                        LangIdentify(this.currentTalkTranslation),
                        LangIdentify(this.currentNameTranslation),
                        this.configuration.TranslateNPCNames ? Translate(this.currentNameTranslation) : string.Empty,
                        Translate(this.currentTalkTranslation), this.LanguagesDictionary[this.configuration.Lang].Code,
                        this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
                      var result = this.InsertTalkData(translatedTalkData);
  #if DEBUG
                      PluginLog.LogVerbose($"Talk Message DB Insert operation result: {result}");
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
                  this.LanguagesDictionary[this.configuration.Lang].Code,
                  this.configuration.ChosenTransEngine,
                  DateTime.Now,
                  DateTime.Now);

                var result = this.InsertTalkData(translatedTalkData);
#if DEBUG
                PluginLog.LogVerbose(result);
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
            PluginLog.LogVerbose($"From database - Name: {nameTranslation}, Message: {translatedText}");
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
                Task.Run(
                  () =>
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
              Task.Run(
                () =>
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
                Task.Run(
                  () =>
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
              Task.Run(
                () =>
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
  }
}