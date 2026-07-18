### Task 3: Extract A Reusable DalaMock Hosted Session Without Changing Plugin Runtime Behavior

**Files:**
- Create: `Echoglossian.Mock/Hosting/HostedPreviewPluginOptions.cs`
- Create: `Echoglossian.Mock/Hosting/HostedPreviewPluginSession.cs`
- Create: `Echoglossian.Mock/Hosting/HostedPreviewPluginSessionFactory.cs`
- Modify: `Echoglossian.Mock/Program.cs`
- Modify: `Echoglossian.Mock.Tests/Echoglossian.Mock.Tests.csproj`
- Modify: `Echoglossian.Mock.Tests/TestBoot.cs`
- Create: `Echoglossian.Mock.Tests/HostedPreviewPluginSessionTests.cs`

**Interfaces:**
- Consumes:
  - `MockContainer`
  - `PluginLoadSettings`
  - `global::Echoglossian.Echoglossian`
  - `StartedPlugin`
- Produces:
  - `public sealed record HostedPreviewPluginOptions(DirectoryInfo StateRoot, DirectoryInfo PluginSavePath, FileInfo ConfigPath, string? DatabasePath, bool CreateWindow)`
  - `public sealed class HostedPreviewPluginSession : IDisposable`
  - `public static Task<HostedPreviewPluginSession> StartAsync(HostedPreviewPluginOptions options, CancellationToken cancellationToken = default)`

- [ ] **Step 1: Write the failing hosted-session smoke test**

```csharp
using FluentAssertions;
using Xunit;

namespace Echoglossian.Mock.Tests;

public sealed class HostedPreviewPluginSessionTests
{
    [Fact]
    public async Task StartAsync_uses_explicit_preview_owned_paths()
    {
        using var fixture = PreviewOwnedHostedSessionFixture.Create();

        await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
            fixture.Options);

        session.StateRoot.FullName.Should().Be(fixture.Options.StateRoot.FullName);
        session.PluginSavePath.FullName.Should().Be(fixture.Options.PluginSavePath.FullName);
        session.ConfigPath.FullName.Should().Be(fixture.Options.ConfigPath.FullName);
    }
}
```

- [ ] **Step 2: Run the hosted-session smoke test to verify it fails**

Run:

```powershell
dotnet restore Echoglossian.Mock\Echoglossian.Mock.csproj
dotnet restore Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --filter FullyQualifiedName~HostedPreviewPluginSessionTests
```

Expected: FAIL because the hosted session types do not exist yet.

- [ ] **Step 3: Add the shared hosted options and session types**

```csharp
namespace Echoglossian.Mock.Hosting;

public sealed record HostedPreviewPluginOptions(
    DirectoryInfo StateRoot,
    DirectoryInfo PluginSavePath,
    FileInfo ConfigPath,
    string? DatabasePath,
    bool CreateWindow);

public sealed class HostedPreviewPluginSession : IAsyncDisposable, IDisposable
{
    public HostedPreviewPluginSession(
        MockContainer container,
        global::Echoglossian.Echoglossian plugin,
        DirectoryInfo stateRoot,
        DirectoryInfo pluginSavePath,
        FileInfo configPath)
    {
        this.Container = container;
        this.Plugin = plugin;
        this.StateRoot = stateRoot;
        this.PluginSavePath = pluginSavePath;
        this.ConfigPath = configPath;
    }

    public MockContainer Container { get; }
    public global::Echoglossian.Echoglossian Plugin { get; }
    public DirectoryInfo StateRoot { get; }
    public DirectoryInfo PluginSavePath { get; }
    public FileInfo ConfigPath { get; }
}
```

- [ ] **Step 4: Move DalaMock plugin bootstrap into a factory shared by runner and tests**

```csharp
public static class HostedPreviewPluginSessionFactory
{
    public static async Task<HostedPreviewPluginSession> StartAsync(
        HostedPreviewPluginOptions options,
        CancellationToken cancellationToken = default)
    {
        var container = new MockContainer(
            new MockDalamudConfiguration
            {
                CreateWindow = options.CreateWindow,
                GamePath = ResolveSqpackDirectory(),
                PluginSavePath = options.PluginSavePath,
            },
            builder => { },
            [],
            false);

        var loader = container.GetPluginLoader();
        var mockPlugin = loader.AddPlugin(typeof(global::Echoglossian.Echoglossian));
        var settings = new PluginLoadSettings(options.StateRoot, options.ConfigPath)
        {
            AssemblyLocation = typeof(global::Echoglossian.Echoglossian).Assembly.Location,
        };

        await loader.StartPlugin(mockPlugin, settings);
        ...
    }
}
```

- [ ] **Step 5: Make `Echoglossian.Mock/Program.cs` use the shared factory**

```csharp
await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
    new HostedPreviewPluginOptions(
        stateRoot,
        pluginSavePath,
        configPath,
        DatabasePath: null,
        CreateWindow: true));

session.Container.GetMockUi().Run();
```

- [ ] **Step 6: Make `TestBoot` use the shared factory instead of re-implementing startup**

```csharp
await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
    new HostedPreviewPluginOptions(
        stateRoot,
        pluginSavePath,
        configPath,
        DatabasePath: null,
        CreateWindow: false));

return new StartedPlugin(
    session.Container,
    session.Plugin,
    stateRoot,
    pluginSavePath,
    configPath);
```

- [ ] **Step 7: Re-run the hosted-session smoke test**

Run:

```powershell
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --filter FullyQualifiedName~HostedPreviewPluginSessionTests
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add Echoglossian.Mock Echoglossian.Mock.Tests
git commit -m "refactor: extract reusable DalaMock hosted preview session"
```

