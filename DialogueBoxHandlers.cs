// <copyright file="DialogueBoxHandlers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Resources;
using System.Threading.Tasks;

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
    private void GetText(ref SeString name, ref SeString text, ref TalkStyle style)
    {
      if (!this.configuration.TranslateTalk)
      {
        return;
      }

      try
      {
        PluginLog.Log(name.TextValue + ": " + text.TextValue);
        var textToTranslate = text.TextValue;
        var detectedLanguage = Lang(textToTranslate);
        PluginLog.LogDebug($"Detected Language: {detectedLanguage}");
        if (!this.configuration.UseImGui)
        {
          var translatedText = Translate(textToTranslate);
          PluginLog.LogWarning(translatedText);

          text = translatedText;
          PluginLog.Log(name.TextValue + ": " + text.TextValue);
        }
        else
        {
          this.talkCurrentTranslationId = Environment.TickCount;
          this.talkCurrentTranslation = Resources.WaitingForTranslation;
          Task.Run(() =>
          {
            var id = this.talkCurrentTranslationId;
            var translation = Translate(textToTranslate);
            this.talkTranslationSemaphore.Wait();
            if (id == this.talkCurrentTranslationId)
            {
              this.talkCurrentTranslation = translation;
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
        PluginLog.Log(sender.TextValue + ": " + message.TextValue);
        var textToTranslate = message.TextValue;
        var detectedLanguage = Lang(textToTranslate);
        PluginLog.LogDebug($"Detected Language: {detectedLanguage}");
        var translatedText = Translate(textToTranslate);
        PluginLog.LogWarning(translatedText);

        message = translatedText;

        PluginLog.Log(sender.TextValue + ": " + message.TextValue);
      }
      catch (Exception e)
      {
        PluginLog.Log("Exception: " + e);
        throw;
      }
    }
  }
}