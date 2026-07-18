namespace DalaMock.Core.Windows;

/// <summary>
/// A mock window system that simulates Dalamud's window system.
/// </summary>
public class MockWindowSystem : IWindowSystem
{
    private readonly List<MockWindowHost> windows = new();

    private readonly MockWindowHost.Factory windowHostFactory;
    private readonly ILogger<MockWindowSystem> logger;
    private readonly MockDalamudConfiguration dalamudConfiguration;

    private string lastFocusedWindowName = string.Empty;

    public delegate MockWindowSystem Factory(string imNamespace);

    /// <summary>
    /// Initializes a new instance of the <see cref="MockWindowSystem"/> class.
    /// A mock window system that implements roughly the same functionality as the real Dalamud window system.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="dalamudConfiguration"></param>
    /// <param name="imNamespace">The namespace of the window system.</param>
    /// <param name="windowHostFactory"></param>
    public MockWindowSystem(MockWindowHost.Factory windowHostFactory, ILogger<MockWindowSystem> logger, MockDalamudConfiguration dalamudConfiguration, string? imNamespace)
    {
        this.Namespace = imNamespace;
        this.windowHostFactory = windowHostFactory;
        this.logger = logger;
        this.dalamudConfiguration = dalamudConfiguration;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IWindow> Windows => this.windows.Select(c => c.Window).ToList();

    /// <inheritdoc/>
    public bool HasAnyFocus { get; set; }

    /// <inheritdoc/>
    public string? Namespace { get; set; }

    /// <inheritdoc/>
    public void AddWindow(IWindow window)
    {
        if (this.windows.Any(x => x.Window.WindowName == window.WindowName))
        {
            throw new ArgumentException("A window with this name/ID already exists.");
        }

        this.windows.Add(this.windowHostFactory.Invoke(window));
    }

    /// <inheritdoc/>
    public void RemoveWindow(IWindow window)
    {
        if (this.windows.All(c => c.Window != window))
        {
            throw new ArgumentException("This window is not registered on this WindowSystem.");
        }

        this.windows.RemoveAll(c => c.Window == window);
    }

    /// <inheritdoc/>
    public void RemoveAllWindows()
    {
        this.windows.Clear();
    }

    /// <inheritdoc/>
    public void Draw()
    {
        var hasNamespace = !string.IsNullOrEmpty(this.Namespace);

        if (hasNamespace)
        {
            ImGui.PushID(this.Namespace);
        }

        // These must be nullable, people are using stock WindowSystems and Windows without Dalamud for tests

        var flags = MockWindowHost.WindowDrawFlags.None;

        // if (this.dalamudConfiguration.EnablePluginUISoundEffects ?? false)
        // {
        //     flags |= MockWindowHost.WindowDrawFlags.UseSoundEffects;
        // }
        //
        // if (this.dalamudConfiguration.EnablePluginUiAdditionalOptions ?? false)
        // {
        //     flags |= MockWindowHost.WindowDrawFlags.UseAdditionalOptions;
        // }
        //
        // if (this.dalamudConfiguration.IsFocusManagementEnabled ?? false)
        // {
        //     flags |= MockWindowHost.WindowDrawFlags.UseFocusManagement;
        // }
        //
        // if (this.dalamudConfiguration.ReduceMotions ?? false)
        // {
        //     flags |= MockWindowHost.WindowDrawFlags.IsReducedMotion;
        // }

        // Shallow clone the list of windows so that we can edit it without modifying it while the loop is iterating
        foreach (var window in this.windows.ToArray())
        {
#if DEBUG
            // Log.Verbose($"[WS{(hasNamespace ? "/" + this.Namespace : string.Empty)}] Drawing {window.WindowName}");
#endif
            var parameters = new MockWindowHost.WindowDrawParameters()
            {
                Flags = flags,
            };

            window.DrawInternal(parameters);
        }

        var focusedWindow = this.windows.FirstOrDefault(window => window.Window.IsFocused);
        this.HasAnyFocus = focusedWindow != default;

        if (this.HasAnyFocus)
        {
            if (this.lastFocusedWindowName != focusedWindow!.Window.WindowName)
            {
                this.logger.LogDebug($"WindowSystem \"{this.Namespace}\" Window \"{focusedWindow.Window.WindowName}\" has focus now");
                this.lastFocusedWindowName = focusedWindow.Window.WindowName;
            }
        }
        else
        {
            if (this.lastFocusedWindowName != string.Empty)
            {
                this.logger.LogDebug("WindowSystem \"{Namespace}\" Window \"{LastFocusedWindowName}\" lost focus", this.Namespace, this.lastFocusedWindowName);
                this.lastFocusedWindowName = string.Empty;
            }
        }

        if (hasNamespace)
        {
            ImGui.PopID();
        }
    }
}
