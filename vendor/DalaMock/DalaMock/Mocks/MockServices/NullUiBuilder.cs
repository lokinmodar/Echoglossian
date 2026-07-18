namespace DalaMock.Core.Mocks.MockServices;

public class NullUiBuilder : IUiBuilder, IMockService
{
    public NullUiBuilder()
    {
        this.DefaultFontHandle = new NullFontHandle();
        this.IconFontHandle = new NullFontHandle();
        this.MonoFontHandle = new NullFontHandle();
        this.IconFontFixedWidthHandle = new NullFontHandle();
        this.DefaultFontSpec = new MockFontSpec(this.FontDefaultSizePx);
        this.FontAtlas = new NullFontAtlas();
    }

    public UldWrapper LoadUld(string uldPath)
    {
        throw new NotImplementedException();
    }

    public Task WaitForUi() => Task.CompletedTask;

    public Task<T> RunWhenUiPrepared<T>(Func<T> func, bool runInFrameworkThread = false) =>
        Task.FromResult(func());

    public Task<T> RunWhenUiPrepared<T>(Func<Task<T>> func, bool runInFrameworkThread = false) =>
        func();

    public IFontAtlas CreateFontAtlas(
        FontAtlasAutoRebuildMode autoRebuildMode,
        bool isGlobalScaled = true,
        string? debugName = null) => new NullFontAtlas();

    public IFontHandle DefaultFontHandle { get; set; }

    public IFontHandle IconFontHandle { get; set; }

    public IFontHandle MonoFontHandle { get; set; }

    public IFontHandle IconFontFixedWidthHandle { get; set; }

    public IFontSpec DefaultFontSpec { get; set; }

    public float FontDefaultSizePt { get; set; } = 12f;

    public float FontDefaultSizePx { get; set; } = (12f * 4f) / 3f;

    public ImFontPtr FontDefault { get; set; }

    public ImFontPtr FontIcon { get; set; }

    public ImFontPtr FontMono { get; set; }

    public ImFontPtr FontIconFixedWidth { get; }

    public Device Device { get; set; }

    public IntPtr DeviceHandle { get; set; }

    public IntPtr WindowHandlePtr { get; set; }

    public bool DisableAutomaticUiHide { get; set; }

    public bool DisableUserUiHide { get; set; }

    public bool DisableCutsceneUiHide { get; set; }

    public bool DisableGposeUiHide { get; set; }

    public bool OverrideGameCursor { get; set; }

    public ulong FrameCount { get; set; }

    public bool CutsceneActive { get; set; }

    public bool ShouldModifyUi { get; set; }

    public bool UiPrepared { get; set; }

    public IFontAtlas FontAtlas { get; set; }

    public bool ShouldUseReducedMotion { get; set; }

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

    public string ServiceName { get; set; } = "Ui Builder";
}
