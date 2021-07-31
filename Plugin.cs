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
        public string Name => "Echoglossian";

        internal DalamudPluginInterface PluginInterface;
        private PluginCommandManager<Plugin> _commandManager;
        internal Configuration Config;
        internal PluginUi Ui;
        internal Glossian Glossian { get; set; } = null!;

        internal bool _config;
        internal int _languageInt = 16;
        internal int _languageInt2;
        internal bool _picker;

        internal UiColorPick _chooser;

        internal ExcelSheet<UIColor> _uiColours;
        internal int ChosenLangCode;

        internal List<int> _chosenLanguages;



        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.PluginInterface = pluginInterface;

            this.Config = (Configuration)this.PluginInterface.GetPluginConfig() ?? new Configuration();
            this.Config.Initialize(this.PluginInterface);

            this.Ui = new PluginUi(this);
            this.PluginInterface.UiBuilder.OnBuildUi += this.Ui.Draw;

            this._commandManager = new PluginCommandManager<Plugin>(this, this.PluginInterface);

            if (this.Config != null)
            {
                this._config = true;
            }

            this.Glossian = new Glossian(this);           

            this._uiColours = pluginInterface.Data.Excel.GetSheet<UIColor>();

            this._languageInt = Config.Lang;

            this._chosenLanguages = this.Config.ChosenLanguages;
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


        public void SaveConfig()
        {
            this.Config.Lang = _languageInt;
            this.Config.ChosenLanguages = _chosenLanguages;
            this.PluginInterface.SavePluginConfig(this.Config);
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

            this.Glossian?.Dispose();

            this._commandManager.Dispose();

            this.PluginInterface.SavePluginConfig(this.Config);

            this.PluginInterface.UiBuilder.OnBuildUi -= this.Ui.Draw;

            this.Ui.Dispose();

            this.PluginInterface.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}