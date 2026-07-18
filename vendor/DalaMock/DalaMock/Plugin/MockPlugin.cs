namespace DalaMock.Core.Plugin;

/// <summary>
/// A mock plugin, managed by <see cref="IPluginLoader"/>.
/// </summary>
/// <param name="pluginType">The type of the plugin being loaded.</param>
public class MockPlugin(Type pluginType) : IMockPlugin
{
    private IContainer? container;
    private IAsyncDalamudPlugin? dalamudPlugin;
    private PluginLoadSettings? pluginLoadSettings;

    public MockPlugin(Type pluginType, PluginLoadSettings pluginLoadSettings)
        : this(pluginType)
    {
        this.pluginLoadSettings = pluginLoadSettings;
    }

    /// <inheritdoc/>
    public bool IsLoaded => this.DalamudPlugin != null && this.PluginLoadSettings != null && this.Container != null;

    /// <inheritdoc/>
    public bool IsTransitioning => this.TransitionState != PluginTransitionState.None;

    /// <inheritdoc/>
    public PluginTransitionState TransitionState { get; private set; }

    /// <inheritdoc/>
    public bool IsFaulted { get; private set; }

    /// <inheritdoc/>
    public Exception? LastException { get; private set; }

    /// <inheritdoc/>
    public IAsyncDalamudPlugin? DalamudPlugin => this.dalamudPlugin;

    /// <inheritdoc/>
    public PluginLoadSettings? PluginLoadSettings => this.pluginLoadSettings;

    /// <inheritdoc/>
    public IContainer? Container => this.container;

    /// <inheritdoc/>
    public Type PluginType => pluginType;

    /// <inheritdoc/>
    public void Startup(IAsyncDalamudPlugin dalamudPlugin, PluginLoadSettings pluginLoadSettings, IContainer container)
    {
        this.dalamudPlugin = dalamudPlugin;
        this.pluginLoadSettings = pluginLoadSettings;
        this.container = container;
    }

    /// <inheritdoc/>
    public void Teardown()
    {
        this.dalamudPlugin = null;
    }

    /// <inheritdoc/>
    public void BeginTransition(PluginTransitionState state)
    {
        this.TransitionState = state;
        this.IsFaulted = false;
        this.LastException = null;
    }

    /// <inheritdoc/>
    public void EndTransition()
    {
        this.TransitionState = PluginTransitionState.None;
    }

    /// <inheritdoc/>
    public void SetFault(Exception exception)
    {
        this.TransitionState = PluginTransitionState.None;
        this.IsFaulted = true;
        this.LastException = exception;
    }

    /// <inheritdoc/>
    public void ClearFault()
    {
        this.IsFaulted = false;
        this.LastException = null;
    }
}
