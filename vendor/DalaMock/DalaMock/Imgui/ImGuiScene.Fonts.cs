namespace DalaMock.Core.Imgui;

/// <summary>
/// Bridges the scene to its <see cref="GlobalFontManager"/>: exposes the font-facing surface that the
/// rest of DalaMock calls, and orchestrates the between-frames global-atlas rebuild (the manager builds
/// the fonts; the scene clears the atlas, re-uploads the GPU texture, and flags plugin atlases).
/// </summary>
public partial class ImGuiScene
{
    /// <summary>
    /// Gets the current global font/UI scale.
    /// </summary>
    public float GlobalFontScale => this.fontManager.GlobalScale;

    /// <summary>Bytes of the bundled gf.ttf game-symbol font, used as a fallback by plugin atlases.</summary>
    internal byte[] GameFontData => this.fontManager.GameFontData;

    /// <summary>Glyph range for SeIconChar covered by <see cref="GameFontData"/>.</summary>
    internal unsafe ushort* GameGlyphRanges => this.fontManager.GameGlyphRanges;

    /// <summary>
    /// Requests a new global font/UI scale. The actual atlas rebuild happens between frames (in
    /// <see cref="Update"/>), since rebuilding the live ImGui atlas mid-frame is unsafe.
    /// </summary>
    /// <param name="scale">The desired scale (1.0 == 100%).</param>
    public void RequestGlobalFontScale(float scale) => this.fontManager.RequestGlobalScale(scale);

    /// <summary>
    /// Requests a change to the global default font (the fonts[1] slot returned by
    /// <c>UiBuilder.DefaultFont</c> and used as <c>io.FontDefault</c>). The atlas rebuild happens between
    /// frames in <see cref="Update"/>. Pass <c>null</c> to restore the built-in default (AXIS game font).
    /// </summary>
    /// <param name="spec">The chosen font spec, or <c>null</c> for the built-in default.</param>
    public void RequestDefaultFont(SingleFontSpec? spec) => this.fontManager.RequestDefaultFont(spec);

    /// <summary>
    /// Loads a TTF/OTF from this assembly's embedded resources and adds it to the global ImGui atlas.
    /// </summary>
    public unsafe ImFontPtr LoadFontFromEmbeddedResource(
        string resourceName,
        float fontSize,
        ushort* glyphRanges = null,
        bool mergeMode = false) =>
        this.fontManager.LoadFontFromEmbeddedResource(resourceName, fontSize, glyphRanges, mergeMode);

    /// <summary>
    /// Applies a pending global-scale or default-font change if one is queued. Must run on the render
    /// thread, between frames (before <see cref="ImGui.NewFrame"/>): rebuilds the global atlas via the
    /// font manager, re-uploads its textures, updates <c>io.FontGlobalScale</c>, and flags global-scaled
    /// plugin atlases for rebuild.
    /// </summary>
    private unsafe void ApplyPendingGlobalScale()
    {
        if (!this.fontManager.TryConsumePending(out var scale))
        {
            return;
        }

        var atlas = ImGui.GetIO().Fonts;
        atlas.Clear();

        try
        {
            this.fontManager.BuildInto();
        }
        catch (Exception ex)
        {
            Log.Error(
                ex,
                "[ImGuiScene] Font rebuild failed during scale change — falling back to the ImGui built-in default font to prevent a native crash. ");

            atlas.AddFontDefault();
            atlas.Build();
        }

        this.RecreateFontDeviceTexture(this.GraphicsDevice);

        // FontGlobalScale carries the scale for layout (ImGuiHelpers.GlobalScale reads it); the manager
        // already set each font's Scale to 1/scale so glyphs still render 1:1.
        ImGui.GetIO().FontGlobalScale = scale;

        // Tell global-scaled plugin atlases to rebuild at the new scale on this same frame.
        MockFontAtlas[] atlasesSnapshot;
        lock (this.registeredFontAtlases)
        {
            atlasesSnapshot = this.registeredFontAtlases.ToArray();
        }

        foreach (var pluginAtlas in atlasesSnapshot)
        {
            if (pluginAtlas.IsGlobalScaled)
            {
                pluginAtlas.RequestGlobalScaleRebuild();
            }
        }
    }
}
