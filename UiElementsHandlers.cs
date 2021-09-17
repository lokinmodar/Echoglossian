// <copyright file="DialogueBoxHandlers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Resources;
using System.Threading.Tasks;

using Dalamud.Game.Gui;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using Echoglossian.Properties;
using XivCommon.Functions;

namespace Echoglossian
{
  /// <summary>
  /// Dialogue box handling.
  /// </summary>
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

    private void GetText(ref SeString name, ref SeString text, ref TalkStyle style)
    {
      if (!this.configuration.TranslateTalk)
      {
        return;
      }

      try
      {
#if DEBUG
        PluginLog.Log(name.TextValue + ": " + text.TextValue);
#endif
        var nameToTranslate = name.TextValue;
        var textToTranslate = text.TextValue;

        if (!this.configuration.UseImGui)
        {
          var translatedText = Translate(textToTranslate);
          var nameTranslation = Translate(nameToTranslate);
#if DEBUG
          PluginLog.LogWarning(translatedText);
#endif
          if (this.configuration.TranslateNPCNames)
          {
            name = nameTranslation;
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
      catch (Exception e)
      {
        PluginLog.Log("Exception: " + e.StackTrace);
        throw;
      }
    }

    private void GetBattleText(ref SeString sender, ref SeString message, ref BattleTalkOptions options,
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