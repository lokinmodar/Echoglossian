using System;
using System.Threading.Tasks;
using Dalamud.Game.Internal.Gui.Addon;
using Dalamud.Game.Internal.Gui.Structs;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using XivCommon.Functions;
using Addon = Dalamud.Game.Internal.Gui.Addon.Addon;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public AtkTextNode atn;
    
    

    private void test()
    {
      
    }
    private void GetText(ref SeString name, ref SeString text, ref TalkStyle style)
    {
      if (!_configuration.TranslateTalk) return;
      try
      {
        PluginLog.Log(name.TextValue + ": " + text.TextValue);
        var textToTranslate = text.TextValue;
        var detectedLanguage = Lang(textToTranslate);
        PluginLog.LogDebug($"Detected Language: {detectedLanguage}");
        if (!_configuration.UseImGui)
        {
          talkCurrentTranslationId = Environment.TickCount;
          talkCurrentTranslation = "Awaiting translation...";
          Task.Run(delegate
          {
            var id = talkCurrentTranslationId;
            var text = Translate(textToTranslate);
            talkTranslationSemaphore.Wait();
            if (id == talkCurrentTranslationId) talkCurrentTranslation = text;
            talkTranslationSemaphore.Release();
          });

          while (!talkTranslationSemaphore.Wait(0))
          {
            text = "Awaiting translation...";
          }

          
          text = talkCurrentTranslation;
          talkTranslationSemaphore.Release();
        }
        else
        {
          talkCurrentTranslationId = Environment.TickCount;
          talkCurrentTranslation = "Awaiting translation...";
          Task.Run(delegate
          {
            var id = talkCurrentTranslationId;
            var text = Translate(textToTranslate);
            talkTranslationSemaphore.Wait();
            if (id == talkCurrentTranslationId) talkCurrentTranslation = text;
            talkDisplayTranslation = true;
            talkTranslationSemaphore.Release();
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
      if (!_configuration.TranslateBattleTalk) return;
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