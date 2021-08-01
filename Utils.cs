using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;

namespace Echoglossian
{
    public partial class Echoglossian
    {
        private void SaveConfig()
        {
            _configuration.Lang = _languageInt;
            _configuration.Channels = _channels;
            _configuration.NotSelf = _notSelf;
            _configuration.Whitelist = _whitelist;
            _configuration.ChosenLanguages = _chosenLanguages;
            _configuration.OneChan = _oneChan;
            _configuration.OneInt = _oneInt;
            _configuration.TextColour = _textColour;
            _configuration.Blacklist = _blacklist;
            _configuration.TranMode = _tranMode;
            _pluginInterface.SavePluginConfig(_configuration);
        }

 /*       private void PrintChat(XivChatType type, string senderName, SeString messageSeString)
        {
            var chat = new XivChatEntry
            {
                Type = type,
                Name = senderName,
                MessageBytes = messageSeString.Encode()
            };

            _pluginInterface.Framework.Gui.Chat.PrintChat(chat);
        }
*/
        /*public void PrintChatToLog(SeString debugMe)
        {
            PluginLog.Log("=================");
            PluginLog.Log($"{debugMe.TextValue}");
            foreach (var pl in debugMe.Payloads)
            {
                PluginLog.Log($"TYPE: {pl.Type}");
                if (pl.Type != PayloadType.UIForeground) continue;
                var pl2 = (UIForegroundPayload)pl;
                PluginLog.Log($"--COL:{pl2.UIColor.UIForeground}");

            }

        }*/

    }
}
