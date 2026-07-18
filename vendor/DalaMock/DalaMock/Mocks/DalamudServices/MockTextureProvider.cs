namespace DalaMock.Core.Mocks.DalamudServices;

public partial class MockTextureProvider : ITextureProvider, IMockService
{
    private const string IconFileFormat = "ui/icon/{0:D3}000/{1}{2:D6}.tex";
    private const string HighResolutionIconFileFormat = "ui/icon/{0:D3}000/{1}{2:D6}_hr1.tex";
    private const uint MillisecondsEvictionTime = 2000;

    private readonly Dictionary<string, TextureInfo> activeTextures = new();
    private readonly MockDalamudConfiguration dalamudConfiguration;
    private readonly IDataManager dataManager;
    private readonly IFramework framework;

    private readonly GraphicsDevice graphicsDevice;
    private readonly ImGuiScene imGuiScene;
    private readonly IPluginLog log;

    private IDalamudTextureWrap? fallbackTextureWrap;

    public MockTextureProvider(
        GraphicsDevice graphicsDevice,
        ImGuiScene imGuiScene,
        IFramework framework,
        IDataManager dataManager,
        MockDalamudConfiguration dalamudConfiguration,
        IPluginLog log)
    {
        this.graphicsDevice = graphicsDevice;
        this.imGuiScene = imGuiScene;
        this.framework = framework;
        this.dataManager = dataManager;
        this.dalamudConfiguration = dalamudConfiguration;
        this.log = log;

        this.framework.Update += this.FrameworkOnUpdate;

        this.CreateFallbackTexture();
    }

    public string ServiceName { get; } = "Texture Provider";

    /// <inheritdoc/>
    public event ITextureSubstitutionProvider.TextureDataInterceptorDelegate? InterceptTexDataLoad;

