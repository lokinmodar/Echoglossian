namespace DalaMock.Core.Fonts;

/// <summary>
/// Owns the CPU-side construction of the global ImGui font atlas: the five default font slots
/// (ProggyClean, the default UI font, Inconsolata, FontAwesome, FontAwesome-fixed), the global UI/font
/// scale, and the chosen default-font override.
/// <para>
/// Font slot 1 is the "default" UI font (what <c>UiBuilder.DefaultFont</c> / <c>io.FontDefault</c>
/// resolve to). It is normally the AXIS game font (or a NotoSansCJKjp fallback); when a default-font
/// override is set it is baked from that spec instead — game families via the custom-rect
/// <see cref="GameFontBuildHelper"/> path, everything else from a TTF/OTF.
/// </para>
/// </summary>
internal sealed unsafe class GlobalFontManager
{
    private static readonly char SeIconCharMin = (char)Enum.GetValues<SeIconChar>().Min();
    private static readonly char SeIconCharMax = (char)Enum.GetValues<SeIconChar>().Max();

    private readonly GameData? gameData;
    private readonly byte[] gameFontData;
    private readonly ushort* gameGlyphRanges;

    private GameFontBakeJob? axisBakeJob;
    private float globalFontScale;
    private float pendingFontScale;
    private SingleFontSpec? defaultFontOverride;
    private SingleFontSpec? pendingDefaultFontOverride;
    private bool defaultFontChangePending;

    public GlobalFontManager(GameData? gameData, float initialScale)
    {
        MockFontAtlasResources.VerifyEmbeddedResources();

        this.gameData = gameData;
        this.globalFontScale = this.pendingFontScale = Math.Clamp(initialScale, 0.5f, 4.0f);
        this.gameFontData = this.LoadEmbeddedResource("gf.ttf");
        this.gameGlyphRanges = this.GetGameGlyphRanges();
    }

    /// <summary>Gets the current global font/UI scale (1.0 == 100%).</summary>
    public float GlobalScale => this.globalFontScale;

    /// <summary>Gets the active game-font bake job, whose pixels the renderer fills during texture upload.</summary>
    public GameFontBakeJob? AxisBakeJob => this.axisBakeJob;

    /// <summary>Bytes of the bundled gf.ttf game-symbol font, used as a fallback by plugin atlases.</summary>
    public byte[] GameFontData => this.gameFontData;

    /// <summary>Glyph range for SeIconChar covered by <see cref="GameFontData"/>.</summary>
    public ushort* GameGlyphRanges => this.gameGlyphRanges;

    /// <summary>Requests a new global font/UI scale, applied on the next <see cref="TryConsumePending"/>.</summary>
    /// <param name="scale">The desired scale (1.0 == 100%).</param>
    public void RequestGlobalScale(float scale) => this.pendingFontScale = Math.Clamp(scale, 0.5f, 4.0f);

    /// <summary>
    /// Requests a change to the global default font (the fonts[1] slot). Pass <c>null</c> to restore the
    /// built-in default (AXIS game font). Applied on the next <see cref="TryConsumePending"/>.
    /// </summary>
    /// <param name="spec">The chosen font spec, or <c>null</c> for the built-in default.</param>
    public void RequestDefaultFont(SingleFontSpec? spec)
    {
        this.pendingDefaultFontOverride = spec;
        this.defaultFontChangePending = true;
    }

    /// <summary>
    /// If a scale or default-font change is queued, commits it into the current state and reports that a
    /// rebuild is needed. The caller is then responsible for clearing/rebuilding the atlas (via
    /// <see cref="BuildInto"/>), re-uploading the texture, and setting <c>io.FontGlobalScale</c>.
    /// </summary>
    /// <param name="scale">The scale to (re)build at.</param>
    /// <returns><c>true</c> if a rebuild is needed.</returns>
    public bool TryConsumePending(out float scale)
    {
        var scaleChanged = Math.Abs(this.pendingFontScale - this.globalFontScale) >= 0.0001f;
        if (!scaleChanged && !this.defaultFontChangePending)
        {
            scale = this.globalFontScale;
            return false;
        }

        if (this.defaultFontChangePending)
        {
            this.defaultFontOverride = this.pendingDefaultFontOverride;
            this.defaultFontChangePending = false;
        }

        this.globalFontScale = this.pendingFontScale;
        scale = this.globalFontScale;
        return true;
    }

