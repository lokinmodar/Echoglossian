namespace DalaMock.Core.Fonts.Handles;

/// <summary>
/// Minimal mock of <see cref="ILockedImFont"/>.
/// Optionally drives a refcount on a <see cref="MockFontAtlas"/> snapshot so the underlying
/// native atlas is not destroyed while the font is still in use.
/// </summary>
internal sealed class MockLockedImFont : ILockedImFont
{
    private readonly SysAction? releaseAction;
    private int disposed;
    
    public MockLockedImFont(ImFontPtr font, SysAction? releaseAction = null)
    {
        this.ImFont = font;
        this.releaseAction = releaseAction;
    }
    
    public ImFontPtr ImFont { get; }
    
    public ILockedImFont NewRef()
    {
        // The release callback (if any) closes over a counter on the atlas snapshot;
        // re-invoking it via a fresh MockLockedImFont increments that counter implicitly
        // through the atlas's own NewRef hook. For mock purposes — no-op semantics suffice
        // because GC won't reclaim ImFontPtr-backed memory mid-frame.
        return new MockLockedImFont(this.ImFont, this.releaseAction);
    }
    
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }
        
        this.releaseAction?.Invoke();
    }
}
