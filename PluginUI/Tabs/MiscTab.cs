using ImGuiNET;

namespace Echoglossian.Tabs;

/// <summary>
/// Renders miscellaneous settings for engine behaviors and plugin preferences.
/// </summary>
public static class MiscTab
{
    public static bool Draw(Config config)
    {
        bool changed = false;

        changed |= ImGui.InputText(Resources.DefaultPluginCultureLabel, ref config.DefaultPluginCulture, 10);
        changed |= ImGui.InputText(Resources.PluginVersionLabel, ref config.PluginVersion, 20);
        changed |= ImGui.SliderInt(Resources.PluginCultureIntLabel, ref config.PluginCultureInt, 0, 999);
        changed |= ImGui.SliderInt(Resources.YandexCharactersTranslatedLabel, ref config.YandexCharactersTranslated, 0, int.MaxValue);

        return changed;
    }
}