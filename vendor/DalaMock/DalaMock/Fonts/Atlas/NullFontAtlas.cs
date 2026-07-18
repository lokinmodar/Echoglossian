namespace DalaMock.Core.Fonts.Atlas;

/// <summary>
/// Used for unit testing/headless.
/// </summary>
internal sealed class NullFontAtlas : IFontAtlas
{
    public event FontAtlasBuildStepDelegate? BuildStepChange
    {
        add { }
        remove { }
    }

    public event SysAction? RebuildRecommend
    {
        add { }
        remove { }
    }

    public string Name => "NullFontAtlas";

    public FontAtlasAutoRebuildMode AutoRebuildMode => FontAtlasAutoRebuildMode.Disable;

    public ImFontAtlasPtr ImAtlas => default;

    public Task BuildTask => Task.CompletedTask;

    public bool HasBuiltAtlas => false;

    public bool IsGlobalScaled => false;

    public IDisposable SuppressAutoRebuild() => NoopDisposable.Instance;

    public IFontHandle NewGameFontHandle(GameFontStyle style) => new NullFontHandle();

    public IFontHandle NewDelegateFontHandle(FontAtlasBuildStepDelegate buildStepDelegate) => new NullFontHandle();

    public void BuildFontsOnNextFrame()
    {
    }

    public void BuildFontsImmediately()
    {
    }

    public Task BuildFontsAsync() => Task.CompletedTask;

    public void Dispose()
    {
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
