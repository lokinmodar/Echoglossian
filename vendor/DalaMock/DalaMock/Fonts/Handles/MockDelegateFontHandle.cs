namespace DalaMock.Core.Fonts.Handles;

/// <summary>
/// Per-atlas <see cref="IFontHandle"/> created via <see cref="IFontAtlas.NewDelegateFontHandle"/>.
/// Backed by a build delegate the atlas re-runs whenever it rebuilds.
/// </summary>
internal sealed class MockDelegateFontHandle : IFontHandle
{
    private readonly MockFontAtlas atlas;
    private readonly object syncRoot = new();
    private TaskCompletionSource<IFontHandle> availableTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ImFontPtr currentFont;
    private int disposed;

    public MockDelegateFontHandle(MockFontAtlas atlas, FontAtlasBuildStepDelegate buildDelegate)
    {
        this.atlas = atlas;
        this.BuildDelegate = buildDelegate;
    }

    public event IFontHandle.ImFontChangedDelegate? ImFontChanged;

    public Exception? LoadException { get; internal set; }

    public unsafe bool Available
    {
        get
        {
            lock (this.syncRoot)
            {
                return (IntPtr)this.currentFont.Handle != IntPtr.Zero;
            }
        }
    }

    /// <summary>Gets the delegate the atlas invokes during its pre-build pass.</summary>
    internal FontAtlasBuildStepDelegate BuildDelegate { get; }

    public ILockedImFont Lock()
    {
        if (!this.Available)
        {
            throw new InvalidOperationException("Font handle is not available yet.");
        }

        return new MockLockedImFont(this.GetCurrentFont(), this.atlas.AcquireSnapshotRef());
    }

    public ILockedImFont? TryLock(out string? errorMessage)
    {
        if (!this.Available)
        {
            errorMessage = "Font handle is not available yet.";
            return null;
        }

        errorMessage = null;
        return new MockLockedImFont(this.GetCurrentFont(), this.atlas.AcquireSnapshotRef());
    }

    public unsafe IDisposable Push()
    {
        var font = this.GetCurrentFont();
        if ((IntPtr)font.Handle == IntPtr.Zero)
        {
            font = ImGui.GetFont();
        }

        ImGui.PushFont(font);
        return new PopOnDispose();
    }

    public void Pop() => ImGui.PopFont();

    public Task<IFontHandle> WaitAsync() => this.WaitAsync(CancellationToken.None);

    public Task<IFontHandle> WaitAsync(CancellationToken cancellationToken)
    {
        if (this.Available)
        {
            return Task.FromResult<IFontHandle>(this);
        }

        var tcs = this.availableTcs;
        if (!cancellationToken.CanBeCanceled)
        {
            return tcs.Task;
        }

        var registration = cancellationToken.Register(
            static state => { ((TaskCompletionSource<IFontHandle>)state!).TrySetCanceled(); },
            tcs);
        tcs.Task.ContinueWith(_ => registration.Dispose(), TaskScheduler.Default);
        return tcs.Task;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        this.atlas.RemoveHandle(this);
    }

    /// <summary>Called by the owner atlas when a new build completes for this handle.</summary>
    internal void OnAtlasBuilt(ImFontPtr newFont)
    {
        lock (this.syncRoot)
        {
            this.currentFont = newFont;
            this.LoadException = null;
            if (!this.availableTcs.Task.IsCompleted)
            {
                this.availableTcs.TrySetResult(this);
            }
        }

        try
        {
            this.ImFontChanged?.Invoke(this, new MockLockedImFont(newFont));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MockDelegateFontHandle] ImFontChanged handler threw");
        }
    }

    /// <summary>Called by the owner atlas when a build fails for this handle.</summary>
    internal void OnAtlasBuildFailed(Exception ex)
    {
        lock (this.syncRoot)
        {
            this.LoadException = ex;
            if (!this.availableTcs.Task.IsCompleted)
            {
                this.availableTcs.TrySetException(ex);
            }
        }
    }

    /// <summary>Called by the atlas to swap in a fresh TCS for the next build cycle.</summary>
    internal void ResetAvailability()
    {
        lock (this.syncRoot)
        {
            this.currentFont = default;
            if (this.availableTcs.Task.IsCompleted)
            {
                this.availableTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    private ImFontPtr GetCurrentFont()
    {
        lock (this.syncRoot)
        {
            return this.currentFont;
        }
    }

    private sealed class PopOnDispose : IDisposable
    {
        private int popped;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.popped, 1) == 0)
            {
                ImGui.PopFont();
            }
        }
    }
}
