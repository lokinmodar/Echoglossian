namespace DalaMock.Core.Mocks;

/// <inheritdoc />
public class MockReplacementContainer : IReplacementContainer
{
    public MockReplacementContainer(
        IUiBuilder uiBuilder,
        MockWindowSystem.Factory factory,
        MockDalamudConfiguration dalamudConfiguration,
        MockSystemFontProvider systemFontProvider)
    {
        this.ImGuiComponents = new MockImGuiComponents(uiBuilder);
        this.WindowSystemFactory = new MockWindowSystemFactory(factory);
        this.Font = new MockFont();
        this.FileDialogManager = new MockFileDialogManager();
        this.FontChooserFactory = new MockFontChooserFactory(uiBuilder, dalamudConfiguration, systemFontProvider);
    }

    public IImGuiComponents ImGuiComponents { get; }

    public IWindowSystemFactory WindowSystemFactory { get; }

    public IFont Font { get; }

    public IFileDialogManager FileDialogManager { get; }

    public IFontChooserFactory FontChooserFactory { get; }

    /// <inheritdoc/>
    public void Register(ContainerBuilder containerBuilder)
    {
        containerBuilder.RegisterInstance(this.ImGuiComponents).AsImplementedInterfaces().AsSelf().SingleInstance();
        containerBuilder.RegisterInstance(this.WindowSystemFactory).AsImplementedInterfaces().AsSelf().SingleInstance();
        containerBuilder.RegisterInstance(this.Font).AsImplementedInterfaces().AsSelf().SingleInstance();
        containerBuilder.RegisterInstance(this.FileDialogManager).AsImplementedInterfaces().AsSelf().SingleInstance();
        containerBuilder.RegisterInstance(this.FontChooserFactory).AsImplementedInterfaces().AsSelf().SingleInstance();
        containerBuilder.RegisterType<MockWindowSystem>().As<IWindowSystem>().InstancePerDependency();
    }
}
