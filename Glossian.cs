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
            this.Plugin = plugin;

            this.Common = new XivCommonBase(this.Plugin.pluginInterface, Hooks.Talk | Hooks.BattleTalk);
            this.Common.Functions.Talk.OnTalk += MessWithText;
            this.Common.Functions.BattleTalk.OnBattleTalk += MessWithText;



        }

        private static void MessWithText(ref SeString name, ref SeString text, ref TalkStyle style)
        {
            PluginLog.Log(name.TextValue + text.TextValue);
            //throw new NotImplementedException();
        }

        private static void MessWithText(ref SeString sender, ref SeString message, ref BattleTalkOptions options, ref bool ishandled)
        {
            PluginLog.Log(sender.TextValue + message.TextValue);
            //throw new NotImplementedException();
        }

        public void Dispose()
        {
            this.Common.Functions.Talk.OnTalk -= MessWithText;
            this.Common.Functions.BattleTalk.OnBattleTalk -= MessWithText;
            Plugin?.Dispose();
            Common?.Dispose();
        }
    }


    
    
}