    /// <summary>
    /// Get a path for a specific icon's .tex file.
    /// </summary>
    /// <returns>
    /// Null, if the icon does not exist in the specified configuration, or the path to the texture's .tex file,
    /// which can be loaded via IDataManager.
    /// </returns>
    public string? GetPathFromGameIcon(in GameIconLookup lookup)
    {
        var hiRes = lookup.HiRes;

        // 1. Item
        var path = FormatIconPath(
            lookup.IconId,
            lookup.ItemHq ? "hq/" : string.Empty,
            hiRes);
        if (this.dataManager.FileExists(path))
        {
            return path;
        }

        var language = lookup.Language ?? this.dalamudConfiguration.ClientLanguage;
        var languageFolder = language switch
        {
            ClientLanguage.Japanese => "ja/",
            ClientLanguage.English => "en/",
            ClientLanguage.German => "de/",
            ClientLanguage.French => "fr/",
            _ => throw new ArgumentOutOfRangeException(nameof(language), $"Unknown Language: {language}"),
        };

        // 2. Regular icon, with language, hi-res
        path = FormatIconPath(
            lookup.IconId,
            languageFolder,
            hiRes);
        if (this.dataManager.FileExists(path))
        {
            return path;
        }

        if (hiRes)
        {
            // 3. Regular icon, with language, no hi-res
            path = FormatIconPath(
                lookup.IconId,
                languageFolder,
                false);
            if (this.dataManager.FileExists(path))
            {
                return path;
            }
        }

        // 4. Regular icon, without language, hi-res
        path = FormatIconPath(
            lookup.IconId,
            null,
            hiRes);
        if (this.dataManager.FileExists(path))
        {
            return path;
        }

        // 4. Regular icon, without language, no hi-res
        if (hiRes)
        {
            path = FormatIconPath(
                lookup.IconId,
                null,
                false);
            if (this.dataManager.FileExists(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Get a texture handle for the texture at the specified path.
    /// You may only specify paths in the game's VFS.
    /// </summary>
    /// <param name="path">The path to the texture in the game's VFS.</param>
    /// <param name="keepAlive">Prevent Dalamud from automatically unloading this texture to save memory. Usually does not need to be set.</param>
    /// <returns>Null, if the icon does not exist, or a texture wrap that can be used to render the texture.</returns>
    public MockTextureManagerTextureWrap? GetTextureFromGame(string path, bool keepAlive = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (Path.IsPathRooted(path))
        {
            throw new ArgumentException(
                "Use GetTextureFromFile() to load textures directly from a file.",
                nameof(path));
        }

        return !this.dataManager.FileExists(path) ? null : this.CreateWrap(path, keepAlive);
    }

    /// <summary>
    /// Get a texture handle for the image or texture, specified by the passed FileInfo.
    /// You may only specify paths on the native file system.
    ///
    /// This API can load .png and .tex files.
    /// </summary>
    /// <param name="file">The FileInfo describing the image or texture file.</param>
    /// <param name="keepAlive">Prevent Dalamud from automatically unloading this texture to save memory. Usually does not need to be set.</param>
    /// <returns>Null, if the file does not exist, or a texture wrap that can be used to render the texture.</returns>
    public MockTextureManagerTextureWrap? GetTextureFromFile(FileInfo file, bool keepAlive = false)
    {
        ArgumentNullException.ThrowIfNull(file);
        return !file.Exists ? null : this.CreateWrap(file.FullName, keepAlive);
    }

    /// <summary>
    /// Get a texture handle for the image or texture, specified by the passed FileInfo.
    /// You may only specify paths on the native file system.
    ///
    /// This API can load .png and .tex files.
    /// </summary>
    /// <param name="file">The FileInfo describing the image or texture file.</param>
    /// <param name="keepAlive">Prevent Dalamud from automatically unloading this texture to save memory. Usually does not need to be set.</param>
    /// <returns>Null, if the file does not exist, or a texture wrap that can be used to render the texture.</returns>
    public MockTextureManagerTextureWrap? GetTextureFromFile(string file, bool keepAlive = false)
    {
        return this.GetTextureFromFile(new FileInfo(file), keepAlive);
    }

    /// <summary>
    /// Get a texture handle for the specified Lumina TexFile.
    /// </summary>
    /// <param name="file">The texture to obtain a handle to.</param>
    /// <returns>A texture wrap that can be used to render the texture.</returns>
    public IDalamudTextureWrap? GetTexture(TexFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var buffer = file.TextureBuffer;
        buffer = buffer.Filter(0, 0, TexFile.TextureFormat.B8G8R8A8);

        var texture = this.graphicsDevice.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                (uint)buffer.Width,
                (uint)buffer.Height,
                1,
                1,
                PixelFormat.B8_G8_R8_A8_UNorm,
                TextureUsage.Sampled));
        var cpUframeBufferTextureId =
            this.imGuiScene.GetOrCreateImGuiBinding(this.graphicsDevice.ResourceFactory, texture);
        this.graphicsDevice.UpdateTexture(
            texture,
            buffer.RawData,
            0,
            0,
            0,
            (uint)buffer.Width,
            (uint)buffer.Height,
            1,
            0,
            0);
        var veldridTextureWrap =
            new MockTextureMap(cpUframeBufferTextureId, buffer.Width, buffer.Height, this.imGuiScene);
        return veldridTextureWrap;
    }

    /// <inheritdoc/>
    public string GetSubstitutedPath(string originalPath)
    {
        if (this.InterceptTexDataLoad == null)
        {
            return originalPath;
        }

        string? interceptPath = null;
        this.InterceptTexDataLoad.Invoke(originalPath, ref interceptPath);

        if (interceptPath != null)
        {
            return interceptPath;
        }

        return originalPath;
    }

    /// <inheritdoc/>
    public void InvalidatePaths(IEnumerable<string> paths)
    {
        lock (this.activeTextures)
        {
            foreach (var path in paths)
            {
                if (!this.activeTextures.TryGetValue(path, out var info) || info == null)
                {
                    continue;
                }

                info.Wrap?.Dispose();
                info.Wrap = null;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.fallbackTextureWrap?.Dispose();
        this.framework.Update -= this.FrameworkOnUpdate;

        foreach (var activeTexture in this.activeTextures)
        {
            activeTexture.Value.Wrap?.Dispose();
        }

        this.activeTextures.Clear();
    }

    /// <summary>
    /// Get texture info.
    /// </summary>
    /// <param name="path">Path to the texture.</param>
    /// <param name="rethrow">
    /// If true, exceptions caused by texture load will not be caught.
    /// If false, exceptions will be caught and a dummy texture will be returned to prevent plugins from using invalid texture handles.
    /// </param>
    /// <returns>Info object storing texture metadata.</returns>
    internal TextureInfo GetInfo(string path, bool rethrow = false)
    {
        TextureInfo? info;
        lock (this.activeTextures)
        {
            if (!this.activeTextures.TryGetValue(path, out info))
            {
                Debug.Assert(rethrow, "This should never run when getting outside of creator");

                info = new TextureInfo();
                this.activeTextures.Add(path, info);
            }

            if (info == null)
            {
                throw new Exception("null info in activeTextures");
            }
        }

        if (info.KeepAliveCount == 0)
        {
            info.LastAccess = DateTime.UtcNow;
        }

        if (info is { Wrap: not null })
        {
            return info;
        }

        // Substitute the path here for loading, instead of when getting the respective TextureInfo
        path = this.GetSubstitutedPath(path);

        IDalamudTextureWrap? wrap;
        try
        {
            // We want to load this from the disk, probably, if the path has a root
            // Not sure if this can cause issues with e.g. network drives, might have to rethink
            // and add a flag instead if it does.
            if (Path.IsPathRooted(path))
            {
                if (Path.GetExtension(path) == ".tex")
                {
                    // Attempt to load via Lumina
                    var file = this.dataManager.GameData.GetFileFromDisk<TexFile>(path);
                    wrap = this.GetTexture(file);
                }
                else
                {
                    // Attempt to load image
                    wrap = this.LoadImage(path);
                }
            }
            else
            {
                // Load regularly from dats
                var file = this.dataManager.GetFile<TexFile>(path);
                if (file == null)
                {
                    throw new Exception("Could not load TexFile from dat.");
                }

                wrap = this.GetTexture(file);
            }

            if (wrap == null)
            {
                throw new Exception("Could not create texture");
            }

            // TODO: We could support this, but I don't think it's worth it at the moment.
            var extents = new Vector2(wrap.Width, wrap.Height);
            if (info.Extents != Vector2.Zero && info.Extents != extents)
            {
                this.log.Warning(
                    "Texture at {Path} changed size between reloads, this is currently not supported.",
                    path);
            }

            info.Extents = extents;
        }
        catch (Exception e)
        {
            this.log.Error(e, "Could not load texture from {Path}", path);

            // When creating the texture initially, we want to be able to pass errors back to the plugin
            if (rethrow)
            {
                throw;
            }

            // This means that the load failed due to circumstances outside of our control,
            // and we can't do anything about it. Return a dummy texture so that the plugin still
            // has something to draw.
            wrap = this.fallbackTextureWrap;

            // Prevent divide-by-zero
            if (info.Extents == Vector2.Zero)
            {
                info.Extents = Vector2.One;
            }
        }

        info.Wrap = wrap;
        return info;
    }

    public IDalamudTextureWrap LoadImage(string filePath)
    {
        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        using (var ms = new MemoryStream())
        {
            fs.CopyTo(ms);
            var image = Stbi.LoadFromMemory(ms, 4);
            var texture = this.graphicsDevice.ResourceFactory.CreateTexture(
                TextureDescription.Texture2D(
                    (uint)image.Width,
                    (uint)image.Height,
                    1,
                    1,
                    PixelFormat.R8_G8_B8_A8_UNorm,
                    TextureUsage.Sampled));
            var cpUframeBufferTextureId =
                this.imGuiScene.GetOrCreateImGuiBinding(this.graphicsDevice.ResourceFactory, texture);
            this.graphicsDevice.UpdateTexture(
                texture,
                image.Data,
                0,
                0,
                0,
                (uint)image.Width,
                (uint)image.Height,
                1,
                0,
                0);
            var veldridTextureWrap =
                new MockTextureMap(cpUframeBufferTextureId, image.Width, image.Height, this.imGuiScene);
            return veldridTextureWrap;
        }
    }

    public IDalamudTextureWrap LoadImageRaw(byte[] imageData, int width, int height, PixelFormat pixelFormat)
    {
        var texture = this.graphicsDevice.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                (uint)width,
                (uint)height,
                1,
                1,
                pixelFormat,
                TextureUsage.Sampled));
        var cpUframeBufferTextureId =
            this.imGuiScene.GetOrCreateImGuiBinding(this.graphicsDevice.ResourceFactory, texture);
        this.graphicsDevice.UpdateTexture(texture, imageData, 0, 0, 0, (uint)width, (uint)height, 1, 0, 0);
        var veldridTextureWrap =
            new MockTextureMap(cpUframeBufferTextureId, width, height, this.imGuiScene);
        return veldridTextureWrap;
    }

    /// <summary>
    /// Notify the system about an instance of a texture wrap being disposed.
    /// If required conditions are met, the texture will be unloaded at the next update.
    /// </summary>
    /// <param name="path">The path to the texture.</param>
    /// <param name="keepAlive">Whether or not this handle was created in keep-alive mode.</param>
    internal void NotifyTextureDisposed(string path, bool keepAlive)
    {
        lock (this.activeTextures)
        {
            if (!this.activeTextures.TryGetValue(path, out var info))
            {
                this.log.Warning("Disposing texture that didn't exist: {Path}", path);
                return;
            }

            info.RefCount--;

            if (keepAlive)
            {
                info.KeepAliveCount--;
            }

            // Clean it up by the next update. If it's re-requested in-between, we don't reload it.
            if (info.RefCount <= 0)
            {
                info.LastAccess = default;
            }
        }
    }

    private static string FormatIconPath(uint iconId, string? type, bool highResolution)
    {
        var format = highResolution ? HighResolutionIconFileFormat : IconFileFormat;

        type ??= string.Empty;
        if (type.Length > 0 && !type.EndsWith("/"))
        {
            type += "/";
        }

        return string.Format(format, iconId / 1000, type, iconId);
    }

    private MockTextureManagerTextureWrap? CreateWrap(string path, bool keepAlive)
    {
        lock (this.activeTextures)
        {
            // This will create the texture.
            // That's fine, it's probably used immediately and this will let the plugin catch load errors.
            var info = this.GetInfo(path, true);

            // We need to increase the refcounts here while locking the collection!
            // Otherwise, if this is loaded from a task, cleanup might already try to delete it
            // before it can be increased.
            info.RefCount++;

            if (keepAlive)
            {
                info.KeepAliveCount++;
            }

            return new MockTextureManagerTextureWrap(path, info.Extents, keepAlive, this);
        }
    }

    private void FrameworkOnUpdate(IFramework fw)
    {
        lock (this.activeTextures)
        {
            var toRemove = new List<string>();

            foreach (var texInfo in this.activeTextures)
            {
                if (texInfo.Value.RefCount == 0)
                {
                    this.log.Verbose("Evicting {Path} since no refs", texInfo.Key);

                    Debug.Assert(texInfo.Value.KeepAliveCount == 0, "texInfo.Value.KeepAliveCount == 0");

                    texInfo.Value.Wrap?.Dispose();
                    texInfo.Value.Wrap = null;
                    toRemove.Add(texInfo.Key);
                    continue;
                }

                if (texInfo.Value.KeepAliveCount > 0 || texInfo.Value.Wrap == null)
                {
                    continue;
                }

                if (DateTime.UtcNow - texInfo.Value.LastAccess > TimeSpan.FromMilliseconds(MillisecondsEvictionTime))
                {
                    this.log.Verbose("Evicting {Path} since too old", texInfo.Key);
                    texInfo.Value.Wrap.Dispose();
                    texInfo.Value.Wrap = null;
                }
            }

            foreach (var path in toRemove)
            {
                this.activeTextures.Remove(path);
            }
        }
    }

    private void CreateFallbackTexture()
    {
        var fallbackTexBytes = new byte[] { 0xFF, 0x00, 0xDC, 0xFF };
        this.fallbackTextureWrap = this.LoadImageRaw(fallbackTexBytes, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm);
        Debug.Assert(this.fallbackTextureWrap != null, "this.fallbackTextureWrap != null");
    }

    public IDalamudTextureWrap CreateEmpty(
        RawImageSpecification specs,
        bool cpuRead,
        bool cpuWrite,
        string? debugName = null)
    {
        var texture = this.graphicsDevice.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                (uint)specs.Width,
                (uint)specs.Height,
                1,
                1,
                PixelFormat.B8_G8_R8_A8_UNorm,
                TextureUsage.Sampled));
        var cpUframeBufferTextureId =
            this.imGuiScene.GetOrCreateImGuiBinding(this.graphicsDevice.ResourceFactory, texture);

        var veldridTextureWrap =
            new MockTextureMap(cpUframeBufferTextureId, specs.Width, specs.Height, this.imGuiScene);
        return veldridTextureWrap;
    }

