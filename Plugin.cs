using System;
using System.Collections.Generic;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using Echoglossian.Attributes;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace Echoglossian
{
    public class Plugin : IDalamudPlugin
    {
        internal DalamudPluginInterface PluginInterface;
        private PluginCommandManager<Plugin> _commandManager;
        internal Configuration Config;
        internal PluginUi Ui;
        private Glossian Glossian { get; set; } = null!;

        internal Echoglossian Echoglossian { get; set; } = null!;


        internal bool _config;
        internal int _languageInt = 16;
        internal int _languageInt2; 
        internal UiColorPick[] _textColour;
        internal UiColorPick _chooser;
        internal bool _picker;
        internal int _tranMode;
        internal int _oneInt;
        internal bool _oneChan;
        internal readonly string[] _tranModeOptions = { "Append", "Replace", "Additional" };
        internal ExcelSheet<UIColor> _uiColours;
        internal bool _notSelf;
        internal bool _whitelist;
        internal List<string> _blacklist;
        internal List<int> _chosenLanguages;
        internal List<XivChatType> _channels = new();
        internal readonly List<string> _lastTranslations = new();


        public string Name => "Echoglossian";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.PluginInterface = pluginInterface;

            Config = (Configuration) this.PluginInterface.GetPluginConfig() ?? new Configuration();
            Config.Initialize(this.PluginInterface);
            if (Config != null)
            {
                _config = true;
            }

            Glossian = new Glossian(this);

            Echoglossian = new Echoglossian(this);

            Ui = new PluginUi(this);
            this.PluginInterface.UiBuilder.OnBuildUi += Ui.Draw;

            _commandManager = new PluginCommandManager<Plugin>(this, this.PluginInterface);

            _uiColours = pluginInterface.Data.Excel.GetSheet<UIColor>();
            //_channels = Config.Channels;
            _textColour = Config.TextColour;
            _tranMode = Config.TranMode;
            _languageInt = Config.Lang;
            _whitelist = Config.Whitelist;
            _notSelf = Config.NotSelf;
            _oneChan = Config.OneChan;
            _oneInt = Config.OneInt;
            _chosenLanguages = Config.ChosenLanguages;
            //_blacklist = Config.Blacklist;

        }

        [Command("/eglo")]
        [HelpMessage("A helpful message...")]
        public void Eglo(string command, string args)
        {
            // You may want to assign these references to private variables for convenience.
            // Keep in mind that the local player does not exist until after logging in.
            var chat = PluginInterface.Framework.Gui.Chat;
            var world = PluginInterface.ClientState.LocalPlayer.CurrentWorld.GameData;
            chat.Print($"Hello {world.Name}!");
            PluginLog.Log("Message sent successfully.");
        }

        [Command("/eglotest")]
        [HelpMessage("Another helpful message...")]
        public void EgloTest(string command, string args)
        {
            // You may want to assign these references to private variables for convenience.
            // Keep in mind that the local player does not exist until after logging in.
            this.Ui.Draw();

        
            //var chat = PluginInterface.Framework.Gui.Chat;
            //var world = this.pluginInterface.ClientState.LocalPlayer.CurrentWorld.GameData;
            //chat.Print($"Address: {dialogBox}!");
            PluginLog.Log("Opa!");
        }


        public class UiColorPick
        {
            public uint Choice { get; set; }
            public uint Option { get; set; }
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            _commandManager.Dispose();

            PluginInterface.SavePluginConfig(Config);

            PluginInterface.UiBuilder.OnBuildUi -= Ui.Draw;

            PluginInterface.Dispose();

            Glossian.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}