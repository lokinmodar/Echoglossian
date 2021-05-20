using ImGuiNET;

namespace DalamudPluginProjectTemplate
{
    public class PluginUI
    {
        public bool IsVisible { get; set; }

        public void Draw()
        {
            if (!IsVisible)
                return;
        }
    }
}
