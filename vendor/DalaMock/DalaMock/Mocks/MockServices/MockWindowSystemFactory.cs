namespace DalaMock.Core.Mocks.MockServices;

/// <inheritdoc />
public class MockWindowSystemFactory : IWindowSystemFactory
{
    private readonly MockWindowSystem.Factory factory;
    private readonly Dictionary<string?, IWindowSystem> cache = new();

    public MockWindowSystemFactory(MockWindowSystem.Factory factory)
    {
        this.factory = factory;
    }

    public IWindowSystem Create(string? imNamespace = null)
    {
        imNamespace ??= string.Empty;
        if (this.cache.TryGetValue(imNamespace, out var existingInstance))
        {
            return existingInstance;
        }

        var newInstance = this.factory.Invoke(imNamespace);
        this.cache[imNamespace] = newInstance;

        return newInstance;
    }
}
