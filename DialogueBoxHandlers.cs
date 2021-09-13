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
        var textToTranslate = message.TextValue;

        if (!this.configuration.UseImGui)
        {
          var translatedText = Translate(textToTranslate);

          message = translatedText;
        }
        else
        {
          this.currentAddonTranslationId = Environment.TickCount;
          this.currentAddonTranslation = Resources.WaitingForTranslation;
          Task.Run(() =>
          {
            var id = this.currentAddonTranslationId;
            var translation = Translate(textToTranslate);
            this.TranslationSemaphore.Wait();
            if (id == this.currentAddonTranslationId)
            {
              this.currentAddonTranslation = translation;
            }

            this.TranslationSemaphore.Release();
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
        var textToTranslate = message.TextValue;

        if (!this.configuration.UseImGui)
        {
          var translatedText = Translate(textToTranslate);

          message = translatedText;
        }
        else
        {
          this.currentAddonTranslationId = Environment.TickCount;
          this.currentAddonTranslation = Resources.WaitingForTranslation;
          Task.Run(() =>
          {
            var id = this.currentAddonTranslationId;
            var translation = Translate(textToTranslate);
            this.TranslationSemaphore.Wait();
            if (id == this.currentAddonTranslationId)
            {
              this.currentAddonTranslation = translation;
            }

            this.TranslationSemaphore.Release();
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
        var textToTranslate = message.TextValue;

        if (!this.configuration.UseImGui)
        {
          var translatedText = Translate(textToTranslate);

          message = translatedText;
        }
        else
        {
          this.currentAddonTranslationId = Environment.TickCount;
          this.currentAddonTranslation = Resources.WaitingForTranslation;
          Task.Run(() =>
          {
            var id = this.currentAddonTranslationId;
            var translation = Translate(textToTranslate);
            this.TranslationSemaphore.Wait();
            if (id == this.currentAddonTranslationId)
            {
              this.currentAddonTranslation = translation;
            }

            this.TranslationSemaphore.Release();
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
        // PluginLog.Log(name.TextValue + ": " + text.TextValue);
        var textToTranslate = text.TextValue;

        if (!this.configuration.UseImGui)
        {
          var translatedText = Translate(textToTranslate);
          // PluginLog.LogWarning(translatedText);

          text = translatedText;
          // PluginLog.Log(name.TextValue + ": " + text.TextValue);
        }
        else
        {
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
        // PluginLog.Log(sender.TextValue + ": " + message.TextValue);
        var textToTranslate = message.TextValue;
        // var detectedLanguage = Lang(textToTranslate);
        // PluginLog.LogDebug($"Detected Language: {detectedLanguage}");
        var translatedText = Translate(textToTranslate);
        // PluginLog.LogWarning(translatedText);

        message = translatedText;

        // PluginLog.Log(sender.TextValue + ": " + message.TextValue);
      }
      catch (Exception e)
      {
        PluginLog.Log("Exception: " + e);
        throw;
      }
    }
  }
}