using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using XivCommon;

namespace Echoglossian
{

    //TODO: Handle polish alphabet correctly. 
    //TODO: Add translation database history.
    //TODO: implement multiple fallback translation engines.
    public partial class Echoglossian : IDalamudPlugin
    {
        public string Name => "Echoglossian";
        private const string SlashCommand = "/eglo";
        private DalamudPluginInterface _pluginInterface;
        private Config _configuration;
        private bool _config;
        private static int _languageInt = 16;
        private UiColorPick _chooser;
        private bool _picker;
        private ExcelSheet<UIColor> _uiColours;
        private List<int> _chosenLanguages;


        private static readonly string[] Codes =
        {
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

        private readonly string[] _languages =
        {
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
            "Nepal Bhasa; Newari", "Dutch; Flemish", "Norwegian Nynorsk; Nynorsk, Norwegian", "Norwegian",
            "Occitan (post 1500)",
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

            _pluginInterface.UiBuilder.OnBuildUi += EchoglossianConfigUi;
            _pluginInterface.UiBuilder.OnOpenConfigUi += EchoglossianConfig;
            _pluginInterface.CommandManager.AddHandler(SlashCommand, new CommandInfo(Command)
            {
                HelpMessage = "Opens Echoglossian config window"
            });
            _uiColours = pluginInterface.Data.Excel.GetSheet<UIColor>();
            _languageInt = _configuration.Lang;
            _chosenLanguages = _configuration.ChosenLanguages;
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

        private void Command(string command, string arguments)
        {
            _config = true;
        }

        // What to do when plugin install config button is pressed
        private void EchoglossianConfig(object sender, EventArgs args)
        {
            _config = true;
        }
    }

    public class UiColorPick
    {
        public uint Choice { get; set; }
        public uint Option { get; set; }
    }

    public class Config : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public int Lang { get; set; } = 16;

        public List<int> ChosenLanguages { get; set; } = new();
    }
}