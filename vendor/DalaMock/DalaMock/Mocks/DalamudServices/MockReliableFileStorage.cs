namespace DalaMock.Core.Mocks.DalamudServices;

[Experimental("Dalamud001")]
public class MockReliableFileStorage : IReliableFileStorage, IMockService, IDisposable
{
    private readonly ILogger<MockReliableFileStorage> logger;

    private readonly ConcurrentDictionary<string, byte[]> files = new();

    private bool disposed;

    public MockReliableFileStorage(ILogger<MockReliableFileStorage> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc/>
    public long MaxFileSizeBytes => 64 * 1024 * 1024;

    /// <inheritdoc/>
    public string ServiceName => "Reliable File Storage";

    /// <inheritdoc/>
    public bool Exists(string path)
    {
        this.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(path);

        return this.files.ContainsKey(Normalize(path));
    }

    /// <inheritdoc/>
    public Task WriteAllTextAsync(string path, string? contents)
        =>
            this.WriteAllTextAsync(path, contents, Encoding.UTF8);

    /// <inheritdoc/>
    public Task WriteAllTextAsync(string path, string? contents, Encoding encoding)
    {
        this.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(encoding);

        var bytes = encoding.GetBytes(contents ?? string.Empty);
        return this.WriteAllBytesAsync(path, bytes);
    }

    /// <inheritdoc/>
    public Task WriteAllBytesAsync(string path, byte[] bytes)
    {
        this.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.LongLength > this.MaxFileSizeBytes)
        {
            throw new ArgumentException(
                $"The provided data exceeds the maximum allowed size of {this.MaxFileSizeBytes} bytes.",
                nameof(bytes));
        }

        var normalized = Normalize(path);

        this.files[normalized] = bytes;

        this.logger.LogDebug("Mock write: {Path} ({Size} bytes)", normalized, bytes.Length);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<string> ReadAllTextAsync(string path, bool forceBackup = false)
        =>
            this.ReadAllTextAsync(path, Encoding.UTF8, forceBackup);

    /// <inheritdoc/>
    public async Task<string> ReadAllTextAsync(string path, Encoding encoding, bool forceBackup = false)
    {
        this.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(encoding);

        var bytes = await this.ReadAllBytesAsync(path, forceBackup);
        return encoding.GetString(bytes);
    }

    /// <inheritdoc/>
    public Task ReadAllTextAsync(string path, Action<string> reader)
        =>
            this.ReadAllTextAsync(path, Encoding.UTF8, reader);

    /// <inheritdoc/>
    public async Task ReadAllTextAsync(string path, Encoding encoding, Action<string> reader)
    {
        this.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(encoding);
        ArgumentNullException.ThrowIfNull(reader);

        try
        {
            var text = await this.ReadAllTextAsync(path, encoding);
            reader(text);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Mock read failed: {Path}", path);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<byte[]> ReadAllBytesAsync(string path, bool forceBackup = false)
    {
        this.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(path);

        var normalized = Normalize(path);

        if (this.files.TryGetValue(normalized, out var data))
        {
            this.logger.LogDebug("Mock read: {Path} ({Size} bytes)", normalized, data.Length);
            return Task.FromResult(data);
        }

        this.logger.LogWarning("Mock read missing file: {Path}", normalized);
        throw new FileNotFoundException($"Mock file not found: {normalized}");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.files.Clear();

        this.logger.LogDebug("MockReliableFileStorage disposed");
        GC.SuppressFinalize(this);
    }

    private static string Normalize(string path)
        => Path.GetFullPath(path);

    private void ThrowIfDisposed()
    {
        if (this.disposed)
        {
            throw new ObjectDisposedException(nameof(MockReliableFileStorage));
        }
    }
}
