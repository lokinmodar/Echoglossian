namespace DalaMock.Core.Fonts.Atlas;

/// <summary>
/// Mock implementation of <see cref="IFontAtlas"/>. Each instance owns its own native
/// <see cref="ImFontAtlasPtr"/> + Veldrid texture, so plugin fonts don't collide with the
/// global atlas. Build can run sync (<see cref="FontAtlasAutoRebuildMode.OnNewFrame"/>) or
/// async (<see cref="FontAtlasAutoRebuildMode.Async"/>); texture upload is always marshalled
/// onto the main render thread via <see cref="ImGuiScene"/>.
/// </summary>
internal sealed unsafe class MockFontAtlas : IFontAtlas
{
    private readonly ImGuiScene scene;
    private readonly List<MockDelegateFontHandle> handles = new();
    private readonly object syncRoot = new();
    private Snapshot? current;
    private int suppressDepth;
    private bool pendingRebuild;
    private bool disposed;
    private Task currentBuildTask = Task.CompletedTask;

    public MockFontAtlas(
        ImGuiScene scene,
        string name,
        FontAtlasAutoRebuildMode autoRebuildMode,
        bool isGlobalScaled)
    {
        this.scene = scene;
        this.Name = name;
        this.AutoRebuildMode = autoRebuildMode;
        this.IsGlobalScaled = isGlobalScaled;
        this.scene.RegisterFontAtlas(this);
    }

    public event FontAtlasBuildStepDelegate? BuildStepChange;

    public event SysAction? RebuildRecommend;

    public string Name { get; }

    public FontAtlasAutoRebuildMode AutoRebuildMode { get; }

    public bool IsGlobalScaled { get; }

    public ImFontAtlasPtr ImAtlas
    {
        get
        {
            lock (this.syncRoot)
            {
                return this.current?.Atlas ?? default;
            }
        }
    }

    public Task BuildTask => this.currentBuildTask;

    public bool HasBuiltAtlas
    {
        get
        {
            lock (this.syncRoot)
            {
                return this.current is { Atlas.Handle: not null };
            }
        }
    }

    public IDisposable SuppressAutoRebuild()
    {
        lock (this.syncRoot)
        {
            this.suppressDepth++;
        }

        return new SuppressionScope(this);
    }

    public IFontHandle NewDelegateFontHandle(FontAtlasBuildStepDelegate buildStepDelegate)
    {
        ArgumentNullException.ThrowIfNull(buildStepDelegate);

        var handle = new MockDelegateFontHandle(this, buildStepDelegate);
        lock (this.syncRoot)
        {
            this.handles.Add(handle);
        }

        this.RebuildRecommend?.Invoke();
        this.MaybeAutoRebuild();
        return handle;
    }

