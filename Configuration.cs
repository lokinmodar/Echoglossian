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
        public int Lang { get; set; } = 16;

        public int ChosenLangCode { get; set; }
        public List<int> ChosenLanguages { get; set; } = new();

        public bool AffectCutscenes { get; set; }

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this._pluginInterface = pluginInterface;
        }

        public void Save()        {
            
            this._pluginInterface.SavePluginConfig(this);
        }
    }
}
