using Texture = Veldrid.Texture;

namespace DalaMock.Core.Imgui;

/// <summary>
/// Simple class to wrap everything necessary to use ImGui inside a window.
/// </summary>
public partial class ImGuiScene : IDisposable
{
    private const string StateFilePath = "dalamock_ui.json";
    private const int DebounceDelayMs = 500;
    private readonly AssertHandler assertHandler;
    private readonly Dictionary<Texture, TextureView> autoViewsByTexture = new();
    private readonly List<IDisposable> ownedResources = new();
    private readonly Dictionary<TextureView, ResourceSetInfo> setsByView = new();
    private readonly Dictionary<IntPtr, ResourceSetInfo> viewsById = new();
    private readonly Vector3 backgroundColour = new(0.45f, 0.55f, 0.6f);
    private readonly bool pauseRendering = false;
    private readonly Vector2 scaleFactor = Vector2.One;
    private readonly GlobalFontManager fontManager;

    private MockWindowState currentWindowState;
    private CancellationTokenSource debounceCts;

    private bool disposedValue;
    private int lastAssignedId = 100;

    private readonly List<MockFontAtlas> registeredFontAtlases = new();
    private readonly ConcurrentQueue<DeferredAtlasDispose> deferredAtlasDisposes = new();

    /// <summary>
    /// Marshals work onto the ImGui render thread (e.g. atlas texture uploads from background-build threads).
    /// </summary>
    internal MockFontAtlasUploadQueue UploadQueue { get; } = new();

    /// <summary>
    /// User methods invoked every ImGui frame to construct custom UIs.
    /// </summary>
    public event BuildUiDelegate OnBuildUi;

    public delegate void BuildUiDelegate();

    /// <summary>
    /// Initializes a new instance of the <see cref="ImGuiScene"/> class.
    /// Creates a new window and a new renderer of the specified type, and initializes ImGUI.
    /// </summary>
    /// <param name="createInfo">Creation details for the window.</param>
    /// <param name="assertHandler"></param>
    /// <param name="gameData">Optional Lumina GameData for loading the AXIS game font.</param>
    public unsafe ImGuiScene(WindowCreateInfo createInfo, AssertHandler assertHandler, GameData? gameData = null, float initialScale = 1f)
    {
        this.assertHandler = assertHandler;
        this.fontManager = new GlobalFontManager(gameData, initialScale);
        GraphicsDevice graphicsDevice;
        Sdl2Window window;
        VeldridStartup.CreateWindowAndGraphicsDevice(
            createInfo,
            new GraphicsDeviceOptions(false, null, false, ResourceBindingModel.Improved, true, true),
            out window,
            out graphicsDevice);
        this.Window = new RawSdl2Window(window);
        this.GraphicsDevice = graphicsDevice;
        this.Window.Resized += () =>
        {
            this.GraphicsDevice.MainSwapchain.Resize((uint)this.Window.Width, (uint)this.Window.Height);
        };

        this.Window.Closed += this.WindowOnClosed;

        this.CommandList = this.GraphicsDevice.ResourceFactory.CreateCommandList();

        var context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);
        assertHandler.Setup();

        this.fontManager.BuildInto();

        ImGui.GetIO().BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

        ImGui.GetIO().FontGlobalScale = this.fontManager.GlobalScale;
        var field = typeof(ImGuiHelpers).GetProperty("MainViewport", BindingFlags.Static | BindingFlags.Public);
        field?.SetValue(null, ImGui.GetMainViewport());

        this.CreateDeviceResources(
            this.GraphicsDevice,
            this.GraphicsDevice.MainSwapchain.Framebuffer.OutputDescription);
        this.SetKeyMappings();
        this.SetClipboardFunctions();
        this.SetPerFrameImGuiData(0);
        ImGui.NewFrame();
        ImGui.EndFrame();

