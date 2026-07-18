### Task 2: Extract The Current Plugin-Window Host Into An Explicit Standalone Backend

**Files:**
- Create: `Echoglossian.Previewer/PluginWindows/IPluginWindowPreviewBackend.cs`
- Create: `Echoglossian.Previewer/PluginWindows/StandalonePluginWindowPreviewBackend.cs`
- Modify: `Echoglossian.Previewer/UI/PreviewPluginWindowHost.cs`
- Modify: `Echoglossian.Previewer/Program.cs`
- Modify: `Echoglossian.Previewer/UI/PreviewShell.cs`
- Modify: `Echoglossian.Previewer/Screenshots/BatchScreenshotRunner.cs`
- Test: `Echoglossian.Previewer.Tests/PluginWindows/StandalonePluginWindowPreviewBackendTests.cs`

**Interfaces:**
- Consumes:
  - `PreviewPluginWindowHost.TryGetStableCrop(PreviewCaptureTarget target) : Rectangle?`
  - `PreviewPluginWindowHost.Draw(PreviewWorkbenchState state) : void`
- Produces:
  - `internal interface IPluginWindowPreviewBackend : IDisposable`
  - `void Draw(PreviewWorkbenchState state)`
  - `Rectangle? TryGetStableCrop(PreviewCaptureTarget target)`
  - `PluginWindowBackendStatus Status { get; }`

- [ ] **Step 1: Write the failing standalone backend test**

```csharp
using FluentAssertions;
using Xunit;

namespace Echoglossian.Previewer.Tests.PluginWindows;

public sealed class StandalonePluginWindowPreviewBackendTests
{
    [Fact]
    public void Standalone_backend_reports_standalone_status()
    {
        using var backend = StandalonePluginWindowPreviewBackend.CreateForTests(
            dbManagerAvailable: true);

        backend.Status.RequestedMode.Should().Be(PluginWindowPreviewBackendMode.Standalone);
        backend.Status.EffectiveMode.Should().Be(PluginWindowPreviewBackendMode.Standalone);
        backend.Status.HostedAvailable.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run the standalone backend test to verify it fails**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~StandalonePluginWindowPreviewBackendTests
```

Expected: FAIL because the backend interface and standalone backend do not exist yet.

- [ ] **Step 3: Define the backend interface**

```csharp
namespace Echoglossian.Previewer.PluginWindows;

internal interface IPluginWindowPreviewBackend : IDisposable
{
    PluginWindowBackendStatus Status { get; }

    bool DbManagerAvailable { get; }

    bool CaptureFailed { get; }

    void Draw(PreviewWorkbenchState state);

    void BeginCapture(PreviewCaptureTarget target);

    void EndCapture();

    Rectangle? TryGetStableCrop(PreviewCaptureTarget target);
}
```

- [ ] **Step 4: Wrap the current host in a standalone backend**

```csharp
internal sealed class StandalonePluginWindowPreviewBackend : IPluginWindowPreviewBackend
{
    private readonly PreviewPluginWindowHost host;

    private StandalonePluginWindowPreviewBackend(PreviewPluginWindowHost host)
    {
        this.host = host;
        this.Status = new PluginWindowBackendStatus(
            PluginWindowPreviewBackendMode.Standalone,
            PluginWindowPreviewBackendMode.Standalone,
            HostedRequested: false,
            HostedAvailable: true,
            FallbackReason: null);
    }

    public PluginWindowBackendStatus Status { get; }

    public bool DbManagerAvailable => this.host.DbManagerAvailable;

    public bool CaptureFailed => this.host.CaptureFailed;

    public void Draw(PreviewWorkbenchState state) => this.host.Draw(state);

    public void BeginCapture(PreviewCaptureTarget target) => this.host.BeginCapture(target);

    public void EndCapture() => this.host.EndCapture();

    public Rectangle? TryGetStableCrop(PreviewCaptureTarget target) =>
        this.host.TryGetStableCrop(target);

    public void Dispose() => this.host.Dispose();
}
```

- [ ] **Step 5: Add a small test-only constructor or factory**

```csharp
internal static StandalonePluginWindowPreviewBackend CreateForTests(bool dbManagerAvailable)
{
    return new StandalonePluginWindowPreviewBackend(
        PreviewPluginWindowHost.CreateForTests(dbManagerAvailable));
}
```

- [ ] **Step 6: Switch `Program` and `PreviewShell` to consume the interface**

```csharp
using var pluginWindowBackend = CreateStandalonePluginWindowPreviewBackend(
    editableConfiguration,
    languages,
    session.ClonedDatabasePath);

shell.SetPluginWindowBackendStatus(pluginWindowBackend.Status);
pluginWindowBackend.Draw(this.workbenchState);
```

- [ ] **Step 7: Route screenshot capture through the backend interface**

```csharp
if (PreviewPluginWindowHost.IsPluginWindowTarget(pendingRequest.CaptureTarget))
{
    if (this.pluginWindowBackend.CaptureFailed)
    {
        ...
    }

    if (this.pluginWindowBackend.TryGetStableCrop(
            pendingRequest.CaptureTarget) is null)
    {
        ...
    }
}
```

- [ ] **Step 8: Re-run the standalone backend test**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~StandalonePluginWindowPreviewBackendTests
```

Expected: PASS.

- [ ] **Step 9: Commit**

```powershell
git add Echoglossian.Previewer\PluginWindows Echoglossian.Previewer\Program.cs Echoglossian.Previewer\UI\PreviewShell.cs Echoglossian.Previewer\UI\PreviewPluginWindowHost.cs Echoglossian.Previewer\Screenshots\BatchScreenshotRunner.cs Echoglossian.Previewer.Tests\PluginWindows\StandalonePluginWindowPreviewBackendTests.cs
git commit -m "refactor: extract standalone preview plugin-window backend"
```

