namespace DalaMock.Core.Fonts.Handles;

/// <summary>
/// Lightweight <see cref="IFontHandle"/> wrapping a font that already lives in the global
/// ImGui atlas — used for the four pre-built handles on <see cref="MockUiBuilder"/>
/// (default, icon, mono, fixed-width icon). No rebuild, no refcount, no async waiting.
/// </summary>
internal sealed class MockGlobalFontHandle : IFontHandle
{
    private readonly Func<ImFontPtr> fontAccessor;

    public MockGlobalFontHandle(Func<ImFontPtr> fontAccessor)
    {
        this.fontAccessor = fontAccessor;
    }

    public event IFontHandle.ImFontChangedDelegate? ImFontChanged
    {
        add { }
        remove { }
    }

    public Exception? LoadException => null;

    public unsafe bool Available => (IntPtr)this.fontAccessor().Handle != IntPtr.Zero;

    public ILockedImFont? TryLock(out string? errorMessage)
    {
        errorMessage = null;
        return new MockLockedImFont(this.fontAccessor());
    }

    public ILockedImFont Lock() => new MockLockedImFont(this.fontAccessor());

    public IDisposable Push()
    {
        ImGui.PushFont(this.fontAccessor());
        return new PopOnDispose();
    }

    public void Pop() => ImGui.PopFont();

    public Task<IFontHandle> WaitAsync() => Task.FromResult<IFontHandle>(this);

    public Task<IFontHandle> WaitAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IFontHandle>(this);

    public void Dispose()
    {
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