        this.currentWindowState = this.CaptureWindowState();
    }

    /// <summary>
    /// Gets the main application container window where we do all our rendering and input processing.
    /// </summary>
    public ISdl2Window Window { get; init; }

    /// <summary>
    /// Gets the veldrid graphics device.
    /// </summary>
    public GraphicsDevice GraphicsDevice { get; init; }

    /// <summary>
    /// Gets the veldrid commandlist.
    /// </summary>
    public CommandList CommandList { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the user application has requested the system to terminate.
    /// </summary>
    public bool ShouldQuit { get; set; }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Helper method to create a fullscreen window.
    /// </summary>
    /// <param name="assertHandler">The assert handler.</param>
    /// <param name="gameData">Optional Lumina GameData for loading the AXIS game font.</param>
    /// <returns>Returns a imguiscene.</returns>
    public static unsafe ImGuiScene CreateWindow(AssertHandler assertHandler, GameData? gameData = null, float initialScale = 1f)
    {
        var savedState = LoadWindowState();

        var createInfo = new WindowCreateInfo
        {
            WindowTitle = "DalaMock",
            WindowWidth = savedState?.Width ?? 1024,
            WindowHeight = savedState?.Height ?? 768,
            X = savedState?.X ?? 0,
            Y = savedState?.Y ?? 0,
            WindowInitialState = WindowState.Maximized,
        };
        Sdl2Native.SDL_Init(SDLInitFlags.Video);
        Sdl2Native.SDL_Init(SDLInitFlags.GameController);
        if (savedState != null)
        {
            createInfo.WindowInitialState = savedState.IsMaximized
                                                ? Veldrid.WindowState.Maximized
                                                : Veldrid.WindowState.Normal;
            int numDisplays = Sdl2Native.SDL_GetNumVideoDisplays();
            if (savedState.MonitorIndex >= numDisplays)
            {
                savedState.MonitorIndex = 0;
            }

            Rectangle displayBounds;
            Sdl2Native.SDL_GetDisplayBounds(savedState.MonitorIndex, &displayBounds);

            int clampedX = Math.Max(displayBounds.X, Math.Min(savedState.X, (displayBounds.X + displayBounds.Width) - 100));
            int clampedY = Math.Max(displayBounds.Y, Math.Min(savedState.Y, (displayBounds.Y + displayBounds.Height) - 100));

            createInfo.X = clampedX;
            createInfo.Y = clampedY;
        }

        var scene = new ImGuiScene(createInfo, assertHandler, gameData, initialScale);
        scene.Window.Opacity = 1;

        return scene;
    }

    /// <summary>
    /// Simple method to run the scene in a loop until the window is closed or the application
    /// requests an exit (via <see cref="ShouldQuit"/>).
    /// </summary>
    public void Run()
    {
        // For now we consider the window closing to be a quit request
        // while ShouldQuit is used for external/application close requests
        while (!this.ShouldQuit)
        {
            this.Update();
        }
    }

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong SDL_GetPerformanceFrequency();

    /// <summary>
    /// Performs a single-frame update of ImGui and renders it to the window.
    /// This method does not check any quit conditions.
    /// </summary>
    public void Update()
    {
        var snapshot = this.Window.PumpEvents();

        this.TrackWindowState();

        if (!this.pauseRendering)
        {
            var deltaSeconds = 1000f / SDL_GetPerformanceFrequency();
            this.SetPerFrameImGuiData(deltaSeconds);
            this.UpdateImGuiInput(snapshot);

            this.UploadQueue.Drain();

            this.ApplyPendingGlobalScale();

            MockFontAtlas[] atlasesSnapshot;
            lock (this.registeredFontAtlases)
            {
                atlasesSnapshot = this.registeredFontAtlases.ToArray();
            }

            foreach (var atlas in atlasesSnapshot)
            {
                atlas.DrainPendingFrameRequests();
            }

            ImGui.NewFrame();
            this.OnBuildUi?.Invoke();
            this.CommandList!.Begin();
            this.CommandList.SetFramebuffer(this.GraphicsDevice!.MainSwapchain.Framebuffer);
            this.CommandList.ClearColorTarget(
                0,
                new RgbaFloat(this.backgroundColour.X, this.backgroundColour.Y, this.backgroundColour.Z, 1f));
            ImGui.Render();
            this.RenderImDrawData(ImGui.GetDrawData(), this.GraphicsDevice, this.CommandList);
            this.CommandList.End();
            this.GraphicsDevice.SubmitCommands(this.CommandList);
            this.GraphicsDevice.SwapBuffers(this.GraphicsDevice.MainSwapchain);

            this.RunDeferredAtlasDisposes();
        }
    }

    private unsafe MockWindowState CaptureWindowState()
    {
        var handle = this.Window.SdlWindowHandle;

        int x, y, width, height;
        Sdl2Native.SDL_GetWindowPosition(handle, &x, &y);
        Sdl2Native.SDL_GetWindowSize(handle, &width, &height);

        bool isMaximized = this.Window.WindowState == WindowState.Maximized;

        int monitorIndex = this.GetMonitorIndexForWindow(handle);

        return new MockWindowState
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            IsMaximized = isMaximized,
            MonitorIndex = monitorIndex,
        };
    }

    private void TrackWindowState()
    {
        var newState = this.CaptureWindowState();

        if (!this.WindowStatesEqual(newState, this.currentWindowState))
        {
            this.currentWindowState = newState;
            this.DebounceSaveWindowState();
        }
    }

    private bool WindowStatesEqual(MockWindowState a, MockWindowState b)
    {
        return a.X == b.X
            && a.Y == b.Y
            && a.Width == b.Width
            && a.Height == b.Height
            && a.IsMaximized == b.IsMaximized
            && a.MonitorIndex == b.MonitorIndex;
    }

    private void DebounceSaveWindowState()
    {
        this.debounceCts?.Cancel();
        this.debounceCts = new CancellationTokenSource();

        var token = this.debounceCts.Token;

        Task.Run(
            async () =>
        {
            try
            {
                await Task.Delay(DebounceDelayMs, token);

                if (!token.IsCancellationRequested)
                {
                    SaveWindowState(this.currentWindowState);
                }
            }
            catch (TaskCanceledException)
            {
            }
        },
            token);
    }

    private static void SaveWindowState(MockWindowState state)
    {
        try
        {
            var json = JsonConvert.SerializeObject(state, Formatting.Indented);
            File.WriteAllText(StateFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save window state: {ex.Message}");
        }
    }

    private static MockWindowState? LoadWindowState()
    {
        try
        {
            if (File.Exists(StateFilePath))
            {
                var json = File.ReadAllText(StateFilePath);
                return JsonConvert.DeserializeObject<MockWindowState>(json);
            }
        }
        catch (Exception ex)
        {
            // Log error or handle gracefully
            Console.WriteLine($"Failed to load window state: {ex.Message}");
        }

        return null;
    }

    private unsafe int GetMonitorIndexForWindow(IntPtr windowHandle)
    {
        int displayIndex = Sdl2Native.SDL_GetWindowDisplayIndex(windowHandle);
        return displayIndex >= 0 ? displayIndex : 0;
    }


    private void WindowOnClosed()
    {
        this.debounceCts?.Cancel();
        SaveWindowState(this.currentWindowState);
        this.ShouldQuit = true;
    }

    private void SetPerFrameImGuiData(float deltaSeconds)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(
            this.Window.Width / this.scaleFactor.X,
            this.Window.Height / this.scaleFactor.Y);
        io.DisplayFramebufferScale = this.scaleFactor;
        io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
    }

    /// <summary>
    /// Registers a <see cref="MockFontAtlas"/> for frame-pumping (drain pending rebuilds before each NewFrame).
    /// </summary>
    internal void RegisterFontAtlas(MockFontAtlas atlas)
    {
        lock (this.registeredFontAtlases)
        {
            this.registeredFontAtlases.Add(atlas);
        }
    }

    /// <summary>
    /// Unregisters a <see cref="MockFontAtlas"/> previously registered via <see cref="RegisterFontAtlas"/>.
    /// </summary>
    internal void UnregisterFontAtlas(MockFontAtlas atlas)
    {
        lock (this.registeredFontAtlases)
        {
            this.registeredFontAtlases.Remove(atlas);
        }
    }

    /// <summary>
    /// Queues an action to be run after the next frame has been submitted to the GPU. Used to
    /// defer native ImFontAtlas destruction past any in-flight draw commands that reference it.
    /// </summary>
    internal void EnqueueDeferredAtlasDispose(SysAction action)
    {
        this.deferredAtlasDisposes.Enqueue(new DeferredAtlasDispose(action, 1));
    }

    private void RunDeferredAtlasDisposes()
    {
        var requeue = new List<DeferredAtlasDispose>();
        while (this.deferredAtlasDisposes.TryDequeue(out var item))
        {
            if (item.FramesRemaining <= 0)
            {
                try
                {
                    item.Action();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[ImGuiScene] deferred atlas dispose action threw");
                }
            }
            else
            {
                requeue.Add(item with { FramesRemaining = item.FramesRemaining - 1 });
            }
        }

        foreach (var item in requeue)
        {
            this.deferredAtlasDisposes.Enqueue(item);
        }
    }

    private record struct DeferredAtlasDispose(SysAction Action, int FramesRemaining);

    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
            }

            MockFontAtlas[] atlasesSnapshot;
            lock (this.registeredFontAtlases)
            {
                atlasesSnapshot = this.registeredFontAtlases.ToArray();
                this.registeredFontAtlases.Clear();
            }

            foreach (var atlas in atlasesSnapshot)
            {
                try
                {
                    atlas.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[ImGuiScene] disposing font atlas failed");
                }
            }

            while (this.deferredAtlasDisposes.TryDequeue(out var item))
            {
                try
                {
                    item.Action();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[ImGuiScene] deferred dispose flush threw");
                }
            }

            this.assertHandler.Dispose();
            this.Window.Closed -= this.WindowOnClosed;

            this.Window.Close();

            ImGui.DestroyContext();

            this.vertexBuffer.Dispose();
            this.indexBuffer.Dispose();
            this.projMatrixBuffer.Dispose();
            this.fontTexture?.Dispose();
            this.fontTextureView.Dispose();
            this.vertexShader.Dispose();
            this.fragmentShader.Dispose();
            this.layout.Dispose();
            this.textureLayout.Dispose();
            this.pipeline.Dispose();
            this.mainResourceSet.Dispose();

            this.ownedResources.ForEach(res => res.Dispose());
            this.ownedResources.Clear();

            this.disposedValue = true;
        }
    }

    ~ImGuiScene()
    {
        this.Dispose(false);
    }
}