    public IDrawListTextureWrap CreateDrawListTexture(string? debugName = null)
    {
        throw new NotImplementedException();
    }

    public Task<IDalamudTextureWrap> CreateFromExistingTextureAsync(
        IDalamudTextureWrap wrap,
        TextureModificationArgs args,
        bool leaveWrapOpen = false,
        string? debugName = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        throw new NotImplementedException();
    }

    public Task<IDalamudTextureWrap> CreateFromImGuiViewportAsync(
        ImGuiViewportTextureArgs args,
        string? debugName = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        throw new NotImplementedException();
    }

    public Task<IDalamudTextureWrap> CreateFromImageAsync(
        ReadOnlyMemory<byte> bytes,
        string? debugName = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        throw new NotImplementedException();
    }

    public Task<IDalamudTextureWrap> CreateFromImageAsync(
        Stream stream,
        bool leaveOpen = false,
        string? debugName = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        throw new NotImplementedException();
    }

    public IDalamudTextureWrap CreateFromRaw(
        RawImageSpecification specs,
        ReadOnlySpan<byte> bytes,
        string? debugName = null)
    {
        var pixelFormat = specs.DxgiFormat switch
        {
            // DXGI_FORMAT_R8G8B8A8_UNORM
            28 => Veldrid.PixelFormat.R8_G8_B8_A8_UNorm,

            // DXGI_FORMAT_B8G8R8A8_UNORM
            87 => Veldrid.PixelFormat.B8_G8_R8_A8_UNorm,
            _ => throw new ArgumentOutOfRangeException(),
        };
        return this.LoadImageRaw(bytes.ToArray(), specs.Width, specs.Height, pixelFormat);
    }

