### Task 5: Surface Hosted/Effective Backend In The Shell And Screenshot Manifest

**Files:**
- Modify: `Echoglossian.Previewer/UI/PreviewShell.cs`
- Modify: `Echoglossian.Previewer/Hosting/PreviewCommandLine.cs`
- Modify: `Echoglossian.Previewer/Screenshots/BatchScreenshotRunner.cs`
- Modify: `Echoglossian.Previewer/Screenshots/ScreenshotManifest.cs` or the current manifest-owning type
- Test: `Echoglossian.Previewer.Tests/Screenshots/BatchScreenshotRunnerBackendManifestTests.cs`
- Test: `Echoglossian.Previewer.Tests/PluginWindows/PreviewShellBackendStatusTests.cs`

**Interfaces:**
- Consumes:
  - `PluginWindowBackendStatus`
  - `IPluginWindowPreviewBackend.TryGetStableCrop(PreviewCaptureTarget target) : Rectangle?`
- Produces:
  - manifest fields for requested/effective plugin-window backend
  - shell UI controls for selecting backend mode interactively

- [ ] **Step 1: Write the failing manifest test**

```csharp
using FluentAssertions;
using Xunit;

namespace Echoglossian.Previewer.Tests.Screenshots;

public sealed class BatchScreenshotRunnerBackendManifestTests
{
    [Fact]
    public void SerializeManifest_plugin_window_capture_records_requested_and_effective_backend()
    {
        var manifest = BatchScreenshotRunner.CreateManifestForTests(
            captureTarget: PreviewCaptureTarget.ConfigWindow,
            requestedBackend: PluginWindowPreviewBackendMode.Auto,
            effectiveBackend: PluginWindowPreviewBackendMode.Standalone,
            fallbackReason: "DalaMock initialization failed");

        manifest.RequestedPluginWindowBackend.Should().Be("Auto");
        manifest.EffectivePluginWindowBackend.Should().Be("Standalone");
        manifest.PluginWindowBackendFallbackReason.Should().Be("DalaMock initialization failed");
    }
}
```

- [ ] **Step 2: Run the manifest test to verify it fails**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~BatchScreenshotRunnerBackendManifestTests
```

Expected: FAIL because the manifest fields do not exist yet.

- [ ] **Step 3: Add backend fields to the screenshot manifest model**

```csharp
internal sealed record ScreenshotManifest(
    ...,
    string RequestedPluginWindowBackend,
    string EffectivePluginWindowBackend,
    string? PluginWindowBackendFallbackReason);
```

- [ ] **Step 4: Populate manifest backend fields from the active plugin-window backend status**

```csharp
var manifest = new ScreenshotManifest(
    ...,
    RequestedPluginWindowBackend: backendStatus.RequestedMode.ToString(),
    EffectivePluginWindowBackend: backendStatus.EffectiveMode.ToString(),
    PluginWindowBackendFallbackReason: backendStatus.FallbackReason);
```

- [ ] **Step 5: Add interactive shell control for backend mode selection**

```csharp
if (ImGui.BeginCombo(
        "Plugin window backend",
        this.requestedPluginWindowBackendMode.ToString()))
{
    DrawBackendSelectable(PluginWindowPreviewBackendMode.Auto);
    DrawBackendSelectable(PluginWindowPreviewBackendMode.Standalone);
    DrawBackendSelectable(PluginWindowPreviewBackendMode.DalaMockHosted);
    ImGui.EndCombo();
}
```

- [ ] **Step 6: Add explicit hosted restart warning text**

```csharp
if (this.requestedPluginWindowBackendMode != this.pluginWindowBackendStatus.EffectiveMode)
{
    ImGui.TextWrapped(
        $"Plugin window backend is running as {this.pluginWindowBackendStatus.EffectiveMode}. Requested mode was {this.pluginWindowBackendStatus.RequestedMode}.");
}
```

- [ ] **Step 7: Re-run the manifest test**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~BatchScreenshotRunnerBackendManifestTests
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add Echoglossian.Previewer\UI\PreviewShell.cs Echoglossian.Previewer\Hosting\PreviewCommandLine.cs Echoglossian.Previewer\Screenshots Echoglossian.Previewer.Tests\Screenshots Echoglossian.Previewer.Tests\PluginWindows\PreviewShellBackendStatusTests.cs
git commit -m "feat: surface preview plugin-window backend diagnostics"
```