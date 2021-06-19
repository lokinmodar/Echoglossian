using System;
using Dalamud.Plugin;
using Echoglossian.Attributes;

namespace Echoglossian
{
    public class Plugin : IDalamudPlugin
    {
        private DalamudPluginInterface pluginInterface;
        private PluginCommandManager<Plugin> commandManager;
        private Configuration config;
        private PluginUI ui;

        public string Name => "Echoglossian";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;

            this.config = (Configuration)this.pluginInterface.GetPluginConfig() ?? new Configuration();
            this.config.Initialize(this.pluginInterface);

            this.ui = new PluginUI();
            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.Draw;

            this.commandManager = new PluginCommandManager<Plugin>(this, this.pluginInterface);
        }

        [Command("/eglo")]
        [HelpMessage("A helpful message...")]
        public void Eglo(string command, string args)
        {
            // You may want to assign these references to private variables for convenience.
            // Keep in mind that the local player does not exist until after logging in.
            var chat = this.pluginInterface.Framework.Gui.Chat;
            var world = this.pluginInterface.ClientState.LocalPlayer.CurrentWorld.GameData;
            chat.Print($"Hello {world.Name}!");
            PluginLog.Log("Message sent successfully.");
        }

        [Command("/eglotest")]
        [HelpMessage("Another helpful message...")]
        public void EgloTest(string command, string args)
        {
            // You may want to assign these references to private variables for convenience.
            // Keep in mind that the local player does not exist until after logging in.
            var dialogBox = this.pluginInterface.Framework.Gui.GetAddonByName("Text", 1);


            var chat = this.pluginInterface.Framework.Gui.Chat;
            //var world = this.pluginInterface.ClientState.LocalPlayer.CurrentWorld.GameData;
            chat.Print($"Address: {dialogBox.Address}!");
            PluginLog.Log("Opa!");
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.commandManager.Dispose();

            this.pluginInterface.SavePluginConfig(this.config);

            this.pluginInterface.UiBuilder.OnBuildUi -= this.ui.Draw;

            this.pluginInterface.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
