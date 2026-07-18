namespace DalaMock.Core.Plugin;

/// <summary>
/// An interface describing a plugin loader.
/// </summary>
public interface IPluginLoader
{
    public Dictionary<Type, MockPlugin> LoadedPlugins { get; }

    MockPlugin AddPlugin(Type dalamudPluginType);

    Task<bool> StartPlugin(MockPlugin plugin, PluginLoadSettings pluginLoadSettings);

    Task<bool> StopPlugin(MockPlugin plugin);

    bool HasPluginsLoaded { get; }

    bool HasPluginsStarted { get; }
}
