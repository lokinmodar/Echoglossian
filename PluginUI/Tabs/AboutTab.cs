using ImGuiNET;

namespace Echoglossian.Tabs;

/// <summary>
/// Displays plugin version and credits information.
/// </summary>
public static class AboutTab
{
    public static bool Draw(Config config)
    {
        ImGui.Text("Echoglossian Plugin");
        ImGui.Text($"Version: {config.PluginVersion}");
        ImGui.Text("Developed by lokinmodar & contributors");
        ImGui.Text("Licensed under CC BY-NC-ND 4.0");
        ImGui.Text("https://github.com/lokinmodar/echoglossian");

        return false;
    }
}