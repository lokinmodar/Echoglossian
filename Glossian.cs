using System;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin;
using XivCommon;
using XivCommon.Functions;

namespace Echoglossian
{
    public class Glossian : IDisposable
    {
        private Plugin Plugin { get; }
        private XivCommonBase Common { get; }

        public Glossian(Plugin plugin)
        {
            Plugin = plugin;

            Common = new XivCommonBase(Plugin.pluginInterface, Hooks.Talk | Hooks.BattleTalk);
            Common.Functions.Talk.OnTalk += MessWithText;
            Common.Functions.BattleTalk.OnBattleTalk += MessWithText;
        }

        private static void MessWithText(ref SeString name, ref SeString text, ref TalkStyle style)
        {
            try
            {
                PluginLog.Log(name.TextValue + ": " + text.TextValue);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static void MessWithText(ref SeString sender, ref SeString message, ref BattleTalkOptions options,
            ref bool ishandled)
        {
            try
            {
                PluginLog.Log(sender.TextValue + ": " + message.TextValue);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public void Dispose()
        {
            Common.Functions.Talk.OnTalk -= MessWithText;
            Common.Functions.BattleTalk.OnBattleTalk -= MessWithText;
            Plugin?.Dispose();
            Common?.Dispose();
        }
    }
}