    public Task<IDalamudTextureWrap> CreateFromRawAsync(
        RawImageSpecification specs,
        ReadOnlyMemory<byte> bytes,
        string? debugName = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        throw new NotImplementedException();
    }

    public Task<IDalamudTextureWrap> CreateFromRawAsync(
        RawImageSpecification specs,
        Stream stream,
        bool leaveOpen = false,
        string? debugName = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        throw new NotImplementedException();
    }

    public IDalamudTextureWrap CreateFromTexFile(TexFile file)
    {
        throw new NotImplementedException();
    }

    public Task<IDalamudTextureWrap> CreateFromTexFileAsync(
        TexFile file,
        string? debugName = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        throw new NotImplementedException();
    }

    public Task<IDalamudTextureWrap> CreateFromClipboardAsync(string? debugName = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        throw new NotImplementedException();
    }

    public IDalamudTextureWrap CreateTextureFromSeString(
        ReadOnlySpan<byte> text,
        scoped in SeStringDrawParams drawParams = new SeStringDrawParams(),
        string? debugName = null)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<IBitmapCodecInfo> GetSupportedImageDecoderInfos()
    {
        throw new NotImplementedException();
    }

    public ISharedImmediateTexture GetFromGameIcon(in GameIconLookup lookup)
    {
        var gamePath = this.GetPathFromGameIcon(lookup);
        if (gamePath == null)
        {
            throw new Exception($"Attempted to load a invalid game icon path: {gamePath}");
        }

        var textureWrap = this.GetTextureFromGame(gamePath, true);
        if (textureWrap == null)
        {
            throw new Exception($"Texture wrap created from game icon was invalid: {gamePath}");
        }

        return new ForwardingSharedImmediateTexture(textureWrap);
    }

