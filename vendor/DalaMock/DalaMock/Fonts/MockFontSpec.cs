namespace DalaMock.Core.Fonts;

/// <summary>
/// Minimal <see cref="IFontSpec"/> implementation used for <see cref="MockUiBuilder.DefaultFontSpec"/>.
/// </summary>
internal sealed class MockFontSpec : IFontSpec
{
    public MockFontSpec(float sizePx)
    {
        this.SizePx = sizePx;
    }

    public float SizePx { get; }

    public float SizePt => (this.SizePx * 3f) / 4f;

    public float LineHeightPx => this.SizePx;

    public int FontNo => 0;

    public IFontHandle CreateFontHandle(IFontAtlas atlas, FontAtlasBuildStepDelegate? callback = null)
    {
        return atlas.NewDelegateFontHandle(e =>
        {
            e.OnPreBuild(tk => tk.Font = tk.AddDalamudDefaultFont(this.SizePx));
            callback?.Invoke(e);
        });
    }

    public ImFontPtr AddToBuildToolkit(IFontAtlasBuildToolkitPreBuild tk, ImFontPtr mergeFont = default)
    {
        var cfg = new SafeFontConfig { SizePx = this.SizePx, MergeFont = mergeFont };
        return tk.AddDalamudDefaultFont(this.SizePx);
    }

    public string ToLocalizedString(string localeCode) => $"Default ({this.SizePx:0.##}px)";
}
