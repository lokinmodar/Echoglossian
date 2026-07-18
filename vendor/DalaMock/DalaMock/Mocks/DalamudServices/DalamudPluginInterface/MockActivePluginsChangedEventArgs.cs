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
    public MockActivePluginsChangedEventArgs(PluginListInvalidationKind kind, IEnumerable<string> affectedInternalNames)
    {
        this.Kind = kind;
        this.AffectedInternalNames = affectedInternalNames;
    }

    /// <inheritdoc/>
    public PluginListInvalidationKind Kind { get; set; }

    /// <inheritdoc/>
    public IEnumerable<string> AffectedInternalNames { get; set; }
}
