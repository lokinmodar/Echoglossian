// TODOS
// Publish

using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using XivCommon;
using XivCommon.Functions;

namespace Echoglossian
{
    public partial class Echoglossian : IDalamudPlugin
    {
        public string Name => "Echoglossian";
        private const string SlashCommand = "/eglo";
        private DalamudPluginInterface _pluginInterface;
        private Config _configuration;
        private bool _config;
        
        private static int _languageInt = 16;
        //private int _languageInt2;
        private UiColorPick[] _textColour;
        private UiColorPick _chooser;
        private bool _picker;
        private int _tranMode;
        private int _oneInt;
        private bool _oneChan;
        //private readonly string[] _tranModeOptions = { "Append", "Replace", "Additional" };
        private Lumina.Excel.ExcelSheet<UIColor> _uiColours;
        private bool _notSelf;
        private bool _whitelist;
        private List<string> _blacklist;
        private List<int> _chosenLanguages;
        private List<XivChatType> _channels = new List<XivChatType>();
        //private readonly List<string> _lastTranslations = new List<string>();

     /*   private readonly List<XivChatType> _order = new List<XivChatType>
        {
            XivChatType.None,
            XivChatType.Debug,
            XivChatType.Urgent,
            XivChatType.Notice,
            XivChatType.Say,
            XivChatType.Shout,
            XivChatType.TellOutgoing,
            XivChatType.TellIncoming,
            XivChatType.Party,
            XivChatType.Alliance,
            XivChatType.Ls1,
            XivChatType.Ls2,
            XivChatType.Ls3,
            XivChatType.Ls4,
            XivChatType.Ls5,
            XivChatType.Ls6,
            XivChatType.Ls7,
            XivChatType.Ls8,
            XivChatType.FreeCompany,
            XivChatType.NoviceNetwork,
            XivChatType.CustomEmote,
            XivChatType.StandardEmote,
            XivChatType.Yell,
            XivChatType.CrossParty,
            XivChatType.PvPTeam,
            XivChatType.CrossLinkShell1,
            XivChatType.Echo,
            XivChatType.SystemMessage,
            XivChatType.SystemError,
            XivChatType.GatheringSystemMessage,
            XivChatType.ErrorMessage,
            XivChatType.RetainerSale,
            XivChatType.CrossLinkShell2,
            XivChatType.CrossLinkShell3,
            XivChatType.CrossLinkShell4,
            XivChatType.CrossLinkShell5,
            XivChatType.CrossLinkShell6,
            XivChatType.CrossLinkShell7,
            XivChatType.CrossLinkShell8
        };*/

      /*  private readonly string[] _orderString =
        {
            "None",
            "Debug",
            "Urgent",
            "Notice",
            "Say",
            "Shout",
            "TellOutgoing",
            "TellIncoming",
            "Party",
            "Alliance",
            "Ls1",
            "Ls2",
            "Ls3",
            "Ls4",
            "Ls5",
            "Ls6",
            "Ls7",
            "Ls8",
            "FreeCompany",
            "NoviceNetwork",
            "CustomEmote",
            "StandardEmote",
            "Yell",
            "CrossParty",
            "PvPTeam",
            "CrossLinkShell1",
            "Echo",
            "SystemMessage",
            "SystemError",
            "GatheringSystemMessage",
            "ErrorMessage",
            "RetainerSale",
            "CrossLinkShell2",
            "CrossLinkShell3",
            "CrossLinkShell4",
            "CrossLinkShell5",
            "CrossLinkShell6",
            "CrossLinkShell7",
            "CrossLinkShell8"
        };

        private readonly bool[] _yesNo = {
            false, false, false, false, true,
            true, false, true, true, true,
            true, true, true, true, true,
            true, true, true, true, true,
            true, false, true, true, true,
            true, true, false, false, false,
            false, false, true, true, true,
            true, true, true, true
        };*/

        private static readonly string[] _codes = {
            "af", "an", "ar", "az", "be_x_old",
            "bg", "bn", "br", "bs",
            "ca", "ceb", "cs", "cy", "da",
            "de", "el", "en", "eo", "es",
            "et", "eu", "fa", "fi", "fr",
            "gl", "he", "hi", "hr", "ht",
            "hu", "hy", "id", "is", "it",
            "ja", "jv", "ka", "kk", "ko",
            "la", "lb", "lt", "lv",
            "mg", "mk", "ml", "mr", "ms",
            "new", "nl", "nn", "no", "oc",
            "pl", "pt", "ro", "roa_rup",
            "ru", "sk", "sl",
            "sq", "sr", "sv", "sw", "ta",
            "te", "th", "tl", "tr", "uk",
            "ur", "vi", "vo", "war", "zh",
            "zh_classical", "zh_yue"
        };

