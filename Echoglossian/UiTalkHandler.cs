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
    private readonly ConcurrentQueue<TalkMessage> _talkMessageQueue = new();
    private readonly ConcurrentQueue<TalkMessage> _translatedTalkMessageQueue = new();
    private ConcurrentDictionary<int, TalkMessage> _talkConcurrentDictionary = new(); // not using this right now

    public bool talkTracker { get; set; } = false;

    private void HandleTalkAsync()
    {
      try
      {
        Thread t = new(this.TalkProc);
        t.Start();
      }
      catch (Exception e)
      {
        PluginLog.LogError($"Talk Translation Thread Error: {e}");
        throw;
      }
    }

    // TODO: clear queues before use!
    private void TalkProc()
    {
      PluginLog.Log("Translation Engine Started");
      while (this.configuration.TranslateTalk)
      {
        if (this._talkMessageQueue.IsEmpty)
        {
          PluginLog.LogVerbose("4");
          this._talkMessageQueue.TryPeek(out var dialogue);
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
            this._translatedTalkMessageQueue.Enqueue(talkMessageToTranslate);
#if DEBUG
            PluginLog.LogVerbose("10");
#endif
          }
        }
      }
    }

    // not working but it is not retrying to translate all the time. Needs tracking logic
    private unsafe void
      TalkHandler(
        string addonName,
        int index)
    {
#if DEBUG
      using StreamWriter logStream = new(this.configDir + "GetTalkLog.txt", true);
#endif
      if (!this.configuration.TranslateTalk)
      {
        return;
      }

      try
      {
        var talk = GameGui.GetAddonByName(addonName, index);
        if (talk != IntPtr.Zero)
        {
          var talkMaster = (AtkUnitBase*)talk;
          if (talkMaster->IsVisible)
          {
            var nameTextNode = talkMaster->GetTextNodeById(2);
            var nameNodeText = nameTextNode->NodeText.ToString();
            var textTextNode = talkMaster->GetTextNodeById(3);
            var textNodeText = textTextNode->NodeText.ToString();
#if DEBUG
            PluginLog.LogError($"Name Node text: {nameNodeText}");
            PluginLog.LogError($"Text Node text: {textNodeText}");
#endif
            if (nameNodeText.IsNullOrEmpty() && textNodeText.IsNullOrEmpty())
            {
              return;
            }

            var nameToTranslate = !nameNodeText.IsNullOrEmpty() ? nameNodeText : string.Empty;
            var textToTranslate = textNodeText;

            var talkMessage = this.FormatTalkMessage(nameToTranslate, textToTranslate);

            // this._talkMessageQueue.Enqueue(talkMessage);

#if DEBUG
            PluginLog.LogVerbose($"Before DB Query attempt: {talkMessage}");
#endif
            var findings = this.FindTalkMessage(talkMessage);
#if DEBUG
            PluginLog.LogVerbose(
              $"After DB Query attempt: {(findings ? "Message found in Db." : "Message not found in Db")}");
#endif
            if (findings)
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
                  // TODO: fix this to use node elements
                  name = nameTranslation == string.Empty ? name : nameTranslation;
                  text = translatedText;
                }
                else
                {
                  // TODO: fix this to use node elements
                  text = translatedText;
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

                  // TODO: fix to use detected node elements
                  name = this.FoundTalkMessage.TranslatedSenderName;
                  text = this.FoundTalkMessage.TranslatedTalkMessage;
                }
              }
            }
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
                    langDict[languageInt].Code,
                    chosenTransEngine,
                    DateTime.UtcNow,
                    null));
              }
            }
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
        this._talkMessageQueue.Enqueue(talkMessage);

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
  LangIdentify(nameToTranslate), string.Empty, string.Empty, langDict[languageInt].Code,
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
                            this.TranslationImageConverter(this.DrawText(this.currentNameTranslation)));
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
                                      PluginLog.LogError($"Before if talk translation: {this.currentTalkTranslation}");
                    //#endif*/
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