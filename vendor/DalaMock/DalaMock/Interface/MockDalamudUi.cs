namespace DalaMock.Core.Interface;

/// <summary>
/// Provides a mock dalamud UI allowing for plugins to be loaded/unloaded at will.
/// </summary>
public class MockDalamudUi : IDisposable
{
    private readonly ImGuiScene imGuiScene;
    private readonly PluginLoader pluginLoader;
    private readonly MockMainMenuBar mockMainMenuBar;
    private readonly MockWindowSystem windowSystem;
    private readonly MockStyleManager mockStyleManager;
    private readonly MockDalamudConfiguration dalamudConfiguration;
    private readonly MockSystemFontProvider systemFontProvider;
    private readonly MockUiBuilder mockUiBuilder;
    private Dictionary<MockPlugin, MockFramework> frameworks;
    private Dictionary<MockPlugin, MockUiBuilder> uiBUilders;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockDalamudUi"/> class.
    /// Keeps track of each loaded plugin, and draws it's windows as required. Also draws any mock windows that have been registered by DalaMock.
    /// </summary>
    /// <param name="mockContainer">The autofac container for the primary mock application.</param>
    /// <param name="pluginLoader">The plugin loader.</param>
    public MockDalamudUi(MockContainer mockContainer, PluginLoader pluginLoader, MockMainMenuBar mockMainMenuBar)
    {
        this.pluginLoader = pluginLoader;
        this.mockMainMenuBar = mockMainMenuBar;
        this.frameworks = new Dictionary<MockPlugin, MockFramework>();
        this.uiBUilders = new Dictionary<MockPlugin, MockUiBuilder>();
        var windows = mockContainer.GetWindows();
        this.windowSystem = mockContainer.GetWindowSystem();
        this.imGuiScene = mockContainer.GetContainer().Resolve<ImGuiScene>();
        this.mockStyleManager = mockContainer.GetContainer().Resolve<MockStyleManager>();
        this.dalamudConfiguration = mockContainer.GetContainer().Resolve<MockDalamudConfiguration>();
        this.systemFontProvider = mockContainer.GetContainer().Resolve<MockSystemFontProvider>();
        this.mockUiBuilder = mockContainer.GetContainer().Resolve<MockUiBuilder>();

        foreach (var window in windows)
        {
            this.windowSystem.AddWindow(window);
        }

        this.pluginLoader.PluginStarted += this.PluginLoaderOnPluginStarted;
        this.pluginLoader.PluginStopped += this.PluginLoaderOnPluginStopped;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.pluginLoader.PluginStarted -= this.PluginLoaderOnPluginStarted;
        this.pluginLoader.PluginStopped -= this.PluginLoaderOnPluginStopped;
    }

    /// <summary>
    /// Starts the ui's render and update loop.
    /// </summary>
    public void Run()
    {
        this.mockStyleManager.ApplyChosenStyle();

        if (this.dalamudConfiguration.DefaultFont is { } savedFont)
        {
            var spec = savedFont.ToSpec(this.systemFontProvider);
            if (spec is not null)
            {
                this.mockUiBuilder.DefaultFontSpec = spec;
                this.imGuiScene.RequestDefaultFont(spec);
            }
        }

        this.imGuiScene.OnBuildUi += () =>
        {
            this.mockMainMenuBar.Draw();

            // Move the updates elsewhere
            foreach (var framework in this.frameworks)
            {
                framework.Value.FireUpdate();
            }

            foreach (var uiBuilder in this.uiBUilders)
            {
                uiBuilder.Value.FireDraw();
            }

            foreach (var window in this.windowSystem.Windows)
            {
                window.AllowClickthrough = false;
                window.AllowPinning = false;
                window.ForceMainWindow = false;
            }

            this.windowSystem.Draw();
        };
        this.imGuiScene.Run();
    }

    private void PluginLoaderOnPluginStopped(MockPlugin mockPlugin)
    {
        this.uiBUilders.Remove(mockPlugin);
        this.frameworks.Remove(mockPlugin);
    }

    private void PluginLoaderOnPluginStarted(MockPlugin mockPlugin)
    {
        if (mockPlugin.Container != null)
        {
            this.uiBUilders.TryAdd(mockPlugin, mockPlugin.Container.Resolve<MockUiBuilder>());
            if (mockPlugin.Container.TryResolve<MockFramework>(out var framework))
            {
                this.frameworks.TryAdd(mockPlugin, framework);
            }
        }
    }
}
