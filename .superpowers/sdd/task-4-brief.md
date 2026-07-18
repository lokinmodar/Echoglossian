### Task 4: Add The DalaMock Hosted Preview Backend With Safe `Auto` Fallback

**Files:**
- Create: `Echoglossian.Previewer/PluginWindows/DalaMockHostedPluginWindowPreviewBackend.cs`
- Create: `Echoglossian.Previewer/PluginWindows/PluginWindowPreviewBackendFactory.cs`
- Modify: `Echoglossian.Previewer/Echoglossian.Previewer.csproj`
- Modify: `Echoglossian.Previewer/Program.cs`
- Modify: `Echoglossian.Previewer/UI/PreviewShell.cs`
- Test: `Echoglossian.Previewer.Tests/PluginWindows/PluginWindowPreviewBackendFactoryTests.cs`
- Test: `Echoglossian.Mock.Tests/HostedPreviewPluginSessionTests.cs`

**Interfaces:**
- Consumes:
  - `HostedPreviewPluginSessionFactory.StartAsync(...)`
  - `IPluginWindowPreviewBackend`
  - `PluginWindowPreviewBackendMode`
- Produces:
  - `internal sealed class DalaMockHostedPluginWindowPreviewBackend : IPluginWindowPreviewBackend`
  - `internal static class PluginWindowPreviewBackendFactory`
  - `Task<(IPluginWindowPreviewBackend Backend, PluginWindowBackendStatus Status)> CreateAsync(...)`

- [ ] **Step 1: Write the failing backend factory tests**

```csharp
using FluentAssertions;
using Xunit;

namespace Echoglossian.Previewer.Tests.PluginWindows;

public sealed class PluginWindowPreviewBackendFactoryTests
{
    [Fact]
    public async Task CreateAsync_auto_falls_back_to_standalone_when_hosted_boot_fails()
    {
        var result = await PluginWindowPreviewBackendFactory.CreateForTestsAsync(
            PluginWindowPreviewBackendMode.Auto,
            static () => throw new InvalidOperationException("synthetic hosted failure"));

        result.Status.EffectiveMode.Should().Be(PluginWindowPreviewBackendMode.Standalone);
        result.Status.FallbackReason.Should().Contain("synthetic hosted failure");
    }

    [Fact]
    public async Task CreateAsync_dalamock_does_not_silently_fallback_when_hosted_boot_fails()
    {
        Func<Task> act = async () => await PluginWindowPreviewBackendFactory.CreateForTestsAsync(
            PluginWindowPreviewBackendMode.DalaMockHosted,
            static () => throw new InvalidOperationException("synthetic hosted failure"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*synthetic hosted failure*");
    }
}
```

- [ ] **Step 2: Run the backend factory tests to verify they fail**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~PluginWindowPreviewBackendFactoryTests
```

Expected: FAIL because the factory and hosted backend do not exist yet.

- [ ] **Step 3: Reference `Echoglossian.Mock` from the previewer project**

```xml
<ItemGroup>
  <ProjectReference Include="..\Echoglossian.Mock\Echoglossian.Mock.csproj" />
</ItemGroup>
```

- [ ] **Step 4: Implement the hosted backend over the shared hosted session**

```csharp
internal sealed class DalaMockHostedPluginWindowPreviewBackend : IPluginWindowPreviewBackend
{
    private readonly HostedPreviewPluginSession session;
    private readonly StandalonePluginWindowPreviewBackend fallbackRenderer;

    public DalaMockHostedPluginWindowPreviewBackend(
        HostedPreviewPluginSession session,
        StandalonePluginWindowPreviewBackend fallbackRenderer)
    {
        this.session = session;
        this.fallbackRenderer = fallbackRenderer;
        this.Status = new PluginWindowBackendStatus(
            PluginWindowPreviewBackendMode.DalaMockHosted,
            PluginWindowPreviewBackendMode.DalaMockHosted,
            HostedRequested: true,
            HostedAvailable: true,
            FallbackReason: null);
    }

    public PluginWindowBackendStatus Status { get; }
    ...
}
```

- [ ] **Step 5: Implement `Auto` fallback logic in the backend factory**

```csharp
internal static async Task<(IPluginWindowPreviewBackend Backend, PluginWindowBackendStatus Status)> CreateAsync(
    PluginWindowPreviewBackendMode requestedMode,
    Func<Task<DalaMockHostedPluginWindowPreviewBackend>> createHostedBackend,
    Func<IPluginWindowPreviewBackend> createStandaloneBackend)
{
    if (requestedMode == PluginWindowPreviewBackendMode.Standalone)
    {
        var backend = createStandaloneBackend();
        return (backend, backend.Status);
    }

    try
    {
        var hostedBackend = await createHostedBackend();
        return (hostedBackend, hostedBackend.Status);
    }
    catch (Exception ex) when (requestedMode == PluginWindowPreviewBackendMode.Auto)
    {
        var standaloneBackend = createStandaloneBackend();
        var fallbackStatus = new PluginWindowBackendStatus(
            PluginWindowPreviewBackendMode.Auto,
            PluginWindowPreviewBackendMode.Standalone,
            HostedRequested: true,
            HostedAvailable: false,
            FallbackReason: ex.Message);
        return (standaloneBackend, fallbackStatus);
    }
}
```

- [ ] **Step 6: Thread the selected backend mode into `Program` startup**

```csharp
var backendCreation = await PluginWindowPreviewBackendFactory.CreateAsync(
    commandLine.PluginWindowBackendMode,
    () => CreateDalaMockHostedPluginWindowBackendAsync(
        editableConfiguration,
        languages,
        session),
    () => CreateStandalonePluginWindowPreviewBackend(
        editableConfiguration,
        languages,
        session.ClonedDatabasePath));
```

- [ ] **Step 7: Re-run the backend factory tests**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~PluginWindowPreviewBackendFactoryTests
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add Echoglossian.Previewer\Echoglossian.Previewer.csproj Echoglossian.Previewer\Program.cs Echoglossian.Previewer\PluginWindows Echoglossian.Previewer.Tests\PluginWindows Echoglossian.Mock Echoglossian.Mock.Tests
git commit -m "feat: add DalaMock hosted preview plugin-window backend"
```

