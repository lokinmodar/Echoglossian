namespace DalaMock.Core.Fonts.Atlas;

/// <summary>
/// Combined pre- and post-build toolkit used by <see cref="MockFontAtlas"/>. A single instance
/// flips <see cref="BuildStep"/> between passes — <see cref="FontAtlasBuildToolkitUtilities.OnPreBuild"/>
/// and <see cref="FontAtlasBuildToolkitUtilities.OnPostBuild"/> gate the user's callbacks on this value.
/// </summary>
internal sealed unsafe class MockFontAtlasBuildToolkit
    : IFontAtlasBuildToolkitPreBuild, IFontAtlasBuildToolkitPostBuild
{
    private readonly MockFontAtlas owner;
    private readonly Dictionary<IntPtr, IFontHandle> fontToHandle = new();
    private readonly Dictionary<IntPtr, FontScaleMode> scaleModes = new();
    private readonly List<SysAction> postBuildActions = new();
    private readonly List<IDisposable> disposeAfterBuild = new();
    private readonly List<SysAction> disposeAfterBuildActions = new();
    private readonly List<IDisposable> disposeWithAtlas = new();
    private readonly List<SysAction> disposeWithAtlasActions = new();
    private readonly byte[] gameFontData;
    private readonly ushort* gameGlyphRanges;

    public MockFontAtlasBuildToolkit(
        MockFontAtlas owner,
        ImFontAtlasPtr newAtlas,
        bool isAsync,
        byte[] gameFontData,
        ushort* gameGlyphRanges)
    {
        this.owner = owner;
        this.NewImAtlas = newAtlas;
        this.IsAsyncBuildOperation = isAsync;
        this.gameFontData = gameFontData;
        this.gameGlyphRanges = gameGlyphRanges;
    }

    public ImFontPtr Font { get; set; }

    public float Scale => this.owner.EffectiveScale;

    public bool IsAsyncBuildOperation { get; }

    public FontAtlasBuildStep BuildStep { get; internal set; } = FontAtlasBuildStep.PreBuild;

    public ImFontAtlasPtr NewImAtlas { get; }

    public ImVectorWrapper<ImFontPtr> Fonts =>
        new((ImVector*)&this.NewImAtlas.Handle->Fonts);

    public IReadOnlyList<SysAction> PostBuildActions => this.postBuildActions;

    public IReadOnlyList<IDisposable> DisposeAfterBuildList => this.disposeAfterBuild;

    public IReadOnlyList<SysAction> DisposeAfterBuildActions => this.disposeAfterBuildActions;

    public IReadOnlyList<IDisposable> DisposeWithAtlasList => this.disposeWithAtlas;

    public IReadOnlyList<SysAction> DisposeWithAtlasActions => this.disposeWithAtlasActions;

    public T DisposeAfterBuild<T>(T disposable) where T : IDisposable
    {
        this.disposeAfterBuild.Add(disposable);
        return disposable;
    }

    public GCHandle DisposeAfterBuild(GCHandle gcHandle)
    {
        var captured = gcHandle;
        this.disposeAfterBuildActions.Add(() =>
        {
            if (captured.IsAllocated)
            {
                captured.Free();
            }
        });
        return gcHandle;
    }

    public void DisposeAfterBuild(SysAction action) => this.disposeAfterBuildActions.Add(action);

    public T DisposeWithAtlas<T>(T disposable) where T : IDisposable
    {
        this.disposeWithAtlas.Add(disposable);
        return disposable;
    }

    public GCHandle DisposeWithAtlas(GCHandle gcHandle)
    {
        var captured = gcHandle;
        this.disposeWithAtlasActions.Add(() =>
        {
            if (captured.IsAllocated)
            {
                captured.Free();
            }
        });
        return gcHandle;
    }

    public void DisposeWithAtlas(SysAction action) => this.disposeWithAtlasActions.Add(action);

    public ImFontPtr SetFontScaleMode(ImFontPtr fontPtr, FontScaleMode mode)
    {
        this.scaleModes[(IntPtr)fontPtr.Handle] = mode;
        return fontPtr;
    }

    public FontScaleMode GetFontScaleMode(ImFontPtr fontPtr)
    {
        return this.scaleModes.TryGetValue((IntPtr)fontPtr.Handle, out var mode) ? mode : default;
    }

    public void RegisterPostBuild(SysAction action) => this.postBuildActions.Add(action);

    public ImFontPtr GetFont(IFontHandle fontHandle)
    {
        foreach (var (handle, h) in this.fontToHandle)
        {
            if (ReferenceEquals(h, fontHandle))
            {
                return new ImFontPtr((ImFont*)handle);
            }
        }

        return default;
    }

    internal void AssociateHandle(IFontHandle handle, ImFontPtr font)
    {
        if ((IntPtr)font.Handle != IntPtr.Zero)
        {
            this.fontToHandle[(IntPtr)font.Handle] = handle;
        }
    }

    public ImFontPtr AddFontFromImGuiHeapAllocatedMemory(
        nint dataPointer,
        int dataSize,
        in SafeFontConfig fontConfig,
        bool freeOnException,
        string debugTag) => this.AddFontFromImGuiHeapAllocatedMemory(
        (void*)dataPointer,
        dataSize,
        fontConfig,
        freeOnException,
        debugTag);

    public ImFontPtr AddFontFromImGuiHeapAllocatedMemory(
        void* dataPointer,
        int dataSize,
        in SafeFontConfig fontConfig,
        bool freeOnException,
        string debugTag)
    {
        fontConfig.Raw.FontDataOwnedByAtlas = true;
        try
        {
            var span = new ReadOnlySpan<byte>(dataPointer, dataSize);
            return this.NewImAtlas.AddFontFromMemoryTTF(
                span,
                fontConfig.SizePx,
                fontConfig.Raw,
                fontConfig.Raw.GlyphRanges);
        }
        catch
        {
            if (freeOnException)
            {
                ImGui.MemFree(dataPointer);
            }

            throw;
        }
    }

    public ImFontPtr AddFontFromFile(string path, in SafeFontConfig fontConfig)
    {
        return this.AddFontBytesCopy(File.ReadAllBytes(path), fontConfig);
    }

    public ImFontPtr AddFontFromStream(Stream stream, in SafeFontConfig fontConfig, bool leaveOpen, string debugTag)
    {
        try
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return this.AddFontBytesCopy(ms.ToArray(), fontConfig);
        }
        finally
        {
            if (!leaveOpen)
            {
                stream.Dispose();
            }
        }
    }

    public ImFontPtr AddFontFromMemory(ReadOnlySpan<byte> span, in SafeFontConfig fontConfig, string debugTag)
    {
        return this.AddFontBytesCopy(span, fontConfig);
    }

    public ImFontPtr AddDalamudDefaultFont(float sizePx, ushort[]? glyphRanges = null)
    {
        if (sizePx < 0)
        {
            sizePx = (12f * 4f / 3f) * -sizePx;
        }

        sizePx *= this.Scale;

        var cfg = new SafeFontConfig { SizePx = sizePx };
        if (glyphRanges is not null)
        {
            var rangesNative = (ushort*)ImGui.MemAlloc((nuint)(glyphRanges.Length * sizeof(ushort)));
            new Span<ushort>(rangesNative, glyphRanges.Length).Clear();
            glyphRanges.CopyTo(new Span<ushort>(rangesNative, glyphRanges.Length));
            cfg.Raw.GlyphRanges = rangesNative;
            this.disposeWithAtlasActions.Add(() => ImGui.MemFree(rangesNative));
        }

        var font = this.AddFontBytesCopy(
            MockFontAtlasResources.LoadEmbeddedResource("NotoSansCJKjp-Medium.otf"),
            cfg);

        this.MergeGameFontInto(font, sizePx * 2f);

        if ((IntPtr)this.Font.Handle == IntPtr.Zero)
        {
            this.Font = font;
        }

        return font;
    }

    public ImFontPtr AddDalamudAssetFont(DalamudAsset asset, in SafeFontConfig fontConfig)
    {
        return this.AddFontBytesCopy(MockFontAtlasResources.LoadAssetBytes(asset), fontConfig);
    }

    public ImFontPtr AddFontAwesomeIconFont(in SafeFontConfig fontConfig)
    {
        var ranges = (ushort*)ImGui.MemAlloc(sizeof(ushort) * 3);
        ranges[0] = 0xF000;
        ranges[1] = 0xF8FF;
        ranges[2] = 0;
        this.disposeWithAtlasActions.Add(() => ImGui.MemFree(ranges));

        var cfg = fontConfig;
        cfg.Raw.GlyphRanges = ranges;
        cfg.GlyphMinAdvanceX = cfg.SizePx;
        cfg.GlyphMaxAdvanceX = cfg.SizePx;
        return this.AddFontBytesCopy(
            MockFontAtlasResources.LoadAssetBytes(DalamudAsset.FontAwesomeFreeSolid),
            cfg);
    }

    public ImFontPtr AddGameSymbol(in SafeFontConfig fontConfig) =>
        this.AddGameFontBytes(fontConfig);

    public ImFontPtr AddGameGlyphs(GameFontStyle gameFontStyle, ushort[]? glyphRanges, ImFontPtr mergeFont) =>
        this.AddGameFontBytes(
            new SafeFontConfig
            {
                SizePx = gameFontStyle.SizePx <= 0 ? (12f * 4f / 3f) : gameFontStyle.SizePx,
                MergeFont = mergeFont,
            });

    public void AttachWindowsDefaultFont(
        CultureInfo cultureInfo,
        in SafeFontConfig fontConfig,
        int weight = (int)DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL,
        int stretch = (int)DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL,
        int style = (int)DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL)
    {
        // Sort me out later
    }

    public void AttachExtraGlyphsForDalamudLanguage(in SafeFontConfig fontConfig)
    {
    }

    public int StoreTexture(IDalamudTextureWrap textureWrap, bool disposeOnError)
    {
        return 0;
    }

    public void FitRatio(ImFontPtr font, bool rebuildLookupTable = true)
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

        if (rebuildLookupTable)
        {
            font.BuildLookupTable();
        }
    }

    public void CopyGlyphsAcrossFonts(
        ImFontPtr source,
        ImFontPtr target,
        bool missingOnly,
        bool rebuildLookupTable = true,
        char rangeLow = ' ',
        char rangeHigh = '￾')
    {
        if (!ContainsFont(this.NewImAtlas, source) || !ContainsFont(this.NewImAtlas, target))
        {
            return;
        }

        ImGuiHelpers.CopyGlyphsAcrossFonts(source, target, missingOnly, rebuildLookupTable, rangeLow, rangeHigh);
    }

    public void BuildLookupTable(ImFontPtr font) => font.BuildLookupTable();

    private ImFontPtr AddFontBytesCopy(ReadOnlySpan<byte> src, in SafeFontConfig fontConfig)
    {
        var native = (byte*)ImGui.MemAlloc((nuint)src.Length);
        src.CopyTo(new Span<byte>(native, src.Length));
        fontConfig.Raw.FontDataOwnedByAtlas = true;
        return this.NewImAtlas.AddFontFromMemoryTTF(
            new ReadOnlySpan<byte>(native, src.Length),
            fontConfig.SizePx,
            fontConfig.Raw,
            fontConfig.Raw.GlyphRanges);
    }

    private ImFontPtr AddGameFontBytes(in SafeFontConfig fontConfig)
    {
        var cfg = fontConfig;
        cfg.Raw.GlyphRanges = this.gameGlyphRanges;
        return this.AddFontBytesCopy(this.gameFontData, cfg);
    }

    private void MergeGameFontInto(ImFontPtr mergeTarget, float gameGlyphSize)
    {
        var cfg = new SafeFontConfig
        {
            SizePx = gameGlyphSize,
            MergeFont = mergeTarget,
            GlyphMinAdvanceX = 13f,
        };
        cfg.Raw.GlyphRanges = this.gameGlyphRanges;
        this.AddFontBytesCopy(this.gameFontData, cfg);
    }

    private static bool ContainsFont(ImFontAtlasPtr atlas, ImFontPtr font)
    {
        if ((IntPtr)font.Handle == IntPtr.Zero)
        {
            return false;
        }

        ref var vec = ref atlas.Fonts;
        for (var i = 0; i < vec.Size; i++)
        {
            if ((IntPtr)vec[i].Handle == (IntPtr)font.Handle)
            {
                return true;
            }
        }

        return false;
    }
}
