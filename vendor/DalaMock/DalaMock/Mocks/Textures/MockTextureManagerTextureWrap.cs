namespace DalaMock.Core.Mocks.Textures;

public class MockTextureManagerTextureWrap : IDalamudTextureWrap
{
    private readonly bool keepAlive;
    private readonly MockTextureProvider textureProvider;
    private readonly string path;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockTextureManagerTextureWrap"/> class.
    /// </summary>
    /// <param name="path">The path to the texture.</param>
    /// <param name="extents">The extents of the texture.</param>
    /// <param name="keepAlive">Keep alive or not.</param>
    /// <param name="manager">Manager that we obtained this from.</param>
    public MockTextureManagerTextureWrap(
        string path,
        Vector2 extents,
        bool keepAlive,
        MockTextureProvider textureProvider)
    {
        this.path = path;
        this.keepAlive = keepAlive;
        this.textureProvider = textureProvider;
        this.Width = (int)extents.X;
        this.Height = (int)extents.Y;
    }

    /// <summary>
    /// Gets a value indicating whether or not this wrap has already been disposed.
    /// If true, the handle may be invalid.
    /// </summary>
    internal bool IsDisposed { get; private set; }

    public ImTextureID Handle
    {
        get
        {
            if (this.IsDisposed)
            {
                throw new InvalidOperationException("Texture already disposed. You may not render it.");
            }

            return this.textureProvider.GetInfo(this.path).Wrap!.Handle;
        }
    }

    /// <inheritdoc />
    public int Width { get; private set; }

    /// <inheritdoc />
    public int Height { get; private set; }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (this)
        {
            if (!this.IsDisposed)
            {
                this.textureProvider.NotifyTextureDisposed(this.path, this.keepAlive);
            }

            this.IsDisposed = true;
        }
    }
}
