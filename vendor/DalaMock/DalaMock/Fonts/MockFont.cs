namespace DalaMock.Core.Fonts;

public class MockFont : IFont, IMockService
{
    // These resolve into the global ImGui atlas (ImGui.GetIO().Fonts.Fonts[1..4]) on every access
    // rather than caching, so the pre-built UiBuilder handles keep pointing at valid fonts after a
    // global atlas rebuild (e.g. a global-scale change) re-creates the underlying ImFontPtr values.
    public unsafe ImFontPtr DefaultFont => ImGui.GetIO().Fonts.Fonts[1];

    /// <inheritdoc/>
    public unsafe ImFontPtr IconFont => ImGui.GetIO().Fonts.Fonts[3];

    /// <inheritdoc/>
    public unsafe ImFontPtr MonoFont => ImGui.GetIO().Fonts.Fonts[2];

    /// <inheritdoc/>
    public unsafe ImFontPtr IconFixedWidth => ImGui.GetIO().Fonts.Fonts[4];

    public string ServiceName => "Font";
}
