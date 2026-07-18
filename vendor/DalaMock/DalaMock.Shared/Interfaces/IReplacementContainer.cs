using Autofac;

namespace DalaMock.Shared.Interfaces;

/// <summary>
/// A mock container that includes all the available services from DalaMock.
/// DalaMock.Host provides a DalamudReplacementContainer.
/// DalaMock.Core provides a MockReplacementContainer.
/// </summary>
public interface IReplacementContainer
{
    IImGuiComponents ImGuiComponents { get; }

    IWindowSystemFactory WindowSystemFactory { get; }

    IFont Font { get; }

    IFileDialogManager FileDialogManager { get; }

    IFontChooserFactory FontChooserFactory { get; }

    /// <summary>
    /// Registers the services with your autofac container.
    /// </summary>
    /// <param name="containerBuilder">The container builder to inject the services into.</param>
    void Register(ContainerBuilder containerBuilder);
}
