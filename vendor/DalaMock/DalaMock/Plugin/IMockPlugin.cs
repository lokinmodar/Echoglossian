namespace DalaMock.Core.Plugin;

public enum PluginTransitionState
{
    None,
    Loading,
    Unloading,
}

/// <summary>
/// An interface describing a mock plugin.
/// </summary>
public interface IMockPlugin
{
    /// <summary>
    /// Gets a value indicating whether the plugin is loaded.
    /// </summary>
    public bool IsLoaded { get; }

    /// <summary>
    /// Gets a value indicating whether a load or unload operation is in progress.
    /// </summary>
    public bool IsTransitioning { get; }

    /// <summary>
    /// Gets the current transition state.
    /// </summary>
    public PluginTransitionState TransitionState { get; }

    /// <summary>
    /// Gets a value indicating whether the last operation faulted.
    /// </summary>
    public bool IsFaulted { get; }

    /// <summary>
    /// Gets the exception from the last faulted operation, if any.
    /// </summary>
    public Exception? LastException { get; }

    /// <summary>
    /// Gets the loaded plugin.
    /// </summary>
    public IAsyncDalamudPlugin? DalamudPlugin { get; }

    /// <summary>
    /// Gets the settings that were used when the plugin was loaded.
    /// </summary>
    public PluginLoadSettings? PluginLoadSettings { get; }

    /// <summary>
    /// Gets the DI container for the plugin.
    /// </summary>
    public IContainer? Container { get; }

    /// <summary>
    /// Gets the type of the plugin being loaded.
    /// </summary>
    public Type PluginType { get; }

    /// <summary>
    /// Mark the plugin as started.
    /// </summary>
    /// <param name="dalamudPlugin">The loaded plugin.</param>
    /// <param name="pluginLoadSettings">The settings that were used when the plugin was loaded.</param>
    /// <param name="container">The DI container for the plugin.</param>
    public void Startup(IAsyncDalamudPlugin dalamudPlugin, PluginLoadSettings pluginLoadSettings, IContainer container);

    /// <summary>
    /// Mark the plugin as unloaded.
    /// </summary>
    public void Teardown();

    /// <summary>
    /// Begin a load or unload transition.
    /// </summary>
    public void BeginTransition(PluginTransitionState state);

    /// <summary>
    /// End a transition successfully.
    /// </summary>
    public void EndTransition();

    /// <summary>
    /// Record a fault that occurred during a transition.
    /// </summary>
    public void SetFault(Exception exception);

    /// <summary>
    /// Clear a previous fault.
    /// </summary>
    public void ClearFault();
}