    public IFontHandle NewGameFontHandle(GameFontStyle style)
    {
        var sizePx = style.SizePx <= 0 ? (12f * 4f / 3f) : style.SizePx;
        return this.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.Font = tk.AddDalamudDefaultFont(sizePx)));
    }

    public void BuildFontsImmediately()
    {
        if (this.AutoRebuildMode == FontAtlasAutoRebuildMode.Async)
        {
            throw new InvalidOperationException(
                "BuildFontsImmediately is not valid for Async auto-rebuild mode; use BuildFontsAsync instead.");
        }

        this.RunBuildOnMainThread();
    }

    public void BuildFontsOnNextFrame()
    {
        if (this.AutoRebuildMode == FontAtlasAutoRebuildMode.Async)
        {
            throw new InvalidOperationException(
                "BuildFontsOnNextFrame is not valid for Async auto-rebuild mode; use BuildFontsAsync instead.");
        }

        lock (this.syncRoot)
        {
            this.pendingRebuild = true;
        }
    }

    public Task BuildFontsAsync()
    {
        if (this.AutoRebuildMode == FontAtlasAutoRebuildMode.OnNewFrame)
        {
            throw new InvalidOperationException(
                "BuildFontsAsync is not valid for OnNewFrame auto-rebuild mode; use BuildFontsOnNextFrame instead.");
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        this.currentBuildTask = tcs.Task;
        _ = Task.Run(() =>
        {
            try
            {
                this.RunBuildAsyncCpu(tcs);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    public void Dispose()
    {
        lock (this.syncRoot)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        foreach (var handle in this.handles.ToArray())
        {
            handle.Dispose();
        }

        this.scene.UnregisterFontAtlas(this);

        Snapshot? snapshot;
        lock (this.syncRoot)
        {
            snapshot = this.current;
            this.current = null;
        }

        if (snapshot is not null)
        {
            this.scene.EnqueueDeferredAtlasDispose(() => DestroySnapshot(snapshot, this.scene));
        }
    }

    /// <summary>Called by <see cref="ImGuiScene.Update"/> before each NewFrame.</summary>
    internal void DrainPendingFrameRequests()
    {
        bool shouldRebuild;
        lock (this.syncRoot)
        {
            shouldRebuild = this.pendingRebuild && this.suppressDepth == 0 && !this.disposed;
            if (shouldRebuild)
            {
                this.pendingRebuild = false;
            }
        }

        if (shouldRebuild)
        {
            try
            {
                this.RunBuildOnMainThread();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MockFontAtlas:{Name}] frame-driven rebuild failed", this.Name);
            }
        }
    }

    /// <summary>Increments the refcount on the current snapshot. Returns a release SysAction for the caller.</summary>
    internal SysAction? AcquireSnapshotRef()
    {
        Snapshot? snapshot;
        lock (this.syncRoot)
        {
            snapshot = this.current;
            if (snapshot is null)
            {
                return null;
            }

            snapshot.RefCount++;
        }

        return () =>
        {
            bool destroy = false;
            lock (this.syncRoot)
            {
                snapshot.RefCount--;
                if (snapshot.RefCount <= 0 && snapshot.MarkedForDestruction)
                {
                    destroy = true;
                }
            }

            if (destroy)
            {
                this.scene.EnqueueDeferredAtlasDispose(() => DestroySnapshot(snapshot, this.scene));
            }
        };
    }

    internal void RemoveHandle(MockDelegateFontHandle handle)
    {
        lock (this.syncRoot)
        {
            this.handles.Remove(handle);
        }
    }

    private void MaybeAutoRebuild()
    {
        switch (this.AutoRebuildMode)
        {
            case FontAtlasAutoRebuildMode.OnNewFrame:
                lock (this.syncRoot)
                {
                    this.pendingRebuild = true;
                }

                break;
            case FontAtlasAutoRebuildMode.Async:
                _ = this.BuildFontsAsync();
                break;
            case FontAtlasAutoRebuildMode.Disable:
            default:
                break;
        }
    }

    /// <summary>
    /// The scale that the build toolkit should apply to font sizes: the scene's global font scale when
    /// this atlas is global-scaled, otherwise 1. Mirrors Dalamud's <c>IFontAtlasBuildToolkit.Scale</c>.
    /// </summary>
    internal float EffectiveScale => this.IsGlobalScaled ? this.scene.GlobalFontScale : 1f;

    /// <summary>
    /// Invoked by <see cref="ImGuiScene"/> when the global font scale changes. Fires the public
    /// <see cref="RebuildRecommend"/> event and queues a rebuild according to this atlas's auto-rebuild
    /// mode so global-scaled fonts are re-baked at the new size.
    /// </summary>
    internal void RequestGlobalScaleRebuild()
    {
        this.RebuildRecommend?.Invoke();
        this.MaybeAutoRebuild();
    }

    private void RunBuildOnMainThread()
    {
        var newSnapshot = this.BuildCpu(isAsync: false);
        this.UploadAndSwap(newSnapshot);
    }

    private void RunBuildAsyncCpu(TaskCompletionSource tcs)
    {
        var newSnapshot = this.BuildCpu(isAsync: true);
        this.scene.UploadQueue.Enqueue(() =>
        {
            try
            {
                this.UploadAndSwap(newSnapshot);
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
    }

    private Snapshot BuildCpu(bool isAsync)
    {
        var newAtlas = ImGui.ImFontAtlas();
        var toolkit = new MockFontAtlasBuildToolkit(
            this,
            newAtlas,
            isAsync,
            this.scene.GameFontData,
            this.scene.GameGlyphRanges);

        MockDelegateFontHandle[] handlesSnapshot;
        lock (this.syncRoot)
        {
            handlesSnapshot = this.handles.ToArray();
        }

        if (handlesSnapshot.Length == 0)
        {
            newAtlas.AddFontDefault();
        }

        toolkit.BuildStep = FontAtlasBuildStep.PreBuild;
        this.BuildStepChange?.Invoke(toolkit);
        var handleFonts = new Dictionary<MockDelegateFontHandle, ImFontPtr>();
        var handleErrors = new Dictionary<MockDelegateFontHandle, Exception>();
        foreach (var handle in handlesSnapshot)
        {
            toolkit.Font = default;
            try
            {
                handle.BuildDelegate(toolkit);
                handleFonts[handle] = toolkit.Font;
                toolkit.AssociateHandle(handle, toolkit.Font);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MockFontAtlas:{Name}] build delegate threw", this.Name);
                handleErrors[handle] = ex;
            }
        }

        newAtlas.Build();

        // Fonts are baked at size*Scale. FontGlobalScale carries the global scale at draw time, so counteract it per font (Scale = 1/Scale) to render glyphs 1:1.
        var inverseScale = toolkit.Scale > 0 ? 1f / toolkit.Scale : 1f;
        var builtFonts = newAtlas.Fonts;
        for (var fi = 0; fi < builtFonts.Size; fi++)
        {
            builtFonts[fi].Handle->Scale = inverseScale;
        }

        toolkit.BuildStep = FontAtlasBuildStep.PostBuild;
        this.BuildStepChange?.Invoke(toolkit);
        foreach (var action in toolkit.PostBuildActions)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MockFontAtlas:{Name}] post-build action threw", this.Name);
            }
        }

        foreach (var d in toolkit.DisposeAfterBuildList)
        {
            try
            {
                d.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MockFontAtlas:{Name}] DisposeAfterBuild threw", this.Name);
            }
        }

        foreach (var a in toolkit.DisposeAfterBuildActions)
        {
            try
            {
                a();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MockFontAtlas:{Name}] DisposeAfterBuild action threw", this.Name);
            }
        }

        return new Snapshot
        {
            Atlas = newAtlas,
            HandleFonts = handleFonts,
            HandleErrors = handleErrors,
            DisposeWithAtlas = toolkit.DisposeWithAtlasList.ToList(),
            DisposeWithAtlasActions = toolkit.DisposeWithAtlasActions.ToList(),
        };
    }

    private void UploadAndSwap(Snapshot newSnapshot)
    {
        // Texture upload must be on main thread.
        var textureIds = this.scene.CreateAtlasTexture(newSnapshot.Atlas);
        newSnapshot.TextureBindingIds = textureIds;
        newSnapshot.RefCount = 1;

        Snapshot? previous;
        lock (this.syncRoot)
        {
            previous = this.current;
            this.current = newSnapshot;
        }

        foreach (var (handle, font) in newSnapshot.HandleFonts)
        {
            handle.OnAtlasBuilt(font);
        }

        foreach (var (handle, ex) in newSnapshot.HandleErrors)
        {
            handle.OnAtlasBuildFailed(ex);
        }

        if (previous is not null)
        {
            bool destroy;
            lock (this.syncRoot)
            {
                previous.RefCount--;
                previous.MarkedForDestruction = true;
                destroy = previous.RefCount <= 0;
            }

            if (destroy)
            {
                this.scene.EnqueueDeferredAtlasDispose(() => DestroySnapshot(previous, this.scene));
            }
        }
    }

    private static void DestroySnapshot(Snapshot snapshot, ImGuiScene scene)
    {
        try
        {
            foreach (var bindingId in snapshot.TextureBindingIds)
            {
                scene.DestroyAtlasTexture(bindingId);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MockFontAtlas] failed to destroy Veldrid atlas texture");
        }

        foreach (var d in snapshot.DisposeWithAtlas)
        {
            try
            {
                d.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MockFontAtlas] DisposeWithAtlas threw");
            }
        }

        foreach (var a in snapshot.DisposeWithAtlasActions)
        {
            try
            {
                a();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MockFontAtlas] DisposeWithAtlas action threw");
            }
        }

        if ((IntPtr)snapshot.Atlas.Handle != IntPtr.Zero)
        {
            snapshot.Atlas.Destroy();
        }
    }

    private sealed class Snapshot
    {
        public ImFontAtlasPtr Atlas;
        public IReadOnlyList<IntPtr> TextureBindingIds = System.Array.Empty<IntPtr>();
        public int RefCount;
        public bool MarkedForDestruction;
        public Dictionary<MockDelegateFontHandle, ImFontPtr> HandleFonts = new();
        public Dictionary<MockDelegateFontHandle, Exception> HandleErrors = new();
        public List<IDisposable> DisposeWithAtlas = new();
        public List<SysAction> DisposeWithAtlasActions = new();
    }

    private sealed class SuppressionScope : IDisposable
    {
        private readonly MockFontAtlas owner;
        private int disposed;

        public SuppressionScope(MockFontAtlas owner)
        {
            this.owner = owner;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }

            bool shouldRebuild = false;
            lock (this.owner.syncRoot)
            {
                this.owner.suppressDepth--;
                if (this.owner.suppressDepth == 0 && this.owner.pendingRebuild)
                {
                    shouldRebuild = true;
                    this.owner.pendingRebuild = false;
                }
            }

            if (shouldRebuild)
            {
                this.owner.MaybeAutoRebuild();
            }
        }
    }
}