        private readonly string[] _languages = {
            "Afrikaans", "Aragonese", "Arabic", "Azerbaijani", "Belarusian",
            "Bulgarian", "Bengali", "Breton", "Bosnian",
            "Catalan; Valencian", "Cebuano", "Czech", "Welsh", "Danish",
            "German", "Greek, Modern", "English", "Esperanto", "Spanish; Castilian",
            "Estonian", "Basque", "Persian", "Finnish", "French",
            "Galician", "Hebrew", "Hindi", "Croatian", "Haitian; Haitian Creole",
            "Hungarian", "Armenian", "Indonesian", "Icelandic", "Italian",
            "Japanese", "Javanese", "Georgian", "Kazakh", "Korean",
            "Latin", "Luxembourgish; Letzeburgesch", "Lithuanian", "Latvian",
            "Malagasy", "Macedonian", "Malayalam", "Marathi", "Malay",
            "Nepal Bhasa; Newari", "Dutch; Flemish", "Norwegian Nynorsk; Nynorsk, Norwegian", "Norwegian", "Occitan (post 1500)",
            "Polish", "Portuguese", "Romanian; Moldavian; Moldovan", "Romance languages",
            "Russian", "Slovak", "Slovenian",
            "Albanian", "Serbian", "Swedish", "Swahili", "Tamil",
            "Telugu", "Thai", "Tagalog", "Turkish", "Ukrainian",
            "Urdu", "Vietnamese", "Volapük", "Waray", "Chinese",
            "Chinese Classical", "Chinese yue"
        };

        public void Initialize(DalamudPluginInterface pluginInterface)
        {

            _pluginInterface = pluginInterface;
            _configuration = pluginInterface.GetPluginConfig() as Config ?? new Config();

            Common = new XivCommonBase(_pluginInterface, Hooks.Talk | Hooks.BattleTalk);

            Common.Functions.Talk.OnTalk += GetText;
            Common.Functions.BattleTalk.OnBattleTalk += GetBattleText;


            //_pluginInterface.Framework.Gui.Chat.OnChatMessage += Chat_OnChatMessage;
            _pluginInterface.UiBuilder.OnBuildUi += EchoglossianConfigUi;
            _pluginInterface.UiBuilder.OnOpenConfigUi += EchoglossianConfig;
            _pluginInterface.CommandManager.AddHandler(SlashCommand, new CommandInfo(Command)
            {
                HelpMessage = "Opens Echoglossian config window"
            });
            _uiColours = pluginInterface.Data.Excel.GetSheet<UIColor>();
            _channels = _configuration.Channels;
            _textColour = _configuration.TextColour;
            _tranMode = _configuration.TranMode;
            _languageInt = _configuration.Lang;
            _whitelist = _configuration.Whitelist;
            _notSelf = _configuration.NotSelf;
            _oneChan = _configuration.OneChan;
            _oneInt = _configuration.OneInt;
            _chosenLanguages = _configuration.ChosenLanguages;
            _blacklist = _configuration.Blacklist;
        }

        public void Dispose()
        {
            Common.Functions.Talk.OnTalk -= GetText;
            Common.Functions.BattleTalk.OnBattleTalk -= GetBattleText;
            //_pluginInterface.Framework.Gui.Chat.OnChatMessage -= Chat_OnChatMessage;
            _pluginInterface.UiBuilder.OnBuildUi -= EchoglossianConfigUi;
            _pluginInterface.UiBuilder.OnOpenConfigUi -= EchoglossianConfig;
            _pluginInterface.CommandManager.RemoveHandler(SlashCommand);
        }

        private void Command(string command, string arguments) => _config = true;

        // What to do when plugin install config button is pressed
        private void EchoglossianConfig(object sender, EventArgs args) => _config = true;
    }

    public class UiColorPick
    {
        public uint Choice { get; set; }
        public uint Option { get; set; }
    }

    public class Config : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public List<XivChatType> Channels { get; set; } = new List<XivChatType>();
        public int Lang { get; set; } = 16;
        public UiColorPick[] TextColour { get; set; } =
        {
            new UiColorPick { Choice = 0, Option =0 }
        };
        public bool NotSelf { get; set; }
        public bool Whitelist { get; set; }
        public List<int> ChosenLanguages { get; set; } = new List<int>();
        public bool OneChan { get; set; }
        public int OneInt { get; set; }
        public List<string> Blacklist { get; set; } = new List<string>();
        public int TranMode { get; set; }
    }
}
