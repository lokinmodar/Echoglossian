using System;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using XivCommon;
using XivCommon.Functions;

namespace Echoglossian
{
    public class Glossian : IDisposable
    {
        private static Echoglossian _echoglossian;
/*
        private readonly TextSubtitle _nullTextSubtitle;

        private Hook<MyGetCutsceneText> _gctHook;
*/
        public Glossian(Plugin plugin)
        {
            Plugin = plugin;
            _echoglossian = new Echoglossian(Plugin);

            Common = new XivCommonBase(Plugin.PluginInterface, Hooks.Talk | Hooks.BattleTalk);
/*
            unsafe
            {
                _nullTextSubtitle.unitBase = null;
            }*/

            Common.Functions.Talk.OnTalk += GetText;
            Common.Functions.BattleTalk.OnBattleTalk += GetBattleText;
            
            /*var intPtr = Plugin.PluginInterface.TargetModuleScanner.ScanText(
                "48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 40 48 63 FA 45 8B F0 48 8B F1 83 FF 34 7C 13 33 C0 48 8B 74 24 ?? 48 8B 7C 24 ?? 48 83 C4 40 41 5E C3");

            _gctHook = new Hook<MyGetCutsceneText>(intPtr, new MyGetCutsceneText(GetCutsceneText));*/
        }

        private Plugin Plugin { get; }
        private XivCommonBase Common { get; }

        #region IDisposable Support
        public void Dispose()
        {
            Common.Functions.Talk.OnTalk -= GetText;
            Common.Functions.BattleTalk.OnBattleTalk -= GetBattleText;
            _echoglossian?.Dispose();
            Plugin?.Dispose();
            Common?.Dispose();
        }

        #endregion

        private void GetText(ref SeString name, ref SeString text, ref TalkStyle style)
        {
            try
            {
                PluginLog.Log(name.TextValue + ": " + text.TextValue);
                var textToTranslate = text.TextValue;
                var detectedLanguage = _echoglossian.Lang(textToTranslate);
                PluginLog.LogDebug($"Detected Language: {detectedLanguage}");
                var translatedText = _echoglossian.Translate(textToTranslate);
                PluginLog.LogWarning(translatedText);
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
            }
            catch (Exception e)
            {
                PluginLog.Log("Exception: " + e);
                throw;
            }
        }

/*        public unsafe TextSubtitle GetCutsceneText()
        {
            var uiObjectByName = Plugin.PluginInterface.Framework.Gui.GetUiObjectByName("TextSubtitle", 1);
            var pointr = (AtkUnitBase*) uiObjectByName.ToPointer();

            if (pointr == null)
            {
                PluginLog.Information("Null pointr_");
                return _nullTextSubtitle;
            }

            var outTextSubtitle = default(TextSubtitle);
            outTextSubtitle.unitBase = pointr;
            PluginLog.Information($"pointr_: {pointr->ToString()}");


            return outTextSubtitle;
        }

        private delegate TextSubtitle MyGetCutsceneText();*/
    }
}