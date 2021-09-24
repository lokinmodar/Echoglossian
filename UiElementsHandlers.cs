// <copyright file="UiElementsHandlers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Properties;
using XivCommon.Functions;

namespace Echoglossian
{
  // TODO: Add logic to invert translation and original texts
  public partial class Echoglossian
  {
    private void OnToast(ref SeString message, ref QuestToastOptions options, ref bool ishandled)
    {
      if (!this.configuration.TranslateToast)
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

    private void OnToast(ref SeString message, ref bool ishandled)
    {
      if (!this.configuration.TranslateToast)
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
      using StreamWriter logStream = new(this.DbOperationsLogPath + "GetToastLog.txt", append: true);
#endif
      if (!this.configuration.TranslateToast)
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
      using StreamWriter logStream = new(this.DbOperationsLogPath + "GetTalkLog.txt", append: true);
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
        PluginLog.LogFatal($"After DB Query attempt: {(findings ? "Message found in Db." : "Message not found in Db")}");
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
                LangIdentify(nameToTranslate), nameTranslation, translatedText, Codes[languageInt], this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
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
                LangIdentify(nameToTranslate), string.Empty, translatedText, Codes[languageInt], this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
#if DEBUG
              var result = this.InsertTalkData(translatedTalkData);
              PluginLog.LogError(result);
#endif
            }
#if DEBUG
            PluginLog.Log($"Using Talk Overlay- {name.TextValue}: {text.TextValue}");
#endif
          }
          else
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
            });
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

      try
      {
#if DEBUG
        PluginLog.Log(sender.TextValue + ": " + message.TextValue);
#endif
        var textToTranslate = message.TextValue;
#if DEBUG
        var detectedLanguage = LangIdentify(textToTranslate);
        PluginLog.LogDebug($"Detected Language: {detectedLanguage}");
#endif
        var translatedText = Translate(textToTranslate);
#if DEBUG
        PluginLog.LogWarning(translatedText);
#endif

        message = translatedText;
#if DEBUG
        PluginLog.Log(sender.TextValue + ": " + message.TextValue);
#endif
      }
      catch (Exception e)
      {
        PluginLog.Log("Exception: " + e);
        throw;
      }
    }
  }
}