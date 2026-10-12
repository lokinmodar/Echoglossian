namespace DalaMock.Core.Mocks.DalamudServices.DalamudPluginInterface;

/// <inheritdoc/>
public class MockActivePluginsChangedEventArgs : IActivePluginsChangedEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MockActivePluginsChangedEventArgs"/> class
    /// with the specified parameters.
    /// </summary>
    /// <param name="kind">The kind of change that triggered the event.</param>
    /// <param name="affectedInternalNames">The internal names of the plugins affected by the change.</param>
    /// <param name="affectedPlugins">The available information about the affected plugins.</param>
    public MockActivePluginsChangedEventArgs(PluginListInvalidationKind kind, IEnumerable<string> affectedInternalNames, IEnumerable<IActivePluginsChangedEventArgs.IAffectedPlugin>? affectedPlugins = null)
    {
        this.Kind = kind;
        this.AffectedInternalNames = affectedInternalNames;
        this.AffectedPlugins = affectedPlugins ?? [];
    }

    /// <inheritdoc/>
    public PluginListInvalidationKind Kind { get; set; }

    /// <inheritdoc/>
    public IEnumerable<string> AffectedInternalNames { get; set; }

    /// <inheritdoc/>
    public IEnumerable<IActivePluginsChangedEventArgs.IAffectedPlugin> AffectedPlugins { get; set; }
}