    /// <summary>
    /// Builds the five default font slots into <see cref="ImGui.GetIO"/>'s font atlas at the current
    /// scale and sets <c>io.FontDefault</c>. The caller must clear the atlas first when rebuilding.
    /// </summary>
    public void BuildInto()
    {
        var scale = this.globalFontScale;
        var baseFontSize = ((12 * 4.0f) / 3.0f) * scale;
        var gameGlyphSize = baseFontSize * 2.0f;
        var atlas = ImGui.GetIO().Fonts;
        this.axisBakeJob = null;

        atlas.AddFontDefault(null);
        this.AddFontFromMemory(this.gameFontData, gameGlyphSize, this.gameGlyphRanges, mergeMode: true);

        var overrideSpec = this.defaultFontOverride;
        GameFontFamily? gameFamily = null;
        var gameFontSize = baseFontSize;
        if (overrideSpec is null)
        {
            gameFamily = GameFontFamily.Axis;
        }
        else if (overrideSpec.FontId is GameFontAndFamilyId gameId)
        {
            gameFamily = gameId.GameFontFamily;
            gameFontSize = overrideSpec.SizePx * scale;
        }

        ImFontPtr defaultFont = default;
        if (gameFamily is { } family && this.gameData != null)
        {
            var templateData = this.LoadEmbeddedResource("NotoSansCJKjp-Medium.otf");
            this.axisBakeJob = GameFontBuildHelper.TryPrebuild(
                this.gameData,
                atlas,
                templateData,
                family,
                gameFontSize,
                out defaultFont);
        }

        if (defaultFont.IsNull)
        {
            defaultFont = this.AddDefaultFontSlot(atlas, gameFamily is null ? overrideSpec : null, baseFontSize, scale);
        }

        this.AddFontFromMemory(this.gameFontData, gameGlyphSize, this.gameGlyphRanges, mergeMode: true);

        this.LoadFontFromEmbeddedResource("Inconsolata-Regular.ttf", baseFontSize, atlas.GetGlyphRangesDefault());
        this.AddFontFromMemory(this.gameFontData, gameGlyphSize, this.gameGlyphRanges, mergeMode: true);

        this.LoadFontFromEmbeddedResource("FontAwesomeFreeSolid.otf", baseFontSize, GetFontAwesomeRanges());
        this.AddFontFromMemory(this.gameFontData, gameGlyphSize, this.gameGlyphRanges, mergeMode: true);

        this.LoadFontFromEmbeddedResource("FontAwesome710FreeSolid.otf", baseFontSize, GetFontAwesomeFixedRanges());
        this.AddFontFromMemory(this.gameFontData, gameGlyphSize, this.gameGlyphRanges, mergeMode: true);

        atlas.Build();

        this.axisBakeJob?.PatchMetrics();

        if (!defaultFont.IsNull)
        {
            ImGui.GetIO().Handle->FontDefault = defaultFont.Handle;
        }

        var fonts = atlas.Fonts;
        var iconFont = fonts[3];
        var iconFixedFont = fonts[4];
        ImGuiHelpers.CopyGlyphsAcrossFonts(iconFont, iconFixedFont, missingOnly: true, rebuildLookupTable: false);
        FitRatio(iconFixedFont);

        var inverseScale = 1f / scale;
        for (var i = 0; i < fonts.Size; i++)
        {
            fonts[i].Handle->Scale = inverseScale;
        }
    }

    /// <summary>
    /// Loads a TTF/OTF from this assembly's embedded resources and adds it to the global ImGui atlas.
    /// </summary>
    public ImFontPtr LoadFontFromEmbeddedResource(
        string resourceName,
        float fontSize,
        ushort* glyphRanges = null,
        bool mergeMode = false)
    {
        var io = ImGui.GetIO();

        using var stream = typeof(GlobalFontManager).Assembly
                                                    .GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException($"Missing resource {resourceName}");

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var fontData = ms.ToArray();

        var fontConfig = ImGui.ImFontConfig();
        fontConfig.FontNo = 0;
        fontConfig.GlyphRanges = glyphRanges;
        fontConfig.MergeMode = mergeMode;

        if (!mergeMode)
        {
            WriteFontConfigName(fontConfig, $"{resourceName}, {fontSize:0}px");
        }

        var font = AddFontFromBytesOwned(io.Fonts, fontData, fontSize, fontConfig, glyphRanges);

        fontConfig.Destroy();
        return font;
    }

