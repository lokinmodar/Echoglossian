using System;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin;
using XivCommon;
using XivCommon.Functions;


namespace Echoglossian
{
    public class Glossian : IDisposable
    {
        internal static Echoglossian _echoglossian;
        private Plugin Plugin { get; }
        private XivCommonBase Common { get; }

        public Glossian(Plugin plugin)
        {
            Plugin = plugin;
            _echoglossian = new Echoglossian(Plugin);

            Common = new XivCommonBase(Plugin.PluginInterface, Hooks.Talk | Hooks.BattleTalk);

            Common.Functions.Talk.OnTalk += GetText;
            Common.Functions.BattleTalk.OnBattleTalk += GetBattleText;
        }




        private static void GetText(ref SeString name, ref SeString text, ref TalkStyle style)
        {
            try
            {
                PluginLog.Log(name.TextValue + ": " + text.TextValue);
                var textToTranslate = text.TextValue;
                var detectedLanguage = _echoglossian.Lang(textToTranslate);
                PluginLog.LogDebug($"Detected Language: {detectedLanguage}");
                var translatedText = _echoglossian.Translate(textToTranslate);
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
                var detectedLanguage = _echoglossian.Lang(textToTranslate);
                PluginLog.LogDebug($"Detected Language: {detectedLanguage}");
                var translatedText = _echoglossian.Translate(textToTranslate);
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


        #region IDisposable Support

        public void Dispose()
        {

            _echoglossian?.Dispose();
            Common.Functions.Talk.OnTalk -= GetText;
            Common.Functions.BattleTalk.OnBattleTalk -= GetBattleText;
            Common?.Functions.Dispose();
            
            Common?.Dispose();



            Plugin?.Dispose();
        }

        #endregion
    }
}