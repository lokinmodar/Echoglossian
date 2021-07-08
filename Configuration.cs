using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace Echoglossian
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }

        // Add any other properties or methods here.
        [JsonIgnore] private DalamudPluginInterface _pluginInterface;

        //public List<XivChatType> Channels { get; set; } = new List<XivChatType>();
        public int Lang { get; set; } = 16;
        public Plugin.UiColorPick[] TextColour { get; set; } =
        {
            new Plugin.UiColorPick { Choice = 0, Option =0 }
        };
        public bool NotSelf { get; set; }
        public bool Whitelist { get; set; }
        public List<int> ChosenLanguages { get; set; } = new List<int>();
        public bool OneChan { get; set; }
        public int OneInt { get; set; }
        //public List<string> Blacklist { get; set; } = new List<string>();
        public int TranMode { get; set; }


        public bool AffectCutscenes { get; set; }

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this._pluginInterface = pluginInterface;
        }

        public void Save()
        {
            
            this._pluginInterface.SavePluginConfig(this);
        }
    }
}
