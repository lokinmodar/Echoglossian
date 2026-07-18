namespace DalaMock.Core.Fonts.Handles;

/// <summary>
/// No-op <see cref="IFontHandle"/> for use by <see cref="NullUiBuilder"/> (headless mode).
/// Push/Pop don't touch ImGui — callers that try to render with this handle won't crash but
/// also won't see any custom font effect.
/// </summary>
internal sealed class NullFontHandle : IFontHandle
{
    public event IFontHandle.ImFontChangedDelegate? ImFontChanged
    {
        add { }
        remove { }
    }

    public Exception? LoadException => null;

    public bool Available => false;

    public ILockedImFont Lock() => new MockLockedImFont(default);

    public ILockedImFont? TryLock(out string? errorMessage)
    {
        errorMessage = null;
        return new MockLockedImFont(default);
    }

    public IDisposable Push() => NoopDisposable.Instance;

    public void Pop()
    {
    }

    public Task<IFontHandle> WaitAsync() => Task.FromResult<IFontHandle>(this);

    public Task<IFontHandle> WaitAsync(CancellationToken cancellationToken) => Task.FromResult<IFontHandle>(this);

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
