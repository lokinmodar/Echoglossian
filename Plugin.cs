using System;
using Dalamud.Plugin;
using Echoglossian.Attributes;

namespace Echoglossian
{
    public class Plugin : IDalamudPlugin
    {
        internal DalamudPluginInterface pluginInterface;
        private PluginCommandManager<Plugin> commandManager;
        internal Configuration config;
        internal PluginUI ui;
        private Glossian Glossian { get; set; } = null!;


        public string Name => "Echoglossian";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;

            config = (Configuration) this.pluginInterface.GetPluginConfig() ?? new Configuration();
            config.Initialize(this.pluginInterface);

            Glossian = new Glossian(this);

            ui = new PluginUI();
            this.pluginInterface.UiBuilder.OnBuildUi += ui.Draw;

            commandManager = new PluginCommandManager<Plugin>(this, this.pluginInterface);
        }

        [Command("/eglo")]
        [HelpMessage("A helpful message...")]
        public void Eglo(string command, string args)
        {
            // You may want to assign these references to private variables for convenience.
            // Keep in mind that the local player does not exist until after logging in.
            var chat = pluginInterface.Framework.Gui.Chat;
            var world = pluginInterface.ClientState.LocalPlayer.CurrentWorld.GameData;
            chat.Print($"Hello {world.Name}!");
            PluginLog.Log("Message sent successfully.");
        }

        [Command("/eglotest")]
        [HelpMessage("Another helpful message...")]
        public void EgloTest(string command, string args)
        {
            // You may want to assign these references to private variables for convenience.
            // Keep in mind that the local player does not exist until after logging in.
            var dialogBox = Glossian;


            var chat = pluginInterface.Framework.Gui.Chat;
            //var world = this.pluginInterface.ClientState.LocalPlayer.CurrentWorld.GameData;
            chat.Print($"Address: {dialogBox}!");
            PluginLog.Log("Opa!");
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            commandManager.Dispose();

            pluginInterface.SavePluginConfig(config);

            pluginInterface.UiBuilder.OnBuildUi -= ui.Draw;

            pluginInterface.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}