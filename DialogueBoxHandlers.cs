using System;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin;
using XivCommon.Functions;

namespace Echoglossian
{
    public partial class Echoglossian
    {
        private static void GetText(ref SeString name, ref SeString text, ref TalkStyle style)
        {
            try
            {
                PluginLog.Log(name.TextValue + ": " + text.TextValue);
                var textToTranslate = text.TextValue;
                var detectedLanguage = Lang(textToTranslate);
                PluginLog.LogDebug($"Detected Language: {detectedLanguage}");
                var translatedText = Translate(textToTranslate);
                PluginLog.LogWarning(translatedText);

                text = translatedText;
                PluginLog.Log(name.TextValue + ": " + text.TextValue);
            }
            catch (Exception e)
            {
                PluginLog.Log("Exception: " + e.StackTrace);
                throw;
            }
        }


        private static void GetBattleText(ref SeString sender, ref SeString message, ref BattleTalkOptions options,
            ref bool ishandled)
        {
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