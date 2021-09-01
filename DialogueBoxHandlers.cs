using System;
using System.Threading.Tasks;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin;
using XivCommon.Functions;

namespace Echoglossian
{
  public partial class Echoglossian
  {
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
          var translatedText = Translate(textToTranslate);
          PluginLog.LogWarning(translatedText);

          text = translatedText;
          PluginLog.Log(name.TextValue + ": " + text.TextValue);
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