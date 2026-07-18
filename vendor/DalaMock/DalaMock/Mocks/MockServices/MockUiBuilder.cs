namespace DalaMock.Core.Mocks.MockServices;

public class MockUiBuilder : IUiBuilder, IMockService
{
    private readonly IFont font;
    private readonly ImGuiScene scene;

    public float FontDefaultSizePt { get; } = 12f;

    public float FontDefaultSizePx { get; } = (12f * 4f) / 3f;

    public ImFontPtr FontDefault => this.font.DefaultFont;

    public ImFontPtr FontIcon => this.font.IconFont;

    public ImFontPtr FontMono => this.font.MonoFont;

    public ImFontPtr FontIconFixedWidth => this.font.IconFixedWidth;

    public GraphicsDevice GraphicsDevice { get; }

    /// <summary>Gets the current global font/UI scale driven by <see cref="ImGuiScene"/>.</summary>
    public float GlobalFontScale => this.scene.GlobalFontScale;

    /// <summary>
    /// Requests a new global font/UI scale. Rebuilds the global atlas (and global-scaled plugin
    /// atlases) at the new pixel size between frames so fonts stay crisp. DalaMock-specific helper.
    /// </summary>
    public void RequestGlobalFontScale(float scale) => this.scene.RequestGlobalFontScale(scale);

    public MockUiBuilder(ImGuiScene scene, IFont font)
    {
        this.font = font;
        this.scene = scene;
        this.GraphicsDevice = scene.GraphicsDevice;
        if (this.GraphicsDevice.GetD3D11Info(out BackendInfoD3D11 dx11Info))
        {
            this.DeviceHandle = dx11Info.Device;
        }

        this.WindowHandlePtr = scene.Window.SdlWindowHandle;

        this.DefaultFontHandle = new MockGlobalFontHandle(() => this.font.DefaultFont);
        this.IconFontHandle = new MockGlobalFontHandle(() => this.font.IconFont);
        this.MonoFontHandle = new MockGlobalFontHandle(() => this.font.MonoFont);
        this.IconFontFixedWidthHandle = new MockGlobalFontHandle(() => this.font.IconFixedWidth);

        this.DefaultFontSpec = new MockFontSpec(this.FontDefaultSizePx);

        this.FontAtlas = new MockFontAtlas(
            scene,
            "MockUiBuilder",
            FontAtlasAutoRebuildMode.OnNewFrame,
            isGlobalScaled: true);
    }

    public DalamudUldWrapper LoadUld(string uldPath)
    {
        throw new NotImplementedException();
    }

    public Task WaitForUi() => Task.CompletedTask;

    public Task<T> RunWhenUiPrepared<T>(Func<T> func, bool runInFrameworkThread = false) =>
        Task.FromResult(func());

    public Task<T> RunWhenUiPrepared<T>(Func<Task<T>> func, bool runInFrameworkThread = false) =>
        func();

    public DalamudIFontAtlas CreateFontAtlas(
        FontAtlasAutoRebuildMode autoRebuildMode,
        bool isGlobalScaled = true,
        string? debugName = null)
    {
        return new MockFontAtlas(
            this.scene,
            debugName ?? "MockAtlas",
            autoRebuildMode,
            isGlobalScaled);
    }

    public DalamudIFontHandle DefaultFontHandle { get; }

    public DalamudIFontHandle IconFontHandle { get; }

    public DalamudIFontHandle MonoFontHandle { get; }

    public DalamudIFontHandle IconFontFixedWidthHandle { get; }

    public IFontSpec DefaultFontSpec { get; set; }

    public Device Device { get; }

    public IntPtr DeviceHandle { get; set; }

    public IntPtr WindowHandlePtr { get; }

    public bool DisableAutomaticUiHide { get; set; }

    public bool DisableUserUiHide { get; set; }

    public bool DisableCutsceneUiHide { get; set; }

    public bool DisableGposeUiHide { get; set; }

    public bool OverrideGameCursor { get; set; }

    public ulong FrameCount { get; }

    public bool CutsceneActive { get; }

    public bool ShouldModifyUi { get; }

    public bool UiPrepared { get; }

    public DalamudIFontAtlas FontAtlas { get; }

    public bool ShouldUseReducedMotion { get; }

    public bool PluginUISoundEffectsEnabled { get; set; }

    public event SysAction? Draw;

    public event SysAction? ResizeBuffers;

    public event SysAction? OpenConfigUi;

    public event SysAction? OpenMainUi;

    public event SysAction? ShowUi;

    public event SysAction? HideUi;

    public event SysAction? DefaultGlobalScaleChanged;

    public event SysAction? DefaultFontChanged;

    public event SysAction? DefaultStyleChanged;

    public void FireOpenMainUiEvent()
    {
        this.OpenMainUi?.Invoke();
    }

    public void FireOpenConfigUiEvent()
    {
        this.OpenConfigUi?.Invoke();
    }

    public void FireDraw()
    {
        this.Draw?.Invoke();
    }

    public string ServiceName { get; set; } = "Ui Builder";
}