    public bool HasClipboardImage()
    {
        return false;
    }

    public bool TryGetFromGameIcon(in GameIconLookup lookup, out ISharedImmediateTexture texture)
    {
        texture = null!;
        var gamePath = this.GetPathFromGameIcon(lookup);
        if (gamePath == null)
        {
            return false;
        }

        var textureWrap = this.GetTextureFromGame(gamePath, true);
        if (textureWrap == null)
        {
            return false;
        }

        texture = new ForwardingSharedImmediateTexture(textureWrap);
        return true;
    }

    public ISharedImmediateTexture GetFromGame(string path)
    {
        var textureFile = this.GetTextureFromGame(path);
        if (textureFile == null)
        {
            throw new Exception($"Failed to create texture {textureFile}");
        }

        return new ForwardingSharedImmediateTexture(textureFile);
    }

    public ISharedImmediateTexture GetFromFile(string path)
    {
        return this.GetFromFile(new FileInfo(path));
    }

    public ISharedImmediateTexture GetFromFile(FileInfo file)
    {
        var textureFile = this.GetTextureFromFile(file);
        if (textureFile == null)
        {
            throw new Exception($"Failed to create texture {textureFile}");
        }

        return new ForwardingSharedImmediateTexture(textureFile);
    }

    public ISharedImmediateTexture GetFromFileAbsolute(string fullPath)
    {
        throw new NotImplementedException();
    }