    /// <summary>
    /// Writes a UTF-8, null-terminated display name into an <see cref="ImFontConfigPtr"/>'s fixed 40-byte
    /// Name buffer. ImGui surfaces this via <c>ImFont.GetDebugName()</c> in the style/font selector.
    /// </summary>
    internal static void WriteFontConfigName(ImFontConfigPtr fontConfig, string name)
    {
        var dst = fontConfig.Name;
        dst.Clear();
        var bytes = System.Text.Encoding.UTF8.GetBytes(name);
        var count = Math.Min(bytes.Length, dst.Length - 1);
        bytes.AsSpan(0, count).CopyTo(dst);
        dst[count] = 0;
    }

    private static ImFontPtr AddFontFromBytesOwned(
        ImFontAtlasPtr atlas,
        byte[] data,
        float sizePx,
        ImFontConfigPtr fontConfig,
        ushort* glyphRanges)
    {
        if (data.Length == 0)
        {
            throw new InvalidOperationException(
                "Font data passed to AddFontFromBytesOwned is empty. "
                + "A zero-length buffer would cause a native crash inside ImGui — the embedded resource may not have been packaged correctly.");
        }

        var native = (byte*)ImGui.MemAlloc((nuint)data.Length);
        data.AsSpan().CopyTo(new Span<byte>(native, data.Length));
        fontConfig.FontDataOwnedByAtlas = true;
        return atlas.AddFontFromMemoryTTF(new ReadOnlySpan<byte>(native, data.Length), sizePx, fontConfig, glyphRanges);
    }

    /// <summary>
    /// Bakes the chosen default-font <paramref name="spec"/> into the global ImGui atlas and returns the
    /// resulting font, for use as the fonts[1] default slot. Handles system fonts (loaded from file),
    /// Dalamud asset fonts and the Dalamud default; game fonts are handled by the AXIS path in
    /// <see cref="BuildInto"/> and never reach here. A <c>null</c> spec produces the built-in NotoSansCJKjp.
    /// </summary>
    private ImFontPtr AddDefaultFontSlot(ImFontAtlasPtr atlas, SingleFontSpec? spec, float baseFontSize, float scale)
    {
        if (spec is null)
        {
            return this.LoadFontFromEmbeddedResource(
                "NotoSansCJKjp-Medium.otf",
                baseFontSize,
                atlas.GetGlyphRangesJapanese());
        }

        var sizePx = spec.SizePx * scale;
        switch (spec.FontId)
        {
            case MockSystemFontId system:
                try
                {
                    var bytes = File.ReadAllBytes(system.Path);
                    return AddFontBytesToGlobal(atlas, bytes, sizePx, system.FaceIndex, atlas.GetGlyphRangesDefault());
                }
                catch (Exception ex)
                {
                    Log.Error(
                        ex,
                        "[GlobalFontManager] failed to load chosen system font {Path}; using default",
                        system.Path);
                    return this.LoadFontFromEmbeddedResource(
                        "NotoSansCJKjp-Medium.otf",
                        sizePx,
                        atlas.GetGlyphRangesJapanese());
                }

            case DalamudAssetFontAndFamilyId asset:
                try
                {
                    var bytes = MockFontAtlasResources.LoadAssetBytes(asset.Asset);
                    var ranges = asset.Asset == DalamudAsset.FontAwesomeFreeSolid
                                     ? GetFontAwesomeRanges()
                                     : atlas.GetGlyphRangesDefault();
                    return AddFontBytesToGlobal(atlas, bytes, sizePx, spec.FontNo, ranges);
                }
                catch (Exception ex)
                {
                    Log.Error(
                        ex,
                        "[GlobalFontManager] failed to load chosen asset font {Asset}; using default",
                        asset.Asset);
                    return this.LoadFontFromEmbeddedResource(
                        "NotoSansCJKjp-Medium.otf",
                        sizePx,
                        atlas.GetGlyphRangesJapanese());
                }

            default:
                return this.LoadFontFromEmbeddedResource(
                    "NotoSansCJKjp-Medium.otf",
                    sizePx,
                    atlas.GetGlyphRangesJapanese());
        }
    }