    public ISharedImmediateTexture GetFromManifestResource(Assembly assembly, string name)
    {
        throw new NotImplementedException();
    }

    public string GetIconPath(in GameIconLookup lookup)
    {
        throw new NotImplementedException();
    }

    public bool TryGetIconPath(in GameIconLookup lookup, out string? path)
    {
        throw new NotImplementedException();
    }

    public bool IsDxgiFormatSupported(int dxgiFormat)
    {
        throw new NotImplementedException();
    }

    public bool IsDxgiFormatSupportedForCreateFromExistingTextureAsync(int dxgiFormat)
    {
        throw new NotImplementedException();
    }

    public IntPtr ConvertToKernelTexture(IDalamudTextureWrap wrap, bool leaveWrapOpen = false)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Internal representation of a managed texture.
    /// </summary>
    internal class TextureInfo
    {
        /// <summary>
        /// Gets or sets the actual texture wrap. May be unpopulated.
        /// </summary>
        public IDalamudTextureWrap? Wrap { get; set; }

        /// <summary>
        /// Gets or sets the time the texture was last accessed.
        /// </summary>
        public DateTime LastAccess { get; set; }

        /// <summary>
        /// Gets or sets the number of active holders of this texture.
        /// </summary>
        public uint RefCount { get; set; }

        /// <summary>
        /// Gets or sets the number of active holders that want this texture to stay alive forever.
        /// </summary>
        public uint KeepAliveCount { get; set; }

        /// <summary>
        /// Gets or sets the extents of the texture.
        /// </summary>
        public Vector2 Extents { get; set; }
    }
}