    private static ImFontPtr AddFontBytesToGlobal(
        ImFontAtlasPtr atlas,
        byte[] data,
        float sizePx,
        int fontNo,
        ushort* glyphRanges)
    {
        var fontConfig = ImGui.ImFontConfig();
        fontConfig.FontNo = fontNo;
        fontConfig.GlyphRanges = glyphRanges;
        fontConfig.MergeMode = false;
        WriteFontConfigName(fontConfig, $"default font, {sizePx:0}px");
        var font = AddFontFromBytesOwned(atlas, data, sizePx, fontConfig, glyphRanges);
        fontConfig.Destroy();
        return font;
    }

    private static ushort* GetFontAwesomeRanges()
    {
        var ranges = (ushort*)ImGui.MemAlloc(sizeof(ushort) * 3);
        ranges[0] = 0xF000;
        ranges[1] = 0xF8FF;
        ranges[2] = 0;
        return ranges;
    }

    private static ushort* GetFontAwesomeFixedRanges()
    {
        var ranges = (ushort*)ImGui.MemAlloc(sizeof(ushort) * 3);
        ranges[0] = 0x20;
        ranges[1] = 0x20;
        ranges[2] = 0;
        return ranges;
    }

    private static void FitRatio(ImFontPtr font)
    {
        var nsize = font.FontSize;
        var glyphs = (ImGuiHelpers.ImFontGlyphReal*)font.Glyphs.Data;
        if (glyphs == null)
        {
            return;
        }

        for (var i = 0; i < font.Glyphs.Size; i++)
        {
            ref var glyph = ref glyphs[i];
            var ratio = 1f;
            if (glyph.X1 - glyph.X0 > nsize)
            {
                ratio = MathF.Max(ratio, (glyph.X1 - glyph.X0) / nsize);
            }

            if (glyph.Y1 - glyph.Y0 > nsize)
            {
                ratio = MathF.Max(ratio, (glyph.Y1 - glyph.Y0) / nsize);
            }

            var w = MathF.Round((glyph.X1 - glyph.X0) / ratio, MidpointRounding.ToZero);
            var h = MathF.Round((glyph.Y1 - glyph.Y0) / ratio, MidpointRounding.AwayFromZero);
            glyph.X0 = MathF.Round((nsize - w) / 2f, MidpointRounding.ToZero);
            glyph.Y0 = MathF.Round((nsize - h) / 2f, MidpointRounding.AwayFromZero);
            glyph.X1 = glyph.X0 + w;
            glyph.Y1 = glyph.Y0 + h;
            glyph.AdvanceX = nsize;
        }

        font.BuildLookupTable();
    }

    private void AddFontFromMemory(byte[] fontData, float size, ushort* glyphRanges, bool mergeMode)
    {
        var fontConfig = ImGui.ImFontConfig();
        fontConfig.MergeMode = mergeMode;
        fontConfig.GlyphMinAdvanceX = 13.0f;

        AddFontFromBytesOwned(ImGui.GetIO().Fonts, fontData, size, fontConfig, glyphRanges);

        fontConfig.Destroy();
    }

    private ushort* GetGameGlyphRanges()
    {
        var builder = ImGuiNative.ImFontGlyphRangesBuilder();

        for (char c = SeIconCharMin; c <= SeIconCharMax; c++)
        {
            ImGuiNative.AddChar(builder, c);
        }

        ImVector<ushort> ranges;
        ImGuiNative.BuildRanges(builder, &ranges);

        return ranges.Data;
    }

    private byte[] LoadEmbeddedResource(string resourceName)
    {
        var assembly = typeof(GlobalFontManager).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new FileNotFoundException(
                               $"Embedded resource '{resourceName}' not found. Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}
